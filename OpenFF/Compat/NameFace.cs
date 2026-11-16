// The speaker's name in a dialogue window, in a face of its own: the first font in the fonts\names folder beside the
// game (OpenFF.exe's folder; the older name-font folder still works). With none there, the name is drawn as before.

using System;
using System.IO;
using System.Linq;

namespace OpenFF.Client
{
	internal static class NameFace
	{
		/// <summary>How far up (in the 480 x 320 text space) the name sits from where the game puts it, to stand off from the lines under it.</summary>
		public const int Lift = 5;

		private static bool _looked;
		private static OpenFF.Content.MenuText _style;

		private static OpenFF.Content.MenuText _story;

		/// <summary>The lettering for the centred story text over a picture (the cutscenes' lines), in the same font; null with no name-font.</summary>
		public static OpenFF.Content.MenuText Story
		{
			get
			{
				if (_story != null) return _story;
				OpenFF.Content.MenuText name = Style;
				if (name == null) return null;
				_story = new OpenFF.Content.MenuText { Scale = 0.85f };
				_story.Families.AddRange(name.Families);
				Log.Write(LogChannel.General, "text: centred story lines in the name font");
				return _story;
			}
		}

		/// <summary>The lettering for a speaker's name, or null when there is no name-font to use.</summary>
		public static OpenFF.Content.MenuText Style
		{
			get
			{
				if (_looked) return _style;
				_looked = true;
				try
				{
					string file = FontFolders.First(FontFolders.Names, "name-font");
					if (file == null) return null;
					OpenFF.Content.MenuText style = new OpenFF.Content.MenuText();
					style.Families.Add("file:" + file);
					style.Scale = 0.85f;
					_style = style;
					Log.Write(LogChannel.General, "text: speaker names in " + Path.GetFileName(file));
				}
				catch (Exception ex) { Log.Write(LogChannel.General, "text: name-font: " + ex.Message); }
				return _style;
			}
		}
	}
}
