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
		// The hand cursor, icon_yubi: two 48 x 48 frames in a 96 x 48 PNG.
		private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "icon_yubi" };
		private static readonly Dictionary<string, (int W, int H)> _original = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

		/// <summary>How much larger (across, down) a sheet's picture is than the game's own, 1 when it is not an upscaled listed sheet.</summary>
		public static void Scale(byte[] png, int width, int height, out float scaleU, out float scaleV)
		{
			scaleU = scaleV = 1f;
			try
			{
				string file = png == null ? null : TextureOverrides.SourceOf(png);
				if (file == null) return;
				string stem = Path.GetFileNameWithoutExtension(file.Replace('\\', '/'));
				if (!Names.Contains(stem)) return;
				if (!_original.TryGetValue(stem, out (int W, int H) original))
				{
					original = Original(file);
					_original[stem] = original;
				}
				if (original.W > 0 && original.H > 0 && width > original.W && height > original.H)
				{
					scaleU = (float)width / original.W;
					scaleV = (float)height / original.H;
					Log.First(LogChannel.General, "upscaled-" + stem, 1, () => "ui: " + stem + " is " + width + " x " + height + " over the game's " + original.W + " x " + original.H + "; drawn at x" + scaleU.ToString("0.##"));
				}
			}
			catch (Exception) { }
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
