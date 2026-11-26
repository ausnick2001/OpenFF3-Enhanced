// Brings a Steam game's own SAVE.BIN into this port's save format.
//
// The Steam game's save is the same data in the same order as ours, but it is a raw dump of the original game's memory: slots are
// 15168 bytes apart (ours 13856), narrow fields are stored narrow (a hero's level is one byte, ours four), and structures carry the
// compiler's padding. A player who copies the Steam SAVE.BIN over save.bin gets it converted here, once, with the original kept
// beside it as save.steam-backup.bin. Layout worked out by comparing Steam saves against this port's saves made at the same moments.

using System;
using System.IO;

namespace OpenFF.Client
{
	internal static class SteamSaveImport
	{
		private const int SteamStride = 15168, OurStride = 13856, OurData = 13842, OurQuickData = 13846, Composit = 13254;
		private const int HeroSteam = 760, HeroOurs = 1296;

		// Where our packed tail (the world data and the flag memory from byte 9807 of the composit) has its Steam padding: the offsets of
		// ours at which the Steam copy has extra bytes. After the last one the two are a fixed distance apart.
		private static readonly (int At, int Extra)[] Gaps =
		{
			(10435, 2), (10461, 2), (10463, 2), (10513, 2), (10515, 2), (10565, 2), (10591, 2), (10593, 2), (10597, 3),
			(10709, 1), (10749, 1), (10756, 13),
		};

		/// <summary>Whether a 64 KB save file is the Steam game's (a slot header with its 15160 / 15176 byte data size) and none of ours.</summary>
		public static bool IsSteam(byte[] file)
		{
			if (file == null || file.Length != 65536) return false;
			for (int k = 0; k < 4; k++)
			{
				int o = k * OurStride;
				if (HasHeader(file, o) && (U32(file, o + 32) == OurData || U32(file, o + 32) == OurQuickData)) return false;
			}
			for (int k = 0; k < 4; k++)
			{
				int o = k * SteamStride;
				if (o + 36 > file.Length || !HasHeader(file, o)) continue;
				uint size = U32(file, o + 32);
				if (size == 15160 || size == 15176) return true;
			}
			return false;
		}

		/// <summary>
		/// Looks in the saves folder for the Steam game's save under any capitalisation (the Steam game calls it SAVE.BIN; on Linux that is a
		/// different file from save.bin) and, if it is a Steam save, writes the converted one as save.bin. The original is kept as
		/// save.steam-backup.bin. When a save.bin of the player's own is already there with something in it, it is not touched: the
		/// conversion goes to save.from-steam.bin beside it, to be renamed by hand.
		/// </summary>
		public static void ConvertIfSteam(string path)
		{
			try
			{
				string dir = Path.GetDirectoryName(path) ?? ".";
				string target = Path.Combine(dir, "save.bin");
				foreach (string file in Directory.GetFiles(dir))
				{
					if (!string.Equals(Path.GetFileName(file), "save.bin", StringComparison.OrdinalIgnoreCase)) continue;
					byte[] bytes = File.ReadAllBytes(file);
					if (!IsSteam(bytes)) continue;
					byte[] converted = Convert(bytes);
					string backup = Path.Combine(dir, "save.steam-backup.bin");
					if (File.Exists(backup)) backup = Path.Combine(dir, "save.steam-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bin");
					File.WriteAllBytes(backup, bytes);
					// A save.bin that is itself a Steam save (or the very same file: Windows ignores the capitals) is replaced; one of the
					// player's own, with something in it, is not touched.
					bool ownSave = File.Exists(target) && Array.Exists(File.ReadAllBytes(target), b => b != 0) && !IsSteam(File.ReadAllBytes(target));
					string outPath = ownSave ? Path.Combine(dir, "save.from-steam.bin") : target;
					File.WriteAllBytes(outPath, converted);
					// SAVE.BIN on a case-sensitive system is a second file and goes once the conversion is written; on Windows it was the
					// same file, now converted, and stays.
					if (!ownSave && !string.Equals(file, target, StringComparison.Ordinal) && File.Exists(file) && IsSteam(File.ReadAllBytes(file))) File.Delete(file);
					Log.Write(LogChannel.General, "save: " + Path.GetFileName(file) + " was a Steam save; converted it to " + Path.GetFileName(outPath) + " (original kept as " + Path.GetFileName(backup) + ")");
				}
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "save: could not convert a Steam save: " + ex.Message);
			}
		}

		/// <summary>The same slots in this port's layout (slots 0-2 and the quicksave in the fourth place); a slot the Steam file does not have stays empty.</summary>
		public static byte[] Convert(byte[] steam)
		{
			byte[] ours = new byte[65536];
			for (int k = 0; k < 4; k++)
			{
				int so = k * SteamStride;
				if (so + SteamStride > steam.Length || !HasHeader(steam, so)) continue;
				uint size = U32(steam, so + 32);
				if (size != 15160 && size != 15176) continue;
				byte[] slot = new byte[SteamStride];
				Array.Copy(steam, so, slot, 0, SteamStride);
				byte[] comp = Slot(slot);
				int oo = k * OurStride;
				Array.Copy(slot, 0, ours, oo, 32);
				bool quick = k == 3;
				PutU32(ours, oo + 32, quick ? (uint)OurQuickData : (uint)OurData);
				int at = oo + 36;
				if (quick)
				{
					ours[at] = (byte)'V'; ours[at + 1] = (byte)'S'; ours[at + 2] = (byte)'S'; ours[at + 3] = (byte)'D';
					at += 4;
				}
				Array.Copy(comp, 0, ours, at, Composit);
			}
			return ours;
		}

