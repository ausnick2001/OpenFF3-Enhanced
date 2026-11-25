// The battle's HUD geometry from the "battle_hud" screen of BattleDefine.xbn (Shared's
// BattleHudLayout puts the game's own numbers there when the file has none; a mod's copy
// replaces them). The game's code asks here instead of its constants: the command window's
// rows, the Run Away button, the cursor's seat, the scroll arrows, the party lines.
//
// Read once per load of the file (MenuManager.LoadXbnFile tells us), straight off the XBN
// node tree the game holds - no Medgets, since the battle never builds this screen: it only
// needs the rectangles.

using System;
using System.Collections.Generic;
using OpenFF.Content;

namespace OpenFF.Client
{
	internal static class BattleHud
	{
		public struct Rect
		{
			public int X, Y, Width, Height;
			public bool Found;
		}

		private static Dictionary<string, Rect> _frames;   // "screen/path" -> rectangle, from the layout file the game holds
		private static bool _noted;

		/// <summary>The file was loaded again: read the geometry afresh next time.</summary>
		public static void Invalidate() { _frames = null; }

		/// <summary>A frame of battle_hud by id ("cmd1", or "player2/hp" for a child), absolute; Found false when the layout lacks it.</summary>
		public static Rect Frame(string path) => Frame(BattleHudLayout.Screen, path);

		/// <summary>A frame of any screen in the layout file the game holds now, by screen and id path.</summary>
		public static Rect Frame(string screen, string path)
		{
			// The field's HUD as the client last built it (FieldHud): its frames there while another file is loaded too.
			if (string.Equals(screen, BattleHudLayout.FieldScreen, StringComparison.OrdinalIgnoreCase) && FieldHud.TryRect(path, out Rect kept)) return kept;
			try
			{
				if (_frames == null) Read();
				if (_frames != null && _frames.TryGetValue(screen + "/" + path, out Rect r)) return r;
			}
			catch (Exception ex) { if (!_noted) { _noted = true; Log.Write(LogChannel.General, "battle hud: " + ex.Message); } }
			return default;
		}

		/// <summary>A frame, or the fallback rectangle when the layout has none (the game's own numbers).</summary>
		public static Rect Frame(string path, int x, int y, int w, int h) => Frame(BattleHudLayout.Screen, path, x, y, w, h);

		public static Rect Frame(string screen, string path, int x, int y, int w, int h)
		{
			Rect r = Frame(screen, path);
			return r.Found ? r : new Rect { X = x, Y = y, Width = w, Height = h, Found = false };
		}

		private static void Read()
		{
			_frames = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
			GlobalScope.XbnNode root = GlobalScope.menu.MenuManager.getSingleton()?.xbnRoot();
			if (root == null) { _frames = null; return; }
			// Every screen of the file: the ones the code reads (battle_hud, field_hud) are few, and the walk is cheap.
			// The nodes lie in one flat array, so a node's nextSibling past its parent's last child is simply the
			// next node of the file - the children are walked by their count, as the game does, never to a null.
			GlobalScope.XbnNode menu = root.firstChild();
			for (int i = 0, n = root.numberOfChildren(); i < n && menu != null; i++, menu = menu.nextSibling())
			{
				if (menu.nodeName() != "menu") continue;
				string screen = menu.getFirstNodeByTagNameFromChildren("name")?.nodeValueString();
				if (string.IsNullOrEmpty(screen)) continue;
				GlobalScope.XbnNode frame = menu.firstChild();
				for (int j = 0, m = menu.numberOfChildren(); j < m && frame != null; j++, frame = frame.nextSibling())
				{
					if (frame.nodeName() == "frame") Walk(frame, screen, 0, 0);
				}
			}
		}

