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
		private SpriteBatch _batch;

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
		public static void Set(IList<(int X0, int X1, int Y)> lines)
		{
			if (_instance == null) return;
			_instance._lines.Clear();
			_instance._lines.AddRange(lines);
			_instance._step = GameHost.StepsTaken;
		}

		public override void Draw(GameTime gameTime)
		{
			if (_lines.Count == 0 || RenderTest.Active || GameHost.StepsTaken - _step > 2) return;
			Ui.Ensure(GraphicsDevice);
			_batch ??= new SpriteBatch(GraphicsDevice);
			Viewport view = GraphicsDevice.Viewport;
			float sx = view.Width / 480f, sy = view.Height / 320f;
			int thick = System.Math.Max(2, (int)System.Math.Round(view.Height / 480f));
			_batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
			foreach ((int x0, int x1, int y) in _lines)
			{
				int px = (int)System.Math.Round(x0 * sx), pw = (int)System.Math.Round((x1 - x0) * sx), py = (int)System.Math.Round(y * sy) - thick;
				Ui.Fill(_batch, new Rectangle(px, py, pw, thick), Dark);
				Ui.Fill(_batch, new Rectangle(px, py + thick, pw, thick), Light);
			}
			_batch.End();
		}
	}
}
