// The battle menus' size options (Text and Menus > Battle menus on the pause menu): the command list (with
// Run Away's window, the list's last row on Steam's layout), the party's lines and the help bar scaled together with their text.
// The windows come from the battle_hud rectangles (BattleHud) and each is scaled about the corner of the layout it sits in, so a
// smaller one stays against its screen edge: the command list about its bottom-left, the party about its bottom-right, the help
// bar about its top, centred across the screen. Smaller only: the game's own size is the largest, as the handhelds'
// menus are already big on a monitor.
//
// The text follows the box (Box x Text), and the Text option gives some leeway on top of that.

using System;
using OpenFF.Content;

namespace OpenFF.Client
{
	internal static class BattleSizing
	{
		public const double BoxMin = 0.75, BoxMax = 1.0, TextMin = 0.75, TextMax = 1.0;

		/// <summary>The battle's screen width, in the layout's units.</summary>
		private const float ScreenWidth = 480f;
		/// <summary>A line of the game's battle text: how tall its letters are, for keeping them level in a scaled row.</summary>
		private const float GlyphHeight = 12f;

		private static double Clamp(double v, double min, double max) => double.IsFinite(v) ? Math.Clamp(v, min, max) : 1;

		/// <summary>The windows, 0.75 to 1: 1 as the game draws them.</summary>
		public static float Box => (float)Clamp(Qol.S.BattleBoxSize, BoxMin, BoxMax);
		/// <summary>The text on top of the box: 0.75 to 1.</summary>
		public static float Text => (float)Clamp(Qol.S.BattleTextSize, TextMin, TextMax);
		/// <summary>The letters' size altogether.</summary>
		public static float Letters => Box * Text;
		/// <summary>The party's lines on their own, 0.75 to 1.</summary>
		public static float PartyBox => (float)Clamp(Qol.S.BattlePartySize, BoxMin, BoxMax);
		/// <summary>The party's letters: its own size, with the text option on top.</summary>
		public static float PartyLetters => PartyBox * Text;

		/// <summary>The battle's lists (magic, items), as the options set them when the battle's layout is loaded: how much of its size the panel keeps, and the letters' size.</summary>
		public static float ListBox => Box;
		public static float ListLetters => Letters;

		private static bool Unchanged => Math.Abs(Box - 1f) < 0.001f;
		private static bool PartyUnchanged => Math.Abs(PartyBox - 1f) < 0.001f;

		private static int At(float anchor, float v, float s) => (int)Math.Round(anchor - (anchor - v) * s);

		private static BattleHud.Rect Scale(BattleHud.Rect r, float ax, float ay, float s)
		{
			if (Math.Abs(s - 1f) < 0.001f) return r;
			return new BattleHud.Rect { X = At(ax, r.X, s), Y = At(ay, r.Y, s), Width = (int)Math.Round(r.Width * s), Height = (int)Math.Round(r.Height * s), Found = r.Found };
		}

		private static (int X, int Y) Point(int x, int y, float ax, float ay, float s) => Math.Abs(s - 1f) < 0.001f ? (x, y) : (At(ax, x, s), At(ay, y, s));

		/// <summary>A command row (Run Away's too), scaled about the list's bottom-left.</summary>
		public static BattleHud.Rect List(BattleHud.Rect r)
		{
			if (Unchanged) return r;
			(float x, float bottom) = BattleHud.ListAnchor();
			return Scale(r, x, bottom, Box);
		}

		/// <summary>A party line's rectangle, scaled about the party's bottom-right.</summary>
		public static BattleHud.Rect Party(BattleHud.Rect r)
		{
			if (PartyUnchanged) return r;
			(float right, float bottom) = BattleHud.PartyAnchor();
			return Scale(r, right, bottom, PartyBox);
		}

		public static (int X, int Y) ListPoint(int x, int y)
		{
			if (Unchanged) return (x, y);
			(float ax, float ay) = BattleHud.ListAnchor();
			return Point(x, y, ax, ay, Box);
		}

		public static (int X, int Y) PartyPoint(int x, int y)
		{
			if (PartyUnchanged) return (x, y);
			(float ax, float ay) = BattleHud.PartyAnchor();
			return Point(x, y, ax, ay, PartyBox);
		}

		// ---- the target boxes (the foes' names, Back, Player, All, Group) ----

		/// <summary>A target name box, scaled about the list's top-left (the first box stays where it was).</summary>
		public static (short X, short Y, short W, short H) TargetRow(short x, short y, short w, short h)
		{
			if (Unchanged) return (x, y, w, h);
			BattleHud.Rect r = Scale(new BattleHud.Rect { X = x, Y = y, Width = w, Height = h }, GlobalScope.BATTLE_TARGET_X(), GlobalScope.BATTLE_TARGET_Y(), Box);
			return ((short)r.X, (short)r.Y, (short)r.Width, (short)r.Height);
		}