		private static void Walk(GlobalScope.XbnNode frame, string parentPath, int px, int py)
		{
			string id = frame.getFirstNodeByTagNameFromChildren("id")?.nodeValueString();
			if (string.IsNullOrEmpty(id)) return;
			int x = px + Int(frame, "x"), y = py + Int(frame, "y");
			string path = parentPath == null ? id : parentPath + "/" + id;
			_frames[path] = new Rect { X = x, Y = y, Width = Int(frame, "width"), Height = Int(frame, "height"), Found = true };
			GlobalScope.XbnNode child = frame.firstChild();
			for (int i = 0, n = frame.numberOfChildren(); i < n && child != null; i++, child = child.nextSibling())
			{
				if (child.nodeName() == "frame") Walk(child, path, x, y);
			}
		}

		private static int Int(GlobalScope.XbnNode frame, string tag)
		{
			GlobalScope.XbnNode n = frame.getFirstNodeByTagNameFromChildren(tag);
			if (n == null) return 0;
			try { return n.nodeValueInt(); } catch (Exception) { return int.TryParse(n.nodeValueString(), out int v) ? v : 0; }
		}

		// ---- what the game's code asks ----

		/// <summary>Command row i (0..2), or the Run Away window (3).</summary>
		public static Rect CommandRow(int i) => BattleSizing.List(RawRow(i));

		/// <summary>A row as the layout has it: the cmd rows, and Run Away's window as the last (Steam's list has five rows).</summary>
		private static Rect RawRow(int i) => i < CmdRows
			? Frame("cmd" + i, GlobalScope.BATTLE_COMMAND_X(), GlobalScope.BATTLE_COMMAND_Y() + (i + 1) * 40, 128, 40)
			: Frame("run_away", 400, 0, 80, 40);

		/// <summary>The list's bottom-left corner in the layout's own numbers: where BattleSizing scales its rows about.</summary>
		public static (float X, float Bottom) ListAnchor()
		{
			int left = int.MaxValue, bottom = int.MinValue;
			for (int i = 0; i <= CmdRows; i++)
			{
				Rect r = RawRow(i);
				left = Math.Min(left, r.X);
				bottom = Math.Max(bottom, r.Y + r.Height);
			}
			return (left, bottom);
		}

		/// <summary>The party's bottom-right corner in the layout's own numbers: where its lines scale about.</summary>
		public static (float Right, float Bottom) PartyAnchor()
		{
			int right = int.MinValue, bottom = int.MinValue;
			for (int i = 0; i < 4; i++)
			{
				Rect r = Frame("player" + i + "/gauge", 282, GlobalScope.BATTLE_PLAYER_Y() - 2 + i * 16, 194, 16);
				right = Math.Max(right, r.X + r.Width);
				bottom = Math.Max(bottom, r.Y + r.Height);
			}
			return (right, bottom);
		}

		/// <summary>How many of the list's windows are 'cmd' rows: the last is Run Away's (Steam's list has five rows, the phone's four windows).</summary>
		private static int CmdRows => GlobalScope.pl.BATTLE_COMMAND_MAX - 1;

		/// <summary>Where a row's name sits, relative to the row.</summary>
		public static (int X, int Y) CommandTextOffset(int i)
		{
			// The offset in the game's own numbers, scaled with the row and kept level with its middle as the face changes size.
			Rect row = RawRow(i);
			Rect text = Frame((i < CmdRows ? "cmd" + i : "run_away") + "/text");
			(int ox, int oy) = text.Found ? (text.X - row.X, text.Y - row.Y) : (8, 14);
			return ((int)Math.Round(ox * BattleSizing.Box), (int)Math.Round(oy * BattleSizing.Box) - BattleSizing.Level);
		}

		/// <summary>Where the hand points in a row, absolute.</summary>
		public static (int X, int Y) CommandCursor(int i)
		{
			Rect row = RawRow(i);
			Rect cursor = Frame((i < CmdRows ? "cmd" + i : "run_away") + "/cursor");
			(int cx, int cy) = cursor.Found ? (cursor.X, cursor.Y) : i < CmdRows ? (row.X + 8, row.Y + 20) : (408, 20);
			// The hand does not shrink with the row, so it sits further left the smaller the list is, clear of the name.
			(int hx, int hy) = BattleSizing.ListPoint(cx, cy);
			return (hx - BattleSizing.HandBack, hy);
		}

