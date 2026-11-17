// A map of the floor the party is on, in the corner of the screen, in dungeons and caves.
//
// It is read from the same collision the party walks on, not from the pictures. From the cell the party stands in,
// the map is filled outward over the ground, cell by cell, a few each frame: ground there (an arrow down, as the
// ground test), would the party stand there (a sphere of the party's radius against the five wall attributes the party
// is subject to), is it a way out of the map (the twelve map-jump attributes). What the party can walk becomes floor,
// what stops it wall, and the cells so found are kept as one picture of the floor (a texture) that is drawn once, with the
// party's marker moving over it - so what is seen of the floor stays when the party leaves it. A wall that is shut
// is wall to the probe, and a cell found to be walkable later (a passage opened) is filled in then. Objects are not
// drawn, but chests not yet opened are marked.
//
// Shown in a dungeon only (STAGE_TYPE_DUNGEON), and not over a message, a menu, an event, a battle or the pause menu.
// Settings: the pause menu's Minimap page.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenFF.Client
{
	using Game = Microsoft.Xna.Framework.Game;
	using GameTime = Microsoft.Xna.Framework.GameTime;
	using Color = Microsoft.Xna.Framework.Color;
	using Vector2 = Microsoft.Xna.Framework.Vector2;
	using Rectangle = Microsoft.Xna.Framework.Rectangle;

	internal sealed class Minimap : DrawableGameComponent
	{
		private const int CellFx = 2 * 4096;        // a cell is 2 units
		private const int SeeCells = 6;             // how near the party must have been for a cell to be shown, with fog of war
		private const int ViewCells = 55;           // cells shown each way from the party
		private const int RecheckCells = 14;        // how far from the party walls are looked at again
		private const int Pad = ViewCells + 2;         // the picture's margin, so the shown part is always inside it
		private const int Budget = 80;              // cells probed each frame
		private const int ReachCells = 160;         // how far the fill goes from where the map was entered
		private const int RecheckEvery = 150;       // frames before a wall is probed again (a passage may have opened)
		private const int WallRadiusFx = 12288;     // the party's radius, as the character's wall test
		private const int FloorRange = 24576;       // 6 units: ground further from the neighbour's than this is another floor

		private const byte Void = 0, Floor = 1, Wall = 2, Exit = 3, Secret = 4, Edge = 5;   // Void: not looked at; Edge: looked at, no ground (the floor ends)   // Secret: a wall of one of the classes that can be switched (WALL_02..05)

		private struct Cell { public byte State; public int GroundY; public int Stamp; public sbyte DirX, DirZ; }   // DirX/DirZ: the way it was probed from, kept for probing it again

		private static readonly float[] Sizes = { 0.26f, 0.34f, 0.44f };
		private static readonly RasterizerState Scissor = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

		private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
		private readonly Queue<(int cx, int cz)> _frontier = new Queue<(int, int)>();
		private readonly List<(int dx, int dz)> _order = new List<(int, int)>();   // offsets nearest first, for the wall re-checks
		private string _stage;
		private int _frame, _recheckAt, _lastSet;
		private int _startX, _startZ;
		private int _minX, _maxX, _minZ, _maxZ;
		private bool _failed, _haveFixed, _dirty, _realloc, _lastPassages, _lastFog;
		private int _seenX = int.MinValue, _seenZ;
		private Dictionary<long, byte> _memory = new Dictionary<long, byte>();   // the cells shown, by what they were when last found: kept between visits
		private readonly List<(int dx, int dz)> _near = new List<(int, int)>();
		private float _fixedX, _fixedZ;   // the camera's way when the map was entered, for the fixed map

		// The picture of the floor: a pixel a cell, from cell (_ox, _oz).
		private Texture2D _tex;
		private Color[] _pix;
		private int _w, _h, _ox, _oz;

		private SpriteBatch _batch;
		private Texture2D _pixel, _arrow, _chest, _stairs;
		private float _faceX, _faceZ;   // the way the hero faces, in the map's space
		private readonly GlobalScope.VecFx32 _dirA = new GlobalScope.VecFx32();
		private readonly GlobalScope.VecFx32 _dirB = new GlobalScope.VecFx32();

		private int _myGen;
		private static Minimap _instance;
		private readonly GlobalScope.VecFx32 _pos = new GlobalScope.VecFx32();
		private readonly GlobalScope.VecFx32 _local = new GlobalScope.VecFx32();
		private readonly GlobalScope.VecFx32 _probe = new GlobalScope.VecFx32();
		private readonly GlobalScope.VecFx32 _down = new GlobalScope.VecFx32(0, -4096, 0);
		private readonly GlobalScope.VecFx32 _ahead = new GlobalScope.VecFx32(0, 0, 4096);
		private readonly GlobalScope.MtxFx43 _inv = new GlobalScope.MtxFx43();
		private readonly GlobalScope.mcl.CollisionResult _result = new GlobalScope.mcl.CollisionResult();

		private Minimap(Game game) : base(game)
		{
			// Over the field and its windows' pictures, under the debug overlay and the screenshot.
			DrawOrder = int.MaxValue - 6;
			for (int dz = -RecheckCells; dz <= RecheckCells; dz++)
				for (int dx = -RecheckCells; dx <= RecheckCells; dx++)
					if (dx * dx + dz * dz <= RecheckCells * RecheckCells) _order.Add((dx, dz));
			_order.Sort((a, b) => (a.dx * a.dx + a.dz * a.dz).CompareTo(b.dx * b.dx + b.dz * b.dz));
			for (int dz = -SeeCells; dz <= SeeCells; dz++)
				for (int dx = -SeeCells; dx <= SeeCells; dx++)
					if (dx * dx + dz * dz <= SeeCells * SeeCells) _near.Add((dx, dz));
		}

		public static void Attach(Game game)
		{
			_instance = new Minimap(game);
			game.Components.Add(_instance);
			Log.Write(LogChannel.General, "minimap: on in dungeons (the pause menu's Minimap page)");
		}

		public override void Draw(GameTime gameTime)
		{
			if (_failed || !Qol.Minimap || GameProfile.IsFf4 || RenderTest.Active) return;
			try
			{
				if (!Visible()) return;
				if (!Locate(out int px, out int py, out int pz)) return;
				string stage = GlobalScope.stg.CStageMng.CurrentName ?? "";
				if (stage != _stage) NewStage(stage, px, pz);
				if (_myGen != _forgetGen) { _myGen = _forgetGen; _memory = Recall(_stage); _realloc = true; _dirty = true; }
				_frame++;
				Explore(px, py, pz);
				Rebuild();
				Paint(px, pz);
			}
			catch (Exception ex)
			{
				_failed = true;   // one failure and it is off for the session, not a failure each frame
				Log.Write(LogChannel.General, "minimap: off - " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private void NewStage(string stage, int px, int pz)
		{
			_stage = stage;
			_memory = Recall(stage);
			_cells.Clear(); _frontier.Clear(); _seenX = int.MinValue;
			_haveFixed = false; _recheckAt = 0;
			_startX = CellOf(px); _startZ = CellOf(pz);
			_minX = _maxX = _startX; _minZ = _maxZ = _startZ;
			_tex?.Dispose(); _tex = null; _pix = null; _grid = null;
			_realloc = true; _dirty = true;
		}

		/// <summary>Whether the field is free: a dungeon, with no talk, menu, event, battle or shop over it.</summary>
		private static bool Visible()
		{
			if ((GlobalScope.GAMEPART)GlobalScope.sys.FF3PartSys.getCurrentPart() != GlobalScope.GAMEPART.GAMEPART_WORLD) return false;
			GlobalScope.stg.CStageMng stage = GlobalScope.stageMng;
			if (stage == null || stage.getStageType() != GlobalScope.stg.STAGE_TYPE.STAGE_TYPE_DUNGEON) return false;
			if (PauseMenu.IsOpen || ModMenus.GameMenuUp) return false;
			GlobalScope.wld.CBaseSystem sys = GlobalScope.wld.CBaseSystem.Current;
			if (sys == null) return false;
			if (sys.IsBattle() || sys.IsShop() || sys.IsTalk() || sys.IsTitle() || sys.IsMenu() || sys.IsMapJump() || sys.IsSave() || sys.IsSpecial()) return false;
			if (GlobalScope.evt.CEventManager.getInstance().isEvent()) return false;
			return true;
		}

		/// <summary>The party's place in the map's own space (the stage's inverse matrix, as the characters' walls are tested in).</summary>
		private bool Locate(out int x, out int y, out int z)
		{
			x = y = z = 0;
			GlobalScope.pl.CBasePlayer player = GlobalScope.wld.CBaseSystem.Current.PlayerMng()?.Player(GlobalScope.chr.CBaseCharacter.getLookIndex());
			GlobalScope.VecFx32 hero = player?.getPosition();
			if (hero == null) return false;
			_pos.copy(hero);
			GlobalScope.stageMng.getInvWldMtx(_inv);
			GlobalScope.MTX_MultVec43(_pos, _inv, _local);
			x = _local.x; y = _local.y; z = _local.z;
			// The way the hero faces: a point a step ahead, in the map's space, less the hero's own.
			GlobalScope.VecFx32 d = player.getDirectionForRotY();
			_dirA.set(hero.x + d.x, hero.y, hero.z + d.z);
			GlobalScope.MTX_MultVec43(_dirA, _inv, _dirB);
			_faceX = (_dirB.x - _local.x) / 4096f; _faceZ = (_dirB.z - _local.z) / 4096f;
			return true;
		}

		private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

		private static int CellOf(int fx) => (int)Math.Floor(fx / (double)CellFx);

		// ---- the maps kept ----

		// What the party has seen of each floor, by the floor's name. It is kept in the save: every time the game writes a
		// save slot the maps go into that slot's chunk (Chunk, below), and reading a slot back brings its maps back, so a
		// map belongs to the playthrough and not to a slot - save the same game in all three and each has the maps as they
		// were when it was written. A new game starts with none.
		private static Dictionary<string, Dictionary<long, byte>> Store = new Dictionary<string, Dictionary<long, byte>>();
		// The game reads every slot (and the suspend save) at the title and applies one of them only when it is chosen, so what each
		// read brought is kept by where it was read, and the chosen one is put in place when the game applies it.
		private static Dictionary<string, Dictionary<long, byte>> _last;
		private static readonly Dictionary<int, Dictionary<string, Dictionary<long, byte>>> _byOffset = new Dictionary<int, Dictionary<string, Dictionary<long, byte>>>();
		private static readonly Dictionary<int, Dictionary<string, Dictionary<long, byte>>> _bySlot = new Dictionary<int, Dictionary<string, Dictionary<long, byte>>>();
		public const int SuspendOffset = 41568;
		private static int _forgetGen;

		private static Dictionary<long, byte> Recall(string stage)
		{
			if (stage == null) stage = "";
			if (!Store.TryGetValue(stage, out Dictionary<long, byte> kept)) Store[stage] = kept = new Dictionary<long, byte>();
			return kept;
		}

		/// <summary>Every kept map is forgotten: a new game, a save read back with none, or the pause menu's Forget explored maps.</summary>
		public static void Reset()
		{
			Store = new Dictionary<string, Dictionary<long, byte>>();
			_forgetGen++;
		}

		/// <summary>A slot's data was read (and its chunk, if it has one, loaded): what it brought is kept under where it was read.</summary>
		public static void Read(int offset)
		{
			_last = Store;
			_byOffset[offset] = Store;
		}

		/// <summary>The game loaded save slot <paramref name="num"/>: the maps its read brought belong to that slot's data.</summary>
		public static void Bind(int num) => _bySlot[num] = _last ?? new Dictionary<string, Dictionary<long, byte>>();

		/// <summary>The game applies slot <paramref name="num"/>'s data: its maps become the game's.</summary>
		public static void Apply(int num)
		{
			Store = _bySlot.TryGetValue(num, out Dictionary<string, Dictionary<long, byte>> kept) ? kept : new Dictionary<string, Dictionary<long, byte>>();
			_forgetGen++;
			Log.Write(LogChannel.General, "minimap: slot " + num + " applied - " + Store.Count + " floor(s)");
		}

		/// <summary>The game applies the suspend save (the autosave): its maps become the game's.</summary>
		public static void ApplySuspend()
		{
			Store = _byOffset.TryGetValue(SuspendOffset, out Dictionary<string, Dictionary<long, byte>> kept) ? kept : new Dictionary<string, Dictionary<long, byte>>();
			_forgetGen++;
			Log.Write(LogChannel.General, "minimap: suspend save applied - " + Store.Count + " floor(s)");
		}

		/// <summary>Every kept map is forgotten (the pause menu's Forget explored maps).</summary>
		public static void Forget()
		{
			Reset();
			Log.Write(LogChannel.General, "minimap: kept maps forgotten");
		}

		/// <summary>The maps in the save: one chunk, the floors packed (a gzipped list of cells) and written as text.</summary>
		public sealed class Chunk : OpenFF.ISaveable
		{
			public string ChunkId => "openff/minimap";
			public int ChunkVersion => 1;

			public object Save()
			{
				int kept = 0;
				foreach (KeyValuePair<string, Dictionary<long, byte>> kv in Store) if (kv.Value.Count > 0) kept++;
				if (kept == 0) return null;
				using (MemoryStream bytes = new MemoryStream())
				{
					using (BinaryWriter w = new BinaryWriter(new GZipStream(bytes, CompressionLevel.Fastest, true)))
					{
						w.Write(kept);
						foreach (KeyValuePair<string, Dictionary<long, byte>> kv in Store)
						{
							if (kv.Value.Count == 0) continue;
							w.Write(kv.Key);
							w.Write(kv.Value.Count);
							foreach (KeyValuePair<long, byte> cell in kv.Value) { w.Write(cell.Key); w.Write(cell.Value); }
						}
					}
					return new Dictionary<string, object> { ["maps"] = Convert.ToBase64String(bytes.ToArray()) };
				}
			}

			public void Load(int version, System.Text.Json.JsonElement data)
			{
				Reset();
				if (version != 1 || !data.TryGetProperty("maps", out System.Text.Json.JsonElement maps) || maps.GetString() == null) return;
				using (BinaryReader r = new BinaryReader(new GZipStream(new MemoryStream(Convert.FromBase64String(maps.GetString())), CompressionMode.Decompress)))
				{
					int stages = r.ReadInt32();
					for (int i = 0; i < stages; i++)
					{
						string name = r.ReadString();
						int count = r.ReadInt32();
						Dictionary<long, byte> cells = new Dictionary<long, byte>(count);
						for (int n = 0; n < count; n++) { long key = r.ReadInt64(); cells[key] = r.ReadByte(); }
						Store[name] = cells;
					}
				}
				Log.Write(LogChannel.General, "minimap: " + Store.Count + " floor(s) back from the save");
			}
		}

		// ---- finding the floor ----

		/// <summary>Fills the floor outward a few cells each frame, and looks again at a few walls near the party.</summary>
		private void Explore(int px, int py, int pz)
		{
			GlobalScope.dgs.CRestrictor first = (GlobalScope.dgs.CRestrictor)GlobalScope.dgs.DGSLinkedList<GlobalScope.dgs.CRestrictor>.dgsllBase();
			if (first == null) return;
			int mapFlags = GlobalScope.wld.CWorldOutSideData.getInstance().MapData().isColFlag();
			int colFlags = GlobalScope.wld.CBaseSystem.Current.PlayerMng().Player(GlobalScope.chr.CBaseCharacter.getLookIndex()).getColFlag();

			// Where the party stands is floor, whatever the probe at the cell's middle says.
			int pcx = CellOf(px), pcz = CellOf(pz);
			MarkSeen(pcx, pcz);
			if (!_cells.TryGetValue(Key(pcx, pcz), out Cell here) || (here.State != Floor && here.State != Exit))
			{
				Put(pcx, pcz, new Cell { State = Floor, GroundY = py, Stamp = _frame });
				_frontier.Enqueue((pcx, pcz));
			}

			int budget = Budget;
			while (budget > 0 && _frontier.Count > 0)
			{
				(int cx, int cz) = _frontier.Dequeue();
				Cell from = _cells[Key(cx, cz)];
				for (int dz = -1; dz <= 1; dz++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dz == 0) continue;
						int nx = cx + dx, nz = cz + dz;
						if (Math.Abs(nx - _startX) > ReachCells || Math.Abs(nz - _startZ) > ReachCells) continue;
						if (_cells.ContainsKey(Key(nx, nz))) continue;
						budget--;
						byte state = ProbeCell(first, mapFlags, colFlags, nx, nz, from.GroundY, dx, dz, out int gy);
						if (state == Void) state = Edge;   // where the floor ends, the map has an edge
						Put(nx, nz, new Cell { State = state, GroundY = gy, Stamp = _frame, DirX = (sbyte)dx, DirZ = (sbyte)dz });
						if (state == Floor || state == Exit) _frontier.Enqueue((nx, nz));
					}
				}
			}

			// A passage may have opened: walls near the party are probed again now and then, and a cell that has
			// become walkable carries the fill on from there.
			for (int n = 0; n < 12; n++)
			{
				_recheckAt = (_recheckAt + 1) % _order.Count;
				int cx = pcx + _order[_recheckAt].dx, cz = pcz + _order[_recheckAt].dz;
				if (!_cells.TryGetValue(Key(cx, cz), out Cell old) || (old.State != Wall && old.State != Secret) || _frame - old.Stamp < RecheckEvery) continue;
				byte state = ProbeCell(first, mapFlags, colFlags, cx, cz, old.GroundY, old.DirX, old.DirZ, out int gy);   // the way it was found: a wall is only hit from its front
				if (state == Void) state = Edge;
				Put(cx, cz, new Cell { State = state, GroundY = state == Edge ? old.GroundY : gy, Stamp = _frame, DirX = old.DirX, DirZ = old.DirZ });
				if (state == Floor || state == Exit) _frontier.Enqueue((cx, cz));
			}
		}

		/// <summary>
		/// What the party would find stepping into a cell, going the way (dirX, dirZ): the game's walls only push back from one
		/// side (a wall is hit when the party moves against its face), so the probe goes the way the party would.
		/// </summary>
		private byte ProbeCell(GlobalScope.dgs.CRestrictor first, int mapFlags, int colFlags, int cx, int cz, int refY, int dirX, int dirZ, out int groundY)
		{
			int x = cx * CellFx + CellFx / 2, z = cz * CellFx + CellFx / 2;
			groundY = refY;
			if (dirX == 0 && dirZ == 0) dirZ = 1;
			int along = dirX != 0 && dirZ != 0 ? 2896 : 4096;   // a diagonal step is a unit vector too
			_ahead.set(dirX * along, 0, dirZ * along);

			// Ground: an arrow down from above the neighbour's height, as the ground test.
			bool ground = false;
			for (GlobalScope.dgs.CRestrictor r = first; r != null; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
			{
				if (!r.rorActivity()) continue;
				_probe.set(x, refY + 28672, z);
				if (r.rorEvaluateArrow(_probe, _down, 81920, 1, _result))
				{
					int gy = _result.pos.y;
					if (Math.Abs(gy - refY) <= FloorRange) { ground = true; groundY = gy; break; }
				}
			}
			if (!ground) return Void;

			// The way out of the map: the map-jump attributes.
			for (int k = 0; k < 12; k++)
			{
				if ((mapFlags & (4096 << k)) == 0) continue;
				for (GlobalScope.dgs.CRestrictor r = first; r != null; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
				{
					if (!r.rorActivity()) continue;
					_probe.set(x, groundY + 20480, z);
					if (r.rorEvaluateSphere(_probe, _ahead, 12288, (int)GlobalScope.mcl.MCL_ATTRIBUTE.ATTRIBUTE_MAPJUMP_01 + k, _result)) return Exit;
				}
			}

			// The walls the party is subject to (the character's wall test: the map's flags against the character's).
			for (int i = 0; i < 5; i++)
			{
				if ((mapFlags & (16 << i)) == 0 || (colFlags & (32 << i)) == 0) continue;
				for (GlobalScope.dgs.CRestrictor r = first; r != null; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
				{
					if (!r.rorActivity()) continue;
					_probe.set(x, groundY, z);
					if (r.rorEvaluateSphere(_probe, _ahead, WallRadiusFx, (int)GlobalScope.mcl.MCL_ATTRIBUTE.ATTRIBUTE_WALL_01 + i, _result)
						&& !(_result.normal.x == 0 && _result.normal.y == 0 && _result.normal.z == 0)) return i == 0 ? Wall : Secret;
				}
			}
			return Floor;
		}

		// ---- the picture of the floor ----

		// The floor is kept as a grid of cells, a byte each (what the map shows there), and drawn from a picture of four texels
		// a cell, shaded by smoothing between the cells' middles, so its edges are smooth lines and not steps a cell wide.

		private const int S = 4;   // texels a cell
		private const byte ShowNone = 0, ShowFloor = 1, ShowWall = 2, ShowExit = 3, ShowSecret = 4;
		private byte[] _grid;
		private readonly List<Vector2> _exits = new List<Vector2>();   // where the ways out are, in cells (the middle of each run of exit cells)
		private bool _exitsDirty = true;
		private int _exitsAt;
		private int _cw, _ch;
		private int _dx0, _dy0, _dx1 = -1, _dy1;   // the part of the picture changed since it was last sent (empty when _dx1 < _dx0)

		/// <summary>The cells about the party, shown from now on with fog of war (the floor is found before it is shown, so a hidden room is not given away).</summary>
		private void MarkSeen(int pcx, int pcz)
		{
			if (pcx == _seenX && pcz == _seenZ) return;
			_seenX = pcx; _seenZ = pcz;
			foreach ((int dx, int dz) in _near)
			{
				long key = Key(pcx + dx, pcz + dz);
				if (_memory.ContainsKey(key)) continue;
				_memory[key] = _cells.TryGetValue(key, out Cell c) ? c.State : Void;
				SetShown(pcx + dx, pcz + dz);
			}
		}

		/// <summary>What the map shows of a cell: what is found of it now, else what was kept of it; with fog of war, only if the party has been near.</summary>
		private byte ShownState(int cx, int cz)
		{
			long key = Key(cx, cz);
			bool remembered = _memory.TryGetValue(key, out byte kept);
			if (Qol.MinimapFog && !remembered) return ShowNone;
			byte state = _cells.TryGetValue(key, out Cell c) ? c.State : remembered ? kept : Void;
			switch (state)
			{
				case Floor: return ShowFloor;
				case Exit: return ShowExit;
				case Wall: case Edge: return ShowWall;
				case Secret: return Qol.MinimapPassages ? ShowSecret : ShowWall;
				default: return ShowNone;
			}
		}

		/// <summary>A cell found or changed: kept, and put in the picture (or the picture is made again, larger, when the cell is near its edge).</summary>
		private void Put(int cx, int cz, Cell c)
		{
			long key = Key(cx, cz);
			_cells[key] = c;
			if (_memory.ContainsKey(key) && _memory[key] != c.State) _memory[key] = c.State;
			if (cx < _minX) _minX = cx; if (cx > _maxX) _maxX = cx;
			if (cz < _minZ) _minZ = cz; if (cz > _maxZ) _maxZ = cz;
			if (_grid == null || cx - _ox < 4 || cz - _oz < 4 || cx - _ox >= _cw - 4 || cz - _oz >= _ch - 4) { _realloc = true; return; }
			SetShown(cx, cz);
		}

		/// <summary>A cell's grid byte made again, and the texels it reaches shaded again.</summary>
		private void SetShown(int cx, int cz)
		{
			if (_grid == null) return;
			int gx = cx - _ox, gz = cz - _oz;
			if (gx < 0 || gz < 0 || gx >= _cw || gz >= _ch) return;
			byte shown = ShownState(cx, cz);
			if ((shown == ShowExit) != (_grid[gz * _cw + gx] == ShowExit)) _exitsDirty = true;
			_grid[gz * _cw + gx] = shown;
			ShadeRegion(gx * S - S / 2, gz * S - S / 2, gx * S + S + S / 2 - 1, gz * S + S + S / 2 - 1);
		}

		private void ShadeRegion(int x0, int y0, int x1, int y1)
		{
			x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(_w - 1, x1); y1 = Math.Min(_h - 1, y1);
			if (x1 < x0 || y1 < y0) return;
			for (int y = y0; y <= y1; y++)
				for (int x = x0; x <= x1; x++)
					_pix[y * _w + x] = Shade(x, y);
			if (_dx1 < _dx0) { _dx0 = x0; _dy0 = y0; _dx1 = x1; _dy1 = y1; }
			else { _dx0 = Math.Min(_dx0, x0); _dy0 = Math.Min(_dy0, y0); _dx1 = Math.Max(_dx1, x1); _dy1 = Math.Max(_dy1, y1); }
		}

		private static float Soft(float x)
		{
			float t = Math.Clamp((x - 0.35f) / 0.3f, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		/// <summary>One texel: the four cells whose middles are nearest, weighed by nearness, give how much floor, wall and marked passage is here (a way out is floor, with a stairs icon over it).</summary>
		private Color Shade(int tx, int ty)
		{
			float u = (tx + 0.5f) / S - 0.5f, v = (ty + 0.5f) / S - 0.5f;
			int i0 = (int)Math.Floor(u), j0 = (int)Math.Floor(v);
			float a = u - i0, b = v - j0;
			a = a * a * (3f - 2f * a); b = b * b * (3f - 2f * b);   // rounder corners
			float f = 0, w = 0, p = 0;
			for (int k = 0; k < 4; k++)
			{
				int gi = i0 + (k & 1), gj = j0 + (k >> 1);
				if (gi < 0 || gj < 0 || gi >= _cw || gj >= _ch) continue;
				byte s = _grid[gj * _cw + gi];
				if (s == ShowNone) continue;
				float wt = ((k & 1) == 0 ? 1 - a : a) * ((k >> 1) == 0 ? 1 - b : b);
				if (s == ShowFloor || s == ShowExit) f += wt;
				if (s == ShowWall || s == ShowSecret) w += wt;
				if (s == ShowSecret) p += wt;
			}
			float af = Soft(f), aw = Soft(w * 1.6f);   // a wall a cell thick still shows along a slant
			float mark = w > 1e-4f ? Math.Clamp(p / w, 0f, 1f) : 0f;
			// Wall first (pale, orange where a passage is marked), the floor over it; premultiplied.
			float r = (205f + (255f - 205f) * mark) / 255f * aw, g = (215f + (150f - 215f) * mark) / 255f * aw, bl = (235f + (60f - 235f) * mark) / 255f * aw, alpha = aw;
			r = 40f / 255f * af + r * (1 - af); g = 62f / 255f * af + g * (1 - af); bl = 100f / 255f * af + bl * (1 - af); alpha = af + alpha * (1 - af);
			return new Color(r, g, bl, alpha);
		}

		private void Rebuild()
		{
			bool passages = Qol.MinimapPassages;
			if (passages != _lastPassages) { _lastPassages = passages; _realloc = true; }
			if (Qol.MinimapFog != _lastFog) { _lastFog = Qol.MinimapFog; _realloc = true; }
			if (_tex != null && _tex.IsDisposed) _realloc = true;
			if (_realloc)
			{
				// The grid covers every cell found and a margin, with room to grow so it is not made again at every step.
				foreach (long key in _memory.Keys)   // and the cells kept from earlier visits
				{
					int mx = (int)(key >> 32), mz = (int)(uint)key;
					if (mx < _minX) _minX = mx; if (mx > _maxX) _maxX = mx;
					if (mz < _minZ) _minZ = mz; if (mz > _maxZ) _maxZ = mz;
				}
				_ox = _minX - 24; _oz = _minZ - 24;
				_cw = _maxX - _minX + 1 + 48; _ch = _maxZ - _minZ + 1 + 48;
				_grid = new byte[_cw * _ch];
				foreach (long key in _cells.Keys) GridCell(key);
				foreach (long key in _memory.Keys) GridCell(key);
				_w = _cw * S; _h = _ch * S;
				_pix = new Color[_w * _h];
				_dx1 = -1;
				ShadeRegion(0, 0, _w - 1, _h - 1);
				_tex?.Dispose();
				_tex = new Texture2D(GraphicsDevice, _w, _h);
				_tex.SetData(_pix);
				_realloc = false; _dx1 = -1; _lastSet = _frame; _exitsDirty = true;
				return;
			}
			if (_dx1 >= _dx0 && _frame - _lastSet >= 3)
			{
				int rw = _dx1 - _dx0 + 1, rh = _dy1 - _dy0 + 1;
				if (rw * rh >= _w * _h / 2) _tex.SetData(_pix);
				else
				{
					// Only the rows that changed go to the card.
					Color[] part = new Color[rw * rh];
					for (int y = 0; y < rh; y++) Array.Copy(_pix, (_dy0 + y) * _w + _dx0, part, y * rw, rw);
					_tex.SetData(0, new Rectangle(_dx0, _dy0, rw, rh), part, 0, part.Length);
				}
				_dx1 = -1; _lastSet = _frame;
			}
		}

		/// <summary>The ways out: each run of exit cells is one, put at its middle.</summary>
		private void FindExits()
		{
			_exits.Clear();
			_exitsDirty = false; _exitsAt = _frame;
			if (_grid == null) return;
			bool[] done = new bool[_grid.Length];
			Stack<int> open = new Stack<int>();
			for (int start = 0; start < _grid.Length; start++)
			{
				if (_grid[start] != ShowExit || done[start]) continue;
				float sx = 0, sz = 0; int count = 0;
				done[start] = true; open.Push(start);
				while (open.Count > 0)
				{
					int at = open.Pop();
					int gx = at % _cw, gz = at / _cw;
					sx += gx; sz += gz; count++;
					for (int dz = -1; dz <= 1; dz++)
						for (int dx = -1; dx <= 1; dx++)
						{
							int nx = gx + dx, nz = gz + dz;
							if (nx < 0 || nz < 0 || nx >= _cw || nz >= _ch) continue;
							int n = nz * _cw + nx;
							if (_grid[n] == ShowExit && !done[n]) { done[n] = true; open.Push(n); }
						}
				}
				_exits.Add(new Vector2(_ox + sx / count + 0.5f, _oz + sz / count + 0.5f));
			}
		}

		private void GridCell(long key)
		{
			int cx = (int)(key >> 32), cz = (int)(uint)key;
			int gx = cx - _ox, gz = cz - _oz;
			if (gx < 0 || gz < 0 || gx >= _cw || gz >= _ch) return;
			_grid[gz * _cw + gx] = ShownState(cx, cz);
		}

		// ---- the two small pictures: the party's arrow and a chest ----

		private const int ArrowSize = 64, ChestW = 14, ChestH = 8, StairSize = 64;

		/// <summary>An arrow pointing up (white, with a dark outline), drawn smooth: its shape is sampled sixteen times a pixel.</summary>
		private static Texture2D MakeArrow(GraphicsDevice device)
		{
			// The shape in a unit square: the tip, the two corners and the notch between them.
			(float x, float y)[] shape = { (0.49f, 0.03f), (0.97f, 0.96f), (0.49f, 0.72f), (0.03f, 0.96f) };
			const float half = 0.04f;   // half the outline's width
			Color[] pixels = new Color[ArrowSize * ArrowSize];
			for (int py = 0; py < ArrowSize; py++)
			{
				for (int px = 0; px < ArrowSize; px++)
				{
					float r = 0, g = 0, b = 0, a = 0;
					for (int sy = 0; sy < 4; sy++)
					{
						for (int sx = 0; sx < 4; sx++)
						{
							float x = (px + (sx + 0.5f) / 4f) / ArrowSize, y = (py + (sy + 0.5f) / 4f) / ArrowSize;
							bool inside = false;
							float near = float.MaxValue;
							for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
							{
								if ((shape[i].y > y) != (shape[j].y > y) && x < (shape[j].x - shape[i].x) * (y - shape[i].y) / (shape[j].y - shape[i].y) + shape[i].x) inside = !inside;
								float ex = shape[j].x - shape[i].x, ey = shape[j].y - shape[i].y;
								float t = Math.Clamp(((x - shape[i].x) * ex + (y - shape[i].y) * ey) / (ex * ex + ey * ey), 0f, 1f);
								float dx = x - (shape[i].x + t * ex), dy = y - (shape[i].y + t * ey);
								near = Math.Min(near, (float)Math.Sqrt(dx * dx + dy * dy));
							}
							if (near < half) { a += 1; }                       // the outline: dark, r g b stay 0
							else if (inside) { a += 1; r += 1; g += 1; b += 1; }   // the inside: white
						}
					}
					pixels[py * ArrowSize + px] = new Color(r / 16f, g / 16f, b / 16f, a / 16f);   // premultiplied
				}
			}
			Texture2D tex = new Texture2D(device, ArrowSize, ArrowSize);
			tex.SetData(pixels);
			return tex;
		}

		// The stairs icon (a royalty-free staircase picture, 64 x 64, with a dark outline added), as premultiplied RGBA bytes in base64.
		private const string StairData =
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAUAAAAUAAABIAAAASEAAAEiAAABIQAAAR4AAAAPAAAABAAAAAEAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAABQAAABEBAQIuAwULeAcLF68HDBm4BwwZuAcMGbgGCxWrAgQHZAABASYAAAAOAAAABgAAAAEAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAEAAAAGAAAADQAAASUCAwdhBwwXsAsTJuMMFizzDhYs9g4WLPYOFiz2DBYs8goSJN0GCRKdAQIFTgAAAR4AAAAJAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAJAAABHgECBU4GCROf" +
				"DBQl3A4YLvMOGC78Ehw1/3R/lf+/y9r/Ul50/w4YMP4OGC78Dxgu7woRIdEEBw2HAQECOQAAABYAAAAHAAAAAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwAAAAcAAAAWAQEDOgQHDYcKESHRDxcs7w4YLvsOGDD+TVht/7bC" +
				"1f/m8v//4u7//+Lu//+eqb3/Mj1U/w4YMP4OGC76Dxcr6AcNG74DBQlwAAEBKgAAABAAAAAFAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAABQAAABAAAQErAwUJcAcNG78PFyvoDhgu+g4YMP4zPlX/oKu+/+fz///k8f//1+P7/9jk/P/Z5Pz/" +
				"6fb//9zn+v+BjKL/HSc//w4YMP0OFy32DBQo4QYLFaoCAwZaAAABIwAAAA0AAAAFAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAABAAAACwAAASMCAwZbBgsWrAwUKOEOFy32Dhgw/R4oQP+BjKL/2ub5/+j1///Y5Pv/2OT7/9vn/v/b5/7/2+f+/9jk+//b6P//6/j//8jU" +
				"5/9ga4H/Dhgx/w4YLvwPGS/yCxIk2AUIEJQBAgNEAAABGwAAAAgAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAACgEB" +
				"Az4FCBCVCxIl2Q8ZL/IOGC78Dxky/2Jtgv/J1uj/6/f//9zo///Y5Pv/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2eX8/9fj+v/g7P//6fb//6+8zv9CTWT/" +
				"Dhgw/g4YLvsPFyzsCRAfyQMGDH0BAQIyAAAAEwAAAAUAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAARwFCBCTDBMm5A4YLvoOGDD+" +
				"RU9m/7K+0v/q9v//4Oz//9fj+v/Z5fz/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2eT8/9jj+//l8v//4u7//5Kdsf8oM0v/Dhgw/Q4W" +
				"LvkOFinlBwwYtgIECGYAAQEmAAAADgAAAAUAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAEfBgoVpA0XK/EtN07+mKK2/+Pv///l8f//1+P7/9nl" +
				"/P/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/1+P6/9nk/P/q9v//1ODy/3J9k/8VHzj/Dhgw/Q8YLvYMFCbe" +
				"BgkToAEDBVEAAAEfAAAACgAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAABHwgNF6YMFizylKK8/+79///Z5Pz/2OT7/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/1+P7/93q///r+P//vsvc/1JddP8OGDD/Dhgu/A8YLfALESLTBAcOigEB" +
				"AzwAAAAYAAAABwAAAAMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAR8IDBemDBYs8o6duf/O4P3/ytn0/9jl/f/d6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/Z5fz/1+P6/+Pv///n9P//o67D/zZAV/8OGDD+Dhgu+g8XLOkIDhvAAwUJcgABASsAAAAQ" +
				"AAAAAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAUAAAEhCAwXpgwWLPKPnbj/0uT//8LT8P/G1/T/0N/5/9vn/v/c6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/Y5Pv/2OT7/+j0///c6Pn/hI+k/x8pQv8OGDD9DhYs9gwUKOEGCxasAgMFVgAAABAAAAACAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAL" +
				"AQECLgkNGa8MFizzjp24/9Lk///D1PH/xtf0/8TW8//J2fX/1OH6/9zo///c6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/Y5Pv/2+f+/+v3///L1+r/ZG+F/w8ZMv8OGC78DBUq7wcNGrsAAQEpAAAABgAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAUAAAATAQECMgMGDH8NFSfcERsw/I6d" +
				"uP/S5P//w9Tx/8bX9P/G1/T/xdb0/8XW8//L2/b/1+T8/9zo///b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9rm/P/X4/r/3+z//+n2//+0wNL/RlBm/w8ZMPoKECHPAQECLwAAAAYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAABQAAAA4AAQEmAgQIZgcMGLYOFynmDhYv+Q4YMP+Mm7f/0eP//8PU8f/G1/T/" +
				"xtf0/8bX9P/G1/T/xdbz/8bX9P/O3fj/2eb9/93o///b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2uf+/9nl/P/a5fz/4/L//7LH5f8UHzf6CRAg0AEBAi8AAAAGAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAACQAAAR8BAwVRBgkToQwUJt4PGC72Dhgw/RQfN/9xfZL/0N3v/83c9//D1PL/xdb0/8bX9P/G1/T/xtf0/8bX" +
				"9P/G1/T/xdbz/8fY9f/S4Pr/2+f+/9zo///b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/a5/7/2+f+/97p///a5v3/" +
				"wNTx/6zG6/+Zstb/FiE5+gkQINEBAQIwAAAABgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAABAAAACgEBAjkEBw6LCxEi1A8YLfAOGC78Dhgw/1NedP+/y97/6/f//97q///b5/7/1eL7/8na9f/E1vP/xtf0/8bX9P/G1/T/xtf0/8bX9P/G1/T/" +
				"xNbz/8ra9f/V4/z/3Oj//9zo/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/a5v7/3en//93o///K2/b/sMjq/6S/5P+tyO3/n7fa/xch" +
				"OfoJECDRAQECLwAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAwAAAR8GChSk" +
				"DBQn5g4YLvoOGDD+N0JZ/6Wvw//n9P//4u///9fj+v/Z5fz/2+f+/9zo///b5/7/0eD6/8fY9P/F1vP/xtf0/8bX9P/G1/T/xtf0/8bX9P/F1vT/xdb0/83c" +
				"9//Y5f3/3ej//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9rm/v/c6P//3un//9Hg+f+2zO3/p8Ln/6nD5/+pw+b/rcjt/5222v8XITj6CQ8gzwEBAi8AAAAF" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAQAAQEmCQ4dwg8XLvckLkX/hpKm/93p" +
				"/P/n9P//1+P6/9jk+//b5/7/2+f+/9vn/v/b5/7/2+f+/93o///Z5f3/zt34/8XW9P/F1vP/xtf0/8bX9P/G1/T/xtf0/8bX9P/F1vP/xtf0/9Df+f/a5/7/" +
				"3Oj//9vn/v/b5/7/2ub+/9vn/v/e6v//1+T8/73R8P+pw+f/p8Ln/6vF6P+qxOj/qMLm/63I7f+et9r/FyI5+gkPIM8BAQIvAAAABQAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAEAQICJwkPHcURGTD4rbrU/+/9///a5vz/2OT7/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/3Of+/9zo///W4/z/y9v2/8XW8//F1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/E1vP/yNn1/9Ph+v/c6P//3Oj+/97p" +
				"///b5/7/xNf0/6zG6f+nwub/qsTo/6vE6P+qxOj/qsTo/6jC5v+tyOz/n7fb/xciOfoJDyDPAQECLwAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAABAECAicIDx3FEhoy+K/A3P/L3Pn/0t/4/9zo/v/c6P//2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/b5/7/3Oj//9zn/v/T4fr/yNn1/8TW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8XW9P/E1vP/y9v2/9fk/P/N3ff/scnr/6fB5v+pw+j/" +
				"q8Xo/6rE6P+qxOj/qsTo/6rE6P+owub/rcjs/5+42/8YIjr6CRAg0AEBAjAAAAAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAQBAgIoCA8dxRIaMfiuvdn/y938/8PV8f/L2/b/1+T8/9zo///c5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/3Oj//9rm/v/Q3/n/xtf0/8XW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8bX8//D1fP/qcTn/6jD5/+rxej/qsTo/6rE6P+qxOj/qsTo/6rE" +
				"6P+qxOj/qMLm/63I7P+gudv/GCM6+gkQINABAQIwAAAABQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAIAAAAHAQICLgkPHsgRGjH4rr3Z/8ze/P/F1fH/xdbz/8XX9P/O3fj/2eb9/93o///b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/3ej//9jl/f/M3Pf/xdbz/8XW9P/G1/T/xtf0/8bX9P/H2PT/w9Xz/6vF6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+tx+z/" +
				"n7jc/xgjOvoJECDRAQECMAAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUAAAAKAAABHwME" +
				"B1sKESLWExwz+q692f/M3vz/xdXx/8bX9P/G1/T/xNbz/8fY9P/S4Pn/2+f+/9zo///b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/c6P7/" +
				"3Oj//9Xi+//K2vb/xNbz/8bX9P/G1/T/x9j0/8PV8/+rxej/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rcfs/5+53P8YIzv6CRAg0QEB" +
				"AjAAAAAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAwAAAAcAAAAXAQEDOwQHDogLEiPUEBkv9g4YMP+puNX/" +
				"zd/9/8XV8f/G1/T/xtf0/8bX9P/G1/T/xNbz/8na9v/V4vv/3Oj//9zo/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/c6P//2+f+/9Lg" +
				"+v/H2PX/xNbz/8fY9P/D1fP/q8Xo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLm/63H7P+hudz/GSQ7+gkQINEBAQIxAAAABQAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABQAAABAAAQErAwUJcgcNG78PFyzoDhgu+g4YMP82QFj/wdDo/8fY9v/D1PH/xtf0/8bX" +
				"9P/G1/T/xtf0/8bX9P/F1vT/xdbz/8zc9//Y5f3/3ej//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/d6P//2eb9/87d+P/G1/T/" +
				"wdTy/6vF6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+tx+z/obnc/xkjO/sKECHSAQECMQAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAABgAAASICAwZcBgsWrQwVKOEOFiz2Dhgw/R8pQf+Dj6P/2+f4/+Dr///T4Pn/ytr2/8TW8//G1/T/xtf0/8bX9P/G1/T/" +
				"xtf0/8bX9P/F1vP/xtf0/9De+f/a5v7/3ej//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/c6P7/2eb9/8ra9v+qxOj/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rcfs/6G63f8ZIzz7ChAh0gEBAjIAAAAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAwAAAR8EBw+OChEj2Q4YL/IOGC78Dxky/2RvhP/K1un/6vf//9vn/v/Z5fz/3ej//9vn/v/S4Pn/x9j1/8XW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8bX" +
				"9P/E1vP/yNn1/9Ph+v/c6P7/3Oj//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9rm/v/c5/7/3un//9Xj+/+5z+//qcPn/6rE6P+qxOj/qsTo/6rE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/qMLm/63H7P+hut3/GiQ8+woQIdIBAQIyAAAABQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAYBAQI0CRAg0A4W" +
				"LvkOGDD+RlBo/7S+0v/q9///4Oz//9fj+v/Z5fz/2+f+/9vn/v/b5/7/3ej//9nm/f/O3fj/xtf0/8XW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8XX9P/E1vP/" +
				"y9v2/9bj/P/c6P//3Of+/9vn/v/b5/7/2uf+/9vn/v/e6f//2eX9/8HU8v+qxOj/p8Ln/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC" +
				"5v+tx+z/obre/xokPPsKESHTAQECMwAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAGAQECNwsTJNwVHzf9jpqy/+fz///k8f//" +
				"1+P6/9nl/P/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/c5/7/3ej//9fk/P/L2/b/xdbz/8XW9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8XW8//F1vT/zt34/9nl" +
				"/f/d6P//2+f+/93p///c6P//ydn2/6/I6v+mweb/qsTo/6vE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rcfr/6G63v8aJTz7" +
				"ChEh0wEBAjMAAAAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAABgICAzgKEiPcICpC/cna9v/S4Pr/2OP7/9zo///b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/c6P//3Oj+/9Th+v/I2fX/xNbz/8bX9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8TW8//H2PT/0d/5/9vn/v/Q3/n/" +
				"tMvt/6fC5v+pw+f/q8Xo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLm/6zH6/+hut7/GyU9+woRIdMBAQI0AAAABQAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAYCAgM4ChIj3B4pQP26yuj/x9j2/8fX8//U4vv/3Oj+/9zo/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9vn/v/d6P//2ub+/9Df+f/G1/T/xdbz/8bX9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/C1fP/qcPn/6fC5/+rxej/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+sx+v/o7ve/xslPfsKESHTAQECNAAAAAUAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAGAgIDOAoSI9seKUD9vczq/8na9//D1PL/xdbz/8zb9//X5Pz/3Oj//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/d6P//2OX9/83c9//F1vP/xdb0/8bX9P/G1/T/xtf0/8bX9P/I2PX/vdHx/6nD5/+rxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rMfr/6O83v8cJj77ChEh1AEBAjUAAAAFAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAABAAAACAICAzcKESHVHig/+73M6f/J2vf/xNXy/8bX9P/F1vP/xtf0/8/e+P/a5v7/3ej//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9zo/v/c6P//1eP7/8ra9v/E1vP/xtf0/8bX9P/G1/T/yNj1/77S8f+pw+f/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/qMLm/6zH6/+jvN7/HCY++woRIdQBAQI1AAAABQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAABgAAABQCAwVK" +
				"ChEj2B4nP/u8zen/ydr3/8TV8v/G1/T/xtf0/8bX9P/E1vP/yNj1/9Ph+v/b5/7/3Oj//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9zo" +
				"///b5/7/0uD6/8fY9f/E1vP/xtf0/8jY9f++0vH/qcPo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/" +
				"qsTo/6jC5v+sx+v/o7zg/xwnP/sKESHVAQECNgAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAFAAAADAAAASICAwVXBwsWrA4YLfAfKUH+u8zp/8na" +
				"9//E1fL/xtf0/8bX9P/G1/T/xtf0/8XX9P/E1vP/ytr2/9bj+//c6P//3Oj+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/93o///Z5v3/" +
				"z974/8XX9P/G1/T/v9Lx/6nD6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rMbr/6O9" +
				"4P8dJz/7ChEh1QEBAjcAAAAGAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAIAAABGwECA0MFCA+SCxIj1xAaL/IOGC78GCM7/77O6//H2ff/xNXy/8bX9P/G1/T/" +
				"xtf0/8bX9P/G1/T/xtf0/8XW8//F1vT/zd33/9jl/f/d6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/3Of+/93o///W5Pz/zdz3/8DT" +
				"8f+pw+j/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLm/6zG6/+jvOD/HSdA+woRIdUBAQI3" +
				"AAAABgAAAAAAAAAAAAAAAAAAAAMAAAATAQECMQMGC3sJEB/IDxcs6w4YLvsOGDD+Pkhg/6y4y//Z5vv/ydn0/8XW8//F1/T/xtf0/8bX9P/G1/T/xtf0/8bX" +
				"9P/G1/T/xtf0/8XW8//H1/T/0d/5/9vn/v/c6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/a5v7/3un//97p//+/0/H/qMPn/6vE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+sxuv/o7zh/x0pQP0KEiTdAQEDOgAAAAcAAAAAAAAAAAAA" +
				"AAIAAAAYAgMHYgcMGLQOFSnkDhYu+Q4YMP0mMEj/jpqv/+Ds/v/m8v//2+f+/9zo/v/T4fr/yNj1/8TW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/G1/T/" +
				"xtf0/8TW8//J2fX/1OL7/9zo///c6P7/2+f+/9vn/v/b5/7/2+f+/9rm/v/c6P//3un//9Df+f+1zO3/qcPn/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owub/rMbr/6O83v8eKUH9ChIk3QEBAzoAAAAHAAAAAAAAAAAAAAAKAQIERgkPHswMFiz0" +
				"Dhgw/RQeN/9vepH/0d3w/+r3///Z5fz/2OT7/9vn/v/b5/7/3Oj//9rm/v/Q3vn/xtf0/8XW8//G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/G1/T/xdb0/8XW" +
				"8//L2/f/1+T8/9zo///c5/7/2ub+/9vn/v/f6v//1uT8/7zR8P+pw+f/qMLn/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLl/6rE6P+rxen/HipD/QoSJN0BAQM6AAAABwAAAAAAAAABAAAADQEDBVELEybgEhwz/VJccv+9ytz/6vf//97q" +
				"///X4/r/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/3ej//9jl/P/M3Pf/xdbz/8XW9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/G1/T/xdbz/8bX9P/P3vj/" +
				"2eb9/97p///a5/7/xNf0/6zG6f+nwub/qsTo/6vE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE" +
				"6P+pwub/p8Hk/67I7f+30/r/g5m7/xYiOv0LEyXdAQEDOgAAAAcAAAAAAAAAAQAAAA0CAwVSCxMl4TdCW/3g8P//4+///9jk/P/Z5fz/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/b5/7/2+f+/9vn/v/c6P7/3Oj//9Xi+//J2vX/xNbz/8bX9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/G1/T/xNbz/8nZ9f/L3Pb/scnr/6bB" +
				"5v+pxOj/q8Xo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+mweX/qsXp/7bS+P+btNj/" +
				"S1x5/w8ZMf8OGC76ChEi1AEBAjcAAAAHAAAAAAAAAAEAAAANAgMFUgsTJeE8RmD9zN37/8bW8v/V4/v/3Oj//9zo/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn" +
				"/v/b5/7/2+f+/9vn/v/c6P//2+f+/9Lg+v/H2PX/xNbz/8bX9P/G1/T/xtf0/8bX9P/G1/T/xtf0/8bX9P/I2PX/uc7u/6bC5v+rxej/qsTo/6rE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+owub/tND2/6nC5/9jd5X/GSQ9/w4YMP0OFi32ChIl3gYJ" +
				"Ep0AAAEkAAAABQAAAAAAAAAAAAAADAIDBVILEyXhO0Ve/c7g/v/E1fH/xdbz/8zc9//Y5f3/3ej//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/" +
				"2+f+/9vn/v/d6P//2eb9/87d+P/F1vT/xdbz/8bX9P/G1/T/xtf0/8bX9P/G1/T/yNj1/7zQ7/+ow+f/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLm/6bB5P+xzPH/ss3y/3qQsf8pNVD/Dhgw/g4YLvoOFyvoBw0bvgMECW8AAQEnAAAACAAAAAEAAAAA" +
				"AAAAAAAAAAwCAwVSCxMl4TpEXv3O4P7/xdbx/8bX9P/F1vP/xtf0/9De+f/a5v7/3Oj//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9zn" +
				"/v/c6P//1+T8/8vb9//F1vP/xdb0/8bX9P/G1/T/xtf0/8jY9f+70O//qMPn/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/" +
				"qsTo/6rE6P+qxOj/p8Hk/63H7P+20vj/j6bJ/z1LZ/8OGDD+Dhgu+w8XLO8KESHQBAcNhgEBAjkAAAAXAAAABQAAAAAAAAAAAAAAAAAAAAAAAAAMAQMFUQsT" +
				"JeE6RF39zt/9/8XW8f/G1/T/xtf0/8bX9P/E1vP/yNn1/9Ph+v/c6P7/3Oj//9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9zo///c6P//" +
				"0+H6/8jZ9f/E1vP/xtf0/8bX9P/I2PX/u9Dv/6jD5/+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+oweX/qsTo/7bS" +
				"+P+gud3/U2SC/xEbM/8OGC78Dhgu8wsTJdsGCRKcAQIETAAAAR0AAAAKAAAABQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAEDBVALEyXhOkRd/czc+v/D1PD/" +
				"xtf0/8bX9P/G1/T/xtf0/8XX9P/F1vP/y9v2/9bj/P/c6P//3Of+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9zo///a5/7/0N/5/8bX" +
				"9P/E1vP/yNj1/7vQ7/+ow+f/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6jC5v+owuX/tM/1/6vG6v9rfp7/HSlB/w4YMP0OFi33" +
				"DBQp4wcMF7ACAwdhAAABJQAAAA0AAAAGAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAsBAwVQCxMl4TZAWf3V6P//zN37/8PT8P/E1fL/xtf0/8bX" +
				"9P/G1/T/xtf0/8XW8//F1/T/zt34/9nm/f/d6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/93o///Y5f3/zNz3/8bX9P+6z+//" +
				"qMPn/6rE6P+qxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+qxOj/qMLm/6fB5P+wy/D/s8/1/4GWuP8uO1b/Dhgw/g4YLvoOFyvqCA4cwwMFCnYAAQEtAAAAEgAA" +
				"AAUAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALAQIFTwsTJuATHDX9U195/7PD3//U5///x9j1/8PU8P/G1/T/xtf0/8bX9P/G1/T/" +
				"xtf0/8XW8//H2PT/0eD5/9vn/v/c6P//2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2+f+/9zo///a5v3/vtLx/6fC5/+qxOj/qsTo/6rE" +
				"6P+qxOj/qsTo/6rE6P+qxOj/psHk/6zG6/+20vn/la3P/0NTb/8OGDD/Dhgu/A4YLfALESLUBAcPjQEBAz4AAAAZAAAABwAAAAMAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACgECBEUJDx/NDBYs9A4YMP0ZJDz/cH6Y/8PU8P/S5P//xNXx/8TV8f/G1/T/xtf0/8bX9P/G1/T/xtf0/8TW" +
				"8//J2fX/1eL7/9zo///c6P7/2+f+/9vn/v/b5/7/2+f+/9vn/v/b5/7/2uf+/93p///c6P7/ydr2/6/I6v+pxOj/qsTo/6rE6P+qxOj/qsTo/6rE6P+owuX/" +
				"qMLm/7XR9/+jveH/WmyK/xMeNv8OGDD9Dhgu9gwUJt4GCROhAgMFUgAAASAAAAAKAAAABQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAMAAAEaAgQIaAcNGrwOFyrnDhYu+Q4YMP4sN0//jJu3/8/h/f/O4P3/w9Pw/8TV8v/G1/T/xtf0/8bX9P/G1/T/xdb0/8XW8//M3Pf/" +
				"1+T8/93o///b5/7/2+f+/9vn/v/a5v7/3Oj//97p///P3vn/tMvs/6bB5v+pw+f/qsTo/6rE6P+qxOj/qsTo/6nD5v+oweT/ss7z/6/J7v9xhqb/IS5H/w4Y" +
				"MP0OFi75DRYp5QcMGLYCBAhnAAEBJgAAAA4AAAAFAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAwAAABQBAQI2BAYNhAoQIM4PFy7uDhgu+w4YMP5EUGn/p7bR/9Tm///J2/j/w9Pw/8bX9P/G1/T/xtf0/8bX9P/G1/T/xdbz/8bX9P/Q3vn/2ub9/9zo" +
				"///c6P7/3+n//9Xj+/+70O//qMPn/6jC5/+rxej/qsTo/6rE6P+qxOj/qMLm/6fB5P+vye7/tdD3/4edv/8zQl3/Dhgw/g4YLvsOFyvsCRAfyQMGDH0BAQIy" +
				"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" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAkAAAEcAQIERwUIEZkLFCXaDhkv8g4YLvwPGTL/ZXOM/5+z0f9BUm7/Dhgw/g4YLvwOFy7v" +
				"CxEi0wQHDosBAQM8AAAAFwAAAAcAAAADAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAABgAAAA0AAAEkAgMGXAYLFqwLEybiDBYs8w4WLPYOFiz2DhYs9gwWLPIKEiTdBgkTngECBU8AAAEfAAAACQAA" +
				"AAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAACAAAABgAAABAAAQErAwUKdAcLF64HDBi3BwwYtwcMGLcGCxWrAgQIZQABASYAAAAOAAAABgAAAAEAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAABAAAABQAAABMAAAEfAAABIQAAASEAAAEhAAABHgAAABAAAAAEAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAA==";

		private static Texture2D MakeStairs(GraphicsDevice device)
		{
			byte[] raw = Convert.FromBase64String(StairData);
			Color[] pixels = new Color[StairSize * StairSize];
			for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
			Texture2D tex = new Texture2D(device, StairSize, StairSize);
			tex.SetData(pixels);
			return tex;
		}

		/// <summary>A small chest: a red lid, a silver band, a blue base and a gold lock, as the game's chests look.</summary>
		private static Texture2D MakeChest(GraphicsDevice device)
		{
			string[] rows =
			{
				"..oooooooooo..",
				".orrrrrrrrrro.",
				".orRRRRRRRRro.",
				".osssssssssso.",
				".obbbbggbbbbo.",
				".obbbbggbbbbo.",
				".obbbbbbbbbbo.",
				".oooooooooooo.",
			};
			Color[] pixels = new Color[ChestW * ChestH];
			for (int y = 0; y < ChestH; y++)
			{
				for (int x = 0; x < ChestW; x++)
				{
					switch (rows[y][x])
					{
						case 'o': pixels[y * ChestW + x] = new Color(28, 16, 36, 255); break;
						case 'r': pixels[y * ChestW + x] = new Color(176, 52, 46, 255); break;
						case 'R': pixels[y * ChestW + x] = new Color(224, 96, 78, 255); break;
						case 's': pixels[y * ChestW + x] = new Color(205, 210, 222, 255); break;
						case 'b': pixels[y * ChestW + x] = new Color(96, 78, 150, 255); break;
						case 'g': pixels[y * ChestW + x] = new Color(250, 208, 86, 255); break;
						default: pixels[y * ChestW + x] = Color.Transparent; break;
					}
				}
			}
			Texture2D tex = new Texture2D(device, ChestW, ChestH);
			tex.SetData(pixels);
			return tex;
		}

		// ---- drawing ----

		private void Paint(int px, int pz)
		{
			if (_tex == null) return;
			if (_batch == null) _batch = new SpriteBatch(GraphicsDevice);
			if (_pixel == null || _pixel.IsDisposed) { _pixel = new Texture2D(GraphicsDevice, 1, 1); _pixel.SetData(new[] { Color.White }); }
			if (_arrow == null || _arrow.IsDisposed) _arrow = MakeArrow(GraphicsDevice);
			if (_chest == null || _chest.IsDisposed) _chest = MakeChest(GraphicsDevice);
			if (_stairs == null || _stairs.IsDisposed) _stairs = MakeStairs(GraphicsDevice);
			if (_exitsDirty && _frame - _exitsAt >= 20) FindExits();

			Viewport view = GraphicsDevice.Viewport;
			int size = Math.Max(96, (int)(view.Height * Sizes[Math.Clamp(Qol.MinimapSize, 0, 2)]));
			int margin = Math.Max(8, (int)(view.Height * 0.03f));
			Rectangle box = new Rectangle(view.Width - size - margin, margin, size, size);
			float scale = size / (2f * (ViewCells - 1));   // pixels a cell

			// The camera's way on the ground, in the map's space: where the map's up is when it turns.
			float fx = (GlobalScope.NNS_G3dGlb.camTarget.x - GlobalScope.NNS_G3dGlb.camPos.x) / 4096f;
			float fz = (GlobalScope.NNS_G3dGlb.camTarget.z - GlobalScope.NNS_G3dGlb.camPos.z) / 4096f;
			float len = (float)Math.Sqrt(fx * fx + fz * fz);
			if (len < 1e-4f) { fx = 0; fz = -1; } else { fx /= len; fz /= len; }
			if (GlobalScope.stg.MirrorZ) fz = -fz;
			if (!Qol.MinimapRotate)
			{
				// Fixed: the way the camera looked when this map was entered.
				if (!_haveFixed) { _fixedX = fx; _fixedZ = fz; _haveFixed = true; }
				fx = _fixedX; fz = _fixedZ;
			}
			float rotation = (float)Math.Atan2(-fx, -fz);   // the picture's x axis on the screen: the map's x axis, turned

			bool solid = Qol.MinimapOpaque;
			Vector2 centre = new Vector2(box.X + size / 2f, box.Y + size / 2f);
			Vector2 party = new Vector2((px / (float)CellFx - _ox) * S, (pz / (float)CellFx - _oz) * S);   // the party's place in the picture, in its texels

			Rectangle oldScissor = GraphicsDevice.ScissorRectangle;
			GraphicsDevice.ScissorRectangle = box;
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, Scissor);
			_batch.Draw(_pixel, box, solid ? new Color(8, 12, 24, 255) : new Color(0, 0, 0, 80));
			// One picture of the floor, drawn once (so, no seams), turned about the party.
			_batch.Draw(_tex, centre, null, solid ? Color.White : Color.White * 0.85f, rotation, party, scale / S, SpriteEffects.None, 0f);

			// The stairs are a picture, smoothed; the chests are small drawn icons, so they are drawn crisp.
			PaintStairs(centre, scale, px, pz, fx, fz, box, size);
			_batch.End();
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Scissor);
			if (Qol.MinimapChests) PaintChests(centre, scale, px, pz, fx, fz, box, size);
			_batch.End();
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, Scissor);

			// The party: an arrow at the middle, pointing the way the hero faces.
			float hu = _faceX * (-fz) + _faceZ * fx, hv = _faceX * fx + _faceZ * fz;
			float turn = hu * hu + hv * hv < 1e-8f ? 0f : (float)Math.Atan2(hu, hv);
			float arrow = Math.Clamp(size * 0.1f, 14f, 40f);
			_batch.Draw(_arrow, centre, null, Color.White, turn, new Vector2(ArrowSize / 2f, ArrowSize / 2f), arrow / ArrowSize, SpriteEffects.None, 0f);
			// The frame.
			Color edge = new Color(235, 235, 245, 220);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, 2, box.Height), edge);
			_batch.Draw(_pixel, new Rectangle(box.Right - 2, box.Y, 2, box.Height), edge);
			_batch.End();
			GraphicsDevice.ScissorRectangle = oldScissor;
		}

		private void PaintStairs(Vector2 centre, float scale, int px, int pz, float fx, float fz, Rectangle box, int size)
		{
			float across = Math.Clamp(size * 0.09f, 18f, 56f);   // the icon's size on the screen
			float half = across / 2f;
			foreach (Vector2 exit in _exits)
			{
				float ox = exit.X - px / (float)CellFx, oz = exit.Y - pz / (float)CellFx;
				float u = ox * (-fz) + oz * fx, v = ox * fx + oz * fz;
				Vector2 at = centre + new Vector2(u * scale, -v * scale);
				if (at.X < box.X + half || at.X > box.Right - half || at.Y < box.Y + half || at.Y > box.Bottom - half) continue;
				_batch.Draw(_stairs, at, null, Color.White, 0f, new Vector2(StairSize / 2f, StairSize / 2f), across / StairSize, SpriteEffects.None, 0f);
			}
		}

		private void PaintChests(Vector2 centre, float scale, int px, int pz, float fx, float fz, Rectangle box, int size)
		{
			GlobalScope.pl.CPlayerManager players = GlobalScope.wld.CBaseSystem.Current.PlayerMng();
			for (int i = 0; i < (int)GlobalScope.pl.MAP_OBJECT_NUM; i++)
			{
				GlobalScope.map.CMapObject o = players.MapObject(i);
				if (o == null || o.getCharacterId() == -1) continue;
				if (o.MapObjType() != GlobalScope.map.MAP_OBJECT_TYPE.TREASURE_BOX) continue;
				if (GlobalScope.FlagManager.singleton().get(o.getFlagGroup(), o.getFlagIndex()) != 0) continue;   // opened
				_pos.copy(o.getPosition());
				GlobalScope.MTX_MultVec43(_pos, _inv, _local);
				float ox = (_local.x - px) / (float)CellFx, oz = (_local.z - pz) / (float)CellFx;
				float u = ox * (-fz) + oz * fx, v = ox * fx + oz * fz;
				Vector2 at = centre + new Vector2(u * scale, -v * scale);
				if (at.X < box.X + 3 || at.X > box.Right - 3 || at.Y < box.Y + 3 || at.Y > box.Bottom - 3) continue;
				// A whole number of pixels a texel, so the little picture stays sharp.
				int texel = Math.Max(1, (int)Math.Round(size * 0.075f / ChestW));
				_batch.Draw(_chest, new Vector2((float)Math.Round(at.X), (float)Math.Round(at.Y)), null, Color.White, 0f, new Vector2(ChestW / 2f, ChestH / 2f), texel, SpriteEffects.None, 0f);
			}
		}
	}
}
