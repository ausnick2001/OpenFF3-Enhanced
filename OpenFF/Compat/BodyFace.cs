// Ordinary dialogue (the lines a speaker says in a message window) in a face of its own: the first font in the
// fonts\dialogue folder beside the game (OpenFF3 Enhanced.exe's folder; the older body-font folder still works), at a size of
// its own. With no font there, dialogue is drawn as before. A scale.txt beside the font holding a number (1 = the
// menus' size) sets the size; 0.88 without one. See FontFolders.

using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenFF.Client
{
	internal static class BodyFace
	{
		private const float DefaultScale = 0.88f;

		private static bool _looked;
		private static OpenFF.Content.MenuText _style;

		/// <summary>The lettering for dialogue, or null when there is no body-font to use.</summary>
		public static OpenFF.Content.MenuText Style
		{
			get
			{
				if (_looked) return _style;
				_looked = true;
				try
				{
					string file = FontFolders.First(FontFolders.Dialogue, "body-font");
					if (file == null) return null;
					float scale = DefaultScale;
					string scaleFile = Path.Combine(Path.GetDirectoryName(file), "scale.txt");
					if (File.Exists(scaleFile) && float.TryParse(File.ReadAllText(scaleFile).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float read) && read >= 0.5f && read <= 1.5f) scale = read;
					OpenFF.Content.MenuText style = new OpenFF.Content.MenuText();
					style.Families.Add("file:" + file);
					style.Scale = scale;
					_style = style;
					Log.Write(LogChannel.General, "text: dialogue in " + Path.GetFileName(file) + " at " + scale.ToString("0.##", CultureInfo.InvariantCulture));
				}
				catch (Exception ex) { Log.Write(LogChannel.General, "text: body-font: " + ex.Message); }
				return _style;
			}
		}
	}
}
