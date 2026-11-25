// FF3-NES / FF6-style staggered bouncy damage, heal, hit, critical, and miss popups.
// Replaces the flat DS sprite cells in GlobalScope.u2d.PopUpDamageNumber and PopUpHitNumber
// with crisp, outlined TrueType characters and distinct per-type animations.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenFF.Client
{
	internal sealed class DamagePopups : DrawableGameComponent
	{
		public enum AnimStyle
		{
			DamageBounce,
			HealFloat,
			CriticalScaleBounce,
			MissDodge,
			HitsSlideIn
		}

		private const float TextSpaceWidth = 800f;
		private const float TextSpaceHeight = 480f;
		private const int DamageTextSize = 20;

		// Timing (seconds) tuned for snappy FF3-NES / FF6 feel (no drift on disappear).
		private const float Stagger = 0.045f;
		private const float Hop1Duration = 0.26f;
		private const float Hop1Height = 28f;
		private const float Hop2Duration = 0.13f;
		private const float Hop2Height = 7f;
		private const float DamageHoldEnd = 0.78f;
		private const float HitTextHoldEnd = 0.56f;

		private sealed class Popup
		{
			public int Id;
			public string Text;
			public float[] Offsets480;
			public float X480;
			public float Y320;
			public byte R, G, B;
			public int TextSize;
			public AnimStyle Style;
			public float StaggerSec;
			public float Elapsed;
			public float TotalDuration;
		}

		private static readonly List<Popup> Active = new List<Popup>();
		private static int _nextId = 1;

		private DamagePopups(Microsoft.Xna.Framework.Game game) : base(game)
		{
			DrawOrder = int.MaxValue - 5;
		}

		public static void Attach(Microsoft.Xna.Framework.Game game) => game.Components.Add(new DamagePopups(game));

		/// <summary>Spawns a damage (white double-bounce) or heal (green staggered float) popup at 480x320 virtual coordinates.</summary>
		public static int Spawn(int value, float x480, float y320, bool isHeal)
		{
			int clamped = Math.Clamp(Math.Abs(value), 0, 9999);
			string text = clamped.ToString();
			return SpawnText(
				text,
				x480,
				y320,
				isHeal ? (byte)96 : (byte)255,
				isHeal ? (byte)255 : (byte)255,
				isHeal ? (byte)132 : (byte)255,
				isHeal ? AnimStyle.HealFloat : AnimStyle.DamageBounce,
				DamageTextSize,
				Stagger,
				DamageHoldEnd,
				extraTracking480: 1.2f,
				centerX: true);
		}

		/// <summary>Spawns a styled text popup (MISS, CRITICAL!, N HITS) at 480x320 virtual coordinates.</summary>
		public static int SpawnText(
			string text,
			float x480,
			float y320,
			byte r,
			byte g,
			byte b,
			AnimStyle style,
			int textSize = 16,
			float staggerSec = 0.025f,
			float holdEnd = HitTextHoldEnd,
			float extraTracking480 = 1.0f,
			bool centerX = false)
		{
			if (string.IsNullOrEmpty(text)) return 0;

			// Measure each glyph proportionally so wide letters (M, W, H) never overlap narrow ones (I, 1).
			float[] offsets = new float[text.Length];
			float cursor = 0f;
			for (int i = 0; i < text.Length; i++)
			{
				offsets[i] = cursor;
				string ch = text.Substring(i, 1);
				float w800 = ch == " " ? textSize * 0.45f : TrueTypeText.Width(ch, textSize);
				if (w800 <= 0f) w800 = textSize * 0.6f;
				cursor += (w800 / TextSpaceWidth * 480f) + extraTracking480;
			}
			float totalWidth480 = MathF.Max(0f, cursor - extraTracking480);

			int id = _nextId++;
			Active.Add(new Popup
			{
				Id = id,
				Text = text,
				Offsets480 = offsets,
				X480 = centerX ? x480 - totalWidth480 * 0.5f : x480,
				Y320 = y320,
				R = r,
				G = g,
				B = b,
				TextSize = textSize,
				Style = style,
				StaggerSec = staggerSec,
				Elapsed = 0f,
				TotalDuration = holdEnd + (text.Length - 1) * staggerSec
			});
			return id;
		}

		public static bool IsAlive(int id)
		{
			if (id <= 0) return false;
			for (int i = 0; i < Active.Count; i++)
			{
				if (Active[i].Id == id) return true;
			}
			return false;
		}

		public static void Cancel(int id)
		{
			if (id <= 0) return;
			for (int i = Active.Count - 1; i >= 0; i--)
			{
				if (Active[i].Id == id) Active.RemoveAt(i);
			}
		}

		private static bool InBattle()
		{
			try { return (GlobalScope.GAMEPART)GlobalScope.sys.FF3PartSys.getCurrentPart() == GlobalScope.GAMEPART.GAMEPART_BATTLE; }
			catch (Exception) { return false; }
		}

		public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
		{
			if (Active.Count == 0) return;
			if (!Qol.BouncyDamagePopups || GameProfile.IsFf4 || !InBattle())
			{
				Active.Clear();
				return;
			}
			if (PauseMenu.HoldsGame) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (dt <= 0f || dt > 0.1f) dt = 1f / 60f;
			for (int i = Active.Count - 1; i >= 0; i--)
			{
				Active[i].Elapsed += dt;
				if (Active[i].Elapsed >= Active[i].TotalDuration)
				{
					Active.RemoveAt(i);
				}
			}
		}

		public override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
		{
			if (Active.Count == 0 || RenderTest.Active || GameProfile.IsFf4 || !TrueTypeText.Enabled) return;
			if (!(GlobalScope.m_Graphics is GlobalScope.Graphics graphics)) return;

			graphics.SetImageOrigin(0f, 0f);
			graphics.SetImageRotation(0f);
			graphics.SetImageScale(1f, 1f);
			graphics.DrawStringStart();

			for (int p = 0; p < Active.Count; p++)
			{
				Popup pop = Active[p];

				for (int i = 0; i < pop.Text.Length; i++)
				{
					char c = pop.Text[i];
					if (c == ' ') continue;

					float t = pop.Style == AnimStyle.HitsSlideIn
						? pop.Elapsed
						: pop.Elapsed - i * pop.StaggerSec;
					if (t < 0f) continue;

					float dx = 0f;
					float dy = 0f;
					int drawSize = pop.TextSize;

					switch (pop.Style)
					{
						case AnimStyle.DamageBounce:
							if (t < Hop1Duration)
							{
								float u = t / Hop1Duration;
								dy = -Hop1Height * MathF.Sin(MathF.PI * u);
							}
							else if (t < Hop1Duration + Hop2Duration)
							{
								float u = (t - Hop1Duration) / Hop2Duration;
								dy = -Hop2Height * MathF.Sin(MathF.PI * u);
							}
							break;

						case AnimStyle.HealFloat:
						{
							float u = Math.Clamp(t / 0.45f, 0f, 1f);
							float ease = 1f - MathF.Pow(1f - u, 3f);
							dy = -16f * ease;
							break;
						}

						case AnimStyle.CriticalScaleBounce:
							if (t < 0.11f)
							{
								float u = t / 0.11f;
								float wave = MathF.Sin(MathF.PI * u);
								drawSize = (int)MathF.Round(pop.TextSize * (1f + 0.95f * wave));
							}
							break;

						case AnimStyle.MissDodge:
						{
							float u = Math.Clamp(t / 0.20f, 0f, 1f);
							float ease = 1f - MathF.Pow(1f - u, 3f);
							dx = -14f * (1f - ease);
							break;
						}

						case AnimStyle.HitsSlideIn:
							break;
					}

					float sizeDeltaY = (drawSize - pop.TextSize) * 0.35f;
					float tx = (pop.X480 + pop.Offsets480[i] + dx) / 480f * TextSpaceWidth;
					float ty = (pop.Y320 + dy - 12f) / 320f * TextSpaceHeight - sizeDeltaY;
					string ch = c.ToString();

					// Crisp dark outline + drop shadow for readability over any battle background.
					graphics.SetColor(10, 12, 22, 235);
					graphics.DrawString(ch, tx + 2f, ty + 2.5f, drawSize);
					graphics.DrawString(ch, tx - 1.5f, ty, drawSize);
					graphics.DrawString(ch, tx + 1.5f, ty, drawSize);
					graphics.DrawString(ch, tx, ty - 1.5f, drawSize);
					graphics.DrawString(ch, tx, ty + 1.5f, drawSize);

					// Main character fill.
					graphics.SetColor(pop.R, pop.G, pop.B, 255);
					graphics.DrawString(ch, tx, ty, drawSize);
				}
			}

			graphics.DrawStringEnd();
		}
	}
}
