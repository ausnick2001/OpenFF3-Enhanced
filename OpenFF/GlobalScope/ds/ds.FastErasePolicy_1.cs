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
	public static partial class ds
	{
		/// <summary>PORT: how a ds.Vector takes an element out (the game's own policies are classes with an erase method; Vector used to find it by reflection on every removal).</summary>
		public interface IErasePolicy<T>
		{
			void erase(int pos, T[] vector, int size);
		}

		public class FastErasePolicy<T> : IErasePolicy<T>
		{
			public void erase(int pos, T[] vector, int size)
			{
				if (pos < size - 1)
				{
					vector[pos] = vector[size - 1];
				}
				vector[size - 1] = default(T);
			}
		}
	}
}
