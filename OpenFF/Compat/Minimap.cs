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

		private const int ArrowSize = 64, ChestW = 14, ChestH = 8, StairW = 16, StairH = 12;

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

		/// <summary>A small flight of steps going up to the right: pale, with a dark outline.</summary>
		private static Texture2D MakeStairs(GraphicsDevice device)
		{
			Color[] pixels = new Color[StairW * StairH];
			for (int y = 0; y < StairH; y++)
			{
				for (int x = 0; x < StairW; x++)
				{
					int step = x / 4, top = StairH - 3 * (step + 1);
					if (y < top) { pixels[y * StairW + x] = Color.Transparent; continue; }
					bool outline = y == top || y == StairH - 1 || x == StairW - 1 || x % 4 == 0 && y >= top && (x == 0 || y < StairH - 3 * step);
					pixels[y * StairW + x] = outline ? new Color(18, 36, 28, 255) : new Color(150, 235, 170, 255);
				}
			}
			Texture2D tex = new Texture2D(device, StairW, StairH);
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

			// The stairs and chests are small drawn icons, so they are drawn crisp, not smoothed.
			_batch.End();
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Scissor);
			PaintStairs(centre, scale, px, pz, fx, fz, box, size);
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
			int texel = Math.Max(1, (int)Math.Round(size * 0.085f / StairW));
			float half = Math.Max(StairW, StairH) * texel / 2f;
			foreach (Vector2 exit in _exits)
			{
				float ox = exit.X - px / (float)CellFx, oz = exit.Y - pz / (float)CellFx;
				float u = ox * (-fz) + oz * fx, v = ox * fx + oz * fz;
				Vector2 at = centre + new Vector2(u * scale, -v * scale);
				if (at.X < box.X + half || at.X > box.Right - half || at.Y < box.Y + half || at.Y > box.Bottom - half) continue;
				_batch.Draw(_stairs, new Vector2((float)Math.Round(at.X), (float)Math.Round(at.Y)), null, Color.White, 0f, new Vector2(StairW / 2f, StairH / 2f), texel, SpriteEffects.None, 0f);
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
