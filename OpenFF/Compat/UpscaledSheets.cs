// A mod's larger picture for one of the game's 2D sheets, drawn over the same cells.
//
// The game's sprites cut their sheet by pixel positions in the sheet's own size, and the engine turns those into
// texture coordinates with 1 / (the picture's size). A picture upscaled by a mod is bigger, so the same cells would
// cut only its top-left corner. For the sheets listed here the size the game's own file has is read from the shipped
// content, and the coordinates are scaled by how much larger the mod's picture is, so an upscale of any factor draws
// the whole of what the original did. (A sheet whose code says its size itself - Cell.SheetBase - is not listed.)

using System;
using System.Collections.Generic;
using System.IO;

namespace OpenFF.Client
{
	internal static class UpscaledSheets
	{
		// The hand cursor, icon_yubi: two 48 x 48 frames in a 96 x 48 PNG. The speech balloons (fukidashi), and the world map's markers,
		// ship and cursor sheets, which the world map draws as cells over their sheets.
		private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"icon_yubi", "fukidashi", "fukidashi_2",
			"map_marker_02", "map_marker_03", "map_marker_04", "map_marker_05", "map_marker_06", "map_marker_07", "map_marker_08", "map_marker_09", "map_marker_10",
			"w_map_mark", "w_map_ship01", "w_map_ship02", "w_map_ship03"
		};
		private static readonly Dictionary<string, (int W, int H)> _original = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

		/// <summary>How much larger (across, down) a sheet's picture is than the game's own, 1 when it is not an upscaled listed sheet. The engine divides this by the padded texture's size.</summary>
		public static void Scale(byte[] png, out float scaleU, out float scaleV)
		{
			scaleU = scaleV = 1f;
			try
			{
				string file = png == null ? null : TextureOverrides.SourceOf(png);
				if (file == null || png.Length < 24) return;
				// The picture's own size, from its header: the texture the engine makes is padded up to powers of two (192 x 96 -> 256 x 128),
				// and the scale is of the picture, not of the padding.
				int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
				int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
				string stem = Path.GetFileNameWithoutExtension(file.Replace('\\', '/'));
				if (!Names.Contains(stem)) { WarnIfUnlisted(stem, file, width, height); return; }
				if (!_original.TryGetValue(stem, out (int W, int H) original))
				{
					original = Original(file);
					_original[stem] = original;
				}
				if (original.W > 0 && original.H > 0 && width > original.W && height > original.H)
				{
					float up = (float)width / original.W;
					int baseW = original.W, baseH = original.H;
					scaleU = up;
					scaleV = (float)height / original.H;
					Log.First(LogChannel.General, "upscaled-" + stem, 1, () => "ui: " + stem + " is " + width + " x " + height + " over the game's " + baseW + " x " + baseH + "; drawn at x" + up.ToString("0.##"));
				}
			}
			catch (Exception) { }
		}

		// Sheets whose code gives their size itself (Cell.SheetBase), so a larger picture is already handled.
		private static readonly HashSet<string> Elsewhere = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name_i", "ok" };

		/// <summary>A mod's picture for a sheet that is not listed above, larger than the game's own: it would be cut by the old cells and show only its corner. Said once in the log.</summary>
		private static void WarnIfUnlisted(string stem, string file, int width, int height)
		{
			if (Elsewhere.Contains(stem)) return;
			if (!_original.TryGetValue(stem, out (int W, int H) original)) { original = Original(file); _original[stem] = original; }
			if (original.W > 0 && original.H > 0 && width > original.W && height > original.H)
				Log.First(LogChannel.General, "upscaled-unlisted-" + stem, 1, () => "ui: " + stem + " is " + width + " x " + height + " over the game's " + original.W + " x " + original.H + " but is not in UpscaledSheets - it may draw as its top-left corner only");
		}

		/// <summary>The size of the game's own picture of a background (the world map's pieces: "w_map00_00" -> its .NCGR), (0, 0) when the shipped content has none. A background cut by cells is told this as Bg.BaseW / BaseH.</summary>
		public static (int W, int H) OriginalOf(string name)
		{
			try
			{
				if (string.IsNullOrEmpty(name)) return (0, 0);
				if (!_original.TryGetValue(name, out (int W, int H) size)) { size = Original(name + ".NCGR"); _original[name] = size; }
				return size;
			}
			catch (Exception) { return (0, 0); }
		}

		/// <summary>The size of the PNG the shipped content has under this name, (0, 0) when it has none.</summary>
		private static (int W, int H) Original(string file)
		{
			OpenFF.Content.ContentChain chain = GameArchive.Chain;
			if (chain == null) return (0, 0);
			string name = Path.GetFileName(file.Replace('\\', '/'));
			List<OpenFF.Content.IContentSource> sources = new List<OpenFF.Content.IContentSource> { chain.Shipped };
			sources.AddRange(chain.Fallbacks);
			foreach (OpenFF.Content.IContentSource source in sources)
				foreach (string candidate in new[] { "files/" + name, name })
					if (source.TryRead(candidate, out byte[] data) && data != null && data.Length >= 24 && data[0] == 0x89 && data[1] == 'P')
						return ((data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19], (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23]);
			return (0, 0);
		}
	}
}
