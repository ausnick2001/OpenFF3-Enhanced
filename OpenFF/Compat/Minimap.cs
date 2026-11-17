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
		private const int Radius = 22;              // cells shown each way from the party
		private const int Pad = Radius + 2;         // the picture's margin, so the shown part is always inside it
		private const int Budget = 80;              // cells probed each frame
		private const int ReachCells = 160;         // how far the fill goes from where the map was entered
		private const int RecheckEvery = 150;       // frames before a wall is probed again (a passage may have opened)
		private const int WallRadiusFx = 12288;     // the party's radius, as the character's wall test
		private const int FloorRange = 24576;       // 6 units: ground further from the neighbour's than this is another floor

		private const byte Void = 0, Floor = 1, Wall = 2, Exit = 3, Secret = 4;   // Secret: a wall of one of the classes that can be switched (WALL_02..05)

		private struct Cell { public byte State; public int GroundY; public int Stamp; }

		private static readonly float[] Sizes = { 0.26f, 0.34f, 0.44f };
		private static readonly RasterizerState Scissor = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

		private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
		private readonly Queue<(int cx, int cz)> _frontier = new Queue<(int, int)>();
		private readonly List<(int dx, int dz)> _order = new List<(int, int)>();   // offsets nearest first, for the wall re-checks
		private string _stage;
		private int _frame, _recheckAt, _lastSet;
		private int _startX, _startZ;
		private int _minX, _maxX, _minZ, _maxZ;
		private bool _failed, _haveFixed, _dirty, _realloc, _lastPassages;
		private float _fixedX, _fixedZ;   // the camera's way when the map was entered, for the fixed map

		// The picture of the floor: a pixel a cell, from cell (_ox, _oz).
		private Texture2D _tex;
		private Color[] _pix;
		private int _w, _h, _ox, _oz;

		private SpriteBatch _batch;
		private Texture2D _pixel;

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
			for (int dz = -Radius; dz <= Radius; dz++)
				for (int dx = -Radius; dx <= Radius; dx++)
					if (dx * dx + dz * dz <= Radius * Radius) _order.Add((dx, dz));
			_order.Sort((a, b) => (a.dx * a.dx + a.dz * a.dz).CompareTo(b.dx * b.dx + b.dz * b.dz));
		}

		public static void Attach(Game game)
		{
			game.Components.Add(new Minimap(game));
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
			_cells.Clear(); _frontier.Clear();
			_haveFixed = false; _recheckAt = 0;
			_startX = CellOf(px); _startZ = CellOf(pz);
			_minX = _maxX = _startX; _minZ = _maxZ = _startZ;
			_tex?.Dispose(); _tex = null; _pix = null;
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
			GlobalScope.VecFx32 hero = GlobalScope.wld.CBaseSystem.Current.PlayerMng()?.Player(GlobalScope.chr.CBaseCharacter.getLookIndex())?.getPosition();
			if (hero == null) return false;
			_pos.copy(hero);
			GlobalScope.stageMng.getInvWldMtx(_inv);
			GlobalScope.MTX_MultVec43(_pos, _inv, _local);
			x = _local.x; y = _local.y; z = _local.z;
			return true;
		}

		private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

		private static int CellOf(int fx) => (int)Math.Floor(fx / (double)CellFx);

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
						byte state = ProbeCell(first, mapFlags, colFlags, nx, nz, from.GroundY, out int gy);
						Put(nx, nz, new Cell { State = state, GroundY = gy, Stamp = _frame });
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
				byte state = ProbeCell(first, mapFlags, colFlags, cx, cz, old.GroundY, out int gy);
				Put(cx, cz, new Cell { State = state, GroundY = state == Void ? old.GroundY : gy, Stamp = _frame });
				if (state == Floor || state == Exit) _frontier.Enqueue((cx, cz));
			}
		}

		private byte ProbeCell(GlobalScope.dgs.CRestrictor first, int mapFlags, int colFlags, int cx, int cz, int refY, out int groundY)
		{
			int x = cx * CellFx + CellFx / 2, z = cz * CellFx + CellFx / 2;
			groundY = refY;

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

		private Color ColourOf(byte state)
		{
			switch (state)
			{
				case Floor: return new Color(40, 62, 100, 255);
				case Wall: return new Color(205, 215, 235, 255);
				case Secret: return Qol.MinimapPassages ? new Color(255, 150, 60, 255) : new Color(205, 215, 235, 255);
				case Exit: return new Color(90, 220, 120, 255);
				default: return Color.Transparent;
			}
		}

		/// <summary>A cell found or changed: kept, and put in the picture if it is inside it (else the picture is made again, larger).</summary>
		private void Put(int cx, int cz, Cell c)
		{
			_cells[Key(cx, cz)] = c;
			if (cx < _minX) _minX = cx; if (cx > _maxX) _maxX = cx;
			if (cz < _minZ) _minZ = cz; if (cz > _maxZ) _maxZ = cz;
			_dirty = true;
			if (_pix == null || cx - Pad < _ox || cx + Pad >= _ox + _w || cz - Pad < _oz || cz + Pad >= _oz + _h) { _realloc = true; return; }
			_pix[(cz - _oz) * _w + (cx - _ox)] = ColourOf(c.State);
		}

		private void Rebuild()
		{
			bool passages = Qol.MinimapPassages;
			if (passages != _lastPassages) { _lastPassages = passages; _realloc = true; }
			if (_tex != null && _tex.IsDisposed) _realloc = true;
			if (_realloc)
			{
				// The picture covers every cell found and a margin, with room to grow so it is not made again at every step.
				_ox = _minX - Pad - 16; _oz = _minZ - Pad - 16;
				_w = _maxX + Pad + 16 - _ox + 1; _h = _maxZ + Pad + 16 - _oz + 1;
				_pix = new Color[_w * _h];
				foreach (KeyValuePair<long, Cell> kv in _cells)
				{
					int cx = (int)(kv.Key >> 32), cz = (int)(uint)kv.Key;
					if (kv.Value.State != Void) _pix[(cz - _oz) * _w + (cx - _ox)] = ColourOf(kv.Value.State);
				}
				_tex?.Dispose();
				_tex = new Texture2D(GraphicsDevice, _w, _h);
				_realloc = false; _dirty = true; _lastSet = -99;
			}
			if (_dirty && _frame - _lastSet >= 4)
			{
				_tex.SetData(_pix);
				_dirty = false; _lastSet = _frame;
			}
		}

		// ---- drawing ----

		private void Paint(int px, int pz)
		{
			if (_tex == null) return;
			if (_batch == null) _batch = new SpriteBatch(GraphicsDevice);
			if (_pixel == null || _pixel.IsDisposed) { _pixel = new Texture2D(GraphicsDevice, 1, 1); _pixel.SetData(new[] { Color.White }); }

			Viewport view = GraphicsDevice.Viewport;
			int size = Math.Max(96, (int)(view.Height * Sizes[Math.Clamp(Qol.MinimapSize, 0, 2)]));
			int margin = Math.Max(8, (int)(view.Height * 0.03f));
			Rectangle box = new Rectangle(view.Width - size - margin, margin, size, size);
			float scale = size / (2f * (Radius - 1));   // pixels a cell

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
			Vector2 party = new Vector2(px / (float)CellFx - _ox, pz / (float)CellFx - _oz);   // the party's place in the picture, in its pixels

			Rectangle oldScissor = GraphicsDevice.ScissorRectangle;
			GraphicsDevice.ScissorRectangle = box;
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, Scissor);
			_batch.Draw(_pixel, box, solid ? new Color(8, 12, 24, 255) : new Color(0, 0, 0, 80));
			// One picture of the floor, drawn once (so, no seams), turned about the party.
			_batch.Draw(_tex, centre, null, solid ? Color.White : Color.White * 0.85f, rotation, party, scale, SpriteEffects.None, 0f);

			if (Qol.MinimapChests) PaintChests(centre, scale, px, pz, fx, fz, box);

			// The party: a gold square at the middle.
			_batch.Draw(_pixel, centre, null, new Color(255, 214, 80, 255), 0f, new Vector2(0.5f, 0.5f), Math.Max(4f, scale * 1.4f), SpriteEffects.None, 0f);
			// The frame.
			Color edge = new Color(235, 235, 245, 220);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, 2, box.Height), edge);
			_batch.Draw(_pixel, new Rectangle(box.Right - 2, box.Y, 2, box.Height), edge);
			_batch.End();
			GraphicsDevice.ScissorRectangle = oldScissor;
		}

		private void PaintChests(Vector2 centre, float scale, int px, int pz, float fx, float fz, Rectangle box)
		{
			GlobalScope.pl.CPlayerManager players = GlobalScope.wld.CBaseSystem.Current.PlayerMng();
			Color tint = new Color(255, 120, 200, 255);
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
				float d = Math.Max(5f, scale * 1.5f);
				_batch.Draw(_pixel, at, null, new Color(20, 10, 30, 255), 0f, new Vector2(0.5f, 0.5f), d + 2f, SpriteEffects.None, 0f);
				_batch.Draw(_pixel, at, null, tint, 0f, new Vector2(0.5f, 0.5f), d, SpriteEffects.None, 0f);
			}
		}
	}
}
