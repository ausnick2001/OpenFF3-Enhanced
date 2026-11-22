// A mod's PNG in place of one of the battle effects' pictures.
//
// The effects (the .efp packs) carry two kinds of picture. A particle's is an NTPK block - the game's ds.Texture, loaded by name
// ("chinmoku.tga") - and a model's is a texture of the model's own TEX0, loaded through the same dictionary path as any model's
// (GlobalScope.LoadTextureNamed). Neither has a package name a PNG could be pinned to the way the characters' are, and the
// same name turns up in many packs (the 8x8 "tama_00_32bit" in thirty of them), so the PNGs for effects are found by what the
// picture is: its name without the ".tga", its size, and - where the game has two different pictures with one name and size -
// a CRC32 of its texel bytes followed by its palette bytes:
//
//   particle   textures/fx.<name>_<W>x<H>.png            or  fx.<name>_<W>x<H>.<crc, 8 hex digits>.png
//   model      textures/fxm.<name>_<W>x<H>.png
//
// Tools/make-effect-textures.py writes the names. A picture is drawn at the PNG's own size with linear filtering, as every other
// mod texture is; the coordinates are normalised by the size the effect declares (SendTextureParam), not the picture's, so a
// larger PNG maps the same. The log ("textures: fx.chinmoku_64x16 drawn from ...") says which were used.

using System;

namespace OpenFF.Client
{
	internal static class EffectTextures
	{
		private static uint[] _table;

		/// <summary>A particle picture's PNG (name as the pack gives it, the declared size, the texel and palette bytes it was read with); false when no mod has one.</summary>
		public static bool TryGetParticle(string name, uint sizeS, uint sizeT, Array texels, int texelBytes, Array palette, int paletteBytes, out int width, out int height, out byte[] rgba)
		{
			width = height = 0; rgba = null;
			if (!TextureOverrides.Any || string.IsNullOrEmpty(name)) return false;
			try
			{
				string key = "fx." + Stem(name) + "_" + (8 << (int)sizeS) + "x" + (8 << (int)sizeT);
				uint crc = Crc(palette, paletteBytes, Crc(texels, texelBytes, 0));
				if (TextureOverrides.TryGet(key + "." + crc.ToString("x8"), null, out width, out height, out rgba)) return true;
				if (TextureOverrides.TryGet(key, null, out width, out height, out rgba)) return true;
				string own = key + "." + crc.ToString("x8");
				Log.First(LogChannel.File, "fx-own-" + own, 1, () => "textures: effect picture " + own + " is the game's own (no PNG of the mods')");
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "textures: effect picture " + name + " - " + ex.Message); }
			return false;
		}

		/// <summary>An effect model's picture's PNG (the texture's name in its TEX0 and its declared size); false when no mod has one.</summary>
		public static bool TryGetModel(string name, uint sizeS, uint sizeT, out int width, out int height, out byte[] rgba)
		{
			width = height = 0; rgba = null;
			if (!TextureOverrides.Any || string.IsNullOrEmpty(name)) return false;
			try { return TextureOverrides.TryGet("fxm." + Stem(name) + "_" + (8 << (int)sizeS) + "x" + (8 << (int)sizeT), null, out width, out height, out rgba); }
			catch (Exception) { return false; }
		}

		/// <summary>"chinmoku.tga" -> "chinmoku" (only a trailing ".tga" goes; the packs' own names such as "power_reactor_explosion_00_00.tg" stay as they are).</summary>
		private static string Stem(string name) => name.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;

		/// <summary>CRC32 (the zlib one) of the first <paramref name="count"/> bytes of a little-endian ushort[] / byte[], continuing from <paramref name="crc"/>.</summary>
		private static uint Crc(Array data, int count, uint crc)
		{
			if (_table == null)
			{
				uint[] t = new uint[256];
				for (uint i = 0; i < 256; i++)
				{
					uint c = i;
					for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
					t[i] = c;
				}
				_table = t;
			}
			uint r = ~crc;
			if (data is ushort[] words)
			{
				int n = Math.Min(count, words.Length * 2);
				for (int i = 0; i < n; i++) r = _table[(r ^ (uint)((words[i >> 1] >> ((i & 1) * 8)) & 0xFF)) & 0xFF] ^ (r >> 8);
			}
			else if (data is byte[] bytes)
			{
				int n = Math.Min(count, bytes.Length);
				for (int i = 0; i < n; i++) r = _table[(r ^ bytes[i]) & 0xFF] ^ (r >> 8);
			}
			return ~r;
		}
	}
}
