// The HD textures' offer over the title (HdTextures): a panel of the menus' look that says the textures are not installed, are
// highly recommended, and how big the download is - Download now / Not now / Don't ask again, by keyboard, mouse or pad - then
// the download's progress bar (size, rate, time left), the check and the unpacking, a failure said plainly with OK. When the
// pack is in place the client starts itself again, on the title, so the mod is taken in (Restart).
//
// Only before a game is started or loaded, and after the update's offer is answered, so nothing unsaved is lost to the restart;
// the game holds still under it (GameHost), as under the update's panel.

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

	internal sealed class TexturesScreen : DrawableGameComponent
	{
		public static TexturesScreen Instance { get; private set; }
		public static bool IsOpen => Instance != null && Instance._open;

		private bool _open;
		private int _selected;
		private SpriteBatch _batch;
		private int _previousPad;
		private readonly HashSet<Keys> _wasDown = new HashSet<Keys>();
		private bool _mouseWasDown;
		private bool _restartTried, _restarted;
		private static readonly Keys[] Watched = { Keys.Up, Keys.Down, Keys.W, Keys.S, Keys.Enter, Keys.Space, Keys.Z, Keys.X, Keys.Back, Keys.Escape };

		private static readonly Rectangle PanelRect = new Rectangle(160, 70, 480, 340);

		private TexturesScreen(Game game) : base(game)
		{
			DrawOrder = int.MaxValue - 3;     // over the title and the Esc menu, under the update's panel
			UpdateOrder = int.MaxValue - 6;
		}

		public static void Attach(Game game)
		{
			Instance = new TexturesScreen(game);
			game.Components.Add(Instance);
		}

		/// <summary>The panel is up before any game: the game holds still under it.</summary>
		public static bool HoldsGame => IsOpen;

		private static bool Busy(HdTextures.Stage s) => s == HdTextures.Stage.Downloading || s == HdTextures.Stage.Verifying || s == HdTextures.Stage.Unpacking;

		private string[] Rows()
		{
			switch (HdTextures.Now)
			{
				case HdTextures.Stage.Offer: return new[] { "Download now", "Not now", "Don't ask again" };
				case HdTextures.Stage.Downloading: return new[] { "Cancel" };
				case HdTextures.Stage.Failed: return new[] { "OK" };
				case HdTextures.Stage.Done: return _restartTried && !_restarted ? new[] { "OK" } : Array.Empty<string>();
				default: return Array.Empty<string>();
			}
		}

		private static Rectangle RowRect(int row, int count)
		{
			const int h = 28, step = 34;
			int top = PanelRect.Bottom - 46 - count * step;
			return new Rectangle(PanelRect.X + 60, top + row * step, PanelRect.Width - 120, h);
		}

		public override void Update(GameTime gameTime)
		{
			HdTextures.Stage stage = HdTextures.Now;
			if (!_open)
			{
				// Offered once the game has not started and the update's panel (and the file check's) is not asking.
				if (stage != HdTextures.Stage.Offer || !UpdateScreen.AtStart || GameFilesScreen.IsOpen) return;
				if (Updates.Now == Updates.Stage.Checking || Updates.Now == Updates.Stage.Available || UpdateScreen.IsOpen) return;
				_open = true;
				_selected = 0;
				_restartTried = _restarted = false;
				_wasDown.Clear();
				KeyboardState now = Keyboard.GetState();
				foreach (Keys k in Watched) if (Down(now, k)) _wasDown.Add(k);
				_previousPad = DesktopInput.RawPadBits();
				Log.Write(LogChannel.General, "textures: offer shown");
				return;
			}
			if (stage == HdTextures.Stage.Idle || stage == HdTextures.Stage.Checking) { _open = false; return; }

			if (stage == HdTextures.Stage.Done && !_restartTried)
			{
				// The pack is in place: the client starts itself again so the mod is taken in.
				_restartTried = true;
				DisplaySettings.Current.Save();
				if (Restart.ToTitle("HD textures installed")) { _restarted = true; Game.Exit(); return; }
			}
			if (!Game.IsActive && !Drive.Active) return;

			KeyboardState keys = Keyboard.GetState();
			int pad = DesktopInput.RawPadBits(), edge = pad & ~_previousPad;
			_previousPad = pad;
			bool up = Pressed(keys, Keys.Up) || Pressed(keys, Keys.W) || (edge & 64) != 0;
			bool down = Pressed(keys, Keys.Down) || Pressed(keys, Keys.S) || (edge & 128) != 0;
			bool confirm = Pressed(keys, Keys.Enter) || Pressed(keys, Keys.Space) || Pressed(keys, Keys.Z) || (edge & 1) != 0;
			bool cancel = Pressed(keys, Keys.Escape) || Pressed(keys, Keys.X) || Pressed(keys, Keys.Back) || (edge & 2) != 0;
			_wasDown.Clear();
			foreach (Keys k in Watched) if (Down(keys, k)) _wasDown.Add(k);

			string[] rows = Rows();
			if (rows.Length > 0)
			{
				if (up) _selected = (_selected + rows.Length - 1) % rows.Length;
				if (down) _selected = (_selected + 1) % rows.Length;
				_selected = Math.Clamp(_selected, 0, rows.Length - 1);
			}
			bool mouse = DesktopInput.MouseInView(out int mx, out int my);
			if (mouse && !_mouseWasDown)
			{
				for (int i = 0; i < rows.Length; i++) if (RowRect(i, rows.Length).Contains(mx, my)) { _selected = i; confirm = true; }
			}
			_mouseWasDown = mouse;

			switch (stage)
			{
				case HdTextures.Stage.Offer:
					if (cancel) HdTextures.Later();
					else if (confirm)
					{
						if (_selected == 0) HdTextures.Install();
						else if (_selected == 1) HdTextures.Later();
						else HdTextures.Decline();
						_selected = 0;
					}
					break;
				case HdTextures.Stage.Downloading:
					if (cancel || confirm) HdTextures.Cancel();
					break;
				case HdTextures.Stage.Failed:
					if (cancel || confirm) HdTextures.Dismiss();
					break;
				case HdTextures.Stage.Done:
					if (_restartTried && !_restarted && (cancel || confirm)) HdTextures.Dismiss();
					break;
			}
		}

		private static bool Down(KeyboardState keys, Keys key) => keys.IsKeyDown(key) || DesktopInput.Injected.Contains(key);
		private bool Pressed(KeyboardState keys, Keys key) => Down(keys, key) && !_wasDown.Contains(key);

		public override void Draw(GameTime gameTime)
		{
			if (RenderTest.Active || !_open) return;
			GlobalScope.Graphics graphics = GlobalScope.m_Graphics;
			if (graphics == null) return;
			if (_batch == null) _batch = new SpriteBatch(GraphicsDevice);
			UiTheme.Ensure(GraphicsDevice);
			Viewport view = GraphicsDevice.Viewport;

			HdTextures.Stage stage = HdTextures.Now;
			Rectangle panel = PanelRect;
			string[] rows = Rows();
			HdTextures.Pack pack = HdTextures.Found;
			string title = stage == HdTextures.Stage.Offer ? "HD textures" : stage == HdTextures.Stage.Failed ? "The download did not go in" : stage == HdTextures.Stage.Done ? "HD textures installed" : "Getting the HD textures";
			string sub = stage == HdTextures.Stage.Offer ? "Sharper textures and portraits for Final Fantasy III" : "";
			Rectangle bar = new Rectangle(panel.X + 50, panel.Y + 150, panel.Width - 100, 22);
			bool progress = Busy(stage);
			float fraction = HdTextures.Total > 0 ? (float)HdTextures.Got / HdTextures.Total : 0f;

			_batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
			UiTheme.Panel(_batch, panel, view);
			UiTheme.Divider(_batch, panel.Center.X, panel.Y + 78, panel.Width - 60, view);
			if (progress) UiTheme.Progress(_batch, bar, stage == HdTextures.Stage.Verifying ? 1f : fraction, view);
			for (int i = 0; i < rows.Length; i++) { Rectangle rr = RowRect(i, rows.Length); UiTheme.Row(_batch, rr, i == _selected, view); if (i == _selected) UiTheme.Hand(_batch, rr, rr.X + 44, view); }
			_batch.End();

			graphics.SetImageOrigin(0f, 0f);
			graphics.SetImageRotation(0f);
			graphics.SetImageScale(1f, 1f);
			graphics.DrawStringStart();
			TrueTypeText.TitleFace = true;
			try
			{
				UiTheme.Left(graphics, title, panel.X + 30, panel.Y + 14, 34, 15, UiTheme.Ink);
				if (sub.Length > 0) UiTheme.Left(graphics, sub, panel.X + 32, panel.Y + 46, 20, 8, UiTheme.Sub);
				float y = panel.Y + 92;
				if (stage == HdTextures.Stage.Offer)
				{
					string size = pack != null && pack.Size > 0 ? "about " + Size(pack.Size) : "large";
					List<string> lines = new List<string>
					{
						"HD textures are not installed. These are highly recommended.",
						"",
						"Would you like OpenFF3 Enhanced to download them now? Total download size is " + size + ".",
						"",
						"The game restarts when they are in. You can also add them later with --get-textures."
					};
					foreach (string line in Wrap(graphics, lines, 8, panel.Width - 100)) { UiTheme.Left(graphics, line, panel.X + 50, y, 18, 8, UiTheme.Ink); y += 17; }
				}
				else if (stage == HdTextures.Stage.Failed)
				{
					foreach (string line in Wrap(graphics, new List<string> { HdTextures.Error ?? "", "", "Nothing was installed. It is offered again at the next start." }, 8, panel.Width - 100)) { UiTheme.Left(graphics, line, panel.X + 50, y, 18, 8, UiTheme.Ink); y += 17; }
				}
				else if (stage == HdTextures.Stage.Done)
				{
					string said = _restartTried && !_restarted ? "Close the game and start it again to use them." : "The game restarts in a moment...";
					foreach (string line in Wrap(graphics, new List<string> { said }, 8, panel.Width - 100)) { UiTheme.Left(graphics, line, panel.X + 50, y, 18, 8, UiTheme.Ink); y += 17; }
				}
				else
				{
					string what = stage == HdTextures.Stage.Downloading ? "Downloading..." : stage == HdTextures.Stage.Verifying ? "Checking the download..." : "Unpacking...";
					UiTheme.Left(graphics, what, panel.X + 50, panel.Y + 110, 26, 10, UiTheme.Ink);
					string detail = "";
					if (stage == HdTextures.Stage.Downloading)
					{
						string percent = (int)Math.Round(fraction * 100) + "%";
						UiTheme.Left(graphics, percent, bar.Right - Ui.Width(graphics, percent, 12), panel.Y + 108, 30, 12, UiTheme.GoldBright);
						detail = Size(HdTextures.Got) + " / " + Size(HdTextures.Total);
						if (HdTextures.PartCount > 1) detail += "     part " + HdTextures.PartNow + " of " + HdTextures.PartCount;
						if (HdTextures.Rate > 0) detail += "     " + Size((long)HdTextures.Rate) + "/s     " + Left() + " left";
					}
					else if (stage == HdTextures.Stage.Unpacking)
					{
						string percent = (int)Math.Round(fraction * 100) + "%";
						UiTheme.Left(graphics, percent, bar.Right - Ui.Width(graphics, percent, 12), panel.Y + 108, 30, 12, UiTheme.GoldBright);
						detail = HdTextures.Got + " of " + HdTextures.Total + " files";
					}
					if (detail.Length > 0) UiTheme.Left(graphics, detail, bar.X, bar.Bottom + 8, 20, 8, UiTheme.Sub);
				}
			}
			finally { TrueTypeText.TitleFace = false; }
			TrueTypeText.TitleFace = true;
			try
			{
				for (int i = 0; i < rows.Length; i++) UiTheme.Centred(graphics, rows[i], RowRect(i, rows.Length), 11, i == _selected ? Color.White : UiTheme.Ink);
			}
			finally { TrueTypeText.TitleFace = false; }
			graphics.DrawStringEnd();
		}

		/// <summary>A size in the unit that reads best: MB, or GB from a gigabyte up.</summary>
		private static string Size(long bytes)
		{
			System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
			return bytes >= 1073741824L ? (bytes / 1073741824.0).ToString("0.0", c) + " GB" : (bytes / 1048576.0).ToString("0", c) + " MB";
		}

		private static string Left()
		{
			if (HdTextures.Rate <= 0 || HdTextures.Total <= 0) return "";
			double s = (HdTextures.Total - HdTextures.Got) / HdTextures.Rate;
			return s >= 90 ? Math.Ceiling(s / 60) + " min" : Math.Max(1, (int)Math.Ceiling(s)) + " s";
		}

		/// <summary>Lines broken at the words that pass a width.</summary>
		private static List<string> Wrap(GlobalScope.Graphics g, List<string> lines, int size, float width)
		{
			List<string> result = new List<string>();
			foreach (string line in lines)
			{
				if (line.Length == 0) { result.Add(""); continue; }
				string current = "";
				foreach (string word in line.Split(' '))
				{
					string attempt = current.Length == 0 ? word : current + " " + word;
					if (current.Length > 0 && Ui.Width(g, attempt, size) > width) { result.Add(current); current = word; }
					else current = attempt;
				}
				if (current.Length > 0) result.Add(current);
			}
			return result;
		}
	}
}
