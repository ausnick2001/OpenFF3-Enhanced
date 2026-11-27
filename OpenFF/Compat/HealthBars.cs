// Health bars on the battle party panel (Text and Menus > Battle menus > Health bars): off; a short bar under the HP numbers
// (centred on them, the numbers a little smaller to make room); the whole row filling from the left as health drops, the hero
// whose turn it is outlined in gold; or a long line along the bottom of the row from the name's start. Drawn by PlayerWindow with the
// panel's own rows, so they sit behind the lettering and follow the party menu's size and Compact mode. The bar follows the
// number: it moves only when the number changes (when the damage shows), not when the hit is worked out.

using System;

namespace OpenFF.Client
{
	internal static class HealthBars
	{
		public const int Off = 0, Short = 1, FullRow = 2, Long = 3;
		public static readonly string[] Names = { "Off", "Short bar", "Full row", "Long bar" };

		public static int Mode => Math.Clamp(Qol.S.HealthBars, Off, Long);

		/// <summary>The colour schemes (Colour scheme under Health bars): each a deeper green for a hero at full health and a lighter one for above half, in the game's 5-bit colour. Amber and red are the same in all.</summary>
		public static readonly string[] SchemeNames = { "Forest", "Emerald", "Mint", "Lime", "Teal", "Jade" };
		private static readonly byte[][] SchemeFull = { new byte[] { 3, 19, 8 }, new byte[] { 2, 22, 10 }, new byte[] { 4, 25, 19 }, new byte[] { 12, 25, 5 }, new byte[] { 2, 22, 20 }, new byte[] { 1, 21, 13 } };
		private static readonly byte[][] SchemeWell = { new byte[] { 8, 24, 12 }, new byte[] { 10, 26, 14 }, new byte[] { 16, 29, 23 }, new byte[] { 22, 28, 9 }, new byte[] { 11, 27, 25 }, new byte[] { 8, 26, 18 } };
		public static int Scheme => Math.Clamp(Qol.S.HealthBarColours, 0, SchemeNames.Length - 1);

		/// <summary>Under Full row the hero whose turn it is is outlined in gold; the other styles keep the gold band.</summary>
		public static bool OutlinesTurn => Mode == FullRow;

		/// <summary>The bar turns amber at this fraction of the hero's health or less, and red at the second (the game's own numbers turn yellow at a quarter).</summary>
		public const float AmberAt = 0.5f, RedAt = 0.25f;

		/// <summary>The colour of a hero's HP numbers: with a bar on, white while well, yellow at half or less and red at a quarter or less, as the bar is; with bars off, the game's own.</summary>
		public static GlobalScope.dgs.TXT_COLOR NumberColor(int hero, GlobalScope.dgs.TXT_COLOR vanilla)
		{
			if (Mode == Off || hero < 0 || hero > 3 || _number[hero] < 0) return vanilla;
			try
			{
				int limit = GlobalScope.pl.PlayerParty.instance().player((byte)hero).hp().getLimit();
				if (limit <= 0) return vanilla;
				float f = _number[hero] / (float)limit;
				if (f <= RedAt) return GlobalScope.dgs.TXT_COLOR.TXT_COLOR_RED;
				if (f <= AmberAt) return GlobalScope.dgs.TXT_COLOR.TXT_COLOR_YELLOW;
				return GlobalScope.dgs.TXT_COLOR.TXT_COLOR_WHITE;
			}
			catch (Exception) { return vanilla; }
		}

		// What each hero's number shows now, and what the bar has drawn so far on its way there.
		private static readonly int[] _number = { -1, -1, -1, -1 };
		private static readonly float[] _bar = { -1f, -1f, -1f, -1f };

		public static void Reset()
		{
			for (int i = 0; i < 4; i++) { _number[i] = -1; _bar[i] = -1f; }
		}

		/// <summary>The number on the party panel changed to this (as the damage shows): the bar now runs to it.</summary>
		public static void Set(int hero, int hp)
		{
			if (hero < 0 || hero > 3) return;
			_number[hero] = hp;
			if (_bar[hero] < 0f) _bar[hero] = hp;
		}

		/// <summary>The HP the party panel's number shows (the given one before it has been set).</summary>
		public static int Shown(int hero, int fallback) => hero >= 0 && hero <= 3 && _number[hero] >= 0 ? _number[hero] : fallback;

