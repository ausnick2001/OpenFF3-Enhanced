// The player's display settings: %LocalAppData%\OpenFF\settings.json.
//
//   {
//     "width": 1920, "height": 1080,       the window's size (windowed mode)
//     "mode": "borderless",                  windowed | borderless (the desktop's size, no frame) | fullscreen
//     "msaa": 4,                            anti-aliasing samples: 0 (off), 2, 4, 8
//     "vsync": true                         wait for the monitor
//   }
//
// Written with its defaults the first time the client runs, so there is a file to edit.
// The command line still wins for one run (--size=WxH, --fullscreen, --msaa=off|2|4|8,
// --windowed); Alt+Enter switches windowed and full screen while playing and remembers
// the choice here. A settings screen in the game, or a launcher, is the plan; this file
// is what either will write.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenFF.Client
{
	// MonoGame's types by name: the engine's OpenFF.Game sits a namespace up.
	using Game = Microsoft.Xna.Framework.Game;

	internal sealed class DisplaySettings
	{
		[JsonPropertyName("width")] public int Width { get; set; } = 1920;
		[JsonPropertyName("height")] public int Height { get; set; } = 1080;
		/// <summary>windowed, borderless or fullscreen.</summary>
		[JsonPropertyName("mode")] public string Mode { get; set; } = "borderless";
		/// <summary>Anti-aliasing samples: 0, 2, 4 or 8.</summary>
		[JsonPropertyName("msaa")] public int Msaa { get; set; } = 4;
		[JsonPropertyName("vsync")] public bool VSync { get; set; } = true;
		/// <summary>What the display shows: "30" - the game's frames as they are; "60" - smoothed, a frame drawn between each two of the game's (motion and the camera interpolated), presented 60 a second, each frame held the same number of refreshes (every second one at 120 Hz), or on a display sixty does not divide the nearest even rate above it (72 at 144 Hz); "max" - smoothed, at the display's rate. The game's own logic runs 30 steps a second in every mode.</summary>
		[JsonPropertyName("fps")] public string Fps { get; set; } = "60";
		/// <summary>How a game pad's left stick runs: "stick" - the push is the pace, a walk part way rising to the full run all the way (the touch stick's way); "hold" - the run button held runs, as with the keyboard's Shift.</summary>
		[JsonPropertyName("run")] public string Run { get; set; } = "stick";
		/// <summary>The game pad's buttons, DS button -> pad button. Names: cross/circle/square/triangle (or a/b/x/y), l1/r1/l2/r2 (or lb/rb/lt/rt), l3/r3, options/start, share/create/back, touchpad/guide. "none" unbinds.</summary>
		[JsonPropertyName("pad")] public PadMap Pad { get; set; } = new PadMap();
		/// <summary>Which glyphs the client's screens show for the pad's buttons: "auto" (by the pad's name), "ps" or "xbox".</summary>
		[JsonPropertyName("padStyle")] public string PadStyle { get; set; } = "auto";   // auto | ps | xbox | switch | keyboard (Settings > Button icons)
		/// <summary>Quality of life, as the Pixel Remaster's boosters (Qol): the game's speed, random encounters, EXP and job EXP, saving anywhere, the job change's adjustment period, the indicator.</summary>
		[JsonPropertyName("qol")] public QolSettings Qol { get; set; } = new QolSettings();
		/// <summary>Updates from GitHub's releases (Updates): "ask" offers a new version on the title, "auto" installs it, "off" never looks.</summary>
		[JsonPropertyName("updates")] public string Updates { get; set; } = "ask";
		/// <summary>The version "Skip this version" put away (its tag, v0.2.3); offered again once a newer one is out.</summary>
		[JsonPropertyName("skipVersion")] public string SkipVersion { get; set; }
		/// <summary>The HD textures' offer (HdTextures): "ask" offers them on the title while they are not installed, "no" - "Don't ask again" - never does.</summary>
		[JsonPropertyName("hdTextures")] public string HdTextures { get; set; } = "ask";
		/// <summary>The pale glow behind the prologue's and cutscenes' story text, so it reads over the picture: 0 off, 1 the default, up to 3 stronger.</summary>
		[JsonPropertyName("storyGlow")] public double StoryGlow { get; set; } = 1;
		/// <summary>Steam achievements (Achievements): earned in OpenFF as the game earns them, when it plays from the Steam
		/// install and Steam is running; false keeps OpenFF away from Steam.</summary>
		[JsonPropertyName("achievements")] public bool Achievements { get; set; } = true;
		[JsonPropertyName("_help")] public string Help { get; } =
			"width/height: the window (windowed mode). mode: windowed | borderless | fullscreen. msaa: 0, 2, 4 or 8. vsync: true/false. fps: 30 (the game's frames as they are) | 60 (smoothed - a frame interpolated between each two of the game's; where the display's refresh is no multiple of sixty, the nearest even rate above it, 72 at 144 Hz) | max (smoothed, at the display's rate); the game's logic runs 30 steps a second whatever is chosen. run: stick (the left stick's push is the pace - a walk part way, the full run all the way) | hold (the run button held runs). pad: which pad button is each DS button - cross/circle/square/triangle or a/b/x/y, l1/r1/l2/r2 or lb/rb/lt/rt, l3/r3, options/start, share/create/back, touchpad/guide, none; y is also the overworld map on FF3 (M on the keyboard too), select changes the party's leader on the field, start opens the client's menu; run and fast are the run and fast-forward buttons, speed and encounters the quality-of-life keys (F8, F11). updates: ask (a new version offered at the start, before the logos) | auto (installed at the start) | off. achievements: true (Steam achievements earned as the game earns them, when playing from the Steam install with Steam running; Steam then shows you in the game) | false. qol: speed 1-8 (the game's steps a frame; F8 cycles 1-4), encounterRate 0, 0.5, 1, 2 or 4 (how often random battles come, 0 none; F11 switches between 0 and the last rate; the older \"encounters\": false still means none), exp and jobExp multipliers 0-100 (the menu offers 0, 0.5, 1, 2, 4), saveAnywhere, jobAdjustment (false: no lowered stats after a job change), indicator (the corner's note of what is on). storyGlow: 0-3, the pale glow behind the prologue's text (0 off, 1 default). Alt+Enter in the game switches windowed and full screen and saves it here. The command line (--size=WxH, --fullscreen, --windowed, --msaa=n, --fps=30|60|max) wins for one run.";

		/// <summary>DS buttons as pad button names; the defaults are a PlayStation pad's natural layout, which SDL lays out the same as an Xbox pad's.</summary>
		public sealed class PadMap
		{
			[JsonPropertyName("a")] public string A { get; set; } = "cross";
			[JsonPropertyName("b")] public string B { get; set; } = "circle";
			[JsonPropertyName("x")] public string X { get; set; } = "triangle";
			[JsonPropertyName("y")] public string Y { get; set; } = "square";
			[JsonPropertyName("l")] public string L { get; set; } = "l1";
			[JsonPropertyName("r")] public string R { get; set; } = "r1";
			[JsonPropertyName("start")] public string Start { get; set; } = "options";
			[JsonPropertyName("select")] public string Select { get; set; } = "share";
			/// <summary>Held to run (with "run": "hold"; with "stick" it runs too, on top of the stick).</summary>
			[JsonPropertyName("run")] public string RunButton { get; set; } = "r2";
			/// <summary>Held to fast-forward, like Tab.</summary>
			[JsonPropertyName("fast")] public string Fast { get; set; } = "l2";
			/// <summary>Turns auto battle on and off, like F; R3 by default (on the Controller configuration page).</summary>
			[JsonPropertyName("auto")] public string Auto { get; set; } = "r3";
			/// <summary>Cycles the game's speed, like F8 (quality of life); L3 by default.</summary>
			[JsonPropertyName("speed")] public string SpeedUp { get; set; } = "l3";
			/// <summary>Turns random encounters on and off, like F11 (quality of life).</summary>
			[JsonPropertyName("encounters")] public string Encounters { get; set; } = "none";
		}

		/// <summary>Quality of life (Qol): everything off as the game plays unless the player turns it on.</summary>
		public sealed class QolSettings
		{
			/// <summary>How many of the game's steps a frame runs, 1-8 (F8 and the menu go 1-4).</summary>
			[JsonPropertyName("speed")] public int Speed { get; set; } = 1;
			/// <summary>Random battles; scripted ones always come.</summary>
			[JsonPropertyName("encounters")] public bool Encounters { get; set; } = true;
			/// <summary>How often random battles come: 0 (none), 0.5, 1, 2 or 4 times as often.</summary>
			[JsonPropertyName("encounterRate")] public double EncounterRate { get; set; } = 1;
			/// <summary>Battle EXP multiplier, 0-100.</summary>
			[JsonPropertyName("exp")] public double Exp { get; set; } = 1;
			/// <summary>Job EXP multiplier, 0-100.</summary>
			[JsonPropertyName("jobExp")] public double JobExp { get; set; } = 1;
			/// <summary>Save from the menu on any map, dungeons and towns too, not only the world map.</summary>
			[JsonPropertyName("saveAnywhere")] public bool SaveAnywhere { get; set; }
			/// <summary>The job change's adjustment period (lowered stats for a few battles); false for none.</summary>
			[JsonPropertyName("jobAdjustment")] public bool JobAdjustment { get; set; } = true;
			/// <summary>FF3's battles turn by turn (each hero chooses when their turn comes); false plays the game's own order. Read when a battle begins.</summary>
			[JsonPropertyName("turnBased")] public bool TurnBased { get; set; } = true;
			/// <summary>The white screen flash on a critical hit; false skips it (for those it bothers).</summary>
			[JsonPropertyName("criticalFlash")] public bool CriticalFlash { get; set; } = true;
			/// <summary>FF3's dialogue window with its text and the speaker's name, together (DialogueSizing): 0.6 to 1, 1 as the game draws it.</summary>
			[JsonPropertyName("dialogueSize")] public double DialogueSize { get; set; } = 1;
			/// <summary>The said lines' size on top of the window's: 0.7 to 1.3, 1 as set.</summary>
			[JsonPropertyName("dialogueTextSize")] public double DialogueTextSize { get; set; } = 1;
			/// <summary>The speaker's name size on top of the window's: 0.7 to 1.3, 1 as set. The prologue's and cutscenes' centred lines are not touched by any of these.</summary>
			[JsonPropertyName("nameSize")] public double NameSize { get; set; } = 1;
			/// <summary>The drop shadow behind the dialogue's lines (FF3); false draws them plain. Where the game's own script turns a message's shadow off it stays off.</summary>
			[JsonPropertyName("dialogueShadow")] public bool DialogueShadow { get; set; } = true;
			/// <summary>The drop shadow behind the menus' text and the other windows' (FF3); false draws it plain. The dialogue and the prologue's lines have their own.</summary>
			[JsonPropertyName("menuShadow")] public bool MenuShadow { get; set; } = true;
			/// <summary>How strong the dialogue's shadow is when it is on: 1 light (one text-grid unit), 2 medium, 3 heavy (two units, as the game drew most menus).</summary>
			[JsonPropertyName("dialogueShadowStrength")] public int DialogueShadowStrength { get; set; } = 2;
			/// <summary>How strong the menus' shadow is when it is on: 1 light, 2 medium, 3 heavy. A battle's text keeps the light one whatever this says.</summary>
			[JsonPropertyName("menuShadowStrength")] public int MenuShadowStrength { get; set; } = 2;
			/// <summary>A small map of the walls about the party in the corner of the screen, in dungeons and caves (Minimap.cs).</summary>
			[JsonPropertyName("minimap")] public bool Minimap { get; set; } = true;
			/// <summary>The minimap turns with the camera (up is where it looks); false keeps it fixed, as the camera first looked.</summary>
			[JsonPropertyName("minimapFog")] public bool MinimapFog { get; set; } = true;
			[JsonPropertyName("minimapRotate")] public bool MinimapRotate { get; set; }
			/// <summary>0 small, 1 medium, 2 large.</summary>
			[JsonPropertyName("minimapSize")] public int MinimapSize { get; set; } = 0;
			[JsonPropertyName("minimapZoom")] public int MinimapZoom { get; set; } = 2;
			/// <summary>How much of the map the party uncovers with fog of war: 0 is 100% (as first made), up to 4, 200%.</summary>
			[JsonPropertyName("minimapSee")] public int MinimapSee { get; set; } = 4;
			/// <summary>A solid map; false lets the game show through.</summary>
			[JsonPropertyName("minimapOpaque")] public bool MinimapOpaque { get; set; }
			/// <summary>A marker on each chest not yet opened.</summary>
			[JsonPropertyName("minimapChests")] public bool MinimapChests { get; set; } = true;
			/// <summary>Walls that can open (secret passages) marked in their own colour; false draws them as walls.</summary>
			[JsonPropertyName("minimapPassages")] public bool MinimapPassages { get; set; }
			/// <summary>The corner's note of what is on.</summary>
			[JsonPropertyName("indicator")] public bool Indicator { get; set; }
			/// <summary>With the indicator on: show only the speed and the encounter rate (when they are not x1), not every setting.</summary>
			[JsonPropertyName("indicatorSimple")] public bool IndicatorSimple { get; set; }
		}

		/// <summary>Whether a pad button, by its name in the map, is held in a pad state. Unknown names and "none" are never held.</summary>
		public static bool Held(GamePadState pad, string button)
		{
			switch ((button ?? "").Trim().ToLowerInvariant())
			{
				case "cross": case "a": return pad.Buttons.A == ButtonState.Pressed;
				case "circle": case "b": return pad.Buttons.B == ButtonState.Pressed;
				case "square": case "x": return pad.Buttons.X == ButtonState.Pressed;
				case "triangle": case "y": return pad.Buttons.Y == ButtonState.Pressed;
				case "l1": case "lb": return pad.Buttons.LeftShoulder == ButtonState.Pressed;
				case "r1": case "rb": return pad.Buttons.RightShoulder == ButtonState.Pressed;
				case "l2": case "lt": return pad.Triggers.Left > 0.5f;
				case "r2": case "rt": return pad.Triggers.Right > 0.5f;
				case "l3": case "ls": return pad.Buttons.LeftStick == ButtonState.Pressed;
				case "r3": case "rs": return pad.Buttons.RightStick == ButtonState.Pressed;
				case "options": case "start": return pad.Buttons.Start == ButtonState.Pressed;
				case "share": case "create": case "back": case "select": return pad.Buttons.Back == ButtonState.Pressed;
				case "touchpad": case "guide": case "ps": return pad.Buttons.BigButton == ButtonState.Pressed;
				default: return false;
			}
		}

		public static string Path => System.IO.Path.Combine(DataFolder.Settings, "settings.json");

		public static DisplaySettings Current { get; private set; } = new DisplaySettings();

		private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };

		/// <summary>Reads the file, writing one with the defaults when there is none; the command line's one-run overrides on top.</summary>
		public static DisplaySettings Load()
		{
			DisplaySettings settings = null;
			try
			{
				if (File.Exists(Path)) settings = JsonSerializer.Deserialize<DisplaySettings>(File.ReadAllText(Path));
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "settings: " + Path + " could not be read (" + ex.Message + "); the defaults stand");
			}
			if (settings == null)
			{
				settings = new DisplaySettings();
				settings.Save();
			}
			settings.Width = Math.Clamp(settings.Width, 320, 16384);
			settings.Height = Math.Clamp(settings.Height, 200, 16384);
			settings.Msaa = NormaliseMsaa(settings.Msaa);
			settings.Mode = NormaliseMode(settings.Mode);
			settings.Run = string.Equals(settings.Run, "hold", StringComparison.OrdinalIgnoreCase) ? "hold" : "stick";
			settings.Fps = NormaliseFps(settings.Fps);
			settings.StoryGlow = double.IsFinite(settings.StoryGlow) ? Math.Clamp(settings.StoryGlow, 0, 3) : 1;
			settings.Pad ??= new PadMap();
			settings.Qol ??= new QolSettings();
			settings.Updates = OpenFF.Client.Updates.NormaliseMode(settings.Updates);
			settings.Qol.Speed = Math.Clamp(settings.Qol.Speed, 1, OpenFF.Client.Qol.MostSpeed);
			settings.Qol.Exp = double.IsFinite(settings.Qol.Exp) ? Math.Clamp(settings.Qol.Exp, 0, OpenFF.Client.Qol.MostMultiplier) : 1;
			settings.Qol.JobExp = double.IsFinite(settings.Qol.JobExp) ? Math.Clamp(settings.Qol.JobExp, 0, OpenFF.Client.Qol.MostMultiplier) : 1;
			// Written back as read, so a file from an older build gains the keys it lacks, with their defaults, to edit.
			settings.Save();

			// The command line, for this run only.
			string size = Options.Get("size");
			if (!string.IsNullOrEmpty(size))
			{
				string[] parts = size.Split('x', 'X');
				if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && w > 0 && h > 0) { settings.Width = w; settings.Height = h; }
				else Log.Write(LogChannel.General, "ignoring --size=" + size + ", which is not <width>x<height>");
			}
			if (Options.Get("fullscreen") != null) settings.Mode = "fullscreen";
			if (Options.Get("windowed") != null) settings.Mode = "windowed";
			if (Options.Get("borderless") != null) settings.Mode = "borderless";
			string msaa = Options.Get("msaa");
			if (!string.IsNullOrEmpty(msaa)) settings.Msaa = string.Equals(msaa, "off", StringComparison.OrdinalIgnoreCase) ? 0 : int.TryParse(msaa, out int n) ? NormaliseMsaa(n) : settings.Msaa;
			if (Options.Get("novsync") != null) settings.VSync = false;
			if (!string.IsNullOrEmpty(Options.Get("fps"))) settings.Fps = NormaliseFps(Options.Get("fps"));
			Current = settings;
			return settings;
		}

		private static int NormaliseMsaa(int n) => n >= 8 ? 8 : n >= 4 ? 4 : n >= 2 ? 2 : 0;

		/// <summary>"30", "60" or "max": a number under 60 is "30", one over it (or unlimited/uncapped/0/off) is "max"; anything else is the default, "60".</summary>
		public static string NormaliseFps(string fps)
		{
			if (string.IsNullOrWhiteSpace(fps)) return "60";
			fps = fps.Trim().ToLowerInvariant();
			if (fps == "30" || fps == "60" || fps == "max") return fps;
			if (fps == "unlimited" || fps == "uncapped" || fps == "0" || fps == "off") return "max";
			return int.TryParse(fps, out int n) ? (n < 60 ? "30" : n == 60 ? "60" : "max") : "60";
		}

		private static string NormaliseMode(string mode)
		{
			switch ((mode ?? "").Trim().ToLowerInvariant())
			{
				case "fullscreen": case "full": return "fullscreen";
				case "borderless": case "windowed fullscreen": case "windowed-fullscreen": return "borderless";
				default: return "windowed";
			}
		}

		public void Save()
		{
			try
			{
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
				File.WriteAllText(Path, JsonSerializer.Serialize(this, Json));
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "settings: could not write " + Path + ": " + ex.Message);
			}
		}

		/// <summary>Lays the settings on the device manager before the device is made.</summary>
		public void Apply(GraphicsDeviceManager gdm)
		{
			DisplayMode desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
			bool full = Mode != "windowed";
			gdm.IsFullScreen = full;
			// Borderless: the desktop's own mode, no frame, no mode switch - what most players want
			// from "fullscreen" today. Fullscreen proper switches the display to the size asked.
			gdm.HardwareModeSwitch = Mode == "fullscreen";
			gdm.PreferredBackBufferWidth = Mode == "borderless" ? desktop.Width : Width;
			gdm.PreferredBackBufferHeight = Mode == "borderless" ? desktop.Height : Height;
			gdm.PreferMultiSampling = Msaa > 0;
			gdm.SynchronizeWithVerticalRetrace = VSync;
			gdm.PreparingDeviceSettings += (_, e) =>
			{
				e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = Msaa;
			};
			// MonoGame sets the swap interval to 1 or 0 itself when the device is made and at every
			// reset (a VSync change, Alt+Enter); the pacer then sets the one its plan wants again
			// (every second refresh for 60 a second on 120 Hz) and asks afresh whether VSync holds.
			gdm.DeviceCreated += (_, _) => FramePacer.DeviceChanged();
			gdm.DeviceReset += (_, _) => FramePacer.DeviceChanged();
			Log.Write(LogChannel.General, "settings: " + Width + "x" + Height + " " + Mode + " msaa=" + Msaa + " vsync=" + VSync + " fps=" + Fps + " (" + Path + ")");
		}

		// ---- Alt+Enter while playing ----

		private static bool _altEnterDown;

		/// <summary>Alt+Enter: windowed and full screen (borderless) in turn, remembered in the file. Called once a frame.</summary>
		public static void Poll(Game game, GraphicsDeviceManager gdm)
		{
			if (game == null || gdm == null || !game.IsActive) return;
			KeyboardState keys = Keyboard.GetState();
			bool down = (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt)) && keys.IsKeyDown(Keys.Enter);
			if (down && !_altEnterDown) Toggle(gdm);
			_altEnterDown = down;
		}

		/// <summary>Windowed to full screen (the file's fullscreen or borderless; borderless when it says windowed) and back.</summary>
		public static void Toggle(GraphicsDeviceManager gdm)
		{
			DisplaySettings s = Current;
			DisplayMode desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
			if (gdm.IsFullScreen)
			{
				s.Mode = "windowed";
				gdm.IsFullScreen = false;
				gdm.HardwareModeSwitch = false;
				gdm.PreferredBackBufferWidth = s.Width;
				gdm.PreferredBackBufferHeight = s.Height;
			}
			else
			{
				if (s.Mode == "windowed") s.Mode = "borderless";
				gdm.HardwareModeSwitch = s.Mode == "fullscreen";
				gdm.PreferredBackBufferWidth = s.Mode == "borderless" ? desktop.Width : s.Width;
				gdm.PreferredBackBufferHeight = s.Mode == "borderless" ? desktop.Height : s.Height;
				gdm.IsFullScreen = true;
			}
			try { gdm.ApplyChanges(); }
			catch (Exception ex) { Log.Write(LogChannel.General, "settings: the display did not switch: " + ex.Message); }
			Log.Write(LogChannel.General, "settings: " + (gdm.IsFullScreen ? s.Mode : "windowed") + " (Alt+Enter)");
			s.Save();
		}

		/// <summary>The window's size as the player left it, kept for next time (windowed mode only).</summary>
		public static void WindowResized(GraphicsDeviceManager gdm, Game game)
		{
			if (gdm == null || gdm.IsFullScreen || game == null) return;
			Rectangle bounds = game.Window.ClientBounds;
			if (bounds.Width < 320 || bounds.Height < 200) return;
			if (bounds.Width == Current.Width && bounds.Height == Current.Height) return;
			Current.Width = bounds.Width;
			Current.Height = bounds.Height;
			Current.Save();
		}
	}
}
