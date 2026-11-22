// The lines between the rows of the battle's command list, as the party menu's list has them: a dark line with a
// light one under it. Drawn here, over the frame, rather than as polygons in the game's own passes (those land under
// the panel's fill and all but vanish). The command list says where its row boundaries are each step; the lines
// are drawn while those are fresh.

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenFF.Client
{
	using Game = Microsoft.Xna.Framework.Game;
	using GameTime = Microsoft.Xna.Framework.GameTime;
	using Color = Microsoft.Xna.Framework.Color;

	internal sealed class ListLines : DrawableGameComponent
	{
		private static ListLines _instance;

		private readonly List<(int X0, int X1, int Y)> _lines = new List<(int, int, int)>();
		private long _step = long.MinValue;
		private System.Func<bool> _visible;
		private SpriteBatch _batch;
		private bool _drawing;

		// How far in from each end of a row the lines stop, in text space: the grey border covers about a unit and a
		// bit of the row at each side (measured on screen: the lines ran 6 pixels onto it), and they must not touch it.
		private const float Inset = 1.75f;

		private static readonly Color Dark = new Color(0, 0, 4, 175);
		private static readonly Color Light = new Color(190, 215, 255, 50);

		private ListLines(Game game) : base(game)
		{
			DrawOrder = int.MaxValue - 8;
		}

		public static void Attach(Game game)
		{
			_instance = new ListLines(game);
			game.Components.Add(_instance);
		}

		/// <summary>The command list's row boundaries (the 480 x 320 text space), set again every step it is up.</summary>
		public static void Set(IList<(int X0, int X1, int Y)> lines, System.Func<bool> visible = null)
		{
			if (_instance == null) return;
			_instance._lines.Clear();
			_instance._lines.AddRange(lines);
			_instance._visible = visible;
			_instance._step = GameHost.StepsTaken;
		}

		/// <summary>The command list has gone: the lines go with it on the very next frame.</summary>
		public static void Clear()
		{
			if (_instance == null) return;
			_instance._lines.Clear();
			_instance._visible = null;
		}

		public override void Draw(GameTime gameTime)
		{
			if (_lines.Count == 0 || RenderTest.Active || (_visible == null && GameHost.StepsTaken - _step > 2)) { _drawing = false; return; }   // with a visibility test the owner clears the lines when the list goes; without, they must be fresh
			if (_visible != null && !_visible()) { _drawing = false; return; }   // the panel under them is not up
			if (!_drawing) { _drawing = true; Log.Write(LogChannel.File, "lines: first drawn at step " + GameHost.StepsTaken + " (set at step " + _step + ")"); }
			Ui.Ensure(GraphicsDevice);
			_batch ??= new SpriteBatch(GraphicsDevice);
			Viewport view = GraphicsDevice.Viewport;
			float sx = view.Width / 480f, sy = view.Height / 320f;
			int thick = System.Math.Max(2, (int)System.Math.Round(view.Height / 480f));
			_batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
			foreach ((int x0, int x1, int y) in _lines)
			{
				int px = (int)System.Math.Round((x0 + Inset) * sx), pw = (int)System.Math.Round((x1 - x0 - 2f * Inset) * sx), py = (int)System.Math.Round(y * sy);
				Ui.Fill(_batch, new Rectangle(px, py - thick, pw, thick), Dark);
				Ui.Fill(_batch, new Rectangle(px, py, pw, thick), Light);
			}
			_batch.End();
		}
	}
}
