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
		private const int SeeCells = 6;             // how near the party must have been for a cell to be shown, with fog of war (at 100%)
		public static readonly float[] Areas = { 1f, 1.25f, 1.5f, 1.75f, 2f };   // the area uncovered, times SeeCells
		private int _see = SeeCells;
		private const int ViewCells = 55;           // cells shown each way from the party
		private const int RecheckCells = 14;        // how far from the party walls are looked at again
		private const int Pad = ViewCells + 2;         // the picture's margin, so the shown part is always inside it
		private const int Budget = 48;              // cells probed each frame
		private const int ReachCells = 160;         // how far the fill goes from where the map was entered
		private const int RecheckEvery = 150;       // frames before a wall is probed again (a passage may have opened)
		private const int WallRadiusFx = 12288;     // the party's radius, as the character's wall test
		private const int FloorRange = 10240;       // 2.5 units (a slope of about 50 degrees over a cell): ground higher or lower than this from the neighbour's is a cliff or another floor, not a way on

		private const byte Void = 0, Floor = 1, Wall = 2, Exit = 3, Secret = 4, Edge = 5;   // Void: not looked at; Edge: looked at, no ground (the floor ends)   // Secret: a wall of one of the classes that can be switched (WALL_02..05)

		private struct Cell { public byte State; public int GroundY; public int Stamp; public sbyte DirX, DirZ; }   // DirX/DirZ: the way it was probed from, kept for probing it again

		private static readonly float[] Sizes = { 0.26f, 0.34f, 0.44f };
		public static readonly float[] Zooms = { 0.5f, 0.75f, 1f, 1.5f, 2f };   // the map zoom: 100% is the scale the map was first made at
		private static readonly RasterizerState Scissor = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

		private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
		private readonly Queue<(int cx, int cz)> _frontier = new Queue<(int, int)>();
		private readonly List<(int dx, int dz)> _order = new List<(int, int)>();   // offsets nearest first, for the wall re-checks
		private string _stage;
		private int _frame, _recheckAt, _lastSet;
		private int _startX, _startZ;
		private int _minX, _maxX, _minZ, _maxZ;
		private bool _failed, _haveFixed, _dirty, _realloc, _lastFog;
		private int _seenX = int.MinValue, _seenZ;
		private Dictionary<long, byte> _memory = new Dictionary<long, byte>();   // the cells shown, by what they were when last found: kept between visits
		private readonly List<(int dx, int dz)> _near = new List<(int, int)>();
		private float _fixedX, _fixedZ;   // the camera's way when the map was entered, for the fixed map

		// The picture of the floor: a pixel a cell, from cell (_ox, _oz).
		private Texture2D _tex;
		private Color[] _pix;
		private int _w, _h, _ox, _oz;

		private SpriteBatch _batch;
		private Texture2D _pixel, _arrow, _chest, _door;
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
			RebuildNear();
		}

		private void RebuildNear()
		{
			_near.Clear();
			for (int dz = -_see; dz <= _see; dz++)
				for (int dx = -_see; dx <= _see; dx++)
					if (dx * dx + dz * dz <= _see * _see) _near.Add((dx, dz));
		}

		/// <summary>The area the party uncovers, as the setting has it: the cells about it are looked at again when it changes.</summary>
		private void SyncSee()
		{
			int see = (int)Math.Round(SeeCells * Areas[Math.Clamp(Qol.MinimapSee, 0, Areas.Length - 1)]);
			if (see == _see) return;
			_see = see;
			RebuildNear();
			_seenX = int.MinValue;
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
				SyncSee();
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
				// The four ways along the axes first, then the diagonals, which only go between two cells the party could walk
				// (a wall one cell thick on a slant must not be squeezed through, or the fill leaks into rooms beyond it).
				for (int pass = 0; pass < 2; pass++)
				for (int dz = -1; dz <= 1; dz++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dz == 0) continue;
						if ((dx != 0 && dz != 0) != (pass == 1)) continue;
						if (pass == 1 && !(Walkable(cx + dx, cz) && Walkable(cx, cz + dz))) continue;
						int nx = cx + dx, nz = cz + dz;
						if (Math.Abs(nx - _startX) > ReachCells || Math.Abs(nz - _startZ) > ReachCells) continue;
						if (_cells.ContainsKey(Key(nx, nz))) continue;
						budget--;
						byte state = ProbeCell(first, mapFlags, colFlags, nx, nz, from.GroundY, dx, dz, out int gy);
						if (state == Void) state = Edge;   // where the floor ends, the map has an edge
						Put(nx, nz, new Cell { State = state, GroundY = gy, Stamp = _frame, DirX = (sbyte)dx, DirZ = (sbyte)dz });
						if (_cells[Key(nx, nz)].State == Floor || _cells[Key(nx, nz)].State == Exit) _frontier.Enqueue((nx, nz));
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
				if (ProbeLog && (state == Floor || state == Exit)) Log.Write(LogChannel.General, "minimap-probe: wall cell " + cx + "," + cz + " (" + (old.State == Secret ? "switchable" : "wall") + ", found going " + old.DirX + "," + old.DirZ + ") is floor on a re-check; the party is at " + pcx + "," + pcz);
				Put(cx, cz, new Cell { State = state, GroundY = state == Edge ? old.GroundY : gy, Stamp = _frame, DirX = old.DirX, DirZ = old.DirZ });
				if (state == Floor || state == Exit) _frontier.Enqueue((cx, cz));
			}

			if (ProbeLog && _frame - _probeAt >= 180) { _probeAt = _frame; DumpProbe(first, mapFlags, colFlags, pcx, pcz, py); }
		}

		// ---- --minimap-probe: what the map found round the party, written to the log every few seconds ----

		private int _probeAt;
		private static readonly bool ProbeLog = Options.Get("minimap-probe") != null;

		/// <summary>
		/// Two pictures of the cells round the party: what the map made of each (F floor, W wall, S switchable wall, E edge, X way out, . not found,
		/// P the party) what the map draws there (- nothing, f floor, w wall, x way out, s marked passage), and which of the five wall classes have any wall at the cell whatever the map's flags say (a digit/letter: bit 1 = WALL_01
		/// ... bit 16 = WALL_05). A wall that is shut shows in the second picture, so if the first calls the cell floor, the flags or the probe are
		/// what missed it.
		/// </summary>
		private void DumpProbe(GlobalScope.dgs.CRestrictor first, int mapFlags, int colFlags, int pcx, int pcz, int py)
		{
			const int R = 11;
			Log.Write(LogChannel.General, "minimap-probe: party cell " + pcx + "," + pcz + " y " + py + " mapFlags 0x" + mapFlags.ToString("X") + " colFlags 0x" + colFlags.ToString("X") + " (north is up: z grows downwards)");
			for (int dz = -R; dz <= R; dz++)
			{
				var states = new System.Text.StringBuilder();
				var classes = new System.Text.StringBuilder();
				var shown = new System.Text.StringBuilder();
				for (int dx = -R; dx <= R; dx++)
				{
					int cx = pcx + dx, cz = pcz + dz;
					shown.Append("-fwxs"[ShownState(cx, cz)]);   // what the map draws: nothing, floor, wall, way out, marked passage
					if (dx == 0 && dz == 0) { states.Append('P'); }
					else if (!_cells.TryGetValue(Key(cx, cz), out Cell c)) { states.Append('.'); }
					else states.Append(c.State == Floor ? 'F' : c.State == Wall ? 'W' : c.State == Secret ? 'S' : c.State == Edge ? 'E' : c.State == Exit ? 'X' : '?');
					if (_cells.TryGetValue(Key(cx, cz), out Cell cc))
					{
						int mask = 0;
						int x = cx * CellFx + CellFx / 2, z = cz * CellFx + CellFx / 2;
						for (int i = 0; i < 5; i++)
						{
							bool hit = false;
							for (int d = 0; d < 4 && !hit; d++)
							{
								_ahead2.set(d == 0 ? 4096 : d == 1 ? -4096 : 0, 0, d == 2 ? 4096 : d == 3 ? -4096 : 0);
								for (GlobalScope.dgs.CRestrictor r = first; r != null && !hit; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
								{
									if (!r.rorActivity()) continue;
									_probe.set(x, cc.GroundY, z);
									if (r.rorEvaluateSphere(_probe, _ahead2, WallRadiusFx, (int)GlobalScope.mcl.MCL_ATTRIBUTE.ATTRIBUTE_WALL_01 + i, _result)
										&& !(_result.normal.x == 0 && _result.normal.y == 0 && _result.normal.z == 0)) hit = true;
								}
							}
							if (hit) mask |= 1 << i;
						}
						classes.Append("0123456789ABCDEFGHIJKLMNOPQRSTUV"[mask]);
					}
					else classes.Append(' ');
				}
				Log.Write(LogChannel.General, "minimap-probe: " + states + "   |   " + classes + "   |   " + shown);
			}
		}

		private bool Walkable(int cx, int cz) => _cells.TryGetValue(Key(cx, cz), out Cell c) && (c.State == Floor || c.State == Exit);

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

			// The walls the party is subject to (the character's wall test: the map's flags against the character's). A wall only pushes back from
			// its front, so a cell is looked at going the way the fill came and then each of the four ways along the axes: whichever way a wall
			// faces, one of them meets it.
			int wall = WallAt(first, mapFlags, colFlags, x, z, groundY, _ahead);
			for (int d = 0; d < 4 && wall == 0; d++)
			{
				_ahead2.set(d == 0 ? 4096 : d == 1 ? -4096 : 0, 0, d == 2 ? 4096 : d == 3 ? -4096 : 0);
				wall = WallAt(first, mapFlags, colFlags, x, z, groundY, _ahead2);
			}
			if (wall != 0) return wall == 1 ? Wall : Secret;
			return Floor;
		}

		private readonly GlobalScope.VecFx32 _ahead2 = new GlobalScope.VecFx32(0, 0, 4096);

		/// <summary>0: no wall; 1: a wall; 2: a wall of one of the classes that can be switched.</summary>
		private int WallAt(GlobalScope.dgs.CRestrictor first, int mapFlags, int colFlags, int x, int z, int groundY, GlobalScope.VecFx32 dir)
		{
			for (int i = 0; i < 5; i++)
			{
				if ((mapFlags & (16 << i)) == 0 || (colFlags & (32 << i)) == 0) continue;
				for (GlobalScope.dgs.CRestrictor r = first; r != null; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
				{
					if (!r.rorActivity()) continue;
					_probe.set(x, groundY, z);
					if (r.rorEvaluateSphere(_probe, dir, WallRadiusFx, (int)GlobalScope.mcl.MCL_ATTRIBUTE.ATTRIBUTE_WALL_01 + i, _result)
						&& !(_result.normal.x == 0 && _result.normal.y == 0 && _result.normal.z == 0)) return i == 0 ? 1 : 2;
				}
			}
			return 0;
		}

		// ---- the picture of the floor ----

		// The floor is kept as a grid of cells, a byte each (what the map shows there), and drawn from a picture of four texels
		// a cell, shaded by smoothing between the cells' middles, so its edges are smooth lines and not steps a cell wide.

		private const int S = 4;   // texels a cell
		private const byte ShowNone = 0, ShowFloor = 1, ShowWall = 2, ShowExit = 3, ShowSecret = 4;
		private byte[] _grid;
		private readonly List<Vector2> _exits = new List<Vector2>();
		private bool _exitsDirty = true;
		private int _exitsAt;
		private int _cw, _ch;
		private int _dx0, _dy0, _dx1 = -1, _dy1;   // the part of the picture changed since it was last sent (empty when _dx1 < _dx0)

		/// <summary>The cells about the party, shown from now on with fog of war (the floor is found before it is shown, so a hidden room is not given away).</summary>
		private bool _seePending;   // some cell near the party could not be judged yet (not explored): looked at again next frame

		private void MarkSeen(int pcx, int pcz)
		{
			bool moved = !(pcx == _seenX && pcz == _seenZ);
			if (!moved && !_seePending) return;
			_seenX = pcx; _seenZ = pcz;
			_seePending = false;
			bool any = false;
			foreach ((int dx, int dz) in _near)
			{
				long key = Key(pcx + dx, pcz + dz);
				if (_memory.ContainsKey(key)) continue;
				// A cell is only seen once what lies between it and the party has been explored: until then it is neither seen nor ruled out
				// (the floor is found a few cells a frame, so right after a map starts, or the party arrives somewhere, nothing is known yet).
				if (!_cells.ContainsKey(key)) { _seePending = true; continue; }
				int sight = Sight(pcx, pcz, pcx + dx, pcz + dz);
				if (sight < 0) { _seePending = true; continue; }
				if (sight == 0) continue;   // behind a wall: the party cannot see it, so it stays blank
				_memory[key] = _cells[key].State;
				SetShown(pcx + dx, pcz + dz);
				any = true;
			}
			if (!moved && !any) return;
			// ...and the ring just outside, whose walls now border seen floor.
			int ring = _see + 1;
			for (int dz = -ring; dz <= ring; dz++)
				for (int dx = -ring; dx <= ring; dx++)
					if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == ring || !_memory.ContainsKey(Key(pcx + dx, pcz + dz)))
						SetShown(pcx + dx, pcz + dz);
		}

		/// <summary>
		/// Whether the party, standing in cell (ax, az), could see cell (bx, bz): 1 if so, 0 if a wall (or a switchable wall) is between, -1 if a cell
		/// between is not explored yet. A slant between two wall cells that touch at a corner is no gap. The wall cell itself is seen (its face),
		/// what lies past it is not.
		/// </summary>
		private int Sight(int ax, int az, int bx, int bz)
		{
			int n = Math.Max(Math.Abs(bx - ax), Math.Abs(bz - az));
			int px = ax, pz = az;
			for (int i = 1; i < n; i++)
			{
				int cx = ax + (int)Math.Round((bx - ax) * (double)i / n, MidpointRounding.AwayFromZero);
				int cz = az + (int)Math.Round((bz - az) * (double)i / n, MidpointRounding.AwayFromZero);
				int o = Opaque(cx, cz);
				if (o != 0) return o < 0 ? -1 : 0;
				if (cx != px && cz != pz)
				{
					int o1 = Opaque(cx, pz), o2 = Opaque(px, cz);
					if (o1 > 0 && o2 > 0) return 0;
					if (o1 < 0 || o2 < 0) return -1;
				}
				px = cx; pz = cz;
			}
			if (n > 0 && bx != px && bz != pz)
			{
				int o1 = Opaque(bx, pz), o2 = Opaque(px, bz);
				if (o1 > 0 && o2 > 0) return 0;
				if (o1 < 0 || o2 < 0) return -1;
			}
			return 1;
		}

		/// <summary>1: wall (or switchable wall); 0: open; -1: not explored yet.</summary>
		private int Opaque(int cx, int cz)
		{
			long key = Key(cx, cz);
			if (!_cells.ContainsKey(key) && !_memory.ContainsKey(key)) return -1;
			byte st = StateOf(cx, cz);
			return st == Wall || st == Secret ? 1 : 0;
		}

		/// <summary>What the map shows of a cell: what is found of it now, else what was kept of it; with fog of war, only if the party has been near.</summary>
		private byte ShownState(int cx, int cz)
		{
			long key = Key(cx, cz);
			bool remembered = _memory.TryGetValue(key, out byte kept);
			byte state = _cells.TryGetValue(key, out Cell c) ? c.State : remembered ? kept : Void;
			if (Qol.MinimapFog && !remembered)
			{
				// Not seen yet: but a wall (or edge) touching floor that has been seen is the floor's border, so it is shown.
				if (state != Wall && state != Edge && state != Secret) return ShowNone;
				bool border = false;
				for (int dz = -1; dz <= 1 && !border; dz++)
					for (int dx = -1; dx <= 1 && !border; dx++)
						border = (dx != 0 || dz != 0) && _memory.TryGetValue(Key(cx + dx, cz + dz), out byte nb) && (nb == Floor || nb == Exit);
				if (!border) return ShowNone;
			}
			if (state == Floor && !Roomy(cx, cz)) return ShowWall;   // a pocket too narrow to walk into is wall, not floor
			switch (state)
			{
				case Floor: return ShowFloor;
				case Exit: return ShowExit;
				case Wall: case Edge: return ShowWall;
				case Secret: return ShowWall;   // a wall that can open is drawn as plain wall
				default: return ShowNone;
			}
		}

		private byte StateOf(int cx, int cz)
		{
			long key = Key(cx, cz);
			if (_cells.TryGetValue(key, out Cell c)) return c.State;
			return _memory.TryGetValue(key, out byte m) ? m : Void;
		}

		/// <summary>Floor with enough floor about it for the party to stand in: the party is wider than a cell, so a cell with one or two floor cells beside it is not a place it can go.</summary>
		private bool Roomy(int cx, int cz)
		{
			int n = 0;
			for (int dz = -1; dz <= 1; dz++)
				for (int dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dz == 0) continue;
					byte st = StateOf(cx + dx, cz + dz);
					if (st == Floor || st == Exit) n++;
				}
			return n >= 3;
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
			for (int dz = -1; dz <= 1; dz++)
				for (int dx = -1; dx <= 1; dx++)
					SetShown(cx + dx, cz + dz);   // its neighbours too: whether they are floor to stand in depends on it
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

		/// <summary>
		/// One texel: the four cells whose middles are nearest, weighed by nearness, give how much floor and how much wall is here. The floor
		/// is filled; the outline is one smooth line along the floor's edge where a wall (or an edge) lies, so it does not matter how thick
		/// the wall cells are. A way out is floor, with an icon over it; a marked passage is an orange line.
		/// </summary>
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
			float af = Soft(f);
			float band = Math.Clamp(1f - Math.Abs(f - 0.5f) / 0.3f, 0f, 1f);
			band = band * band * (3f - 2f * band);
			float ao = band * Math.Clamp(w * 4f, 0f, 1f);
			float mark = w > 1e-4f ? Math.Clamp(p / w, 0f, 1f) : 0f;
			float lr = (205f + (255f - 205f) * mark) / 255f, lg = (215f + (150f - 215f) * mark) / 255f, lb = (235f + (60f - 235f) * mark) / 255f;
			float r = lr * ao + 40f / 255f * af * (1 - ao);
			float g = lg * ao + 62f / 255f * af * (1 - ao);
			float bl = lb * ao + 100f / 255f * af * (1 - ao);
			float alpha = ao + af * (1 - ao);
			return new Color(r, g, bl, alpha);
		}

		private void Rebuild()
		{
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
				int x0 = int.MaxValue, x1 = int.MinValue, z0 = int.MaxValue, z1 = int.MinValue;
				done[start] = true; open.Push(start);
				while (open.Count > 0)
				{
					int at = open.Pop();
					int gx = at % _cw, gz = at / _cw;
					sx += gx; sz += gz; count++;
					if (gx < x0) x0 = gx; if (gx > x1) x1 = gx; if (gz < z0) z0 = gz; if (gz > z1) z1 = gz;
					for (int dz = -6; dz <= 6; dz++)   // runs with a gap of a few cells are one way out
						for (int dx = -6; dx <= 6; dx++)
						{
							int nx = gx + dx, nz = gz + dz;
							if (nx < 0 || nz < 0 || nx >= _cw || nz >= _ch) continue;
							int n = nz * _cw + nx;
							if (_grid[n] == ShowExit && !done[n]) { done[n] = true; open.Push(n); }
						}
				}
				Vector2 mid = new Vector2(_ox + sx / count + 0.5f, _oz + sz / count + 0.5f);
				_exits.Add(mid);
			}
		}

		private bool WallAt(int gx, int gz)
		{
			return gx >= 0 && gz >= 0 && gx < _cw && gz < _ch && _grid[gz * _cw + gx] == ShowWall;
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

		// The door icon (the game's own wooden door texture, 64 x 64, with a dark outline added), as premultiplied RGBA bytes in base64.
		private const string DoorData =
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAYAAAEMAQABDgEAAQ4BAAEOAQABEAEAARABAAEQAQABEAEA" +
				"ARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQ" +
				"AQABEAEAAQ4BAAEOAAABDAAAAAYAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAACAIBAxwFAwdABwQKYAgFDHAJBQx0CQUMdAkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2" +
				"CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx0CAUMcgcE" +
				"CmQFAwdAAgECGgAAAAgAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAEAQABEAQCBTYKBg6GEwsb+BQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQM" +
				"HP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/Ewsb+AoGDoIEAgU0" +
				"AQABEgAAAAYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAQACFAUD" +
				"CEoPCRXAFAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/" +
				"FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/DggUuAUDCEoBAQIWAAAABgAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABEAUDB0YQCRbQFAwc/xQMHP8UDBz/" +
				"FAwc/w8JFf8FAwj/GxAG/y0ZCP80HQv/KhYI/y8aCf81Hw3/NB4M/zMeDf8tGgr/KxcH/ysWB/8qFgf/KhYH/yoWBv8qFwf/KhcI/ywaCf8uHAr/LxwL/y4d" +
				"DP8uHQv/Lx0M/ywaCf8sGAj/KxgI/yoXCP8rFwj/LRoK/zAbCv8dEAj/EAoV/xQMHP8UDBz/FAwc/xQMHP8QChfWBgMJVAEBAhYAAAAGAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABCgQCBTQOCBO2FAwc/xQMHP8UDBz/Ewwb/yUiIP8UCgP/RicP/1sx" +
				"EP9ZMRD/WTIS/1QwEf9XMhL/WjMT/1gyFP9VMRL/VjIR/1YxEP9WMRD/VTIQ/1UyEP9VMhD/VDEQ/1QyEP9VMhD/VTIQ/1QyEP9TMhD/VDIQ/1QyEP9UMRD/" +
				"UjEQ/1MxEf9TMRP/VDIU/1QyFf9VMhT/VzMS/1Y0E/8hFRH/Ewwb/xQMHP8UDBz/FAwc/xEKGNwGAwhQAQECFgAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAIBAyAKBg6IFAwc/xQMHP8UDBz/Ewwa/wEAAf8nGAn/bEAZ/00pDP83HQn/LhsJ/y8ZCf84IAv/" +
				"Nx8L/zQeDP81Hwz/NyAM/zohDP86IQz/PCMM/z4lDP8+JQz/PyYN/0AmDf9AJg3/PyYM/z8mDP8/Jg3/PyYM/z8mDf8+Jgz/PiYN/zojDP83IQz/OyMM/z4k" +
				"DP8+JQz/OiIM/zcgCv8/Iw3/WjcW/ygYDf8SCxn/FAwc/xQMHP8UDBz/EAkX0gUDB0gBAAEQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"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" +
				"FAwc/xQMHP8QCgP/k1sj/0AmDf8bEAb/QCUP/1gzFP9sQRz/bUMf/3hLI/+DWy//MyEL/4VcLv+KXS//dksk/3NJIv9xRyH/ek8m/4NXK/+JXS7/k2U1/4Va" +
				"Lv+EWC3/l2k4/3ZPKf9SNhj/imAx/4tfL/+GWy3/iV0v/4VZLP+FVyv/hVcr/39TKf+LXi//kmEy/0kvFf9WORv/ilwu/4hbLf9/VSr/hlou/49iMf+PYjP/" +
				"RywT/1c3Gf+IXC3/RC8X/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/DQkD/5xk" +
				"Kv9KLRH/GRAF/0AmEP9aNBb/bUMe/25FIP96TCX/g1ou/zUkDf+KYzP/jWIy/31SKP96Tyb/eU4m/4FVKv+MYDL/lGc2/5xtPP+KXjH/jWIz/51vPP+AVy7/" +
				"X0Ef/5FnN/+QZDT/jmIz/5FlNP+PYjL/jV8y/41fMf+IWy//lGc2/5loOP9PMxj/X0Af/49iMv+MYDD/h10w/4tgMf+bbTj/mmw6/08xF/9fPhz/j2Qy/0w3" +
				"Hf8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/wwIA/+PXCn/RyoP/xkQBf9BJhD/" +
				"XDYY/21DHv90SSP/e00m/4RZMP85Jw//i2Q0/5FlNP+FWi3/gFQq/35UKf+HWi7/kWQ0/59yPv+oeUT/i2Ay/5VpOv+idUL/hFow/2ZGI/+Tajr/kmY2/5Vp" +
				"Of+Xazn/k2Y2/5JlNv+UZjf/jmIz/5xuPP+gcD7/UzYZ/2dHI/+WZzb/kGMz/41hM/+QYzT/onM9/55xPv9UNxv/Z0Ug/5VqN/9SPCH/FAwc/xQMHP8JBQx2" +
				"AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8QCgP/hFIi/0ImD/8bEgb/QCYQ/1w3GP9vRSD/ckgj/3hM" +
				"Jf97Uiz/PioS/4ZgMf+RZjT/il8x/4ZaLv+BVy3/iV4x/5JkNf+ccD3/oHZB/41iNP+bbj7/pHhG/4dgNP9pSiX/kWk5/5NnN/+bbz7/nHA+/5drOv+Wajn/" +
				"l2o5/5VqOf+hdEH/onNB/1g6HP9sTCf/mWw5/5NnNv+RZTX/lGc2/6R0P/+idEH/Wj0f/2xKI/+ZbTr/WUIm/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/Fw8G/3hCGP89IAf/FAoA/z4iDP9gOBb/c0cf/3NIIf99TST/fFIo/zkmDf+FXzD/" +
				"kmUy/49hMP+KXC3/g1cr/4ldLv+VZzT/m2w4/5dqNv+QZDP/m247/6R3Qv+HXzH/ZEQg/5NpN/+WZzX/onRB/59yQP+bbjv/mms6/5dpOP+XaTn/onNA/6N0" +
				"QP9RMxX/aEgj/5lrOf+WaTf/lmo3/5dpN/+hcz//oXRC/2FCIf9uSyX/nHA+/2NKLf8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/xILBf+DXTX/Y0ot/z4vHP9UOiD/ZkUl/2pJJ/9yUC3/elcx/3laNf9SPiP/hWc//49tQP+PbEH/jWlA/41p" +
				"P/+QbUL/lnJH/5h0Sf+UcUb/kXBG/5NyR/+aeEz/iGhC/3NXNv+Vc0j/k3BE/599UP+ifk//nnpL/597Tv+mgFH/qIFQ/6yEUv+zi1b/hWhA/49yRv+jfk7/" +
				"m3VG/41mOf+RZjn/l2s5/55xPv9lRCL/cU8o/5xyP/9lTS//FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEA" +
				"AQ4JBQx2FAwc/xQMHP8TDQf/oIle/4JuTP9bSi//V0Yr/1dEKv9WRCv/WEct/11KL/9hTjL/bFY4/2xWN/9uWDn/dF4+/3diQ/95YkP/eGJD/3dhQ/92YEH/" +
				"dmFC/3VhQ/92YUP/dWFD/3pkRv9/akr/eWVF/3ZiQ/98Z0b/gmxJ/4VvS/+HcU7/kHhS/5N8VP+UfFX/nIFX/7SYZv++o2z/0rp+/497VP83LBr/cU4q/31S" +
				"J/+VZzX/ZkQj/3RRKv+bcT7/ZEwv/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/" +
				"DQgE/4pvRf9vWTf/KBwM/zMiD/84JhH/PSoU/z8sFf9GMBj/Tjcc/1I7H/9WPyL/WUEi/11EJP9fRSX/X0Ul/19GJv9gRyf/YEco/2BHKP9gRyj/YEco/19G" +
				"J/9fRif/Y0kq/2FIKv9eRSf/XUQn/1lBJf9eRSb/YEco/11FJv9bQiX/WkIm/1tEJ/9hRyj/ZEop/450R/+Aa0b/KB8O/2I8Gv9rQBr/fFIp/2VEIv91Uiv/" +
				"nHI//2lQMP8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/wsHA/+DaUL/cFs5/zor" +
				"Gf9ROyH/Ujwi/2BIK/9oTzD/cFY1/3ZcOv9/ZUL/hWtH/4dsSf+Kb0v/jXNN/45zTP+Oc03/jnNN/490Tv+PdE7/kHZQ/5N4Uv+Sd1D/k3lS/5p+V/+cgFj/" +
				"mX9W/52CWP+ghVv/pIle/6aKX/+liV7/pIle/6SJXv+agFf/k3hT/5N6VP+KcEv/cFo7/x8ZDP9cNxf/ZTsZ/3BHI/9kQyH/dVMs/59zQP9xVTL/FAwc/xQM" +
				"HP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8MCAT/d147/15KLv80JBP/RjIZ/0kzG/9TPCD/" +
				"WEEk/11GKP9hSSv/alEw/29VM/9wVjX/dFk2/3ddOP93XDr/d1w6/3ZcOf93XDj/dls4/3leOf99YTz/fWA8/35iPP9/Yz3/g2Y+/4lsQv+MbkT/jnBG/5V2" +
				"Sf+YeEn/mntM/5t8S/+Vd0r/f2Q9/31iO/9+Yz3/h2tD/3BaOf8dFwz/XDcX/2Q6GP9uRiL/ZEIh/3RTKv+ziFD/e104/xQMHP8UDBz/CQUMdgEAAQ4AAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/DAgE/3JYN/9WQir/MCEQ/zwqFP89KxT/PywV/0UxGf9MNx3/UDoe/1pC" +
				"Iv9hSCb/YUko/2JKKP9lTCr/Z00s/2pRLv9pUC3/alEu/2pQL/9rUS7/a1Eu/2tRLv9tUi7/a1At/2xRLv9uUi//blIu/29TL/9xVS7/cVMs/2xPKP9wUyz/" +
				"cFQv/21TMP9uVDD/blUx/4puRP91YDz/HBUK/1w3F/9kOhj/a0Mh/2RCIP9zUir/w5xh/4NmP/8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/w8KBf9xWTj/VEAo/y8hEP86KRP/OyoU/z0rFP9FMRn/Tzke/1dAI/9cQyT/YEcm/2BIJ/9hSSj/" +
				"Y0oo/2RLKv9oTiv/Z04s/2lOLP9pTy3/aE4t/2hOLf9pTy3/Z00s/2hOLP9oTi3/aU8s/2pPLP9qUC3/aU8s/3RYNP+FbUj/cVg1/2pPLf9rUS//bFEv/21T" +
				"MP+JbUP/dF49/xwVCv9cNxf/ZDsY/2tDIP9jQSD/c1Mr/82kYf98YDb/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAEAAQ4JBQx2FAwc/xQMHP8PCgX/cFk5/1ZDKv8xJBL/STUc/1M8IP9TPCH/Vj8l/2BIKv9rUjD/bFQ0/21UM/9uVTT/clk3/3ZdOv94Xzz/d147/3le" +
				"O/96Xzv/el86/3leOv95Xjr/el86/3heOv94XTr/eF05/3peOf97Xzr/eV45/3pfO/+IbUf/moFb/4RqRv96Xzr/eV46/3peOf95XTj/iGxD/3JcPP8cFQr/" +
				"WzcX/2Q6GP9qQiD/Y0Eg/3RTK//VqmD/el0w/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQM" +
				"HP8UDBz/EAsF/21WNv9XRSr/NCYU/0UyGf9JNBr/SjUc/005IP9RPCD/VkEl/11GKf9kSyv/aE8t/2tTMf9sUzL/bVUz/3FXNP90Wjb/dFo3/3VbN/92XDj/" +
				"dVs4/3RbN/91Wzf/dlw4/3ZcOP91XDj/dVw4/3VcOP91Wzf/clg1/25VMf90Wzb/dFs3/3ZdOP94Xjj/d144/5J1Sf92Xz3/HBUJ/1s3GP9kOxj/a0Ig/2NB" +
				"H/90Uyv/z6Vg/2lOJ/8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/xMNBv95YT3/" +
				"W0Ys/zQmE/8+KxL/SjMY/0UwFv9KNBn/VT0f/11EJv9gRyj/ZEsp/2tRL/9tUjD/bVMw/21TMf9uVDL/clc0/3VbOP9zWTf/c1o4/3ZbOf96YD3/eWA+/3dd" +
				"Ov93XTn/eF06/3hdOv93XTr/eF89/3phP/96YD7/fWI9/49wSP+Rckf/jm9D/4VnPf+piFT/hWxG/x0UCP9cOBj/YzoY/2pDIP9iQB//dlQs/7SJT/9ROx3/" +
				"FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8KBwT/SToj/zgqGf8kGw3/LCAP/zYo" +
				"FP8yJRT/MycV/zwvGv9GNyH/UUEn/1FCJ/9RQij/UEIp/05AKP9NPyj/Sz4n/09BKv9WRiz/WEct/1hILv9bSi7/X08z/2FQNf9hTzL/XU0w/11NMf9eTjH/" +
				"YVA0/2NSNv9kVTj/Z1Y6/2xZO/+CbEj/gGlF/3tlQP96YDn/inFH/11NMP8WDwT/WzcY/2M7F/9uRR//Y0Ef/3VTLP+nfEX/UDoc/xQMHP8UDBz/CQUMdgEA" +
				"AQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/AQEB/yIQA/8ZDQP/CgYB/x8SBf8oFwf/LhoI/zMeCv82Hwn/" +
				"Mh4K/xUNA/9AKRH/SS0Q/0gsEP9HKg//RSkO/0YpDv9JLA//TTAR/04wEf9OMBD/TC4P/00tDv8zHgj/JBcG/0wvEf9LLg//TjAQ/1AxE/9PMRL/Ti8R/04x" +
				"Ev9LLQ//Si0O/00vEP8yIQv/MiEM/08yFv9NMRX/TjIW/2E6Gf9kORb/dksi/2VCIP93VCz/pXpE/0s0Gf8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/wYDAf8/Hgb/KhYF/wwJAv8xGwj/QiUM/0oqD/9RLxL/VjET/1AvEf8jFgf/XDgX/2o/" +
				"G/9rQR3/bUEd/2tBHv9rQR7/b0Uf/3NJIf9xSCH/c0kh/3FHIP94SiH/UzQW/zYjDP9zRx//ckcf/3NJIf9ySSL/c0oj/3NJI/9zSSL/ckgh/3RIIP94SyL/" +
				"UzcX/0wyFf91SyL/c0kh/3FFHv9rPxv/ZjsY/4daLv9nRCH/dlMr/6Z6RP9fRCH/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8MBwH/Uy0P/zshDf8RCwL/NCAM/0kqD/9aNRb/Yjwa/2xDH/9kPx3/MCEM/3FMJf98Uif/glcs/4NXLP9+VSr/" +
				"gFYs/4NYLv+JXzH/i18z/4FXLP+CWC3/i14x/21KJP9IMhT/jmMz/4deMP+GXTD/hl0w/4ZdMP+GXTD/h1ww/4FWK/+WZzj/kWM0/2RHI/9lRyP/h10v/4Zc" +
				"L/+BViv/eEwj/31SKP+YaTn/Z0Ui/3VTK/+ne0X/bE4n/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEO" +
				"CQUMdhQMHP8UDBz/DwgC/2Q5F/9MLRP/Fw4F/zokD/9UMhP/aT8a/3VKIv+LXC7/hFou/z0qEP+EXTD/kmc1/5hsOf+hcj3/lGc2/5VpN/+Xazr/o3ZB/6R0" +
				"QP+TZzb/lGg3/5puO/+HYDX/W0Ac/6t/Rf+edED/mnA+/5pxPv+bcD//m3A+/5ltPP+SZTb/rn1H/6Z4RP+BXTL/flww/5txPv+ccD7/m3A8/5RoNv+YbDr/" +
				"m289/2pGI/94VCv/qHtF/21NJ/8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/w8I" +
				"Av9kORb/SiwT/xcPBv89JhD/VjMT/2U8Gf92TCH/mmoz/5FkNP89KQ//hF4w/5NoNv+Wazn/pndB/5hrOf+UaTf/lGk4/6R3Qv+kdkD/kGQ0/5JmNv+ZbTr/" +
				"hV80/1o/G/+uhEj/oHZC/5hvPf+acD7/mXA+/5pvPv+XbDv/kWU1/6h6RP+ld0L/hF8y/35bLv+Zbzz/nHA+/5tvPf+bbzz/mG08/5xwP/9rRyP/elQq/7GE" +
				"S/9vUCr/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8PCAL/ZDoW/0wtE/8YEAf/" +
				"PSYQ/1Y0FP9mPhr/b0Yg/45gLv+SZjT/PioQ/4ReMf+TaTj/lWo6/6Z3Qf+dcDz/lGk4/5RpOf+neUX/pnhC/41iM/+PZTX/mm47/4hhNv9aPhv/rIFH/6B3" +
				"Qv+ZcD7/mnE//5pxP/+acD//mG08/5FmN/+md0P/qHpF/4ZhNP99Wy7/m3A9/5xwP/+abz3/m3A+/5luPP+ccT//akcj/3dUKv+0h03/cFIr/xQMHP8UDBz/" +
				"CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/DggC/2M4Ff9GKRH/GBAH/z0mEf9WMxT/ZT0a/21E" +
				"IP+BVir/i2Ex/z4qEP+FXzL/k2k4/5NpOf+kdUD/onM+/5ZqOf+Vajn/pXZD/6R1Qf+NYzP/j2Q1/5puPP+GYDX/XEEd/6N5Qv+ccj//m3E//5pwP/+bcT//" +
				"m3A//5dtPP+TZzb/pnZC/6h6Rf+JYjX/fVsv/5ltOv+Zbj3/nnI+/5twPv+Wazj/nHE//2dGIv95Vyv/sYVL/21PKP8UDBz/FAwc/wkFDHYBAAEOAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/w8IAv9lORX/SisS/xgRCP87JhH/VjMU/2M8Gf9sRSD/gFYq/4phMf89KA//" +
				"h180/5ZrOP+Tajr/o3VA/6R0Pv+Wajj/mWw6/6Z3Q/+hcj//jGIz/45jNP+Yazv/hWA2/11CHv+kekP/nHM//5pxPv+acD//m3E//5twPv+XbTz/lGg2/6Z2" +
				"Qv+oeUT/flct/31ZLv+YbDn/mm49/51xPv+ccD7/lmk3/5ttPP9nRiL/e1kt/6h8Rv9pSyb/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8QCQL/ZDkV/00uE/8YEQj/OSYR/1Y0FP9jPBr/bUUg/4BVKv+KYDL/PSkP/4dgNP+Wazj/k2o5/6N1" +
				"P/+ldj//mW07/5psOv+oeEL/n3E9/41hM/+NYjP/lmk5/4dhN/9gQx//pHpD/5xyQP+acD7/mnA//5txP/+bcT//mG07/5NnNf+ldT//pXdD/3pTKv99WCz/" +
				"lms4/5ltPP+Xazr/mGs6/5NoN/+Xajn/ZEQi/3tZLv+me0X/Z0sn/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAABAAEOCQUMdhQMHP8UDBz/DwgC/2U6Ff9MLRP/GBEI/zomEf9VMxX/Yzwa/2xFIP+AVSr/i2Ey/zwoD/+HYTX/mm48/5JpOf+jdUH/pXU//5ltOv+cbz3/" +
				"pndB/55xPf+NYjT/jmMz/5drOf+GYDb/YUQf/6J5Qv+ccT//mG48/5pwPv+bcT//m3A+/5hsO/+RZjT/pHQ9/6ByP/9wTSb/elQr/5VqOP+abjz/mGs5/5Rp" +
				"OP+RZjX/lWk4/2JEIv98WS7/pXpE/1c+IP8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/" +
				"FAwc/w4IAv9mORX/TS0T/xgRCP85JRH/VjQV/2M8Gv9sRSD/gFUp/45iMv87Jw//iGI2/5twPf+QZzf/oHM//6R1QP+Vazn/nXE//6R2QP+ecDz/jGAy/5Bl" +
				"NP+ZbTv/h181/2FCH/+hd0H/nHA9/5NpOf+abj3/m3A+/5tvPf+YbDv/kmYz/6BwOv+dbzz/b0sm/3dSKf+Vajj/l2o5/5dqOP+UaTj/kGMz/5NmNf9iQyL/" +
				"e1cu/6R5RP9QOR3/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8SCQP/ZTgU/0wt" +
				"Ef8YEAf/OSUQ/1k1Fv9iPBn/bUUg/4RXKv+OYjL/OycP/4diNv+ccT7/kGY3/55xPv+kdUD/lWo5/59yQP+neEL/oXM+/41iM/+PZDX/l2s5/4BbMf9fQiD/" +
				"mG47/5RqOP+TaDn/mW89/5twPv+abjz/lGc3/5JlM/+gbzn/mms5/25LJv93Uir/mGs5/5dpOP+XaTj/lWk4/4xhMv+SZDX/YEEh/3tZL/+jeUT/UTkd/xQM" +
				"HP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/FQsD/2k6Ff9MLBD/GBAH/zgkEP9XNRb/" +
				"Yzwa/21FIP+CVSn/i2Aw/zsnD/+IYjb/nHA+/5BnN/+ecD3/pHRA/5NpOf+YbTz/pXZB/6FzPv+PZDX/kmU2/5drOf99WS//X0Mi/5RrOv+TaTj/k2g4/5ht" +
				"PP+bcD7/mW06/5NlNf+RZTL/oG84/5xtOf9xTif/d1Mq/5dqOf+Xajj/l2k3/5VoOP+MYTL/k2Q0/2FCIf98WzD/pHlE/1E4Hf8UDBz/FAwc/wkFDHYBAAEO" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/xUKA/9rOxX/TCwS/xcQB/80IQ7/VjQW/2M8Gf9sRSD/f1Qo/4hc" +
				"L/87Jw//i2Q3/6N2Qf+QZjf/oHE9/6R1P/+SaDj/l2w7/6V2Qf+ecD3/jGEz/5FlNv+Xazn/f1ow/2BDIv+VbDv/kWY3/5JnOP+ZbTz/m3A+/5lsOv+TZTX/" +
				"kWQy/55sN/+XaDb/elYs/3tVLP+Vajj/l2o4/5lqOP+VaTj/jmIy/5NlNP9oRSL/flsx/6B1Qf9hRSP/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8UCgP/ZzkU/0cpEf8XEAf/MiAN/1Y0FP9iOxn/bUUg/39UKP+FWi7/OycO/4xkNv+neEP/" +
				"kGY3/6FyPf+hcz7/kGY3/5htPP+ld0L/nW89/4xhM/+MYjP/kmc2/4FbMv9fQyH/l207/5FlNv+SZjf/mW49/5pvPf+bbzz/lWg4/49iMv+ZaTb/lmg2/31Y" +
				"Lf96VSz/l2o5/5VpOP+Yajn/kmY3/5BjNP+TZjb/akYi/35cMv+edED/Zkol/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/EAkC/2U4Ff9HKRL/FxAH/zIfDf9VMxT/YzsZ/21EIP9/Uif/gFUs/zwnD/+MZTf/qntF/49lNv+icj7/onM+/49l" +
				"Nv+XbTv/oXRA/5tuPP+NYjT/i2Ey/5BlNf+AWzL/XEIh/5pvPv+UZzj/j2Q2/5RpOf+Xazr/lmo4/5JmNv+AVyz/il4v/5dpNv96Vy3/dFIr/5tvPP+VaTj/" +
				"o3M+/5ZrOf+OYzT/lmg3/2dGIv98Wi//nnRA/1M6Hf8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkF" +
				"DHYUDBz/FAwc/w8IAv9lORX/RygQ/xoQBv8zHwz/VDIT/2I7GP9ySCH/fVIn/3pPJ/89KBD/imM2/6p6RP+TaTr/oXM+/6JzP/+QZzf/lGs6/59zQP+Zazr/" +
				"i2Ez/4lgMv+QZTX/fVgv/1pAIf+YbTz/jWI0/41kNf+UaTj/lGg4/5RoN/+LYTH/elIo/4ZbLf+TZTT/d1Mq/29PKf+mekL/oXVA/6V2Qf+XbDr/kWY1/6Fx" +
				"Pf9nRyP/e1ku/6J3Qv9QOB3/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP8NBwL/" +
				"ZTkW/0wsEP8cEAX/NB4M/04uEv9iOxn/bkYg/3lOJf93Tib/RS8T/41lNv+ldkH/p3dD/6R1QP+kdkD/lGo5/5VrOv+meUP/pnhD/41iNP+OYjT/lGc3/4Bb" +
				"Mf9ZPyD/kmg3/5RnN/+UaDj/m248/5xvPv+abDv/kWU0/4hcL/+HWi7/kmMz/2lGIv9rTCb/q3xE/6FzQP+qekP/nG47/5tuOv+fbzz/Z0Uj/3dWLf+jeEH/" +
				"UTgc/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/DAcC/2c6Fv9PLRL/Gg8G/ygY" +
				"Cv85Iw7/RSkR/1EzF/9aOxv/Wjwd/0QuFf9vTyf/cU4o/3RSLP9xUCn/cU8p/3FQKv90Uiv/fVow/31bMf9uTCb/cU4o/3VSK/9sTCj/XUIi/3NTLP9xTij/" +
				"c1Iq/3hVLP95VS3/d1Ut/3JPKP9tSyX/akgj/3NQKP9VNxr/bE4o/31bMP9uTSj/dFIr/3FPKf90Uyr/dlQr/1U6Hf9uTyr/mm46/1A3Hf8UDBz/FAwc/wkF" +
				"DHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQABDgkFDHYUDBz/FAwc/w4IA/9iOBX/RicP/yMUB/8lFgj/JhcJ/y4bC/84JRD/" +
				"PikT/0EtFv9HMBf/STEX/0oyGP9MNBn/TTQZ/0w0Gv9PNhz/UDcc/083Hf9NNhz/Tzcc/083HP9PNxz/UDgd/1I6Hv9QOB3/Tzcc/083G/9POB3/Tjcd/042" +
				"G/9ONhv/TTUa/082Gv9QNxv/Tzcb/1E4HP9ONhv/TjYa/002G/9NNhv/TTYb/043Hf9KNRz/cFIt/5VqOP9SOiH/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4JBQx2FAwc/xQMHP84JRL/bEQd/2hBHP9jQB3/Xz4d/1U3Gf9bPR3/ZEYh/2lJI/9qTCj/Z0oo/3VU" +
				"Lf95WDD/elgw/3lYMf96WTT/eVkz/3hYMv95WjX/eVk0/3lZNP94WTT/eFk0/3hZNP93WDP/eFgy/3hYMf95WTL/eVk0/3lZNP95WTP/eVky/3hYMf94VzH/" +
				"eFgy/3lZM/93WDH/eFgx/3hYMf95WDP/eVkz/3pZM/96WDP/elgx/45mNv+Tajj/Uj0j/xQMHP8UDBz/CQUMdgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAAAAAAAAAAAAAAABAAEOCQUMdhQMHP8UDBz/IxUJ/z4nD/9DKxL/RCwV/0YvFv9LMhj/VDkd/15CI/9uTyz/cVIu/3ZWMf95WTL/f10z/31cNf92VzP/" +
				"eFg1/3ZXNP96WjT/fl02/31dNv9+XTb/fl42/35eNv9+Xjb/fl42/35eNv9+XjX/fl42/35eNv9+Xjb/fl42/39eNf9/XjX/f182/39fNv9/Xzb/f182/39e" +
				"Nv9/Xjb/f142/39eNv9/Xjb/gF82/3tZM/98WjP/d1gy/1M/KP8UDBz/FAwc/wkFDHYBAAEOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AQABDgkFDHYUDBz/FAwc/wwIA/8PCQL/DgkC/w8JA/8QCgP/FQ0F/yUYC/8vIRD/Uz4j/1hDJ/9aRSj/Xkcq/2ZNLf9kTS7/YEkt/11GLP9fSS3/YUsu/2JL" +
				"Lf9iTC7/Ykwu/2JMLv9iTC7/Ykwu/2JMLv9iTC7/Ykwu/2JMLv9iTC7/Ykwu/2NMLv9jTS7/Y00u/2ZPLv9nUS//ZE4v/2NNLv9iTS7/Ykwv/2FMLv9gSy7/" +
				"Xkot/11JLf9VQSn/Tz0o/1E/KP9OPyv/FAwc/xQMHP8JBQx2AQABDgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAQ4IBQxyFAwc/xQM" +
				"HP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/" +
				"FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQM" +
				"HP8UDBz/FAwc/xQMHP8UDBz/CAUMcgEAAQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEMBwQKXBQMHP8UDBz/FAwc/xQMHP8UDBz/" +
				"FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQM" +
				"HP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/FAwc/xQMHP8UDBz/" +
				"FAwc/wcEClwAAAEMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgIBAhoHBAlaCAUMcgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkF" +
				"DHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2" +
				"CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CQUMdgkFDHYJBQx2CAUMcgcECVoCAQIaAAAAAgAA" +
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAABDAEAAQ4BAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQ" +
				"AQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEA" +
				"ARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAARABAAEQAQABEAEAAQ4AAAEMAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
				"AAAAAA==";

		private static Texture2D MakeDoor(GraphicsDevice device)
		{
			byte[] raw = Convert.FromBase64String(DoorData);
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
			if (_door == null || _door.IsDisposed) _door = MakeDoor(GraphicsDevice);
			if (_exitsDirty && _frame - _exitsAt >= 20) FindExits();

			Viewport view = GraphicsDevice.Viewport;
			int size = Math.Max(96, (int)(view.Height * Sizes[Math.Clamp(Qol.MinimapSize, 0, 2)]));
			int margin = Math.Max(8, (int)(view.Height * 0.03f));
			Rectangle box = new Rectangle(view.Width - size - margin, margin, size, size);
			float scale = view.Height * 0.00315f * Zooms[Math.Clamp(Qol.MinimapZoom, 0, Zooms.Length - 1)];   // pixels a cell: the zoom, not the size, says how much of the floor shows

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
			for (int e = 0; e < _exits.Count; e++)
			{
				Vector2 exit = _exits[e];
				float ox = exit.X - px / (float)CellFx, oz = exit.Y - pz / (float)CellFx;
				float u = ox * (-fz) + oz * fx, v = ox * fx + oz * fz;
				Vector2 at = centre + new Vector2(u * scale, -v * scale);
				if (at.X < box.X + half || at.X > box.Right - half || at.Y < box.Y + half || at.Y > box.Bottom - half) continue;
				_batch.Draw(_door, at, null, Color.White, 0f, new Vector2(StairSize / 2f, StairSize / 2f), across / StairSize, SpriteEffects.None, 0f);
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
