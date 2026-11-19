// The client's own menu over the game: Esc on the keyboard or Start (Options) on a pad opens
// it anywhere - the title, the field, a battle - with Resume, Settings and Exit game. It is
// not one of the game's menus (those are the phone's layouts and pictures); it is drawn the
// way the mod list and the text entry are, with the game's font over a panel, and it edits
// what %LocalAppData%\OpenFF\settings.json holds: the window and its size, anti-aliasing,
// vsync, how the stick runs, and which pad button is which DS button (press one to bind); and
// the quality-of-life options (Qol): the game's speed, random encounters, EXP and job EXP,
// saving anywhere, the job change's adjustment period, the corner's indicator.
// Display changes apply as they are made; the file is written on the way out.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenFF.Client
{
	// MonoGame's types by name: the engine's OpenFF.Game, GameTime, Color and vectors sit a namespace up.
	using Game = Microsoft.Xna.Framework.Game;
	using GameTime = Microsoft.Xna.Framework.GameTime;
	using Color = Microsoft.Xna.Framework.Color;

	internal sealed class PauseMenu : DrawableGameComponent
	{
		private const float W = 800f, H = 480f;
		private const int TitleSize = 14, RowSize = 10;
		// A page's geometry in the 800x480 text space: its panel, and its rows - the first's top, the step from one to the next, each's height.
		private readonly struct Layout
		{
			public readonly Rectangle Panel;
			public readonly float Top, Step, Height;
			public readonly bool Note;
			public Layout(Rectangle panel, float top, float step, float height, bool note) { Panel = panel; Top = top; Step = step; Height = height; Note = note; }
			public Rectangle RowAt(int row) => new Rectangle(Panel.X + 26, (int)Math.Round(Top + row * Step), Panel.Width - 52, (int)Math.Round(Height));
		}

		private Layout LayoutFor(Page page)
		{
			switch (page)
			{
				case Page.Main: return new Layout(new Rectangle(152, 44, 496, 392), 120, 41, 34, false);
				case Page.Map: return new Layout(new Rectangle(142, 40, 516, 430), 102, 29, 25, true);
				case Page.Quit: return new Layout(new Rectangle(190, 146, 420, 200), 222, 41, 34, false);
				case Page.Buttons: return new Layout(new Rectangle(142, 24, 516, 440), 92, 22, 19.5f, true);
				case Page.Settings: return new Layout(new Rectangle(142, 30, 516, 446), 96, 28, 25f, true);
				case Page.Qol: return new Layout(new Rectangle(142, 42, 516, 436), 104, 28, 25f, true);
				default: return new Layout(new Rectangle(142, 50, 516, 414), 122, 31, 27, true);
			}
		}

		public static PauseMenu Instance { get; private set; }
		public static bool IsOpen => Instance != null && Instance._page != Page.Closed;
		/// <summary>The game stands still while the menu is open: its last frame stays under the menu.</summary>
		public static bool HoldsGame => IsOpen;

		private enum Page { Closed, Main, Settings, Buttons, Qol, Map, Quit }
		private Page _page = Page.Closed;
		private int _selected;
		private string _binding;            // the DS button being rebound, while a press is awaited
		private int _previousPad, _previousPadRaw;
		private KeyboardState _previousKeys;
		private bool _mouseWasDown;
		private bool _openEdge;             // Esc/Start held since the open, so the press that opened does not also close
		private SpriteBatch _batch;
		private Texture2D _pixel;
		private string _note = "";

		private static readonly (int W, int H)[] Sizes = { (800, 480), (1200, 720), (1600, 960), (2000, 1200), (2400, 1440), (3200, 1920) };
		private static readonly string[] Modes = { "windowed", "borderless", "fullscreen" };
		private static readonly int[] Msaas = { 0, 2, 4, 8 };
		private static readonly (string Key, string Label)[] DsButtons =
		{
			("a", "A  (confirm)"), ("b", "B  (cancel, run)"), ("x", "X  (menu)"), ("y", "Y"), ("l", "L"), ("r", "R"),
			("start", "Start"), ("select", "Select"), ("run", "Run (held)"), ("fast", "Fast-forward (held)"),
			("speed", "Speed up (F8)"), ("encounters", "Encounters on / off (F11)")
		};

		// The main page's rows: Abilities only with a mastery hero in the party.
		private string[] MainRows()
		{
			List<string> rows = new List<string> { "resume" };
			if (SceneSkip.Skippable) rows.Add("skip");   // an FF4 scene that may be skipped: Steam's click, here
			if (AbilitiesRow) rows.Add("abilities");
			rows.Add("settings"); rows.Add("qol"); rows.Add("map");
			if (CanLeaveToTitle) rows.Add("reset");
			rows.Add("exit");
			return rows.ToArray();
		}

		private string _leave = "exit";   // what the confirmation page is for: exit | reset

		/// <summary>Load game and Reset only where the game can go back to the title: out in the world, not on the title or in a battle.</summary>
		private static bool CanLeaveToTitle => EngineApi.InWorld && !TitleEntries.Showing && Restart.Possible;

		private void LeaveToTitle()
		{
			Close();
			DisplaySettings.Current.Save();
			Log.Write(LogChannel.General, "menu: reset, to the title");
			try
			{
				GlobalScope.wld.CBaseSystem.setTitle(b: true);
				GlobalScope.wld.MapSound.stopBGM(15);
				ModMenus.LeaveMenus();
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "menu: to the title: " + ex.Message); }
		}

		private int MainRow(string id) => Math.Max(0, Array.IndexOf(MainRows(), id));

		private PauseMenu(Game game) : base(game)
		{
			DrawOrder = int.MaxValue - 3;
			UpdateOrder = int.MaxValue - 3;
		}

		public static void Attach(Game game)
		{
			Instance = new PauseMenu(game);
			game.Components.Add(Instance);
		}

		/// <summary>Whether Esc or a pad's Start is down: what opens the menu, read each frame while it is closed. In one of the
		/// game's menus Esc is their Back (DesktopInput) - and a press begun there is not this menu's, even after that menu closed on it.
		/// With hold (the menu closed), the pad's Start counts only once held StartHold: a tap is the game's own Start, which
		/// skips its waits; Esc opens at once. Open, a tap of Start closes the menu.</summary>
		private static bool OpenKeyDown(bool hold)
		{
			KeyboardState keys = Keyboard.GetState();
			bool esc = Down(keys, Keys.Escape);
			if (!esc) _escForGameMenu = false;
			else if ((ModMenus.GameMenuUp || Environment.TickCount64 - Ff4Menu.OpenSeen < 100 || MoviePlayer.Current != null || SceneSkip.PromptOpen) && !IsOpen) _escForGameMenu = true;   // a movie's, or the skip question's, Esc is theirs
			if (esc && !_escForGameMenu) return true;
			// The pad's own Start, as the defaults have it: a rebinding is for the game, not for this menu.
			bool start = (DesktopInput.PadOnlyBits(defaultPad: true) & 12) != 0;   // Start (8) or Select (4, Share): held, either opens the menu
			long now = Environment.TickCount64;
			if (!start) { _startSince = -1; return false; }
			if (_startSince < 0) _startSince = now;
			return !hold || now - _startSince >= StartHold;
		}

		private static bool _escForGameMenu;

		/// <summary>How long the pad's Start is held to open the menu, in milliseconds.</summary>
		private const long StartHold = 0;
		private static long _startSince = -1;

		public static void Open()
		{
			if (Instance == null || IsOpen) return;
			Instance._page = Page.Main;
			Instance._selected = 0;
			Instance._note = "";
			Instance._openEdge = true;
			Instance._previousKeys = Keyboard.GetState();
			Instance._previousPad = DesktopInput.RawPadBits(defaultPad: true);
			Instance._wasDown.Clear();
			foreach (Keys k in new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.W, Keys.A, Keys.S, Keys.D, Keys.Enter, Keys.Space, Keys.Z, Keys.X, Keys.Back, Keys.Escape }) if (Down(Instance._previousKeys, k)) Instance._wasDown.Add(k);
			Log.Write(LogChannel.General, "menu: opened (Esc / Start held)");
		}

		private void Close()
		{
			if (_page == Page.Closed) return;
			_page = Page.Closed;
			_binding = null;
			DisplaySettings.Current.Save();
			Log.Write(LogChannel.General, "menu: closed; settings.json written");
		}

		// A drive's injected keys count as held, so the menu can be tried unattended.
		private static bool Down(KeyboardState keys, Keys key) => keys.IsKeyDown(key) || DesktopInput.Injected.Contains(key);
		private bool Pressed(KeyboardState keys, Keys key) => Down(keys, key) && !_wasDown.Contains(key);
		private readonly HashSet<Keys> _wasDown = new HashSet<Keys>();

		public override void Update(GameTime gameTime)
		{
			if (!Game.IsActive && !Drive.Active) return;   // a drive's keys come whether the window has focus or not
			// Nothing else may own the keyboard: the text entry, the mod list, a mod's capture.
			bool othersOwn = (TextEntry.Instance != null && TextEntry.Instance.IsActive) || ModListScreen.IsOpen || AbilitiesMenu.IsOpen || UpdateScreen.IsOpen || GameFilesScreen.IsOpen || TexturesScreen.IsOpen || EngineInput.Captured;
			if (_page == Page.Closed)
			{
				if (othersOwn || RenderTest.Active) { _openEdge = OpenKeyDown(hold: true); return; }
				bool down = OpenKeyDown(hold: true);
				if (down && !_openEdge) Open();
				_openEdge = down;
				return;
			}

			KeyboardState keys = Keyboard.GetState();
			// The pad as the defaults have it (Cross confirms, Circle goes back), whatever it is bound to for the game:
			// otherwise a button bound wrongly here could take away the very buttons that put it right.
			int pad = DesktopInput.RawPadBits(defaultPad: true), edge = pad & ~_previousPad;
			_previousPad = pad;
			bool openDown = OpenKeyDown(hold: false);
			bool up = Pressed(keys, Keys.Up) || Pressed(keys, Keys.W) || (edge & 64) != 0;
			bool downKey = Pressed(keys, Keys.Down) || Pressed(keys, Keys.S) || (edge & 128) != 0;
			bool left = Pressed(keys, Keys.Left) || Pressed(keys, Keys.A) || (edge & 32) != 0;
			bool right = Pressed(keys, Keys.Right) || Pressed(keys, Keys.D) || (edge & 16) != 0;
			bool confirm = Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space) || Pressed(keys, Keys.Z) || (edge & 1) != 0;
			bool cancel = Pressed(keys, Keys.X) || Pressed(keys, Keys.Back) || (edge & 2) != 0 || (openDown && !_openEdge);
			_openEdge = openDown;
			_previousKeys = keys;
			_wasDown.Clear();
			foreach (Keys k in new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.W, Keys.A, Keys.S, Keys.D, Keys.Enter, Keys.Space, Keys.Z, Keys.X, Keys.Back, Keys.Escape }) if (Down(keys, k)) _wasDown.Add(k);

			if (_binding != null)
			{
				// Awaiting a pad button for a DS button: the first one pressed is it; B on the keyboard or Esc gives up.
				string pressed = NewlyPressedPadButton();
				if (pressed != null) { Bind(_binding, pressed); _binding = null; _note = "bound"; }
				else if (Pressed(keys, Keys.Escape) || Pressed(keys, Keys.X)) { _binding = null; _note = ""; }
				return;
			}

			int count = Rows();
			if (up) _selected = (_selected + count - 1) % count;
			if (downKey) _selected = (_selected + 1) % count;

			// The mouse: a row under the pointer is selected; a click on it confirms.
			bool mouseDown = DesktopInput.MouseInView(out int mx, out int my);
			if (mouseDown && !_mouseWasDown)
			{
				Layout layout = LayoutFor(_page);
				int row = (int)Math.Floor((my - layout.Top) / layout.Step);
				Rectangle hit = row >= 0 && row < count ? layout.RowAt(row) : Rectangle.Empty;
				if (hit.Contains(mx, my)) { _selected = row; confirm = true; }
			}
			_mouseWasDown = mouseDown;

			switch (_page)
			{
				case Page.Main:
					if (cancel) { Close(); return; }
					if (confirm)
					{
						// Resume, [Abilities,] Settings, Quality of life, Exit game.
						string[] rows = MainRows();
						string id = rows[Math.Clamp(_selected, 0, rows.Length - 1)];
						if (id == "abilities") { Close(); AbilitiesMenu.Open(); return; }
						if (id == "skip") { Close(); SceneSkip.SkipNow(); return; }
						if (id == "resume") Close();
						else if (id == "settings") { _page = Page.Settings; _selected = 0; _note = ""; }
						else if (id == "qol") { _page = Page.Qol; _selected = 0; _note = QolNote(0); }
						else if (id == "map") { _page = Page.Map; _selected = 0; _note = MapNote(0); }
						else { _leave = id; _page = Page.Quit; _selected = 1; }
					}
					break;
				case Page.Qol:
					if (cancel) { _page = Page.Main; _selected = MainRow("qol"); DisplaySettings.Current.Save(); return; }
					if (up || downKey) _note = QolNote(_selected);
					if (left || right || confirm) ChangeQol(_selected, left ? -1 : 1, confirm);
					break;
				case Page.Map:
					if (cancel) { _page = Page.Main; _selected = MainRow("map"); DisplaySettings.Current.Save(); return; }
					if (up || downKey) _note = MapNote(_selected);
					if (left || right || confirm) ChangeMap(_selected, left ? -1 : 1, confirm);
					break;
				case Page.Settings:
					if (cancel) { _page = Page.Main; _selected = MainRow("settings"); DisplaySettings.Current.Save(); return; }
					if (left || right || confirm) ChangeSetting(_selected, left ? -1 : 1, confirm);
					CheckNote();
					break;
				case Page.Buttons:
					if (cancel) { _page = Page.Settings; _selected = 10; return; }
					if (confirm)
					{
						if (_selected < DsButtons.Length) { _binding = DsButtons[_selected].Key; _note = "press the pad button for " + DsButtons[_selected].Label + "  (Esc gives up)"; }
						else if (_selected == DsButtons.Length) { DisplaySettings.Current.Pad = new DisplaySettings.PadMap(); _note = "the defaults are back"; }
						else { _page = Page.Settings; _selected = 10; }
					}
					break;
				case Page.Quit:
					if (_leave == "forget")
					{
						if (cancel || (confirm && _selected != 0)) { _page = Page.Map; _selected = 8; _note = MapNote(8); return; }
						if (confirm) { Minimap.Forget(); _page = Page.Map; _selected = 8; _note = "every explored floor is forgotten"; }
						return;
					}
					if (cancel) { _page = Page.Main; _selected = MainRow(_leave); return; }
					if (confirm)
					{
						if (_selected == 0 && _leave != "exit") LeaveToTitle();
						else if (_selected == 0) { DisplaySettings.Current.Save(); Log.Write(LogChannel.General, "menu: exit game"); Game.Exit(); }
						else { _page = Page.Main; _selected = MainRow(_leave); }
					}
					break;
			}
		}

		/// <summary>Whether the main page has the Abilities row: a hero on the mastery progression (FF5's way) in the party.</summary>
		private static bool AbilitiesRow
		{
			// Gone when a mod brings an Abilities screen of its own into the game's menu (Samples/Mastery does).
			get { try { return ProgressionLayer.AnyMastery && !ModMenus.HasScreen("abilities"); } catch (Exception) { return false; } }
		}

		private int Rows()
		{
			switch (_page)
			{
				case Page.Main: return MainRows().Length;
				case Page.Settings: return 12;
				case Page.Qol: return QolRows;
				case Page.Map: return MapRows;
				case Page.Buttons: return DsButtons.Length + 2;
				case Page.Quit: return 2;
				default: return 1;
			}
		}

		/// <summary>One settings row turned: left/right cycle, confirm goes forward (or opens the buttons page / goes back).</summary>
		private void ChangeSetting(int row, int by, bool confirm)
		{
			DisplaySettings s = DisplaySettings.Current;
			GraphicsDeviceManager gdm = GlobalScope.m_Graphics?.GetGraphicsDeviceManager();
			switch (row)
			{
				case 0:
				{
					int i = Array.IndexOf(Modes, s.Mode); if (i < 0) i = 0;
					s.Mode = Modes[(i + by + Modes.Length) % Modes.Length];
					ApplyDisplay(gdm);
					break;
				}
				case 1:
				{
					int i = -1;
					for (int k = 0; k < Sizes.Length; k++) if (Sizes[k].W == s.Width && Sizes[k].H == s.Height) i = k;
					i = i < 0 ? (by > 0 ? 0 : Sizes.Length - 1) : (i + by + Sizes.Length) % Sizes.Length;
					s.Width = Sizes[i].W; s.Height = Sizes[i].H;
					if (s.Mode == "windowed") ApplyDisplay(gdm);
					else _note = "";
					break;
				}
				case 2:
				{
					int i = Array.IndexOf(Msaas, s.Msaa); if (i < 0) i = 0;
					s.Msaa = Msaas[(i + by + Msaas.Length) % Msaas.Length];
					_note = "";
					break;
				}
				case 3:
					s.VSync = !s.VSync;
					if (gdm != null) { gdm.SynchronizeWithVerticalRetrace = s.VSync; try { gdm.ApplyChanges(); } catch (Exception) { } }
					_note = "";
					break;
				case 4:
				{
					string[] rates = { "30", "60", "max" };
					int i = Array.IndexOf(rates, s.Fps); if (i < 0) i = 1;
					s.Fps = rates[(i + by + rates.Length) % rates.Length];
					_note = "";
					break;
				}
				case 5:
					s.Run = s.Run == "stick" ? "hold" : "stick";
					break;
				case 6:
				{
					string[] modes = { "ask", "auto", "off" };
					int i = Array.IndexOf(modes, Updates.Mode); if (i < 0) i = 0;
					s.Updates = modes[(i + by + modes.Length) % modes.Length];
					_note = "";
					break;
				}
				case 7:
					if (confirm) { Updates.Check(asked: true); _checking = true; }
					break;
				case 8:
					// The game's files against the release's: the check's panel comes up over the game with its progress and answer.
					if (confirm) { s.Save(); Close(); GameFileCheck.Start(asked: true); }
					break;
				case 9:
				{
					int i = Array.IndexOf(Ui.PadStyles, Ui.NormalisePadStyle(s.PadStyle));
					s.PadStyle = Ui.PadStyles[(i + by + Ui.PadStyles.Length) % Ui.PadStyles.Length];
					_note = "";
					break;
				}
				case 10:
					if (confirm) { _page = Page.Buttons; _selected = 0; _note = ""; }
					break;
				case 11:
					if (confirm) { _page = Page.Main; _selected = MainRow("settings"); s.Save(); }
					break;
			}
		}

		// Settings' Check now asked: its answer in the note as it comes.
		private bool _checking;

		private void CheckNote()
		{
			if (!_checking) return;
			switch (Updates.Now)
			{
				case Updates.Stage.Checking: _note = "checking for updates..."; break;
				case Updates.Stage.UpToDate: _note = "you have the newest - OpenFF " + Updates.Current.ToString(3); _checking = false; break;
				case Updates.Stage.Available: _note = "OpenFF " + Updates.Found.Version.ToString(3) + " is out - it is offered at the next start"; _checking = false; break;
				case Updates.Stage.Failed: _note = Updates.Error ?? "the check did not go through"; _checking = false; break;
			}
		}

		private void ApplyDisplay(GraphicsDeviceManager gdm)
		{
			if (gdm == null) return;
			DisplaySettings s = DisplaySettings.Current;
			DisplayMode desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
			try
			{
				gdm.IsFullScreen = s.Mode != "windowed";
				gdm.HardwareModeSwitch = s.Mode == "fullscreen";
				gdm.PreferredBackBufferWidth = s.Mode == "borderless" ? desktop.Width : s.Width;
				gdm.PreferredBackBufferHeight = s.Mode == "borderless" ? desktop.Height : s.Height;
				gdm.ApplyChanges();
				_note = "";
			}
			catch (Exception ex) { _note = "the display did not switch: " + ex.Message; }
		}

		private static void Bind(string dsButton, string padButton)
		{
			DisplaySettings.PadMap m = DisplaySettings.Current.Pad ??= new DisplaySettings.PadMap();
			switch (dsButton)
			{
				case "a": m.A = padButton; break;
				case "b": m.B = padButton; break;
				case "x": m.X = padButton; break;
				case "y": m.Y = padButton; break;
				case "l": m.L = padButton; break;
				case "r": m.R = padButton; break;
				case "start": m.Start = padButton; break;
				case "select": m.Select = padButton; break;
				case "run": m.RunButton = padButton; break;
				case "fast": m.Fast = padButton; break;
				case "speed": m.SpeedUp = padButton; break;
				case "encounters": m.Encounters = padButton; break;
			}
		}

		// ---- minimap ----

		// Minimap, Type, Size, Zoom, Area uncovered, Style, Chests, Hidden passages, Forget explored maps, Back.
		private const int MapRows = 10;

		private static readonly string[] MapSizes = { "Small", "Medium", "Large" };
		private static readonly string[] MapZooms = { "50%", "75%", "100%", "150%", "200%" };
		private static readonly string[] MapAreas = { "100%", "125%", "150%", "175%", "200%" };

		private void ChangeMap(int row, int by, bool confirm)
		{
			DisplaySettings.QolSettings q = Qol.S;
			switch (row)
			{
				case 0: q.Minimap = !q.Minimap; Qol.Changed("minimap " + (q.Minimap ? "on" : "off")); break;
				case 1: q.MinimapFog = !q.MinimapFog; Qol.Changed("minimap " + (q.MinimapFog ? "fog of war" : "complete")); break;
				case 2: q.MinimapSize = ((q.MinimapSize + by) % 3 + 3) % 3; Qol.Changed("minimap size " + MapSizes[q.MinimapSize]); break;
				case 3: q.MinimapZoom = Math.Clamp(q.MinimapZoom + by, 0, MapZooms.Length - 1); Qol.Changed("minimap zoom " + MapZooms[q.MinimapZoom]); break;
				case 4: q.MinimapSee = Math.Clamp(q.MinimapSee + by, 0, MapAreas.Length - 1); Qol.Changed("minimap area uncovered " + MapAreas[q.MinimapSee]); break;
				case 5: q.MinimapOpaque = !q.MinimapOpaque; Qol.Changed("minimap " + (q.MinimapOpaque ? "opaque" : "transparent")); break;
				case 6: q.MinimapChests = !q.MinimapChests; Qol.Changed("minimap chests " + (q.MinimapChests ? "on" : "off")); break;
				case 7: q.MinimapPassages = !q.MinimapPassages; Qol.Changed("minimap hidden passages " + (q.MinimapPassages ? "shown" : "hidden")); break;
				case 8:
					if (confirm) { _leave = "forget"; _page = Page.Quit; _selected = 1; }   // asks first: the maps cannot be got back
					return;
				case 9:
					if (confirm) { _page = Page.Main; _selected = MainRow("map"); DisplaySettings.Current.Save(); }
					return;
			}
			_note = MapNote(row);
		}

		private static string MapNote(int row)
		{
			switch (row)
			{
				case 0: return "Minimap off or on. May spoil hidden passages and chests";
				case 1: return "Complete: the whole floor is shown from the start, even behind walls";
				case 2: return "how big the map is in its corner";
				case 3: return "How zoomed the map is. Lower % = more of the map visible";
				case 4: return "Fog of war: amount of map uncovered. Higher % may spoil secrets";
				case 5: return "Transparent - game partially visible behind minimap";
				case 6: return "Shows unopened chests on minimap";
				case 7: return "complete maps only: walls that can open are marked";
				case 8: return "Fog of war only: deletes all map progress made";
				default: return "";
			}
		}

		/// <summary>The main page's row: the map's state in a few words.</summary>
		private static string MapSummary()
		{
			DisplaySettings.QolSettings q = Qol.S;
			return q.Minimap ? (q.MinimapFog ? "fog of war" : "complete") : "off";
		}

		// ---- quality of life ----

		// Speed, Random encounters, Encounter rate, Battle EXP, Job EXP, Save anywhere, Job change adjustment, Turn-based battles, Indicator, Back.
		private const int QolRows = 10;

		/// <summary>One quality-of-life row turned: left/right go through its values, confirm forward (or back, on Back).</summary>
		private void ChangeQol(int row, int by, bool confirm)
		{
			DisplaySettings.QolSettings q = Qol.S;
			switch (row)
			{
				case 0:
					q.Speed = (int)Qol.Next(Array.ConvertAll(Qol.Speeds, x => (double)x), Qol.Speed, by);
					Qol.Changed("speed x" + q.Speed);
					break;
				case 1:
					q.Encounters = !q.Encounters;
					Qol.Changed("random encounters " + (q.Encounters ? "on" : "off"));
					break;
				case 2:
					q.EncounterRate = Qol.Next(Qol.Rates, Qol.EncounterRate, by);
					Qol.Changed("encounter rate " + Qol.Times(q.EncounterRate));
					break;
				case 3:
					q.Exp = Qol.Next(Qol.Multipliers, q.Exp, by);
					Qol.Changed("EXP " + Qol.Times(q.Exp));
					break;
				case 4:
					q.JobExp = Qol.Next(Qol.Multipliers, q.JobExp, by);
					Qol.Changed("job EXP " + Qol.Times(q.JobExp));
					break;
				case 5:
					q.SaveAnywhere = !q.SaveAnywhere;
					Qol.Changed("save anywhere " + (q.SaveAnywhere ? "on" : "off"));
					break;
				case 6:
					q.JobAdjustment = !q.JobAdjustment;
					Qol.Changed("job change adjustment " + (q.JobAdjustment ? "on" : "off"));
					break;
				case 7:
					q.TurnBased = !q.TurnBased;
					Qol.Changed("turn-based battles " + (q.TurnBased ? "on" : "off"));
					break;
				case 8:
					// Off, then speed and encounters only, then everything that is changed.
					int level = !q.Indicator ? 0 : q.IndicatorSimple ? 1 : 2;
					level = ((level + by) % 3 + 3) % 3;
					q.Indicator = level != 0; q.IndicatorSimple = level == 1;
					Qol.Changed("indicator " + (level == 0 ? "off" : level == 1 ? "speed and encounters" : "all"));
					break;
				case 9:
					if (confirm) { _page = Page.Main; _selected = MainRow("qol"); DisplaySettings.Current.Save(); }
					return;
			}
			_note = QolNote(row);
		}

		/// <summary>What a quality-of-life row does, under the list.</summary>
		private static string QolNote(int row)
		{
			switch (row)
			{
				case 0: return "The whole game runs faster. F8 while playing, Shift+F8 back";
				case 1: return "off: no random battles, story fights still happen; F11";
				case 2: return "Increase or decrease frequency of random battles, default is 1x";
				case 3: return "EXP from each battle, multiplied";
				case 4: return "job EXP from each action in battle, multiplied";
				case 5: return "";
				case 6: return "Off: no lowered stats after a job change. On: the game's default";
				case 7: return "on: similar to FFX - heroes act as soon as a command is input";
				case 8: return "Text overlay in top-left corner indicating what features are on";
				default: return "";
			}
		}

		/// <summary>The main page's row: what is on, or that nothing is.</summary>
		private static string QolSummary()
		{
			DisplaySettings.QolSettings q = Qol.S;
			List<string> on = new List<string>();
			if (Qol.Speed != 1) on.Add("speed " + Qol.Times(Qol.Speed));
			if (!q.Encounters) on.Add("no encounters");
			
			if (Math.Abs(Qol.EncounterRate - 1) > 1e-9) on.Add("encounter rate " + Qol.Times(Qol.EncounterRate));
			if (Math.Abs(q.Exp - 1) > 1e-9) on.Add("EXP " + Qol.Times(q.Exp));
			if (Math.Abs(q.JobExp - 1) > 1e-9) on.Add("job EXP " + Qol.Times(q.JobExp));
			if (q.SaveAnywhere) on.Add("save anywhere");
			if (!q.JobAdjustment) on.Add("no adjustment");
			if (!q.TurnBased) on.Add("classic battles");
			if (on.Count == 0) return "as the game plays";
			string all = string.Join(", ", on);
			return all.Length > 40 ? on.Count + " on" : all;
		}

		private static string Bound(string dsButton)
		{
			DisplaySettings.PadMap m = DisplaySettings.Current.Pad ?? new DisplaySettings.PadMap();
			switch (dsButton)
			{
				case "a": return m.A; case "b": return m.B; case "x": return m.X; case "y": return m.Y;
				case "l": return m.L; case "r": return m.R; case "start": return m.Start; case "select": return m.Select;
				case "run": return m.RunButton; case "fast": return m.Fast;
				case "speed": return m.SpeedUp; case "encounters": return m.Encounters;
				default: return "";
			}
		}

		private static readonly string[] PadButtonNames = { "cross", "circle", "square", "triangle", "l1", "r1", "l2", "r2", "l3", "r3", "options", "share", "touchpad" };

		/// <summary>The pad button that went down since the last look, by its name; null for none.</summary>
		private string NewlyPressedPadButton()
		{
			for (int i = 0; i < 4; i++)
			{
				GamePadState pad;
				try { pad = GamePad.GetState((PlayerIndex)i); } catch (Exception) { continue; }
				if (!pad.IsConnected) continue;
				int now = 0;
				for (int b = 0; b < PadButtonNames.Length; b++) if (DisplaySettings.Held(pad, PadButtonNames[b])) now |= 1 << b;
				int fresh = now & ~_previousPadRaw;
				_previousPadRaw = now;
				for (int b = 0; b < PadButtonNames.Length; b++) if ((fresh & (1 << b)) != 0) return PadButtonNames[b];
				return null;
			}
			return null;
		}

		// What a row shows on its right: nothing, a stepper (left / right go through values), a switch, a text, or a chevron (a page).
		private enum Kind { None, Stepper, Switch, Text, Page }

		/// <summary>A row's icon, what it shows on its right, whether a switch is on, and a muted word beside it ("as the game plays").</summary>
		private Kind RowKind(int row, out UiTheme.Icon? icon, out bool on, out string hint)
		{
			DisplaySettings s = DisplaySettings.Current;
			icon = null; on = false; hint = null;
			switch (_page)
			{
				case Page.Main:
				{
					string[] rows = MainRows();
					string id = rows[Math.Clamp(row, 0, rows.Length - 1)];
					icon = id == "resume" || id == "skip" ? UiTheme.Icon.Resume : id == "abilities" ? UiTheme.Icon.Abilities : id == "settings" ? UiTheme.Icon.Settings : id == "qol" ? UiTheme.Icon.Quality : id == "map" ? UiTheme.Icon.Window : id == "reset" ? UiTheme.Icon.Settings : UiTheme.Icon.Exit;
					return Kind.None;
				}
				case Page.Qol:
				{
					DisplaySettings.QolSettings q = Qol.S;
					switch (row)
					{
						case 0: icon = UiTheme.Icon.Speed; return Kind.Stepper;
						case 1: icon = UiTheme.Icon.Encounters; on = q.Encounters; return Kind.Switch;
						case 2: icon = UiTheme.Icon.Encounters; return Kind.Stepper;
						case 3: icon = UiTheme.Icon.Exp; return Kind.Stepper;
						case 4: icon = UiTheme.Icon.JobExp; return Kind.Stepper;
						case 5: icon = UiTheme.Icon.Save; on = q.SaveAnywhere; return Kind.Switch;
						case 6: icon = UiTheme.Icon.Adjustment; on = q.JobAdjustment; return Kind.Switch;
						case 7: icon = UiTheme.Icon.Encounters; on = q.TurnBased; return Kind.Switch;
						case 8: icon = UiTheme.Icon.Indicator; return Kind.Stepper;
						default: icon = UiTheme.Icon.Back; return Kind.None;
					}
				}
				case Page.Map:
				{
					DisplaySettings.QolSettings q = Qol.S;
					switch (row)
					{
						case 0: icon = UiTheme.Icon.Window; on = q.Minimap; return Kind.Switch;
						case 1: icon = UiTheme.Icon.Quality; return Kind.Stepper;
						case 2: icon = UiTheme.Icon.WindowSize; return Kind.Stepper;
						case 3: icon = UiTheme.Icon.WindowSize; return Kind.Stepper;
						case 4: icon = UiTheme.Icon.Adjustment; return Kind.Stepper;
						case 5: icon = UiTheme.Icon.Window; return Kind.Stepper;
						case 6: icon = UiTheme.Icon.Quality; on = q.MinimapChests; return Kind.Switch;
						case 7: icon = UiTheme.Icon.Indicator; on = q.MinimapPassages; return Kind.Switch;
						case 8: icon = UiTheme.Icon.Exit; return Kind.None;
						default: icon = UiTheme.Icon.Back; return Kind.None;
					}
				}
				case Page.Settings:
					switch (row)
					{
						case 0: icon = UiTheme.Icon.Window; return Kind.Stepper;
						case 1: icon = UiTheme.Icon.WindowSize; return Kind.Stepper;
						case 2: icon = UiTheme.Icon.AntiAliasing; return Kind.Stepper;
						case 3: icon = UiTheme.Icon.VSync; on = s.VSync; return Kind.Switch;
						case 4: icon = UiTheme.Icon.FrameRate; return Kind.Stepper;
						case 5: icon = UiTheme.Icon.Run; return Kind.Stepper;
						case 6: icon = UiTheme.Icon.Indicator; return Kind.Stepper;
						case 7: icon = UiTheme.Icon.Resume; return Kind.Page;
						case 8: icon = UiTheme.Icon.Save; return Kind.Page;
						case 9: icon = UiTheme.Icon.Pad; return Kind.Stepper;
						case 10: icon = UiTheme.Icon.Pad; return Kind.Page;
						default: icon = UiTheme.Icon.Back; return Kind.None;
					}
				case Page.Buttons:
					return row < DsButtons.Length ? Kind.Text : Kind.None;
				default:
					icon = row == 0 ? UiTheme.Icon.Exit : UiTheme.Icon.Resume;
					return Kind.None;
			}
		}

		private UiTheme.Icon? TitleIcon() => _page == Page.Settings || _page == Page.Qol || _page == Page.Map ? UiTheme.Icon.Settings : _page == Page.Buttons ? UiTheme.Icon.Pad : _page == Page.Quit ? UiTheme.Icon.Exit : (UiTheme.Icon?)null;

		public override void Draw(GameTime gameTime)
		{
			if (_page == Page.Closed || RenderTest.Active) return;
			DrawMenu();
		}

		// Text with a soft black shadow, as the party menu's has.
		private static void L(GlobalScope.Graphics g, string text, float x, float top, float h, int size, Color c)
		{
			Ui.Left(g, text, x + 1, top + 1, h, size, new Color(0, 0, 0, 210));
			Ui.Left(g, text, x, top, h, size, c);
		}

		private static void C(GlobalScope.Graphics g, string text, Rectangle r, int size, Color c)
		{
			Ui.Centred(g, text, new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height), size, new Color(0, 0, 0, 210));
			Ui.Centred(g, text, r, size, c);
		}

		/// <summary>The size of a pad button's icon on a row of the Pad buttons page.</summary>
		private static float BoundDisc(Rectangle row) => Math.Min(18f, row.Height - 2f);

		private void DrawMenu()
		{
			GlobalScope.Graphics graphics = GlobalScope.m_Graphics;
			if (graphics == null) return;
			EnsureResources();
			UiTheme.Ensure(GraphicsDevice);
			Viewport view = GraphicsDevice.Viewport;
			int count = Rows();
			Layout layout = LayoutFor(_page);
			Rectangle panel = layout.Panel;
			bool compact = _page == Page.Buttons;
			int rowSize = compact ? 9 : _page == Page.Main || _page == Page.Quit ? 12 : 11, titleSize = _page == Page.Main ? 19 : 16, subSize = 9, hintSize = 9, noteSize = 9;
			float titleY = panel.Y + 16, dividerY = panel.Y + 62, hintY = panel.Bottom - 22;
			UiTheme.Icon? titleIcon = TitleIcon();
			float titleX = panel.X + 30;
			Rectangle note = new Rectangle(panel.X + 20, (int)(layout.Top + count * layout.Step + 4), panel.Width - 40, compact ? 22 : 36);

			// The values' places: a stepper's box and a switch, sized to what they show.
			Rectangle[] valueBox = new Rectangle[count];
			float[] boundWidth = new float[count];   // a pad button shown as its icon (the Pad buttons page): the icon's width
			TrueTypeText.TitleFace = false;   // the menu uses the same face as the rest of the game
			try
			{
				for (int row = 0; row < count; row++)
				{
					Rectangle r = layout.RowAt(row);
					Kind kind = RowKind(row, out _, out _, out _);
					RowText(row, out _, out string value);
					if (kind == Kind.Text && _page == Page.Buttons && row < DsButtons.Length && _binding != DsButtons[row].Key) boundWidth[row] = Ui.BoundWidth(graphics, value, BoundDisc(r), rowSize);
					int h = (int)Math.Round(r.Height - 3f);   // nearly the row's whole height, so the lettering sits inside it
					int top = r.Y + (r.Height - h) / 2;
					if (kind == Kind.Stepper)
					{
						int w = Math.Max(150, (int)Math.Ceiling(Ui.Width(graphics, value ?? "", rowSize)) + (int)(h * 2.5f) + 36);
						w = Math.Min(w, r.Width - 190);
						valueBox[row] = new Rectangle(r.Right - 8 - w, top, w, h);
					}
					else if (kind == Kind.Switch) valueBox[row] = new Rectangle(r.Right - 8 - 80, top, 80, h);
				}
			}
			finally { TrueTypeText.TitleFace = false; }

			// The hints, right-aligned at the foot (at the left on the short pages), measured in the menu's face.
			(Ui.PadButton, string)[] hints = Hints();
			float[] hintX = new float[hints.Length];
			TrueTypeText.TitleFace = false;   // the menu uses the same face as the rest of the game
			try
			{
				float hx = panel.Right - 30;
				for (int i = hints.Length - 1; i >= 0; i--) { hx -= Ui.HintWidth(graphics, hints[i].Item2, hintSize, 22, hints[i].Item1); hintX[i] = hx; hx -= 30; }
				if (_page == Page.Main || _page == Page.Quit) { float shift = hintX[0] - (panel.X + 30); for (int i = 0; i < hintX.Length; i++) hintX[i] -= shift; }
			}
			finally { TrueTypeText.TitleFace = false; }

			_batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearWrap);
			UiTheme.Panel(_batch, panel, view);
			UiTheme.Divider(_batch, panel.Center.X, dividerY, panel.Width - 60, view);
			for (int row = 0; row < count; row++)
			{
				Rectangle r = layout.RowAt(row);
				bool lit = row == _selected;
				UiTheme.Row(_batch, r, lit, view, diamonds: lit && (_page == Page.Main || _page == Page.Quit));
				Kind kind = RowKind(row, out UiTheme.Icon? icon, out bool on, out _);
				if (lit) UiTheme.Hand(_batch, r, r.X + 40, view);
				if (kind == Kind.Stepper) UiTheme.Stepper(_batch, valueBox[row], lit, view);
				else if (kind == Kind.Switch) UiTheme.Switch(_batch, valueBox[row], on, lit, view);
				else if (kind == Kind.Page) Ui.IconAt(_batch, Ui.Icon.Right, r.Right - 20, r.Y + r.Height / 2f, r.Height * 0.5f, lit ? UiTheme.LitInk : UiTheme.Ink, view);
				else if (kind == Kind.Text && boundWidth[row] > 0)
				{
					RowText(row, out _, out string bound);
					Ui.BoundShape(_batch, bound, r.Right - 14 - boundWidth[row], r.Y + r.Height / 2f, BoundDisc(r), boundWidth[row], view);
				}
			}
			if (layout.Note && !string.IsNullOrEmpty(_note)) UiTheme.NoteBox(_batch, note, view);
			for (int i = 0; i < hints.Length; i++) Ui.HintShape(_batch, hints[i].Item1, hintX[i], hintY, 22, view);
			_batch.End();

			graphics.SetImageOrigin(0f, 0f);
			graphics.SetImageRotation(0f);
			graphics.SetImageScale(1f, 1f);
			graphics.DrawStringStart();
			TrueTypeText.TitleFace = false;   // the menu uses the same face as the rest of the game
			try
			{
				string title = _page == Page.Main ? "OpenFF3 Enhanced" : _page == Page.Settings ? "Settings" : _page == Page.Buttons ? "Pad buttons" : _page == Page.Qol ? "Quality of life" : _page == Page.Map ? "Minimap" : _leave == "forget" ? "Are you sure?" : _leave == "reset" ? "Reset?" : "Exit game?";
				L(graphics, title, titleX, titleY, 30, titleSize, UiTheme.Ink);
				string sub = _page == Page.Main ? ""
					: _page == Page.Settings ? "Kept in settings.json."
					: _page == Page.Buttons ? "Pick one, then press its pad button."
					: _page == Page.Qol ? "Boosters, like the Pixel Remaster's."
					: _page == Page.Map ? "A corner map for dungeons."
					: _leave == "forget" ? "Deletes all map data on this save. This cannot be reversed."
					: "Anything not saved is lost.";
				// Beside the title when it fits there (smaller if it must), else under it.
				float subX = titleX + Ui.Width(graphics, title, titleSize) + 18, room = panel.Right - 30 - subX;
				int fit = subSize;
				while (fit > 7 && Ui.Width(graphics, sub, fit) > room) fit--;
				if (sub.Length == 0) { }
				else if (Ui.Width(graphics, sub, fit) <= room) L(graphics, sub, subX, titleY + 3, 30, fit, UiTheme.Sub);
				else L(graphics, sub, titleX, titleY + 26, 16, 7, UiTheme.Sub);
				// The version, top right of the main page.
				if (_page == Page.Main)
				{
					string version = "v" + Updates.Current.ToString(3);
					L(graphics, version, panel.Right - 30 - Ui.Width(graphics, version, subSize + 1), titleY + 6, 30, subSize + 1, UiTheme.Sub);
				}
				for (int row = 0; row < count; row++)
				{
					Rectangle r = layout.RowAt(row);
					bool lit = row == _selected;
					Kind kind = RowKind(row, out UiTheme.Icon? icon, out bool on, out string hint);
					RowText(row, out string label, out string value);
					float labelX = r.X + 46;
					L(graphics, label, labelX, r.Y, r.Height, rowSize, lit ? Color.White : UiTheme.Ink);
					switch (kind)
					{
						case Kind.Stepper:
							C(graphics, value ?? "", UiTheme.StepperValue(valueBox[row]), FitSize(graphics, value ?? "", UiTheme.StepperValue(valueBox[row]).Width - 10, rowSize), lit ? UiTheme.LitInk : UiTheme.Ink);
							break;
						case Kind.Switch:
							C(graphics, on ? "On" : "Off", UiTheme.SwitchWord(valueBox[row]), rowSize - 1, on ? UiTheme.Ink : UiTheme.Hint);
							break;
						case Kind.Text:
							if (boundWidth[row] > 0) Ui.BoundText(graphics, value, r.Right - 14 - boundWidth[row], r.Y + r.Height / 2f, BoundDisc(r), boundWidth[row], rowSize);
							else if (value != null) { float w = Ui.Width(graphics, value, rowSize); L(graphics, value, r.Right - 14 - w, r.Y, r.Height, rowSize, lit ? UiTheme.LitInk : UiTheme.Hint); }
							break;
					}
					if (hint != null && valueBox[row].Width > 0)
					{
						float w = Ui.Width(graphics, hint, rowSize - 1);
						L(graphics, hint, valueBox[row].X - 12 - w, r.Y, r.Height, rowSize - 1, UiTheme.Hint);
					}
				}
				if (layout.Note && !string.IsNullOrEmpty(_note))
				{
					// The description: its first letter capital, on one line, the size shrunk until it fits the box.
					string text = char.ToUpperInvariant(_note[0]) + _note.Substring(1);
					int size = noteSize;
					float noteRoom = note.Width - 28;
					while (size > 6 && Ui.Width(graphics, text, size) > noteRoom) size--;
					L(graphics, text, note.X + 14, note.Y, note.Height, size, UiTheme.Ink);
				}
				for (int i = 0; i < hints.Length; i++) Ui.HintText(graphics, hints[i].Item1, hints[i].Item2, hintX[i], hintY, 22, hintSize);
			}
			finally { TrueTypeText.TitleFace = false; }
			graphics.DrawStringEnd();
		}

		/// <summary>The words broken into lines no wider than a room, at a size.</summary>
		private static List<string> WrapNote(GlobalScope.Graphics graphics, string text, float room, int size)
		{
			List<string> lines = new List<string>();
			string line = "";
			foreach (string word in text.Split(' '))
			{
				string tryLine = line.Length == 0 ? word : line + " " + word;
				if (line.Length > 0 && Ui.Width(graphics, tryLine, size) > room) { lines.Add(line); line = word; }
				else line = tryLine;
			}
			if (line.Length > 0) lines.Add(line);
			return lines;
		}

		/// <summary>The largest size (down to a floor) at which the text is no wider than the room: a face wider than the one the menu was laid out for must not run out of its box.</summary>
		private static int FitSize(GlobalScope.Graphics graphics, string text, float room, int size)
		{
			int fit = size;
			while (fit > 7 && Ui.Width(graphics, text, fit) > room) fit--;
			return fit;
		}

		private (Ui.PadButton, string)[] Hints()
		{
			if (_page == Page.Settings || _page == Page.Qol || _page == Page.Map) return new[] { (Ui.PadButton.A, "Confirm"), (Ui.PadButton.B, "Back") };
			if (_page == Page.Buttons) return new[] { (Ui.PadButton.A, _binding == null ? "Bind" : "Press a button"), (Ui.PadButton.B, "Back") };
			return new[] { (Ui.PadButton.A, "Select"), (Ui.PadButton.B, "Back") };
		}

		/// <summary>The Frame rate row's value: what the setting does on this display (FramePacer's plan for it).</summary>
		private static string FpsValue(string fps)
		{
			PresentPlan p = FramePacer.PlanFor(fps);
			string hz = p.RefreshHz > 0 ? Math.Round(p.RefreshHz).ToString("0") + " Hz" : null;
			if (fps == "30") return "30 - as the game draws";
			if (fps == "60")
			{
				// Any rate but sixty said so: the nearest even rate above it on a display sixty does not
				// divide or a swap interval cannot hold, the clock's even rate when VSync gave way.
				if (hz != null && Math.Abs(p.Rate - 60) > 60 * PresentPlan.Tolerance) return "60 - smoothed, " + p.Rate.ToString("0.#") + " at " + hz;
				return "60 - smoothed";
			}
			// VSync off, "max" is the clock's rate, not the display's.
			if (p.Swap == 0) return "Display's rate - " + p.Rate.ToString("0") + " a second";
			return hz == null ? "Display's rate - smoothed" : "Display's rate - " + hz;
		}

		/// <summary>The note under the settings when the Frame rate or VSync changes: what the plan does, and why when it is not what the setting asks.</summary>
		private static string FpsNote(string fps)
		{
			PresentPlan p = FramePacer.PlanFor(fps);
			string hz = p.RefreshHz > 0 ? Math.Round(p.RefreshHz).ToString("0") + " Hz" : null;
			string rate = p.Rate.ToString("0.#");
			const string Runs = "; game logic unchanged";
			if (fps == "30")
			{
				if (p.Swap == 0) return "the game's frames as they are, thirty a second by the clock (VSync off)";
				if (p.Held || hz == null) return "the game's frames as they are, thirty a second";
				// Paced by the clock on a known refresh: one slower than thirty, one thirty does not divide,
				// or a whole multiple no swap interval here holds.
				double per = p.RefreshHz / 30;
				if (per < 1 - PresentPlan.Tolerance) return "the game's frames as they are - " + hz + " is slower than thirty, so some are never shown";
				int wants = PresentPlan.RefreshesFor(p.RefreshHz, 30);
				if (Math.Abs(per / wants - 1) > PresentPlan.Tolerance)
					return "the game's frames as they are - " + hz + " is no multiple of thirty, so each is held " + Math.Floor(per).ToString("0") + " or " + Math.Ceiling(per).ToString("0") + " refreshes";
				return "the game's frames, thirty a second by the clock - " + NotHeld(wants, "thirty");
			}
			if (fps == "60")
			{
				if (p.Swap == 0) return "sixty a second by the clock (VSync off), a frame drawn between each two of the game's - motion smoothed";
				if (hz == null || Math.Abs(p.Rate - 60) <= 60 * PresentPlan.Tolerance && p.Held) return "a frame drawn between each two of the game's - motion and the camera smoothed" + Runs;
				if (!p.Held) return "VSync is not holding the loop: " + rate + " a second by the clock, each drawn between two of the game's";
				int wants = PresentPlan.RefreshesFor(p.RefreshHz, 60);
				string every = p.Swap == 1 ? "every refresh" : p.Swap == 2 ? "every second refresh" : p.Swap == 3 ? "every third refresh" : "every " + p.Swap + "th refresh";
				string why = p.Swap < wants ? " - " + NotHeld(wants, "sixty") : ", so each is held evenly";
				if (Math.Abs(p.RefreshHz / wants - 60) > 60 * PresentPlan.Tolerance) return hz + " is no multiple of sixty: a frame " + every + ", " + rate + " a second" + why;
				return "at " + hz + " a frame " + every + ", " + rate + " a second" + why;
			}
			if (p.Swap == 0) return "a frame drawn " + p.Rate.ToString("0") + " times a second by the clock (VSync off), smoothed" + Runs;
			if (hz == null) return "a frame drawn at the display's rate, " + rate + " a second at most, smoothed" + Runs;
			if (!p.Held) return "VSync is not holding the loop: " + rate + " a second by the clock, smoothed" + Runs;
			return "a frame drawn every refresh at " + hz + ", smoothed" + Runs;
		}

		// Why a frame is held fewer refreshes than its rate wants (wants): no swap interval that long can
		// be set here, or the one set did not hold, and the plan gave way.
		private static string NotHeld(int wants, string rate)
		{
			int most = FramePacer.MostSwap;
			if (wants <= 1) return "VSync is not holding the loop";
			if (most <= 1) return "a swap interval cannot be set here";
			if (wants > most) return "a swap interval holds " + most + " refreshes at most, and " + rate + " wants " + wants;
			return "a swap interval of " + wants + " is not holding";
		}

		private void RowText(int row, out string label, out string value)
		{
			DisplaySettings s = DisplaySettings.Current;
			value = null;
			switch (_page)
			{
				case Page.Main:
				{
					string[] rows = MainRows();
					string id = rows[Math.Clamp(row, 0, rows.Length - 1)];
					if (id == "abilities") { label = "Abilities"; value = "job ladders, free slots"; return; }
					if (id == "skip") { label = "Skip scene"; value = "runs through to its end"; return; }
					label = id == "resume" ? "Resume" : id == "settings" ? "Settings" : id == "qol" ? "Quality of life" : id == "map" ? "Minimap" : id == "reset" ? "Reset" : "Exit game";
					if (id == "qol") value = QolSummary();
					else if (id == "map") value = MapSummary();
					else if (id == "reset") value = "back to the title";
					return;
				}
				case Page.Qol:
				{
					DisplaySettings.QolSettings q = Qol.S;
					switch (row)
					{
						case 0: label = "Speed"; value = Qol.Times(Qol.Speed); return;
						case 1: label = "Random encounters"; value = q.Encounters ? "On" : "Off"; return;
						case 2: label = "Encounter rate"; value = Qol.Times(Qol.EncounterRate); return;
						case 3: label = "Battle EXP"; value = Qol.Times(q.Exp); return;
						case 4: label = "Job EXP"; value = Qol.Times(q.JobExp); return;
						case 5: label = "Save anywhere"; value = q.SaveAnywhere ? "On" : "Off"; return;
						case 6: label = "Job change adjustment"; value = q.JobAdjustment ? "On" : "Off"; return;
						case 7: label = "Turn-based battles"; value = q.TurnBased ? "On" : "Off"; return;
						case 8: label = "Indicator"; value = !q.Indicator ? "Off" : q.IndicatorSimple ? "Speed and encounters" : "All"; return;
						default: label = "Back"; return;
					}
				}
				case Page.Map:
				{
					DisplaySettings.QolSettings q = Qol.S;
					switch (row)
					{
						case 0: label = "Minimap"; value = q.Minimap ? "On" : "Off"; return;
						case 1: label = "Type"; value = q.MinimapFog ? "Fog of war" : "Complete"; return;
						case 2: label = "Size"; value = MapSizes[Math.Clamp(q.MinimapSize, 0, 2)]; return;
						case 3: label = "Zoom"; value = MapZooms[Math.Clamp(q.MinimapZoom, 0, MapZooms.Length - 1)]; return;
						case 4: label = "Area uncovered"; value = MapAreas[Math.Clamp(q.MinimapSee, 0, MapAreas.Length - 1)]; return;
						case 5: label = "Style"; value = q.MinimapOpaque ? "Opaque" : "Transparent"; return;
						case 6: label = "Chests"; value = q.MinimapChests ? "On" : "Off"; return;
						case 7: label = "Hidden passages"; value = q.MinimapPassages ? "Marked" : "Not marked"; return;
						case 8: label = "Forget explored maps"; return;
						default: label = "Back"; return;
					}
				}
				case Page.Settings:
					switch (row)
					{
						case 0: label = "Window"; value = s.Mode == "windowed" ? "Windowed" : s.Mode == "borderless" ? "Borderless full screen" : "Full screen"; return;
						case 1: label = "Window size"; value = s.Width + " x " + s.Height; return;
						case 2: label = "Anti-aliasing"; value = s.Msaa == 0 ? "Off" : s.Msaa + "x"; return;
						case 3: label = "VSync"; value = s.VSync ? "On" : "Off"; return;
						case 4: label = "Frame rate"; value = FpsValue(s.Fps); return;
						case 5: label = "Run"; value = s.Run == "stick" ? "By the stick's push" : "Hold the run button"; return;
						case 6: label = "Updates"; value = Updates.Mode == "auto" ? "Automatic" : Updates.Mode == "off" ? "Off" : "Ask"; return;
						case 7: label = "Check for updates"; return;
						case 8: label = "Check game files"; return;
						case 9: label = "Button icons"; value = Ui.PadStyleName(s.PadStyle); return;
						case 10: label = "Pad buttons..."; return;
						default: label = "Back"; return;
					}
				case Page.Buttons:
					if (row < DsButtons.Length) { label = DsButtons[row].Label; value = _binding == DsButtons[row].Key ? "press a button..." : Bound(DsButtons[row].Key); return; }
					label = row == DsButtons.Length ? "Reset to the defaults" : "Back";
					return;
				default:
					label = row == 0 ? (_leave == "forget" ? "Yes, delete the maps" : _leave == "exit" ? "Yes, exit" : "Yes, to the title") : (_leave == "forget" ? "No, keep them" : "No, keep playing");
					return;
			}
		}

		private void Outline(Rectangle r, Color colour, int thickness)
		{
			_batch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), colour);
			_batch.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), colour);
			_batch.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), colour);
			_batch.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), colour);
		}

		private void EnsureResources()
		{
			if (_batch == null) _batch = new SpriteBatch(GraphicsDevice);
			if (_pixel == null || _pixel.IsDisposed)
			{
				_pixel = new Texture2D(GraphicsDevice, 1, 1);
				_pixel.SetData(new[] { Color.White });
			}
		}
	}
}
