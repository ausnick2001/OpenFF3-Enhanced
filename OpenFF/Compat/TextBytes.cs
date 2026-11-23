// Reading a dialogue string whose bytes are not in the encoding the language setting says.
//
// The game's .msd text files are decoded at load (AppShell.decodeString): Shift-JIS when the language is Japanese,
// Windows-1252 otherwise. This client's language is the English one, but a Steam copy installed with Steam's own
// language set to Japanese has its dialogue files in Shift-JIS, and reading those as Windows-1252 turns every
// character line to nonsense ("@Luneth  u, ¨ ,â H") while the treasure-chest lines - already UTF-8 - read fine.
// So each string is looked at first: UTF-8 as it is, Shift-JIS when it is valid Shift-JIS with kana or kanji in it,
// else the encoding asked for. Plain ASCII - nearly all of an English copy - is the same in all of them.

using System;

namespace OpenFF.Client
{
	internal static class TextBytes
	{
		/// <summary>One string of an .msd file as UTF-8 bytes (no terminator).</summary>
		public static byte[] ToUtf8(byte[] file, int start, int length, string charsetName)
		{
			if (HasHighByte(file, start, length))
			{
				if (IsUtf8(file, start, length))
				{
					byte[] copy = new byte[length];
					Buffer.BlockCopy(file, start, copy, 0, length);
					return copy;
				}
				if (charsetName != "SJIS" && LooksShiftJis(file, start, length))
				{
					charsetName = "SJIS";
				}
			}
			return StringUtil.getBytes(StringUtil.createString(file, start, length, charsetName), "UTF-8");
		}

		private static bool HasHighByte(byte[] d, int at, int n)
		{
			for (int i = 0; i < n; i++)
			{
				if (d[at + i] >= 0x80) return true;
			}
			return false;
		}

		/// <summary>Whether the bytes are well-formed UTF-8 with at least one multi-byte character.</summary>
		private static bool IsUtf8(byte[] d, int at, int n)
		{
			bool multi = false;
			for (int i = 0; i < n; i++)
			{
				byte b = d[at + i];
				if (b < 0x80) continue;
				int extra = b >= 0xC2 && b <= 0xDF ? 1 : b >= 0xE0 && b <= 0xEF ? 2 : b >= 0xF0 && b <= 0xF4 ? 3 : -1;
				if (extra < 0 || i + extra >= n) return false;
				for (int j = 1; j <= extra; j++)
				{
					if ((d[at + i + j] & 0xC0) != 0x80) return false;
				}
				i += extra;
				multi = true;
			}
			return multi;
		}

		/// <summary>
		/// Whether every high byte is the lead of a pair the Shift-JIS table has, and at least one pair is kana or kanji
		/// (so Western text with an accent or two is not taken for Japanese).
		/// </summary>
		private static bool LooksShiftJis(byte[] d, int at, int n)
		{
			bool japanese = false;
			for (int i = 0; i < n; i++)
			{
				byte b = d[at + i];
				if (b < 0x80) continue;
				bool lead = (b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xFC);
				if (!lead || i + 1 >= n) return false;
				ushort key = (ushort)((b << 8) | d[at + i + 1]);
				if (!Sjis.Jis0208Table.TryGetValue(key, out char c)) return false;
				if ((c >= 'ぁ' && c <= 'ヿ') || (c >= '一' && c <= '鿿')) japanese = true;
				i++;
			}
			return japanese;
		}
	}
}
