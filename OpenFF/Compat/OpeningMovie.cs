// The opening movie, from the title: a "Play opening FMV" entry above Mods plays the install's opening.mkv (the file
// FF4's title plays on its own; Steam's FF3 install has it too) with the movie player, over the title, and goes back to
// the title when it ends or is skipped. New Game asks first - "Would you like to view the opening FMV (recommended)?" -
// and starts the new game once the movie is over or the answer is No.
//
// It is the engine's own service (drawing into Game.Draw each step like the FF4 title does), so the title's labels
// (ModListScreen) stand down while the question or the movie is up, the game's input is held off (DesktopInput.IsTyping),
// and the title's music is paused for the length of the movie. Offered only when the file is in the install.

using System;
using Microsoft.Xna.Framework.Input;

namespace OpenFF.Client
{
	internal sealed class OpeningMovie : OpenFF.GameService
	{
		private static MoviePlayer _movie;
		private static KeyboardState _keysBefore;
		private static bool _mouseBefore;
		private static int _padBefore;
		private static long _startedAt;

		// The New Game question.
		private static bool _asking, _yes = true, _escBefore, _clickBefore;
		// The movie was chosen from New Game's question: when it ends, the new game goes on.
		private static bool _newGameAfter;
		// What the title takes on its next step: the new game the question or the movie let go on.
		private static bool _newGame;

		private static Pointer _pointer;
		private static bool _looked;
		private static string _file;

		private const int PadA = 1, PadB = 2, PadRight = 16, PadLeft = 32, PadUp = 64, PadDown = 128;

		/// <summary>The question or the movie is up: the title's labels and the game's input stand down.</summary>
		public static bool Busy => _asking || _movie != null;

		/// <summary>The title's labels and the game's input stand down: Busy, and the black held after a movie that leads into a new game.</summary>
		public static bool Hiding => Busy || _cover;

		// After a movie that leads into the new game the screen stays black until the title has left, so it never shows between.
		private static bool _cover;
		private static long _coverAt;

		/// <summary>Whether the movie is on the screen.</summary>
		public static bool Playing => _movie != null;

		public override bool WantsUpdate => true;

		/// <summary>The install's movie, or null when it is not there (looked for once).</summary>
		public static string MovieFile
		{
			get
			{
				if (!_looked)
				{
					_looked = true;
					_file = Ff4Title.SteamFile("opening.mkv");
					if (_file == null) Log.Write(LogChannel.General, "opening movie: opening.mkv is not in the install - no entry, no question on New Game");
				}
				return _file;
			}
		}

		public static bool Exists => MovieFile != null;

		/// <summary>New Game was chosen: asks whether to watch the opening first. False when there is no movie (the new game starts as ever).</summary>
		public static bool Ask()
		{
			if (GameProfile.IsFf4 || !Exists || _newGame || Busy) return false;
			if (_pointer == null)
			{
				try
				{
					Microsoft.Xna.Framework.Game game = GlobalScope.m_Graphics.getGame();
					_pointer = new Pointer(game);
					game.Components.Add(_pointer);
				}
				catch (Exception ex) { Log.Write(LogChannel.General, "opening movie: the hand: " + ex.Message); }
			}
			_asking = true;
			_yes = true;
			_keysBefore = Keyboard.GetState();
			_escBefore = _keysBefore.IsKeyDown(Keys.Escape);
			_clickBefore = Mouse.GetState().LeftButton == ButtonState.Pressed;
			_padBefore = DesktopInput.RawPadBits(defaultPad: true);
			return true;
		}

		/// <summary>The title asks: did the question or the movie settle on a new game? Once true, then cleared.</summary>
		public static bool TakeNewGame()
		{
			if (!_newGame) return false;
			_newGame = false;
			return true;
		}

		/// <summary>Starts the movie over the title (the entry's press, or Yes to the question).</summary>
		public static void Open(bool thenNewGame)
		{
			if (_movie != null) return;
			string path = MovieFile;
			if (path == null) { _newGame = thenNewGame; return; }
			try { GlobalScope.MatrixSound.MtxSoundBGM.getSingleton().pause(true, GlobalScope.MatrixSound.enMtxBGMSlot.enMTX_BGM_SLOT0); } catch (Exception) { }
			_movie = new MoviePlayer(path);
			_movie.Start();
			_newGameAfter = thenNewGame;
			// What is held now (the press that chose the entry) does not skip it.
			_keysBefore = Keyboard.GetState();
			_mouseBefore = Mouse.GetState().LeftButton == ButtonState.Pressed;
			_padBefore = DesktopInput.RawPadBits();
			_startedAt = Environment.TickCount64;
		}