		/// <summary>The scroll triangle: 0 the up one, 1 the down one.</summary>
		public static (int X, int Y) Arrow(int i)
		{
			Rect a = Frame(i == 0 ? "arrow_up" : "arrow_down", GlobalScope.BATTLE_COMMAND_X() + 128, GlobalScope.BATTLE_COMMAND_Y() + (i == 0 ? 38 : 98), 16, 16);
			return BattleSizing.ListPoint(a.X, a.Y);
		}

		/// <summary>A party line's name position.</summary>
		public static (int X, int Y) PlayerName(int i)
		{
			Rect r = Frame("player" + i + "/name", 284, GlobalScope.BATTLE_PLAYER_Y() + i * 16, 136, 16);
			return BattleSizing.PartyPoint(r.X, r.Y);
		}

		/// <summary>Where the hand stands to point at a hero's line of the party menu: its fingertip a gap before the name, level with the line's middle. back is how far the hand's picture reaches left of its point.</summary>
		public static (int X, int Y) PartyHand(int i, int back)
		{
			Rect r = BattleSizing.Party(Frame("player" + i + "/name", 284, GlobalScope.BATTLE_PLAYER_Y() + i * 16, 136, 16));
			// The letters are 12 units high from the line's top, their middle 6 down (less as the line shrinks), and the hand's fingertip rides about 2 units
			// below the point it is given at any size (the hand does not shrink): so the point is the letters' middle less 2.
			return (r.X - back, r.Y + (int)Math.Round(6f * BattleSizing.PartyBox - 2f));
		}

		/// <summary>A party line's HP position (the game right-aligns the number to it).</summary>
		public static (int X, int Y) PlayerHp(int i)
		{
			Rect r = Frame("player" + i + "/hp", 424, GlobalScope.BATTLE_PLAYER_Y() + i * 16, 52, 16);
			return BattleSizing.PartyPoint(r.X, r.Y);
		}

		/// <summary>The help line's window: centre and size, the wide or the small one.</summary>
		public static (int CX, int CY, int W, int H) Help(bool small, int width = 0)
		{
			Rect r = small ? Frame("help_small", 84, 4, 392, 24) : Frame("help", 4, 4, 472, 24);
			// A set width is a short bar in the middle of the screen (a foe's name), clear of the buttons along the top.
			if (width > 0) return BattleSizing.Help((240, r.Y + r.Height / 2, width, r.Height));
			return BattleSizing.Help((r.X + r.Width / 2, r.Y + r.Height / 2, r.Width, r.Height));
		}

		// ---- the field's windows (field_hud in WorldDefine.xbn) ----

