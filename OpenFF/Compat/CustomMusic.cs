// Music the player drops in: a "custom music" folder beside OpenFF3 Enhanced.exe, made at the first start, with a folder for
// each of the game's 59 tunes (BGM00 to BGM58). Put one .ogg or .wav file in a tune's folder and it plays in place of the
// game's, from the next start; a folder left empty keeps the game's own. Nothing to configure, no command line.
//
// The number at the start of the folder's name says which tune it is ("BGM22 - Ur (town)", "22" and "bgm22" all do), so a folder
// may be renamed freely so long as that number stays. The words after it are only a hint, taken from where the game's own data
// plays the tune; a bare "BGMnn" is a tune nobody has worked out the place of.
//
// A tune the game loops (most of them) has the file looped whole; one the game plays once (a jingle, a fanfare) has it played
// once. The game's own intro, where it has one, is dropped: the file takes its place.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenFF.Client
{
	internal static class CustomMusic
	{
		/// <summary>The tune numbers the game has (BGM00..BGM58) and a hint of where each plays; "" where nothing is known.</summary>
		private static readonly Dictionary<int, string> Tunes = new Dictionary<int, string>
		{
			{ 0, "Title screen" }, { 1, "" }, { 2, "" }, { 3, "Early caves" }, { 4, "" }, { 5, "" }, { 6, "" }, { 7, "" }, { 8, "" }, { 9, "" },
			{ 10, "" }, { 11, "Temple of Nepto" }, { 12, "Tower of Owen" }, { 13, "Dungeons" }, { 14, "Dungeons" }, { 15, "Castle of Hein and Cave of Shadows" },
			{ 16, "Eureka" }, { 17, "Forbidden Land" }, { 18, "Crystal Tower" }, { 19, "" }, { 20, "" }, { 21, "" }, { 22, "Ur (town)" },
			{ 23, "Town" }, { 24, "Town" }, { 25, "Main towns" }, { 26, "" }, { 27, "Town" }, { 28, "Town" }, { 29, "Town" }, { 30, "Town" },
			{ 31, "Late towns" }, { 32, "Main towns" }, { 33, "Town" }, { 34, "" }, { 35, "" }, { 36, "" }, { 37, "" }, { 38, "" }, { 39, "" },
			{ 40, "" }, { 41, "" }, { 42, "" }, { 43, "" }, { 44, "" }, { 45, "" }, { 46, "" }, { 47, "" }, { 48, "" }, { 49, "" },
			{ 50, "" }, { 51, "" }, { 52, "" }, { 53, "" }, { 54, "" }, { 55, "Battle" }, { 56, "Boss battle" }, { 57, "Final boss battle" }, { 58, "Victory fanfare" }
		};

		private const string ReadMe =
			"Drop your own music in here.\r\n" +
			"\r\n" +
			"There is a folder for each of the game's 59 tunes, BGM00 to BGM58. Put ONE .ogg or .wav file in the folder of the tune\r\n" +
			"you want to replace, then start the game again. A folder left empty keeps the game's own music.\r\n" +
			"\r\n" +
			"The number at the start of a folder's name is what counts - you may rename a folder as long as it still starts with\r\n" +
			"its number (22, BGM22 or bgm22). The words after it are only a hint of where the game plays that tune, worked out from\r\n" +
			"the game's data, and may be wrong; a folder with no words is a tune nobody has placed yet. Listen to find out which is which.\r\n" +
			"\r\n" +
			"A tune the game loops has your file looped from the start to the end and back again, so trim it to loop cleanly.\r\n" +
			"A jingle or fanfare the game plays once has your file played once. (.ogg is Ogg Vorbis; .wav must be plain 16-bit PCM.\r\n" +
			".mp3 and .flac are not read: convert them first.) If a folder holds several files, the first by name is used.\r\n" +
			"\r\n" +
			"Only use music you have the right to use.\r\n";

		public static string Root => Path.Combine(AppContext.BaseDirectory, "custom music");

		private static Dictionary<int, string> _files;
		private static readonly object _lock = new object();

		private static string FolderName(int tune)
		{
			string hint = Tunes.TryGetValue(tune, out string h) ? h : "";
			return "BGM" + tune.ToString("00") + (hint.Length == 0 ? "" : " - " + hint);
		}

		/// <summary>Makes the folder with its 59 tune folders and a note, once, and reads which tunes have a file; harmless if it cannot (a read-only install).</summary>
		private static Dictionary<int, string> Files()
		{
			lock (_lock) return _files ?? (_files = Scan());
		}

		private static Dictionary<int, string> Scan()
		{
			Dictionary<int, string> found = new Dictionary<int, string>();
			try
			{
				if (!Directory.Exists(Root))
				{
					foreach (int tune in Tunes.Keys) Directory.CreateDirectory(Path.Combine(Root, FolderName(tune)));
				}
				string note = Path.Combine(Root, "README.txt");
				if (!File.Exists(note)) File.WriteAllText(note, ReadMe);
				foreach (string folder in Directory.EnumerateDirectories(Root))
				{
					int tune = Number(Path.GetFileName(folder));
					if (tune < 0 || !Tunes.ContainsKey(tune)) continue;
					string file = Directory.EnumerateFiles(folder).Where(IsSound).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
					if (file != null && !found.ContainsKey(tune))
					{
						found[tune] = file;
						Log.Write(LogChannel.File, "sound: custom music for tune " + tune + ": " + file);
					}
				}
			}
			catch (Exception ex) { Log.Write(LogChannel.File, "sound: custom music folder: " + ex.Message); }
			return found;
		}

		/// <summary>The tune number a folder's name starts with ("BGM22 - Ur", "22", "bgm22"), or -1.</summary>
		private static int Number(string folder)
		{
			string s = folder.TrimStart();
			if (s.StartsWith("bgm", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3);
			int n = 0, digits = 0;
			while (digits < s.Length && char.IsDigit(s[digits])) n = n * 10 + (s[digits++] - '0');
			return digits == 0 || digits > 3 ? -1 : n;
		}

		private static bool IsSound(string file) =>
			file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

		/// <summary>The marker an OggSound entry starts with when it is a player's file, not an entry of the content chain.</summary>
		public const string Prefix = "custom-music:";

		/// <summary>
		/// The player's file for a sound named like BGM22_0 / BGM22_1, as an entry, or null. A looping tune has the file as its
		/// loop half (_1) and no intro; a tune the game plays once (only an intro) has it as the _0.
		/// </summary>
		public static string Find(string name)
		{
			int tune = TuneOf(name, out int part);
			if (part < 0 || !Files().TryGetValue(tune, out string file)) return null;
			return part == WantedPart(tune) ? Prefix + file : null;
		}

		/// <summary>Which parts of this tune play: the player's file only, as the loop half or - for a tune the game plays once - the intro half. -1 when there is no custom file.</summary>
		public static int Parts(string tuneName)
		{
			int tune = TuneOf(tuneName, out _);
			if (tune < 0 || !Files().ContainsKey(tune)) return -1;
			return 1 << WantedPart(tune);
		}

		private static int WantedPart(int tune) => (SoundManager.VanillaParts("BGM" + tune.ToString("00")) & 2) != 0 ? 1 : 0;

		private static int TuneOf(string name, out int part)
		{
			part = -1;
			if (name == null || name.Length < 5 || !name.StartsWith("BGM", StringComparison.OrdinalIgnoreCase)) return -1;
			int i = 3, n = 0;
			while (i < name.Length && char.IsDigit(name[i])) n = n * 10 + (name[i++] - '0');
			if (i == 3) return -1;
			if (i == name.Length) return n;
			if (name[i] == '_' && i + 1 < name.Length && (name[i + 1] == '0' || name[i + 1] == '1') && i + 2 == name.Length) { part = name[i + 1] - '0'; return n; }
			return -1;
		}

		/// <summary>Reads a Prefix entry's file, or null.</summary>
		public static byte[] Read(string entry)
		{
			try { return File.ReadAllBytes(entry.Substring(Prefix.Length)); }
			catch (Exception ex) { Log.Write(LogChannel.File, "sound: " + entry + ": " + ex.Message); return null; }
		}
	}
}