		private static void Close(string why)
		{
			if (_movie == null) return;
			Log.Write(LogChannel.General, "opening movie: " + why);
			try { _movie.Dispose(); } catch (Exception ex) { Log.Write(LogChannel.General, "opening movie: " + ex.Message); }
			_movie = null;
			if (_newGameAfter)
			{
				_newGame = true;
				_newGameAfter = false;
				// Straight on to the game: black from this step, the title's music let go when the cover is.
				_cover = true;
				_coverAt = Environment.TickCount64;
				DrawCover();
			}
			else EndCover();
		}

		private static void DrawCover() => OpenFF.Game.Draw.Rect(0, 0, DrawList.ScreenWidth, DrawList.ScreenHeight, Color.Black);

		private static void EndCover()
		{
			_cover = false;
			try { GlobalScope.MatrixSound.MtxSoundBGM.getSingleton().pause(false, GlobalScope.MatrixSound.enMtxBGMSlot.enMTX_BGM_SLOT0); } catch (Exception) { }
		}

		/// <summary>A key, a click or a pad button newly pressed.</summary>
		private static bool SkipPressed()
		{
			KeyboardState keys = Keyboard.GetState();
			bool key = false;
			foreach (Keys k in keys.GetPressedKeys())
			{
				if (!_keysBefore.IsKeyDown(k)) key = true;
			}
			_keysBefore = keys;
			bool mouse = Mouse.GetState().LeftButton == ButtonState.Pressed;
			bool click = mouse && !_mouseBefore;
			_mouseBefore = mouse;
			int pad = DesktopInput.RawPadBits();
			bool button = (pad & ~_padBefore) != 0;
			_padBefore = pad;
			return key || click || button;
		}

		public override void OnUpdate()
		{
			if (_asking) { Question(); return; }
			if (_movie == null)
			{
				if (!_cover) return;
				// Until the title leaves (it takes the new game on its next step), or five seconds as a failsafe.
				if (!TitleEntries.Showing || Environment.TickCount64 - _coverAt > 5000) EndCover();
				else DrawCover();
				return;
			}
			try
			{
				_movie.Update();
				if (_movie.Done) { Close("done"); return; }
				// A moment's grace so the press that opened it cannot end it.
				if (SkipPressed() && Environment.TickCount64 - _startedAt > 300) { Close("skipped"); return; }
				_movie.Draw(OpenFF.Game.Draw);
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "opening movie: " + ex.GetType().Name + ": " + ex.Message);
				Close("failed");
			}
		}

		// ---- the New Game question ----

		private const string Words = "Would you like to view the opening FMV (recommended)?";
		private const int Size = 9;
		private const float BoxH = 116f;

		private static float BoxW(DrawList d) => Math.Max(300f, d.MeasureText(Words, Size) + 60f);
		private static float BoxX(DrawList d) => (DrawList.ScreenWidth - BoxW(d)) / 2f;
		private static float BoxY => (DrawList.ScreenHeight - BoxH) / 2f;
		private static float YesX(DrawList d) => DrawList.ScreenWidth / 2f - 80f;
		private static float NoX(DrawList d) => DrawList.ScreenWidth / 2f + 30f;
		private static float AnswerY => BoxY + 74f;

