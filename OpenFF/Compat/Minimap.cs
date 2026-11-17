// A small map of the walls about the party, in the corner of the screen, in dungeons and caves.
//
// It is read from the same collision the party walks on, not from the pictures: for each cell about the party the
// game's own probes are asked the questions the party's feet ask - is there ground here (an arrow down, as the
// ground test), would the party stand here (a sphere of the party's radius against the five wall attributes the
// party is subject to), is there a way out of the map (the twelve map-jump attributes). So what blocks the party
// is a wall on the map and nothing else is: a secret passage that is shut is wall to the probe and wall on the map,
// and shows as floor only once it is open. Objects (chests, rocks that crumble) are not drawn.
//
// The probed cells are kept for the map it is on (a cell is probed again after a couple of seconds, so a passage that
// opens appears). A few cells are probed each frame, nearest to the party first, so there is no hitch when a new
// room is entered. The picture turns with the camera, so up the screen is the way the camera looks.
//
// Shown in a dungeon only (STAGE_TYPE_DUNGEON), and not over a message, a menu, an event, a battle or the pause menu.
// Settings: Quality of life, "Minimap".

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
		private const int PerFrameNew = 56;         // cells probed per frame that have never been probed
		private const int PerFrameStale = 10;        // and ones probed again
		private const int StaleFrames = 120;        // how old a cell is before it is probed again
		private const int WallRadiusFx = 12288;     // the party's radius, as the character's wall test
		private const int FloorRange = 24576;       // 6 units: ground further from the party's than this is another floor

		private const byte Void = 0, Floor = 1, Wall = 2, Exit = 3, Secret = 4;   // Secret: a wall of one of the classes that can be switched (WALL_02..05)

		private struct Cell { public byte State; public int Stamp; }

		private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
		private readonly List<(int dx, int dz)> _order = new List<(int, int)>();   // the offsets in the picture, nearest first
		private string _stage;
		private int _frame;
		private int _staleAt;
		private bool _failed;
		private bool _haveFixed;
		private float _fixedX, _fixedZ;   // the camera's way when the map was entered, for the fixed map
		private static readonly float[] Sizes = { 0.26f, 0.34f, 0.44f };

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
			Log.Write(LogChannel.General, "minimap: on in dungeons (Quality of life)");
		}

		public override void Draw(GameTime gameTime)
		{
			if (_failed || !Qol.Minimap || GameProfile.IsFf4 || RenderTest.Active) return;
			try
			{
				if (!Visible()) return;
				if (!Locate(out int px, out int py, out int pz)) return;
				string stage = GlobalScope.stg.CStageMng.CurrentName ?? "";
				if (stage != _stage) { _stage = stage; _cells.Clear(); _staleAt = 0; _haveFixed = false; }
				_frame++;
				Probe(px, py, pz);
				Paint(px, pz);
			}
			catch (Exception ex)
			{
				_failed = true;   // one failure and it is off for the session, not a failure each frame
				Log.Write(LogChannel.General, "minimap: off - " + ex.GetType().Name + ": " + ex.Message);
			}
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

		/// <summary>Probes a few cells: those never probed, nearest first, then a few that have gone stale.</summary>
		private void Probe(int px, int py, int pz)
		{
			GlobalScope.dgs.CRestrictor first = (GlobalScope.dgs.CRestrictor)GlobalScope.dgs.DGSLinkedList<GlobalScope.dgs.CRestrictor>.dgsllBase();
			if (first == null) return;
			GlobalScope.wld.CMapData map = GlobalScope.wld.CWorldOutSideData.getInstance().MapData();
			int mapFlags = map.isColFlag();
			int colFlags = GlobalScope.wld.CBaseSystem.Current.PlayerMng().Player(GlobalScope.chr.CBaseCharacter.getLookIndex()).getColFlag();
			int pcx = CellOf(px), pcz = CellOf(pz);

			int fresh = 0, stale = 0;
			for (int i = 0; i < _order.Count && fresh < PerFrameNew; i++)
			{
				int cx = pcx + _order[i].dx, cz = pcz + _order[i].dz;
				long key = Key(cx, cz);
				if (!_cells.ContainsKey(key))
				{
					_cells[key] = new Cell { State = ProbeCell(first, mapFlags, colFlags, cx, cz, py), Stamp = _frame };
					fresh++;
				}
			}
			if (fresh > 0) return;
			// Nothing new: probe again some that are old, going round the picture over the frames.
			for (int n = 0; n < _order.Count && stale < PerFrameStale; n++)
			{
				_staleAt = (_staleAt + 1) % _order.Count;
				int cx = pcx + _order[_staleAt].dx, cz = pcz + _order[_staleAt].dz;
				long key = Key(cx, cz);
				if (_cells.TryGetValue(key, out Cell old) && _frame - old.Stamp >= StaleFrames)
				{
					_cells[key] = new Cell { State = ProbeCell(first, mapFlags, colFlags, cx, cz, py), Stamp = _frame };
					stale++;
				}
			}
		}

		private byte ProbeCell(GlobalScope.dgs.CRestrictor first, int mapFlags, int colFlags, int cx, int cz, int py)
		{
			int x = cx * CellFx + CellFx / 2, z = cz * CellFx + CellFx / 2;

			// Ground: an arrow down from above the party's height, as the ground test.
			bool ground = false;
			int groundY = py;
			for (GlobalScope.dgs.CRestrictor r = first; r != null; r = (GlobalScope.dgs.CRestrictor)r.dgsllNext())
			{
				if (!r.rorActivity()) continue;
				_probe.set(x, py + 28672, z);
				if (r.rorEvaluateArrow(_probe, _down, 81920, 1, _result))
				{
					int gy = _result.pos.y;
					if (Math.Abs(gy - py) <= FloorRange) { ground = true; groundY = gy; break; }
				}
			}
			if (!ground) return Void;

			// The way out of the map: the map-jump attributes.
			{
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

		private void Paint(int px, int pz)
		{
			if (_batch == null) _batch = new SpriteBatch(GraphicsDevice);
			if (_pixel == null || _pixel.IsDisposed) { _pixel = new Texture2D(GraphicsDevice, 1, 1); _pixel.SetData(new[] { Color.White }); }

			Viewport view = GraphicsDevice.Viewport;
			int size = Math.Max(96, (int)(view.Height * Sizes[Math.Clamp(Qol.MinimapSize, 0, 2)]));
			int margin = Math.Max(8, (int)(view.Height * 0.03f));
			Rectangle box = new Rectangle(view.Width - size - margin, margin, size, size);
			float scale = size / (2f * (Radius - 1));   // pixels per cell: the circle of cells fills the box

			// The camera's way on the ground, in the map's space: up the picture is where the camera looks.
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
			float rotation = (float)Math.Atan2(-fx, -fz);   // the sprite's x axis on the screen: the map's x axis, turned

			Rectangle oldScissor = GraphicsDevice.ScissorRectangle;
			GraphicsDevice.ScissorRectangle = box;
			_batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });
			bool solid = Qol.MinimapOpaque;
			_batch.Draw(_pixel, box, solid ? new Color(8, 12, 24, 255) : new Color(0, 0, 0, 90));

			Vector2 centre = new Vector2(box.X + size / 2f, box.Y + size / 2f);
			// The party's place inside its cell, so the picture slides smoothly as it walks.
			float offX = px / (float)CellFx - CellOf(px) - 0.5f;
			float offZ = pz / (float)CellFx - CellOf(pz) - 0.5f;
			int pcx = CellOf(px), pcz = CellOf(pz);
			Vector2 origin = new Vector2(0.5f, 0.5f);
			float cell = scale + 0.8f;   // a little over, so turned squares leave no gaps

			for (int dz = -Radius; dz <= Radius; dz++)
			{
				for (int dx = -Radius; dx <= Radius; dx++)
				{
					if (!_cells.TryGetValue(Key(pcx + dx, pcz + dz), out Cell c) || c.State == Void) continue;
					float ox = dx - offX, oz = dz - offZ;
					// offset (ox, oz) in the map's space to the picture: forward is up, right is (-fz, fx).
					float u = ox * (-fz) + oz * fx;
					float v = ox * fx + oz * fz;
					Vector2 at = centre + new Vector2(u * scale, -v * scale);
					bool marked = c.State == Secret && Qol.MinimapPassages;
					Color colour = marked ? new Color(255, 150, 60, 255)
						: c.State == Wall || c.State == Secret ? new Color(205, 215, 235, 255)
						: c.State == Exit ? new Color(90, 220, 120, 255)
						: new Color(40, 62, 100, 255);
					_batch.Draw(_pixel, at, null, colour, rotation, origin, cell, SpriteEffects.None, 0f);
				}
			}

			// Chests not yet opened.
			if (Qol.MinimapChests) PaintChests(centre, scale, px, pz, fx, fz, box);

			// The party: a gold square at the middle.
			_batch.Draw(_pixel, centre, null, new Color(255, 214, 80, 255), 0f, origin, Math.Max(4f, scale * 1.4f), SpriteEffects.None, 0f);
			// The frame.
			Color edge = new Color(235, 235, 245, 220);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), edge);
			_batch.Draw(_pixel, new Rectangle(box.X, box.Y, 2, box.Height), edge);
			_batch.Draw(_pixel, new Rectangle(box.Right - 2, box.Y, 2, box.Height), edge);
			_batch.End();
			GraphicsDevice.ScissorRectangle = oldScissor;
		}
	}
}
