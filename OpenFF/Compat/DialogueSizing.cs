// The dialogue's size options (Font / Dialogue on the pause menu; FF3 only): the message window with its text and the speaker's
// name scaled together about the window's bottom centre, the said lines and the name a little larger or smaller on top of that.
// The game's wrapping is in its text (its own line breaks), so a smaller window with a smaller face keeps the same lines.
//
// Not touched: the prologue's and cutscenes' centred lines (NameFace.Story keeps its own size), the menus' text, and a dialogue
// a mod's own layout draws (FieldHud.IsStyled) - that is the layout's.

using System;
using OpenFF.Content;

namespace OpenFF.Client
{
	internal static class DialogueSizing
	{
		public const double BoxMin = 0.6, BoxMax = 1.0, TextMin = 0.7, TextMax = 1.3;

		/// <summary>The line step the game's dialogue uses (a 12 size face and 4 between lines).</summary>
		private const float GameLineStep = 16f;

		private static double Clamp(double v, double min, double max) => double.IsFinite(v) ? Math.Clamp(v, min, max) : 1;

		/// <summary>Whether the dialogue's lines have their drop shadow (FF3's option; FF4 always).</summary>
		public static bool DialogueShadow => GameProfile.IsFf4 || Qol.S.DialogueShadow;
		/// <summary>Whether the menus' text and the other windows' have their drop shadow (FF3's option; FF4 always).</summary>
		public static bool MenuShadow => GameProfile.IsFf4 || Qol.S.MenuShadow;

		/// <summary>The dialogue's shadow: 0 off, 1 light, 2 medium, 3 heavy (FF4: light).</summary>
		public static int DialogueShadowLevel => GameProfile.IsFf4 ? 1 : !Qol.S.DialogueShadow ? 0 : Math.Clamp(Qol.S.DialogueShadowStrength, 1, 3);
		/// <summary>The menus' shadow: 0 off, 1 light, 2 medium, 3 heavy (FF4: light).</summary>
		public static int MenuShadowLevel => GameProfile.IsFf4 ? 1 : !Qol.S.MenuShadow ? 0 : Math.Clamp(Qol.S.MenuShadowStrength, 1, 3);

		private static bool InBattleNow
		{
			get
			{
				try { return (GlobalScope.GAMEPART)GlobalScope.sys.FF3PartSys.getCurrentPart() == GlobalScope.GAMEPART.GAMEPART_BATTLE; }
				catch (Exception) { return false; }
			}
		}

		/// <summary>The strength of one text's drop shadow (1 light, 2 medium, 3 heavy) from its draw flags: 0x8000 marks a dialogue box's lines, 0x10000 a story line (the prologue's, a cutscene's centred words), which keep the light one. A battle's text is always light.</summary>
		public static int ShadowLevelOf(uint flags)
		{
			if (GameProfile.IsFf4 || (flags & 0x10000) != 0 || InBattleNow) return 1;
			int level = (flags & 0x8000) != 0 ? DialogueShadowLevel : MenuShadowLevel;
			return level < 1 ? 1 : level;
		}

		/// <summary>The window, its text and its name together: 0.6 to 1.</summary>
		public static float Box => (float)Clamp(Qol.S.DialogueSize, BoxMin, BoxMax);
		/// <summary>The said lines on top of the box: 0.7 to 1.3.</summary>
		public static float Text => (float)Clamp(Qol.S.DialogueTextSize, TextMin, TextMax);
		/// <summary>The speaker's name on top of the box: 0.7 to 1.3.</summary>
		public static float Name => (float)Clamp(Qol.S.NameSize, TextMin, TextMax);

		/// <summary>Whether the window itself is drawn smaller: FF3, a size under 1, and no layout of a mod's styling the dialogue.</summary>
		private static bool Shrinks => !GameProfile.IsFf4 && Box < 0.999f && !FieldHud.IsStyled;

		private static (float X, float Y) Anchor(BattleHud.Rect window) => (window.X + window.Width / 2f, window.Y + window.Height);

		/// <summary>The window as drawn: the game's rectangle scaled about its bottom centre.</summary>
		public static BattleHud.Rect Window(BattleHud.Rect window)
		{
			if (!Shrinks) return window;
			(float ax, float ay) = Anchor(window);
			float s = Box;
			return new BattleHud.Rect
			{
				X = (int)Math.Round(ax - (ax - window.X) * s),
				Y = (int)Math.Round(ay - (ay - window.Y) * s),
				Width = (int)Math.Round(window.Width * s),
				Height = (int)Math.Round(window.Height * s),
				Found = window.Found,
			};
		}

		/// <summary>A point of the window (its text's start, its name's, its arrow's): scaled the same way.</summary>
		public static (int X, int Y) Point(BattleHud.Rect window, int x, int y)
		{
			if (!Shrinks) return (x, y);
			(float ax, float ay) = Anchor(window);
			float s = Box;
			return ((int)Math.Round(ax - (ax - x) * s), (int)Math.Round(ay - (ay - y) * s));
		}

		/// <summary>The lettering of the said lines: the body font's, or the game's own face when there is none, at the size chosen. Null when nothing changes.</summary>
		public static MenuText Body(MenuText file, float fileScale)
		{
			float size = (GameProfile.IsFf4 || FieldHud.IsStyled) ? 1f : Box * Text;
			if (file == null)
			{
				if (size > 0.999f && size < 1.001f) return null;
				_bodyOwn ??= new MenuText();
				Apply(_bodyOwn, 1f, size, Box, Text);
				return _bodyOwn;
			}
			Apply(file, fileScale, size, Box, Text);
			return file;
		}

		/// <summary>The lettering of a speaker's name: the name font's, or the game's own face, at the size chosen. Null when nothing changes.</summary>
		public static MenuText SpeakerName(MenuText file, float fileScale)
		{
			float size = (GameProfile.IsFf4 || FieldHud.IsStyled) ? 1f : Box * Name;
			if (file == null)
			{
				if (size > 0.999f && size < 1.001f) return null;
				_nameOwn ??= new MenuText();
				Apply(_nameOwn, 1f, size, 1f, 1f);
				return _nameOwn;
			}
			Apply(file, fileScale, size, 1f, 1f);
			return file;
		}

		private static MenuText _bodyOwn, _nameOwn;

		private static void Apply(MenuText style, float baseScale, float size, float box, float text)
		{
			style.Scale = baseScale * size;
			// The game's lines are a step apart whatever the face; a smaller window keeps its lines closer by the same measure, a larger face
			// asks for more room. 1 leaves the style's own spacing.
			float pitch = GameLineStep * box * Math.Max(1f, text);
			if (box > 0.999f && text <= 1.001f) { style.LineHeight = null; style.LineFactor = false; }
			else { style.LineHeight = pitch; style.LineFactor = false; }
		}
	}
}
