// Fonts the player drops in: a "fonts" folder beside OpenFF.exe, made at the first start, with a folder for each
// place text is drawn. Put any .ttf, .otf or .ttc in one and it is used from the next start; empty, the game's own
// is drawn as before. Nothing to configure, no command line.
//
//   fonts\menus      menus, battle windows, everything not named below
//   fonts\dialogue   what a speaker says (fonts\dialogue\scale.txt: a number, 1 = the menus' size; 0.88 without one)
//   fonts\names      the speaker's name and the cutscenes' story lines
//   fonts\title      the title screen's "Press ANY BUTTON to START" and commands
//
// The first font in a folder (by file name) is the one drawn; the game's own stand behind it for any glyph it lacks.
// The older body-font and name-font folders beside the game still work when the matching folder here is empty.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenFF.Client
{
	internal static class FontFolders
	{
		public const string Menus = "menus", Dialogue = "dialogue", Names = "names", Title = "title";

		private static readonly string[] Slots = { Menus, Dialogue, Names, Title };

		private const string ReadMe =
			"Drop your own fonts in here.\r\n" +
			"\r\n" +
			"  menus      the menus, battle windows and everything else\r\n" +
			"  dialogue   what characters say\r\n" +
			"  names      the speaker's name, and the story text in cutscenes\r\n" +
			"  title      the title screen\r\n" +
			"\r\n" +
			"Put a .ttf, .otf or .ttc file in the folder you want, then start the game again.\r\n" +
			"If a folder holds several fonts, the first by file name is used.\r\n" +
			"A folder left empty keeps the game's own font.\r\n" +
			"\r\n" +
			"Dialogue too big or too small? Put a text file called scale.txt in the dialogue folder\r\n" +
			"holding one number: 1 is the size of the menus, 0.88 is the default, allowed 0.5 to 1.5.\r\n" +
			"\r\n" +
			"Only use fonts you have the right to use.\r\n";

		public static string Root => Path.Combine(AppContext.BaseDirectory, "fonts");

		private static bool _made;

		/// <summary>Makes the fonts folder with its four folders and a note, once; harmless if it cannot (a read-only install).</summary>
		public static void Ensure()
		{
			if (_made) return;
			_made = true;
			try
			{
				foreach (string slot in Slots) Directory.CreateDirectory(Path.Combine(Root, slot));
				string note = Path.Combine(Root, "README.txt");
				if (!File.Exists(note)) File.WriteAllText(note, ReadMe);
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "text: fonts folder: " + ex.Message); }
		}

		public static bool IsFont(string file) =>
			file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);

		/// <summary>The fonts in a slot's folder (then in the older folder beside the game, if given), first by name first.</summary>
		public static List<string> Faces(string slot, string legacyFolder = null)
		{
			Ensure();
			List<string> found = new List<string>();
			foreach (string folder in new[] { Path.Combine(Root, slot), legacyFolder == null ? null : Path.Combine(AppContext.BaseDirectory, legacyFolder) })
			{
				try
				{
					if (folder == null || !Directory.Exists(folder)) continue;
					found.AddRange(Directory.EnumerateFiles(folder).Where(IsFont).OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
				}
				catch (Exception ex) { Log.Write(LogChannel.General, "text: " + folder + ": " + ex.Message); }
			}
			return found;
		}

		/// <summary>The font a slot draws from, or null when its folder is empty.</summary>
		public static string First(string slot, string legacyFolder = null) => Faces(slot, legacyFolder).FirstOrDefault();
	}
}
