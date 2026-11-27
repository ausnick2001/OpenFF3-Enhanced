// A turn order bar for the battle: who acts next, down the right edge, as FFX's does.
//
// The game makes the whole round's order once, as the round begins (BattleTurnExecute.sortCharacter: every hero and
// monster that is up, fastest first, with a die roll on each speed), then walks it with nowActionCharacter_. The two
// hooks there tell this the order (Roll) and each step along it (Advance); the bar draws what is left of the round,
// the one acting now first and a little proud of the rest, and fills again when the next round rolls its order.
//
// A hero is the same face the party menu shows for the hero in their job (pcN_JJ.NCGR, read as the menu reads it, so
// a replaced face shows here too); a monster is "E1", "E2" ... from the top of the screen down; in a boss battle the
// monster with the most hit points is BOSS, in red. Heroes and monsters that have fallen, or run, since the order was
// made drop out. The Battle menus page has a switch for it (Turn order bar).

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenFF.Client
{
	using Game = Microsoft.Xna.Framework.Game;
	using GameTime = Microsoft.Xna.Framework.GameTime;
	using Color = Microsoft.Xna.Framework.Color;
	using btl = GlobalScope.btl;

	internal static class TurnOrder
	{
		internal sealed class Entry
		{
			public btl.BaseBattleCharacter Who;
			public bool Hero;
			public int Slot;     // the hero's place in the party, 0..3
			public int Job;      // the hero's job
			public int Index;    // the monster's place in the monster party, 0..5
			public bool Boss;
			/// <summary>Loses this round - a back attack takes the heroes' turns, a pre-emptive strike the monsters': not on the bar.</summary>
			public bool Skipped;
		}

		/// <summary>The round's order, fastest first, and how far along it the round is.</summary>
		internal static readonly List<Entry> Round = new List<Entry>();
		internal static int Next;

		/// <summary>Who the battle's target hands are on, by hand (btl.Cursor's type): one for a single target, one each for all or a group.</summary>
		private static readonly Dictionary<int, btl.BaseBattleCharacter> Aims = new Dictionary<int, btl.BaseBattleCharacter>();

		public static void Aim(int hand, btl.BaseBattleCharacter who)
		{
			if (who == null) Aims.Remove(hand); else Aims[hand] = who;
		}

		public static void Unaim(int hand) => Aims.Remove(hand);

		internal static bool Aimed(btl.BaseBattleCharacter who)
		{
			foreach (btl.BaseBattleCharacter a in Aims.Values) if (ReferenceEquals(a, who)) return true;
			return false;
		}

		public static void Clear()
		{
			Round.Clear();
			Next = 0;
			Aims.Clear();
		}

		/// <summary>Each monster slot's number on the bar (0 for E1, up): the monsters taken from the top of the screen down, ties
		/// by slot. Falls back to the slot itself when a position can't be read.</summary>
		private static int[] ScreenPlaces(btl.BattleMonsterParty monsters)
		{
			int[] place = { 0, 1, 2, 3, 4, 5 };
			try
			{
				List<KeyValuePair<int, int>> rows = new List<KeyValuePair<int, int>>();   // slot, screen y
				for (int i = 0; i < 6; i++)
				{
					btl.BattleMonster m = monsters.battleMonster(i);
					if (m == null || !m.isEnable()) continue;
					GlobalScope.VecFx32 where = new GlobalScope.VecFx32();
					GlobalScope.characterMng.getPosition(m.characterMngId(), where);
					GlobalScope.NNS_G3dWorldPosToScrPos(where, out int px, out int py);
					rows.Add(new KeyValuePair<int, int>(i, py));
				}
				rows.Sort((x, y) => x.Value != y.Value ? x.Value.CompareTo(y.Value) : x.Key.CompareTo(y.Key));
				for (int n = 0; n < rows.Count; n++) place[rows[n].Key] = n;
			}
			catch (Exception ex)
			{
				place = new[] { 0, 1, 2, 3, 4, 5 };
				Log.Write(LogChannel.General, "turn order: numbering: " + ex.Message);
			}
			return place;
		}

		/// <summary>The round's order has just been made: the characters in BCM.order(0..count-1).</summary>
		public static void Roll(btl.BattleCharacterManager bcm, int count, btl.BATTLE_OPENING_TYPE opening)
		{
			try
			{
				Round.Clear();
				Next = 0;
				btl.BATTLE_TYPE type = btl.OutsideToBattle.getInstance().battleType();
				bool bossFight = type == btl.BATTLE_TYPE.BOSS_BATTLE || type == btl.BATTLE_TYPE.LAST_BOSS_BATTLE;
				int boss = -1, bossHp = -1;
				btl.BattleMonsterParty monsters = bcm.monsterParty();
				if (bossFight)
				{
					for (int i = 0; i < 6; i++)
					{
						btl.BattleMonster m = monsters.battleMonster(i);
						if (m == null || !m.isEnable()) continue;
						int hp = m.monster().maxHp();
						if (hp > bossHp) { bossHp = hp; boss = i; }
					}
				}
				int[] place = ScreenPlaces(monsters);   // each monster's number: E1 is the one highest on the screen
				for (int i = 0; i < count && i < 12; i++)
				{
					btl.BaseBattleCharacter c = bcm.order(i);
					if (c == null) continue;
					if (c is btl.BattlePlayer hero)
					{
						Round.Add(new Entry { Who = c, Hero = true, Slot = hero.playerId(), Job = hero.player().jobManager().nowJob(), Skipped = opening == btl.BATTLE_OPENING_TYPE.BACK_ATTACK });
					}
					else if (c is btl.BattleMonster monster)
					{
						int slot = 0;
						for (int k = 0; k < 6; k++) if (monsters.battleMonster(k) == monster) { slot = k; break; }
						Round.Add(new Entry { Who = c, Hero = false, Index = place[slot], Boss = slot == boss, Skipped = opening == btl.BATTLE_OPENING_TYPE.INITIATLVE_ATTACK });
					}
				}
			}
			catch (Exception ex)
			{
				Round.Clear();
				Log.Write(LogChannel.General, "turn order: " + ex.Message);
			}
		}

		/// <summary>The one acting has finished: the next in the order is up.</summary>
		public static void Advance()
		{
			Next++;
		}

		/// <summary>Whether this one is still to act: not losing the round (back attack, pre-emptive strike), up, not fallen, not run.</summary>
		internal static bool Standing(Entry e)
		{
			try
			{
				return !e.Skipped && e.Who != null && e.Who.isEnable() && !e.Who.flag(btl.PLAYER_FLAG.PF_DEAD) && !e.Who.flag(btl.PLAYER_FLAG.PF_ESCAPE);
			}
			catch (Exception) { return false; }
		}
	}

	internal sealed class TurnOrderBar : DrawableGameComponent
	{
		private const float TextSpaceWidth = 800f;
		private const float TextSpaceHeight = 480f;
		// In the game's 480 x 320 units: a tile, the gap between tiles, the right margin, the top of the column, the
		// shift of the one acting now. The party's box starts near y 254, so ten tiles fit above it.
		private const float Tile = 20f, Gap = 2f, Margin = 6f, Top = 12f, Proud = 5f;
		private const int TextSize = 9;

		private SpriteBatch _batch;
		private readonly Dictionary<string, Texture2D> _faces = new Dictionary<string, Texture2D>();

		private TurnOrderBar(Game game) : base(game)
		{
			DrawOrder = int.MaxValue - 7;
		}

		public static void Attach(Game game) => game.Components.Add(new TurnOrderBar(game));

		private static bool InBattle
		{
			get
			{
				try { return (GlobalScope.GAMEPART)GlobalScope.sys.FF3PartSys.getCurrentPart() == GlobalScope.GAMEPART.GAMEPART_BATTLE; }
				catch (Exception) { return false; }
			}
		}

		/// <summary>The hero's face for this job, as the party menu reads it, or null (not found, or not a picture).</summary>
		private Texture2D Face(int slot, int job)
		{
			string name = "pc" + (slot + 1) + "_" + (job + 1).ToString("00") + ".NCGR";
			if (_faces.TryGetValue(name, out Texture2D have)) return have;
			Texture2D made = null;
			try
			{
				byte[] png = AppShell.loadFile(name);
				if (png != null && png.Length > 8 && png[0] == 0x89)
				{
					using MemoryStream stream = new MemoryStream(png);
					made = Texture2D.FromStream(GraphicsDevice, stream);
				}
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "turn order: " + name + ": " + ex.Message); }
			_faces[name] = made;
			return made;
		}

		private readonly Dictionary<int, Texture2D> _art = new Dictionary<int, Texture2D>();

		/// <summary>A pre-drawn tile: a monster's (0 for E1, up), the boss's (-1), the boss's glow (-2); null when there is none.</summary>
		private Texture2D Art(int which)
		{
			if (_art.TryGetValue(which, out Texture2D have)) return have;
			Texture2D made = null;
			try
			{
				string text = which == -1 ? TurnOrderArt.Boss : which == -2 ? TurnOrderArt.Glow : which >= 0 && which < TurnOrderArt.Monsters.Length ? TurnOrderArt.Monsters[which] : null;
				if (text != null)
				{
					using MemoryStream stream = new MemoryStream(Convert.FromBase64String(text));
					made = Texture2D.FromStream(GraphicsDevice, stream);
				}
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "turn order: tile " + which + ": " + ex.Message); }
			_art[which] = made;
			return made;
		}

		/// <summary>A fill that shades from one colour at the top to another at the bottom, in bands.</summary>
		private void Shade(Rectangle r, Color top, Color bottom)
		{
			int bands = Math.Max(1, Math.Min(r.Height, 8));
			for (int i = 0; i < bands; i++)
			{
				int y0 = r.Y + r.Height * i / bands, y1 = r.Y + r.Height * (i + 1) / bands;
				Color c = Color.Lerp(top, bottom, bands == 1 ? 0f : i / (float)(bands - 1));
				Ui.Fill(_batch, new Rectangle(r.X, y0, r.Width, y1 - y0), c);
			}
		}

		/// <summary>A monster: a violet square badge, dark outline, a pale rim inside.</summary>
		private void DrawMonsterTile(Rectangle r, float sx)
		{
			int line = Math.Max(1, (int)Math.Round(sx * 0.6f));
			Ui.Fill(_batch, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), new Color(21, 10, 43, 255));
			Shade(r, new Color(138, 85, 214, 255), new Color(66, 34, 124, 255));
			Ui.Frame(_batch, r, new Color(226, 205, 255, 100), line);
		}

		/// <summary>The boss: dark red, an ember border that pulses, an ember diamond in each top corner.</summary>
		private void DrawBossTile(Rectangle r, float sx, GameTime time)
		{
			float k = 0.5f + 0.5f * (float)Math.Sin(time.TotalGameTime.TotalSeconds * 2.0 * Math.PI / 1.4);
			int line = Math.Max(1, (int)Math.Round(sx * 0.7f));
			for (int i = 3; i >= 1; i--)   // the glow, wider and fainter outward
			{
				int grow = (int)Math.Round(sx * (0.6f + i * 0.9f));
				Ui.Frame(_batch, new Rectangle(r.X - grow, r.Y - grow, r.Width + 2 * grow, r.Height + 2 * grow), new Color(255, 77, 61, (int)(k * 95f / i)), line);
			}
			Shade(r, new Color(90, 13, 13, 255), new Color(18, 2, 3, 255));
			Ui.Frame(_batch, r, new Color(255, 77, 61, (int)(190 + 65 * k)), line);
			int d = Math.Max(2, (int)Math.Round(sx * 2f));
			int dy = r.Y + (int)Math.Round(sx * 3f);
			Ui.Fill(_batch, new Rectangle(r.X + (int)Math.Round(sx * 3f), dy, d, d), new Color(255, 77, 61, 255));
			Ui.Fill(_batch, new Rectangle(r.Right - (int)Math.Round(sx * 3f) - d, dy, d, d), new Color(255, 77, 61, 255));
		}

		public override void Draw(GameTime gameTime)
		{
			if (!Qol.S.TurnOrderBar || RenderTest.Active || GameProfile.IsFf4 || PauseMenu.HoldsGame || AutoBattle.Ending) return;
			if (TurnOrder.Next >= TurnOrder.Round.Count || !InBattle || !BattleFlow.Active) return;   // the bar goes with turn-by-turn battles
			if (!(GlobalScope.m_Graphics is GlobalScope.Graphics graphics) || !TrueTypeText.Enabled) return;

			List<TurnOrder.Entry> shown = new List<TurnOrder.Entry>();
			for (int i = TurnOrder.Next; i < TurnOrder.Round.Count; i++)
			{
				if (TurnOrder.Standing(TurnOrder.Round[i])) shown.Add(TurnOrder.Round[i]);
			}
			if (shown.Count == 0) return;

			UiTheme.Ensure(GraphicsDevice);
			Viewport view = GraphicsDevice.Viewport;
			float sx = view.Width / 480f, sy = view.Height / 320f;
			Rectangle Place(int n)
			{
				float x = 480f - Margin - Tile - (n == 0 ? Proud : 0f);
				float y = Top + n * (Tile + Gap);
				return new Rectangle((int)(x * sx), (int)(y * sy), (int)(Tile * sx), (int)(Tile * sy));
			}

			_batch ??= new SpriteBatch(GraphicsDevice);
			_batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp);
			for (int n = 0; n < shown.Count; n++)
			{
				TurnOrder.Entry e = shown[n];
				Rectangle r = Place(n);
				bool plain = e.Hero;   // a hero keeps the plain frame round the face; monsters and the boss carry their own
				Color edge = n == 0 ? new Color(255, 214, 90, 255) : new Color(190, 190, 205, 200);
				if (e.Hero)
				{
					Ui.Fill(_batch, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), new Color(8, 10, 20, 170));
					Texture2D face = Face(e.Slot, e.Job);
					if (face != null) _batch.Draw(face, r, Color.White);
					else Ui.Fill(_batch, r, new Color(34, 40, 70, 235));
				}
				else if (e.Boss)
				{
					Texture2D art = Art(-1), glow = Art(-2);
					if (art != null)
					{
						float k = 0.5f + 0.5f * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 2.0 * Math.PI / 1.4);
						if (glow != null)
						{
							int grow = r.Width;   // the glow's picture is three tiles wide, the tile in the middle
							_batch.Draw(glow, new Rectangle(r.X - grow, r.Y - r.Height, r.Width + 2 * grow, r.Height * 3), new Color(255, 255, 255, (int)(50 + 205 * k)));
						}
						_batch.Draw(art, r, Color.White);
					}
					else DrawBossTile(r, sx, gameTime);
				}
				else
				{
					Texture2D art = Art(e.Index);
					if (art != null) _batch.Draw(art, r, Color.White);
					else DrawMonsterTile(r, sx);
				}
				if (plain || n == 0) Ui.Frame(_batch, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), edge, Math.Max(1, (int)Math.Round(sx * (n == 0 ? 0.9f : 0.5f))));
				// The target hand is on this one: the game's hand beside it, its fingertip at the tile.
				if (TurnOrder.Aimed(e.Who)) UiTheme.HandAt(_batch, r.X - 2f * sx, r.Y + r.Height / 2f, Tile * 0.65f * sx);
			}
			_batch.End();

			// Monsters' labels (and a hero's number when the face is missing).
			graphics.SetImageOrigin(0f, 0f);
			graphics.SetImageRotation(0f);
			graphics.SetImageScale(1f, 1f);
			graphics.DrawStringStart();
			for (int n = 0; n < shown.Count; n++)
			{
				TurnOrder.Entry e = shown[n];
				if (e.Hero && Face(e.Slot, e.Job) != null) continue;
				if (!e.Hero && Art(e.Boss ? -1 : e.Index) != null) continue;   // the picture has its label
				string text = e.Hero ? "P" + (e.Slot + 1) : e.Boss ? "BOSS" : "E" + (e.Index + 1);
				int size = e.Boss ? TextSize - 3 : TextSize;
				while (e.Boss && size > 4 && TrueTypeText.Width(text, size) / TextSpaceWidth * 480f > Tile - 6f) size--;   // the word sits inside the square
				float x480 = 480f - Margin - Tile - (n == 0 ? Proud : 0f);
				float y320 = Top + n * (Tile + Gap);
				float width = TrueTypeText.Width(text, size) / TextSpaceWidth * 480f;
				float tx = (x480 + (Tile - width) / 2f) / 480f * TextSpaceWidth;
				float ty = (y320 + Tile / 2f - size * 320f / 480f * 0.9f) / 320f * TextSpaceHeight;
				if (e.Boss)
				{
					graphics.SetColor(255, 77, 61, 130);   // an ember glow round the word
					graphics.DrawString(text, tx - 1f, ty, size);
					graphics.DrawString(text, tx + 1f, ty, size);
					graphics.DrawString(text, tx, ty + 1f, size);
				}
				else
				{
					graphics.SetColor(21, 10, 43, 230);   // the badge's dark edge colour, as a shadow
					graphics.DrawString(text, tx + 1f, ty + 1f, size);
				}
				graphics.SetColor(255, 255, 255, 255);
				graphics.DrawString(text, tx, ty, size);
			}
			graphics.DrawStringEnd();
		}
	}
}