		/// <summary>The dialogue window's rectangle.</summary>
		// FF4's own (libff4 menu::MessageWindow::mwOpen: centred at 240, 280, 472 x 72; its text at 12, 252 - mwInitialize).
		public static Rect Dialogue() => GameProfile.IsFf4
			? Frame(BattleHudLayout.FieldScreen, "dialogue", 4, 244, 472, 72)
			: DialogueSizing.Window(RawDialogue());
		private static Rect RawDialogue() => Frame(BattleHudLayout.FieldScreen, "dialogue", 5, 233, 470, 84);
		/// <summary>Where the dialogue's first line starts.</summary>
		public static (int X, int Y) DialogueText() { Rect r = GameProfile.IsFf4 ? Frame(BattleHudLayout.FieldScreen, "dialogue/text", 12, 252, 456, 60) : Frame(BattleHudLayout.FieldScreen, "dialogue/text", 16, 246, 448, 60); return GameProfile.IsFf4 ? (r.X, r.Y) : DialogueSizing.Point(RawDialogue(), r.X, r.Y); }
		/// <summary>Where the dialogue's first line starts in the game's own layout, whatever size the options chose: what a script's SetMessagePosition is measured from (cutscene lines).</summary>
		public static (int X, int Y) DialogueTextBase() { Rect r = GameProfile.IsFf4 ? Frame(BattleHudLayout.FieldScreen, "dialogue/text", 12, 252, 456, 60) : Frame(BattleHudLayout.FieldScreen, "dialogue/text", 16, 246, 448, 60); return (r.X, r.Y); }
		/// <summary>Where the speaker's name sits.</summary>
		/// FF4's: NameWindow::nwDrawMessage_ writes the name at 12, 222, in its own window from 4, 220 (MessageWindow.mwSetNameMessage).
		public static (int X, int Y) DialogueName() { Rect r = GameProfile.IsFf4 ? Frame(BattleHudLayout.FieldScreen, "dialogue/name", 12, 222, 200, 20) : Frame(BattleHudLayout.FieldScreen, "dialogue/name", 24, 139, 200, 20); return GameProfile.IsFf4 ? (r.X, r.Y) : DialogueSizing.Point(RawDialogue(), r.X, r.Y); }
		/// <summary>Where the page-turn icon sits.</summary>
		/// FF4's: the arrow sprite at 448, 304 (MessageWindow::mwInitialize).
		public static (int X, int Y) DialogueNext() { Rect w = GameProfile.IsFf4 ? Dialogue() : RawDialogue(); Rect r = GameProfile.IsFf4 ? Frame(BattleHudLayout.FieldScreen, "dialogue/next", 448, 304, 24, 24) : Frame(BattleHudLayout.FieldScreen, "dialogue/next", w.X + w.Width - 28, w.Y + w.Height - 28, 24, 24); return GameProfile.IsFf4 ? (r.X, r.Y) : DialogueSizing.Point(w, r.X, r.Y); }
		/// <summary>The map-name window's rectangle (the game centres its text in it).</summary>
		public static Rect MapName() => Frame(BattleHudLayout.FieldScreen, "map_name", 4, 4, 472, 28);
		/// <summary>The Yes / No box's rectangle, and a part of it (question, yes, no) as its offset from the box's corner.</summary>
		public static Rect Confirm() => Frame(BattleHudLayout.FieldScreen, "confirm", 86, 120, 84, 64);
		public static (int X, int Y) ConfirmPart(string part, int x, int y)
		{
			Rect box = Confirm();
			Rect r = Frame(BattleHudLayout.FieldScreen, "confirm/" + part);
			return r.Found ? (r.X - box.X, r.Y - box.Y) : (x, y);
		}
		/// <summary>A field button's place (MENU_POSITION: 0 right up - the menu's -, 2 left up - the map's -, 1 right down - talk's), or not found for the game's own.</summary>
		/// <summary>A menu's touch button (CWMenuButton: 0 A, 1 B, 2 L, 3 R) - where a press reaches -, the game's numbers unless a mod moved it.</summary>
		public static Rect MenuButton(int i, int x, int y, int w, int h)
		{
			string id = i == 0 ? "a_button" : i == 1 ? "b_button" : i == 2 ? "l_button" : "r_button";
			return FieldHud.TryRect(id, out Rect r) ? r : new Rect { X = x, Y = y, Width = w, Height = h };
		}

		public static Rect ButtonPlace(int position)
		{
			string id = position == 0 ? "menu_button" : position == 2 ? "map_button" : position == 1 ? "talk_button" : null;
			return id == null ? default : Frame(BattleHudLayout.FieldScreen, id);
		}

		/// <summary>A party line's bar: corner 0 the top-left, 1 the bottom-right.</summary>
		public static (int X, int Y) PlayerGauge(int i, int corner)
		{
			Rect r = BattleSizing.Party(Frame("player" + i + "/gauge", 282, GlobalScope.BATTLE_PLAYER_Y() - 2 + i * 16, 194, 16));
			return corner == 0 ? (r.X, r.Y) : (r.X + r.Width, r.Y + r.Height);
		}
	}
}
