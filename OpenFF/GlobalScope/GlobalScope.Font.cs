using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using OpenFF.Platform;
using OpenFF.Resources;

internal static partial class GlobalScope
{
	public class Font
	{
		private int size;

		private int fontSize;

		private int pitch;

		public Font(int _size)
		{
			fontSize = _size;
			size = _size + 4;
			pitch = 256 / size;
		}

		~Font()
		{
		}

		public void destruct()
		{
		}

		/// <summary>
		/// PORT: a drop shadow's text: the line's own place on the 800 x 480 grid, then a fixed step down and right. The shadow was drawn
		/// from the line's place plus one game unit, each rounded on its own - a step of one grid unit or two by whether the line's row was odd
		/// or even, so a box's lines had shadows of different weight.
		/// </summary>
		public float drawShadow(string str, float x, float y, int color) => drawString(str, x, y, color, 1, 1);

		public float drawString(string str, float x, float y, int color, int draw, int shadowStep = 0)
		{
			if (draw != 0 && skipFrame != 0)
			{
				return x;
			}
			float num = 800f / (float)LCD_WIDTH;
			float num2 = 480f / (float)LCD_HEIGHT;
			float x2 = 1f;
			float y2 = 1f;
			if (draw != 0)
			{
				float num3 = (int)(x * num + 0.5f) + shadowStep;
				float num4 = (int)(y * num2 + 0.5f) + shadowStep;
				m_Graphics.SetImageOrigin(0f, 0f);
				m_Graphics.SetImageRotation((GX_GetFlipScreen() == 1) ? ((float)Math.PI) : 0f);
				m_Graphics.SetImageScale(x2, y2);
				m_Graphics.SetColor((color >> 24) & 0xFF, (color >> 16) & 0xFF, (color >> 8) & 0xFF, color & 0xFF);
				if (GX_GetFlipScreen() == 1)
				{
					m_Graphics.DrawString(str, 800f - (num3 - (float)screenOffset[0]), 480f - (num4 - (float)screenOffset[1]), fontSize);
				}
				else
				{
					m_Graphics.DrawString(str, num3 - (float)screenOffset[0], num4 - (float)screenOffset[1], fontSize);
				}
			}
			x += m_Graphics.StringWidth(str, fontSize) / num;
			return x;
		}
	}
}