		private static void Question()
		{
			DrawList d = OpenFF.Game.Draw;
			bool active = false;
			float mx = 0, my = 0;
			try
			{
				Microsoft.Xna.Framework.Game game = GlobalScope.m_Graphics.getGame();
				active = game.IsActive;
				Microsoft.Xna.Framework.Input.MouseState m = Mouse.GetState();
				Microsoft.Xna.Framework.Rectangle client = game.Window.ClientBounds;
				if (client.Width > 0 && client.Height > 0)
				{
					mx = m.X * DrawList.ScreenWidth / client.Width;
					my = m.Y * DrawList.ScreenHeight / client.Height;
					active = active && m.X >= 0 && m.Y >= 0 && m.X < client.Width && m.Y < client.Height;
				}
			}
			catch (Exception) { }
			int pad = DesktopInput.RawPadBits(defaultPad: true);
			int edge = pad & ~_padBefore;
			_padBefore = pad;
			bool esc = Keyboard.GetState().IsKeyDown(Keys.Escape) || DesktopInput.Injected.Contains(Keys.Escape);
			bool escEdge = esc && !_escBefore;
			_escBefore = esc;
			bool click = active && Mouse.GetState().LeftButton == ButtonState.Pressed;
			bool clickEdge = click && !_clickBefore;
			_clickBefore = click;

			bool overYes = Over(mx, my, YesX(d)), overNo = Over(mx, my, NoX(d));
			if ((edge & (PadLeft | PadRight | PadUp | PadDown)) != 0) { _yes = !_yes; Se(3); }
			else if (click && (overYes || overNo) && overYes != _yes) _yes = overYes;
			if ((edge & PadA) != 0 || (clickEdge && (overYes || overNo)))
			{
				_asking = false;
				Se(1);
				if (_yes) Open(true);
				else _newGame = true;
				return;
			}
			if ((edge & PadB) != 0 || escEdge)
			{
				// Backing out: back to the title, no new game.
				_asking = false;
				Se(2);
				return;
			}
			Draw(d);
		}

		private static bool Over(float x, float y, float answerX) => x >= answerX - 34f && x <= answerX + 60f && y >= AnswerY - 6f && y <= AnswerY + 30f;

		private static void Draw(DrawList d)
		{
			float w = BoxW(d), x = BoxX(d), y = BoxY;
			d.Rect(0, 0, DrawList.ScreenWidth, DrawList.ScreenHeight, new Color(0, 0, 0, 120));
			d.Rect(x - 2, y - 2, w + 4, BoxH + 4, new Color(235, 235, 245, 255));
			d.Rect(x, y, w, BoxH, new Color(20, 34, 74, 245));
			Shadowed(d, Words, x + (w - d.MeasureText(Words, Size)) / 2f, y + 24f);
			Shadowed(d, "Yes", YesX(d), AnswerY);
			Shadowed(d, "No", NoX(d), AnswerY);
			// The hand is the game's, drawn over the box by the Pointer component below.
		}

		private static void Shadowed(DrawList d, string text, float x, float y)
		{
			d.Text(text, x + 1.5f, y + 1.5f, new Color(0, 0, 0, 255), Size);
			d.Text(text, x, y, Color.White, Size);
		}

		/// <summary>The party menu's hand beside the answer the question is on, over the box (the game's own cursor sprite lies under what the mods draw).</summary>
		private sealed class Pointer : Microsoft.Xna.Framework.DrawableGameComponent
		{
			private Microsoft.Xna.Framework.Graphics.SpriteBatch _batch;

			public Pointer(Microsoft.Xna.Framework.Game game) : base(game) { DrawOrder = int.MaxValue - 4; }

			public override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
			{
				if (!_asking) return;
				try
				{
					_batch ??= new Microsoft.Xna.Framework.Graphics.SpriteBatch(GraphicsDevice);
					UiTheme.Ensure(GraphicsDevice);
					OpenFF.DrawList d = OpenFF.Game.Draw;
					float x = _yes ? YesX(d) : NoX(d);
					Microsoft.Xna.Framework.Rectangle r = new Microsoft.Xna.Framework.Rectangle((int)x, (int)AnswerY + 4, 60, 14);
					_batch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied, Microsoft.Xna.Framework.Graphics.SamplerState.LinearWrap);
					UiTheme.Hand(_batch, r, x - 4f, GraphicsDevice.Viewport);
					_batch.End();
				}
				catch (Exception ex) { Log.First(LogChannel.General, "opening-hand", 3, () => "opening movie: the hand: " + ex.Message); }
			}
		}

		private static void Se(int number)
		{
			try { GlobalScope.MatrixSound.MtxSENDS_Play(0, number, 127, 128); } catch (Exception) { }
		}
	}
}