		/// <summary>One hero's bar on the row from <paramref name="from"/> (top-left) to <paramref name="to"/> (bottom-right), in the game's 480 x 320 units;
		/// textLeft / textRight are the numbers' extent and nameX where the name starts.</summary>
		public static void Draw(int hero, GlobalScope.ds.Vector2<short> from, GlobalScope.ds.Vector2<short> to, bool active, int textLeft, int textRight, int nameX)
		{
			if (hero < 0 || hero > 3) return;
			try
			{
				GlobalScope.pl.Player player = GlobalScope.pl.PlayerParty.instance().player((byte)hero);
				int limit = player.hp().getLimit();
				if (limit <= 0) return;
				if (_number[hero] < 0) Set(hero, player.hp().getNow());
				// Ease towards the number: about half a second from empty to full, at the game's 30 steps a second.
				float step = Math.Max(1f, limit / 15f);
				float d = _number[hero] - _bar[hero];
				_bar[hero] += Math.Abs(d) <= step ? d : Math.Sign(d) * step;

				int mode = Mode;
				if (mode == Off) return;
				float f = Math.Clamp(_bar[hero] / limit, 0f, 1f);
				// The scheme's deep green at full health, its lighter green while well, amber at half or less, red at a quarter or less - the colours of the HP numbers' warnings.
				byte[] well = (_bar[hero] >= limit ? SchemeFull : SchemeWell)[Scheme];
				byte r = well[0], g = well[1], b = well[2];
				if (f <= RedAt) { r = 29; g = 9; b = 7; }
				else if (f <= AmberAt) { r = 30; g = 25; b = 8; }

				int x0 = from.vx, x1 = to.vx, y0 = from.vy, y1 = to.vy;
				int w = x1 - x0;
				int one = Math.Max(1, (int)Math.Round(1f * BattleSizing.PartyBox));
				if (mode == FullRow)
				{
					int fill = (int)Math.Round(w * f);
					if (fill > 0) Quad(x0, y0, x0 + fill, y1, r, g, b, 12);
					Quad(x0, y1 - one, x1, y1, 0, 0, 0, 20);   // a faint line under each row so the rows do not run into one block
					if (active)   // the turn: a gold outline round the row
					{
						Quad(x0, y0, x1, y0 + one, 31, 26, 3, 31);
						Quad(x0, y1 - one, x1, y1, 31, 26, 3, 31);
						Quad(x0, y0, x0 + one, y1, 31, 26, 3, 31);
						Quad(x1 - one, y0, x1, y1, 31, 26, 3, 31);
					}
				}
				else if (mode == Short)
				{
					int bx0 = textLeft, bx1 = textRight;
					if (bx1 - bx0 < 8) { bx0 = x0 + (int)Math.Round(w * 0.655f); bx1 = x0 + (int)Math.Round(w * 0.935f); }
					int by1 = y1 - one, by0 = by1 - Math.Max(1, (int)Math.Round(2f * BattleSizing.PartyBox));
					Quad(bx0, by0, bx1, by1, 0, 0, 0, 22);   // a dark track, no outline
					int fill = (int)Math.Round((bx1 - bx0) * f);
					if (fill > 0) Quad(bx0, by0, bx0 + fill, by1, r, g, b, 29);
				}
				else
				{
					// From the name's start to a little past the numbers, inside the row's soft edges.
					int lx0 = nameX > x0 && nameX < x1 ? nameX : x0 + (int)Math.Round(w * 0.04f);
					int lx1 = x0 + (int)Math.Round(w * 0.943f);
					int ly1 = y1, ly0 = y1 - one;
					Quad(lx0, ly0, lx1, ly1, 0, 0, 0, 18);
					int fill = (int)Math.Round((lx1 - lx0) * f);
					if (fill > 0) Quad(lx0, ly0, lx0 + fill, ly1, r, g, b, 27);
				}
			}
			catch (Exception) { }
		}

		private static void Quad(int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte alpha)
		{
			if (x1 <= x0 || y1 <= y0) return;
			GlobalScope.ds.Vector2<short> a = new GlobalScope.ds.Vector2<short>();
			GlobalScope.ds.Vector2<short> c = new GlobalScope.ds.Vector2<short>();
			a.set((short)x0, (short)y0);
			c.set((short)x1, (short)y1);
			GlobalScope.sys2d.PrimitiveQuadDraw quad = new GlobalScope.sys2d.PrimitiveQuadDraw();
			quad.setPosition(a, c);
			quad.setDepth(0);
			ushort tone = GlobalScope.ds.setGXRgb(r, g, b);
			quad.setColor(tone, tone, tone, tone);
			quad.setAlpha(alpha);
			quad.draw();
		}
	}
}