		// One Steam slot (its header included) as the composit of ours.
		private static byte[] Slot(byte[] s)
		{
			byte[] o = new byte[Composit];
			Array.Copy(s, 64, o, 0, 37);   // the mail time and mail data
			for (int h = 0; h < 4; h++) Hero(s, 104 + HeroSteam * h, o, 37 + HeroOurs * h);
			o[5221] = s[3144]; o[5222] = s[3145];
			Items(s, 3152, o, 5223, 384); Items(s, 4688, o, 6375, 64); Items(s, 4944, o, 6567, 384);
			Array.Copy(s, 6480, o, 7719, 4 + 4 + 32);   // gold, play time and the mania figures
			Array.Copy(s, 6520, o, 7759, 2048);         // the monster table
			for (int i = 9807; i < Composit; i++)
			{
				if (i >= 10760 && i < 10772) continue;
				int j = TailSource(i) + 64;
				if (j >= 0 && j < s.Length) o[i] = s[j];
			}
			return o;
		}

		private static int TailSource(int i)
		{
			int shift = 1297;
			foreach (var g in Gaps) if (i >= g.At) shift -= g.Extra;
			if (i >= 10760) shift += 11;
			return i - shift;
		}

		private static void Items(byte[] s, int from, byte[] o, int to, int n)
		{
			for (int i = 0; i < n; i++)
			{
				o[to + 3 * i] = s[from + 4 * i]; o[to + 3 * i + 1] = s[from + 4 * i + 1]; o[to + 3 * i + 2] = s[from + 4 * i + 2];
			}
		}

		// A hero: 760 bytes of the Steam game's memory to the 1296 of ours.
		private static void Hero(byte[] s, int a, byte[] o, int b)
		{
			Array.Copy(s, a, o, b, 27);
			PutU32(o, b + 27, s[a + 27]);                       // level: a byte there, four here
			Array.Copy(s, a + 28, o, b + 31, 4);                // experience
			Array.Copy(s, a + 32, o, b + 35, 4);                // capacity
			Array.Copy(s, a + 40, o, b + 39, 4);                // HP limit then now (the Steam game keeps now first)
			Array.Copy(s, a + 36, o, b + 43, 4);
			for (int i = 0; i < 16; i++) PutU32(o, b + 47 + 4 * i, s[a + 44 + i]);   // MP, a byte each
			Array.Copy(s, a + 60, o, b + 111, 8);
			Array.Copy(s, a + 68, o, b + 119, 10);
			Array.Copy(s, a + 78, o, b + 129, 7);
			Array.Copy(s, a + 88, o, b + 136, 32);              // both hands' attack
			Array.Copy(s, a + 120, o, b + 168, 20);
			Array.Copy(s, a + 140, o, b + 188, 4);
			o[b + 192] = s[a + 144];
			for (int i = 0; i < 5; i++)                         // the equipment
			{
				Array.Copy(s, a + 146 + 4 * i, o, b + 193 + 6 * i, 2);
				PutU32(o, b + 195 + 6 * i, (uint)(int)(short)(s[a + 148 + 4 * i] | (s[a + 149 + 4 * i] << 8)));
			}
			Array.Copy(s, a + 168, o, b + 223, 96);             // the equipment's magic
			Array.Copy(s, a + 264, o, b + 319, 8);              // current and previous job
			for (int k = 0; k < 23; k++)
			{
				int j = a + 272 + 20 * k, d = b + 327 + 40 * k;
				for (int i = 0; i < 7; i++) PutU32(o, d + 4 * i, s[j + i]);
				Array.Copy(s, j + 8, o, d + 28, 12);
			}
			Array.Copy(s, a + 732, o, b + 1247, 9);
			for (int i = 0; i < 3; i++)
				for (int t = 0; t < 3; t++) PutU32(o, b + 1256 + 12 * i + 4 * t, s[a + 744 + 3 * i + t]);
			Array.Copy(s, a + 753, o, b + 1292, 4);
		}

		private static bool HasHeader(byte[] f, int o) =>
			o + 7 <= f.Length && f[o] == (byte)'c' && f[o + 1] == (byte)'d' && f[o + 2] == (byte)'1' && f[o + 3] == (byte)'0' && f[o + 4] == (byte)'0' && f[o + 5] == (byte)'0' && f[o + 6] == 0;

		private static uint U32(byte[] f, int o) => (uint)(f[o] | (f[o + 1] << 8) | (f[o + 2] << 16) | (f[o + 3] << 24));

		private static void PutU32(byte[] f, int o, uint v)
		{
			f[o] = (byte)v; f[o + 1] = (byte)(v >> 8); f[o + 2] = (byte)(v >> 16); f[o + 3] = (byte)(v >> 24);
		}
	}
}