		/// <summary>One of the buttons along the top (Back on the left, Player / All / Group on the right), scaled about its screen corner.</summary>
		public static (short X, short Y, short W, short H) TargetCorner(short x, short y, short w, short h)
		{
			// The Back button is little used and its corner sits under the help bar's end: a fifth smaller than the other
			// buttons, from its corner, at any menu size.
			if (x == 0)
			{
				w = (short)Math.Round(w * 0.8f);
				h = (short)Math.Round(h * 0.8f);
			}
			if (Unchanged) return (x, y, w, h);
			BattleHud.Rect r = Scale(new BattleHud.Rect { X = x, Y = y, Width = w, Height = h }, x == 0 ? 0f : ScreenWidth, 0f, Box);
			return ((short)r.X, (short)r.Y, (short)r.Width, (short)r.Height);
		}

		/// <summary>How far in from a target box's left edge its text starts.</summary>
		public static int TargetTextX => (int)Math.Round(8f * Box);

		/// <summary>How far down a target box its text's top is, so the line sits in the middle.</summary>
		public static int TargetTextY(int boxHeight) => (boxHeight - (int)Math.Round(GlyphHeight * Letters)) / 2 + (int)Math.Round(4f * (1f - Box));

		/// <summary>Whether the top buttons' words sit in the middle of their boxes (once the boxes are smaller; the game's own size keeps its left-aligned words).</summary>
		public static bool CentreTargetButtons => !Unchanged;

		/// <summary>Where the hand stands on the All / Group box whose unscaled left edge is boxX.</summary>
		public static (int X, int Y) TargetCornerHand(int boxX)
		{
			if (Unchanged) return (boxX + 8, 16);
			float left = ScreenWidth - (ScreenWidth - boxX) * Box;
			return ((int)Math.Round(left + 8f * Box), (int)Math.Round(20f * Box - 4f));
		}

		/// <summary>How far left the hand sits as the list shrinks: its picture stays the same size while the names come closer to it.</summary>
		public static int HandBack => (int)Math.Round(36f * (1f - Box));

		/// <summary>How far a text's top moves up so a larger or smaller face stays level with its row's middle.</summary>
		public static int Level => (int)Math.Round(GlyphHeight * (Letters - Box) / 2f);

		/// <summary>The help bar scaled: its top stays, and the edge it sits against (the right one for the narrower bar, the middle for the wide one).</summary>
		public static (int CX, int CY, int W, int H) Help((int CX, int CY, int W, int H) bar)
		{
			if (Unchanged) return bar;
			float s = Box;
			int w = Math.Max(1, (int)Math.Round(bar.W * s)), h = Math.Max(1, (int)Math.Round(bar.H * s));
			int top = bar.CY - bar.H / 2;
			return ((int)(ScreenWidth / 2f), top + h / 2, w, h);   // centred across the screen once it is smaller
		}

		// ---- the text ----

		private static MenuText _own, _help, _party;

		/// <summary>The party's lettering at its own size; null when it is as the game draws it.</summary>
		public static MenuText PartyLettering()
		{
			float k = PartyLetters;
			if (Math.Abs(k - 1f) < 0.001f) return null;
			_party ??= new MenuText { AlignScaled = true };
			_party.Scale = k;
			return _party;
		}

		/// <summary>The party's names' face (a copy) at the party's size.</summary>
		public static MenuText PartyWith(MenuText style) => style == null ? PartyLettering() : WithScale(style, PartyLetters);

		/// <summary>The lettering for a window's text at the size chosen; null when it is as the game draws it.</summary>
		public static MenuText Lettering()
		{
			float k = Letters;
			if (Math.Abs(k - 1f) < 0.001f) return null;
			_own ??= new MenuText { AlignScaled = true };
			_own.Scale = k;
			return _own;
		}

		/// <summary>The help bar's text: the size of its box. (The same as the others' now that nothing is larger than the game's.)</summary>
		public static MenuText HelpLettering()
		{
			float k = Letters;
			if (Math.Abs(k - 1f) < 0.001f) return null;
			_help ??= new MenuText { AlignScaled = true };
			_help.Scale = k;
			return _help;
		}

		private static readonly System.Collections.Generic.Dictionary<int, MenuText> _scaledSet = new System.Collections.Generic.Dictionary<int, MenuText>();

		/// <summary>A plain lettering at a set size (the magic list's four rows); null at the game's size.</summary>
		public static MenuText Scaled(float k)
		{
			if (Math.Abs(k - 1f) < 0.001f) return null;
			int key = (int)Math.Round(k * 1000f);
			if (!_scaledSet.TryGetValue(key, out MenuText t)) _scaledSet[key] = t = new MenuText { AlignScaled = true, Scale = k };
			return t;
		}

		/// <summary>A lettering already in use (the heroes' names' face, say) at the size chosen: a copy, the original left as it is.</summary>
		public static MenuText With(MenuText style) => style == null ? Lettering() : WithScale(style, Letters);

		private static MenuText WithScale(MenuText style, float k)
		{
			if (Math.Abs(k - 1f) < 0.001f) return style;
			MenuText copy = new MenuText
			{
				Key = style.Key, Shadows = style.Shadows, Bold = style.Bold, Italic = style.Italic, LetterSpacing = style.LetterSpacing,
				Scale = style.Scale * k, LineHeight = style.LineHeight, LineFactor = style.LineFactor, Underline = style.Underline,
				LineThrough = style.LineThrough, Transform = style.Transform, StrokeWidth = style.StrokeWidth, StrokeColour = style.StrokeColour,
				AlignScaled = true,
			};
			copy.Families.AddRange(style.Families);
			return copy;
		}
	}
}
