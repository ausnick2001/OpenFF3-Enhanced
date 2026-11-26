// The battle magic list's fourth row. Steam's BattleDefine holds the list as three rows of three cells in a 96-unit panel (32 units
// a row), but the game's own code is written for four (the phone's): it scrolls the last page to start at level 5, and wraps the
// cursor by nine cells - so level 8 was never on show and the wrap landed on the wrong row. This gives the panel its four rows, the
// same height as before (24 units a row), so the list scrolls as the game's code expects. A layout of a mod's own replaces this
// one afterwards, as it replaces any of the game's.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenFF.Client
{
	internal static class BattleMagicRows
	{
		/// <summary>The magic list has its four rows (set when the layout is loaded): the lines behind it, its letters and its icons follow.</summary>
		public static bool Active;

		/// <summary>How much smaller the letters and icons are for four rows in the room of three (at the default size: the options' smaller sizes come on top).</summary>
		public const float Scale = 0.75f;

		/// <summary>The panel's size and the letters' size as the options set them when the layout was loaded (the lists are built from that layout, so they stay as it is until the next battle).</summary>
		public static float ListBox = 1f;
		public static float ListLetters = 1f;
		/// <summary>The lists' width, side to side: the panel's size and the menu spacing together (the layout's x and width follow it, the letters do not).</summary>
		public static float ListWide = 1f;

		/// <summary>The letters and icons of a four-row list at the size in force.</summary>
		public static float K => Scale * ListLetters;

		/// <summary>A distance inside a list (a hand's shift, a gap) at the size in force.</summary>
		public static int Sized(float units) => (int)Math.Round(units * ListBox);

		/// <summary>A screen x (in the layout's 480) for a distance from the list panel's left edge, at the size in force: the panel shrinks about the screen's middle.</summary>
		public static int PanelX(float units) => (int)Math.Round(240f * (1f - ListWide) + units * ListWide);

		/// <summary>A distance along a row inside a list (where a number ends in its cell) at the size and spacing in force.</summary>
		public static int SizedX(float units) => (int)Math.Round(units * ListWide);

		/// <summary>The arrows' scale at the size in force.</summary>
		public static float ArrowK => ArrowScale * ListBox;

		/// <summary>The battle item list has its four rows too (set when the layout is loaded).</summary>
		public static bool ItemActive;

		/// <summary>The up / down arrows of a four-row list: how much of their 24 units they keep, and how far in from the box's edge.</summary>
		public const float ArrowScale = 0.6f;
		public const int ArrowInset = 2;

		/// <summary>Four rows in the magic list of BattleDefine.xbn when it has three. True when the document was changed.</summary>
		public static bool Apply(string file, XDocument doc)
		{
			if (!string.Equals(file, "BattleDefine.xbn", StringComparison.OrdinalIgnoreCase)) return false;
			ListBox = BattleSizing.ListBox;
			ListLetters = BattleSizing.ListLetters;
			ListWide = BattleSizing.ListBox * BattleSizing.Wide;
			bool scaled = Math.Abs(ListBox - 1f) > 0.001f || Math.Abs(ListWide - 1f) > 0.001f;
			bool items = ItemRows(doc);
			bool magic = MagicRows(doc);
			if (scaled)
			{
				if (items) ScaleMenu(doc, "battle_item");
				if (magic) ScaleMenu(doc, "battle_magic");
			}
			bool weapons = WeaponScreen(doc);
			bool equip = EquipScreen(doc);
			if (scaled)
			{
				if (equip) { ScaleMenu(doc, "battle_equip"); ScaleMenu(doc, "battle_item_e"); }
				if (weapons) ScaleMenu(doc, "battle_use_weapon");
			}
			return items || magic || weapons || equip;
		}

		/// <summary>A list's panel at the options' size: its window keeps the screen's middle (side to side) and its bottom edge, and every cell shrinks with it.</summary>
		private static void ScaleMenu(XDocument doc, string name)
		{
			XElement menu = doc?.Root?.Elements("menu").FirstOrDefault(m => (string)m.Element("name") == name);
			if (menu == null) return;
			foreach (XElement top in menu.Elements("frame"))
			{
				// the screen's frames are placed on the screen; those inside them from their parent's corner
				ScaleFrame(top, absolute: true);
			}
		}

		private static void ScaleFrame(XElement frame, bool absolute)
		{
			float s = ListBox;
			float sw = ListWide;   // side to side: the panel's size and the menu spacing
			if (int.TryParse((string)frame.Element("x"), out int x)) Set(frame, "x", (absolute ? (int)Math.Round(240f - (240f - x) * sw) : (int)Math.Round(x * sw)).ToString());
			if (int.TryParse((string)frame.Element("y"), out int y)) Set(frame, "y", (absolute ? (int)Math.Round(320f - (320f - y) * s) : (int)Math.Round(y * s)).ToString());
			if (int.TryParse((string)frame.Element("width"), out int w)) Set(frame, "width", ((int)Math.Round(w * sw)).ToString());
			if (int.TryParse((string)frame.Element("height"), out int h)) Set(frame, "height", ((int)Math.Round(h * s)).ToString());
			foreach (XElement child in frame.Elements("frame")) ScaleFrame(child, absolute: false);
		}

		private static bool MagicRows(XDocument doc)
		{
			Active = false;
			XElement menu = doc?.Root?.Elements("menu").FirstOrDefault(m => (string)m.Element("name") == "battle_magic");
			XElement list = menu?.Descendants("frame").FirstOrDefault(f => (string)f.Element("id") == "magic_list");
			if (list == null) return false;
			List<XElement> cells = list.Elements("frame").ToList();
			if (cells.Count != 9) return false;   // not three rows of three: as it is (the phone's has its four)
			if (!int.TryParse((string)list.Element("height"), out int panel) || panel < 40) return false;
			int row = panel / 4;
			for (int i = 0; i < 9; i++)
			{
				Set(cells[i], "y", (i / 3 * row).ToString());
				Set(cells[i], "height", row.ToString());
			}
			string[] names = { "one", "two", "thr" };
			for (int c = 0; c < 3; c++)
			{
				XElement last = cells[6 + c];
				XElement fourth = new XElement(last);
				Set(fourth, "id", "for_" + names[c]);
				Set(fourth, "y", (3 * row).ToString());
				Set(fourth, "work", "3");
				Set(fourth, "myTag", (9 + c).ToString());
				Set(fourth, "up", (string)last.Element("id"));
				Set(fourth, "down", "dummy");
				Set(fourth, "left", "for_" + names[(c + 2) % 3]);
				Set(fourth, "right", "for_" + names[(c + 1) % 3]);
				Set(last, "down", "for_" + names[c]);
				list.Add(fourth);
			}
			Active = true;
			return true;
		}

		/// <summary>True for a cell of the four-row magic list.</summary>
		private static bool InList(GlobalScope.menu.Medget m) => Active && m?.parentNode()?._id() == "magic_list";

		/// <summary>The hand on a four-row magic list's cell is moved to the middle of the (smaller) orb: left and up.</summary>
		/// <summary>The weapons-in-hand screen (R. Hand / L. Hand, reached from the item list) is laid out as it is (set when the layout is loaded).</summary>
		public static bool WeaponActive;

		private const int WeaponShift = 24;

		/// <summary>The weapons-in-hand screen with everything a little right, so the hand stands left of the weapon's name and not on it.</summary>
		private static bool WeaponScreen(XDocument doc)
		{
			WeaponActive = false;
			XElement menu = doc?.Root?.Elements("menu").FirstOrDefault(m => (string)m.Element("name") == "battle_use_weapon");
			if (menu == null) return false;
			bool any = false;
			foreach (XElement frame in menu.Descendants("frame"))
			{
				string id = (string)frame.Element("id");
				string behavior = (string)frame.Element("behavior")?.Attribute("value");
				bool label = behavior == "Text";
				if (!label && id != "one" && id != "two") continue;
				if (!int.TryParse((string)frame.Element("x"), out int x)) continue;
				// the labels (x 8 and 240) line up with the names (cells at 24 and 256) after the move
				int to = label ? x + 40 : x + WeaponShift;
				Set(frame, "x", to.ToString());
				any = true;
			}
			WeaponActive = any;
			return any;
		}

		/// <summary>The battle equipment screen is laid out as Steam has it, bar the move below (set when the layout is loaded).</summary>
		public static bool EquipActive;

		/// <summary>The equipment screen's upper part (the hands, their numbers) a little right, so the hand stands left of a weapon's name and not on it.</summary>
		private static bool EquipScreen(XDocument doc)
		{
			EquipActive = false;
			XElement menu = doc?.Root?.Elements("menu").FirstOrDefault(m => (string)m.Element("name") == "battle_equip");
			XElement hands = menu?.Descendants("frame").FirstOrDefault(f => (string)f.Element("id") == "equip_list");
			if (hands == null) return false;
			foreach (XElement frame in menu.Descendants("frame"))
			{
				bool label = (string)frame.Element("behavior")?.Attribute("value") == "Text";
				if (!label && frame.Parent != hands) continue;
				if (!int.TryParse((string)frame.Element("x"), out int x)) continue;
				Set(frame, "x", (x + WeaponShift).ToString());
			}
			EquipActive = true;
			return true;
		}

		/// <summary>True for a hand cell of the battle equipment screen (the right / left hand rows).</summary>
		private static bool InEquipHand(GlobalScope.menu.Medget m) => EquipActive && m != null && (m._id() == "right_hand" || m._id() == "left_hand");

		/// <summary>True for a cell of the equipment screen's weapon list (two rows of two).</summary>
		private static bool InEquipList(GlobalScope.menu.Medget m)
		{
			if (!EquipActive) return false;
			GlobalScope.menu.Medget list = m?.parentNode();
			if (list == null || list._id() != "item_list") return false;
			int cells = 0;
			for (GlobalScope.menu.Medget c = list.childNode(); c != null; c = c.nextSibling()) cells++;
			return cells == 4;
		}

		/// <summary>True for one of the two hand cells of the weapons-in-hand screen.</summary>
		private static bool InWeaponCell(GlobalScope.menu.Medget m) => WeaponActive && m != null && (m._id() == "one" || m._id() == "two") && m.height() == 80;

		/// <summary>True for a cell of the four-row battle item list.</summary>
		private static bool InItemList(GlobalScope.menu.Medget m)
		{
			if (!ItemActive) return false;
			GlobalScope.menu.Medget list = m?.parentNode();
			if (list == null || list._id() != "item_list") return false;
			int cells = 0;
			for (GlobalScope.menu.Medget c = list.childNode(); c != null; c = c.nextSibling()) cells++;
			return cells == 8;
		}

		/// <summary>The equipment and weapons-in-hand screens' letters and distances at the size in force (1 when neither is in use).</summary>
		public static float ScreenK => (EquipActive || WeaponActive) ? ListLetters : 1f;

		public static int ScreenSized(float units) => (EquipActive || WeaponActive) ? Sized(units) : (int)units;

		/// <summary>The same along a row (where a number ends in its cell), at the spacing in force too.</summary>
		public static int ScreenSizedX(float units) => (EquipActive || WeaponActive) ? SizedX(units) : (int)units;

		/// <summary>How far down a row a line of letters at the screens' size starts, so it sits in the row's middle.</summary>
		public static int ScreenLift(int height) => (int)((height - 12f * ScreenK) / 2f);

		/// <summary>True while one of those screens is being built, so its labels take the size too.</summary>
		public static bool LabelScreen;

		public static void Letter(GlobalScope.dgs.DGSMessage m)
		{
			if (m != null && ScreenK != 1f) m.Lettering = BattleSizing.Scaled(ScreenK);
		}

		public static int HandShiftX(GlobalScope.menu.Medget m) => InList(m) ? Sized(-8) : InItemList(m) ? Sized(-9) : InWeaponCell(m) ? Sized(-22) : InEquipHand(m) ? Sized(-22) : InEquipList(m) ? Sized(-9) : 0;

		public static int HandShiftY(GlobalScope.menu.Medget m) => InList(m) ? Sized(-2) : 0;

		/// <summary>The item list: two columns of three rows in the same 96 units, made four rows of 24 (the game's code scrolls it as four).</summary>
		private static bool ItemRows(XDocument doc)
		{
			ItemActive = false;
			XElement menu = doc?.Root?.Elements("menu").FirstOrDefault(m => (string)m.Element("name") == "battle_item");
			XElement list = menu?.Descendants("frame").FirstOrDefault(f => (string)f.Element("id") == "item_list");
			if (list == null) return false;
			List<XElement> cells = list.Elements("frame").ToList();
			if (cells.Count != 6) return false;
			if (!int.TryParse((string)list.Element("height"), out int panel) || panel < 40) return false;
			int row = panel / 4;
			for (int i = 0; i < 6; i++)
			{
				Set(cells[i], "y", (i / 2 * row).ToString());
				Set(cells[i], "height", row.ToString());
			}
			string[] names = { "one", "two" };
			for (int c = 0; c < 2; c++)
			{
				XElement last = cells[4 + c];
				XElement fourth = new XElement(last);
				Set(fourth, "id", "for_" + names[c]);
				Set(fourth, "y", (3 * row).ToString());
				Set(fourth, "myTag", (6 + c).ToString());
				Set(fourth, "up", (string)last.Element("id"));
				Set(fourth, "down", "dummy");
				Set(fourth, "left", "for_" + names[1 - c]);
				Set(fourth, "right", "for_" + names[1 - c]);
				Set(last, "down", "for_" + names[c]);
				list.Add(fourth);
			}
			ItemActive = true;
			return true;
		}

		private static void Set(XElement frame, string name, string value)
		{
			XElement e = frame.Element(name);
			if (e == null) frame.Add(new XElement(name, value)); else e.Value = value;
		}
	}
}
