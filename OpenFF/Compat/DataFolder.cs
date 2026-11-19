// Where the game keeps what it writes: portable by default.
//
// Everything the player's game makes - saves, settings, the achievements, the game-file check's cache, the record of
// the game last played - goes in one folder, "userdata", beside OpenFF3 Enhanced.exe, so deleting the game's folder deletes the
// game and moving the folder moves it. --data-dir=<dir> (FF3_DATA_DIR) puts it elsewhere. (Not "Data": that is the
// client's own files, which an update replaces.)
//
// The first run beside an install that has played before copies what was in the old places (%APPDATA%\FF3 for the
// saves, %LOCALAPPDATA%\OpenFF for the settings, %LOCALAPPDATA%\FF3 for the achievements) into it - copied, never
// moved or overwritten, so the old copies stay as a backup - and from then on the folder beside the game is the one
// used. When the game's folder cannot be written to (Program Files), the old places are used as they were.

using System;
using System.Collections.Generic;
using System.IO;

namespace OpenFF.Client
{
	internal static class DataFolder
	{
		private static string _root;
		private static bool _resolved;
		private static readonly Dictionary<string, string> _places = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>--data-dir was given: that folder is used as it is, with nothing brought over from the old places.</summary>
		public static bool Explicit => !string.IsNullOrWhiteSpace(Options.Get("data-dir"));

		/// <summary>The data folder, or null when it cannot be made or written to (the old places are used then).</summary>
		public static string Root
		{
			get
			{
				if (_resolved) return _root;
				_resolved = true;
				string dir = Explicit ? Options.Get("data-dir") : Path.Combine(AppContext.BaseDirectory, "userdata");
				try
				{
					Directory.CreateDirectory(dir);
					string probe = Path.Combine(dir, ".write-test");
					File.WriteAllText(probe, "x");
					File.Delete(probe);
					_root = Path.GetFullPath(dir);
					Log.Write(LogChannel.General, "data folder: " + _root + (Explicit ? " (--data-dir)" : ""));
				}
				catch (Exception ex)
				{
					_root = null;
					Log.Write(LogChannel.General, "data folder: " + dir + " cannot be written to (" + ex.Message + "); the system's places are used");
				}
				return _root;
			}
		}

		private static string Local(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
		private static string Roaming(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name);

		/// <summary>settings.json, launch.json, ff4-settings.json and the game-file check's cache (was %LOCALAPPDATA%\OpenFF).</summary>
		public static string Settings => Place("settings", Local("OpenFF"), recursive: false);

		/// <summary>The game's saves and the mods' save chunks (was %APPDATA%\FF3).</summary>
		public static string Saves => Place("saves", Roaming("FF3"), recursive: true);

		/// <summary>The achievements (was %LOCALAPPDATA%\FF3).</summary>
		public static string Achievements => Place("achievements", Local("FF3"), recursive: false);

		private static string Place(string name, string legacy, bool recursive)
		{
			lock (_places)
			{
				if (_places.TryGetValue(name, out string known)) return known;
				string root = Root, folder = legacy;
				if (root != null)
				{
					folder = Path.Combine(root, name);
					bool fresh = !Directory.Exists(folder);
					try { Directory.CreateDirectory(folder); } catch (Exception) { folder = legacy; fresh = false; }
					if (fresh && !Explicit) BringOver(legacy, folder, recursive);
				}
				_places[name] = folder;
				return folder;
			}
		}

		/// <summary>The old place's files copied in, where the new place has none by those names.</summary>
		private static void BringOver(string from, string to, bool recursive)
		{
			try
			{
				if (!Directory.Exists(from)) return;
				int copied = Copy(from, to, recursive);
				if (copied > 0) Log.Write(LogChannel.General, "data folder: " + copied + " file(s) copied from " + from + " (the old copies stay where they were)");
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "data folder: nothing brought over from " + from + ": " + ex.Message); }
		}

		private static int Copy(string from, string to, bool recursive)
		{
			int copied = 0;
			Directory.CreateDirectory(to);
			foreach (string file in Directory.GetFiles(from))
			{
				string dest = Path.Combine(to, Path.GetFileName(file));
				if (File.Exists(dest)) continue;
				try { File.Copy(file, dest); copied++; } catch (Exception) { }
			}
			if (recursive)
				foreach (string dir in Directory.GetDirectories(from))
					copied += Copy(dir, Path.Combine(to, Path.GetFileName(dir)), true);
			return copied;
		}
	}
}
