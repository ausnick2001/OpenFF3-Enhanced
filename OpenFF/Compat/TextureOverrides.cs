// A mod's PNG in place of one of the game's textures, at any size.
//
// The game's textures live in .ntxp packages in the DS's formats - a palette of 4, 16 or 256
// colours or 4x4 blocks, sides of 8 to 1024 texels - and a character's is 64 x 64. Replacing
// one inside the package keeps that (Crystal's Replace with a PNG writes into the slot it
// has), which is what the Steam game can read. The OpenFF client draws through OpenGL, and
// its texture coordinates are normalised by the size the *material* declares (SendTextureParam:
// 1 / (8 << sizeS)), not by the picture's, so a picture of any size drawn in a texture's place
// maps the same. This is that: <mod>/textures/<name>.png (n021.png for the texture called n021
// in any package; <package>.<name>.png to name the package too) is decoded and uploaded when
// the game would decode the package's texels for that name, at the PNG's own size, with
// linear filtering (the game's own textures are drawn nearest, as the DS did). Crystal writes
// such a PNG beside the downsized package copy when a project targets OpenFF and the picture
// given is larger than the slot, so one replace serves both targets.
//
// Textures are asked for by name, and names repeat - every hero's package has an "eye" and a
// "mouth". So a PNG can also say which package it is for, in the names Crystal saves a texture
// under: "files_j104.ntxp.eye.png" (the editor's download) or "j104.eye.png" (crystal tex). The
// file the game read a package from is remembered by its bytes (CFileData), the package's
// texture block carries it (NNSG3dResTex.package), and a PNG that names a package is drawn only
// for that package's texture. A PNG with just the texture's name ("eye.png") is still for every
// texture of that name; a pinned one wins over it where the package is known.
//
// Expressions are different: a character's blinks and smiles are the pictures of a ".face" pack
// (j101_001.face), one set per number, each holding one picture called "eye" or "mouth" - so no
// package and texture name can tell them apart. A PNG named <pack>-<part>-<number>.png
// (j101_001-eye-0.png, j101_001-mouth-7.png: the names Crystal's Expressions card and the texture
// export use) is drawn for that set of that pack. The game repeats pictures across a model's packs
// (j101_002's four are j101_001's 0, 6, 3 and 7), so a picture is named after the first pack and number
// holding it, and every other set of the model's packs with the very same picture (FacePack.Signature) draws that PNG too.

using System;
using System.Collections.Generic;
using System.IO;

namespace OpenFF.Client
{
	internal static class TextureOverrides
	{
		private static readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, (int W, int H, byte[] Rgba)> _decoded = new Dictionary<string, (int, int, byte[])>(StringComparer.OrdinalIgnoreCase);
		// the pinned ones: texture name -> (the package part of the file's name, the file's key)
		private static readonly Dictionary<string, List<(string Package, string Key)>> _pinned = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], string> _sources = new System.Runtime.CompilerServices.ConditionalWeakTable<byte[], string>();
		private static readonly HashSet<string> _told = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		// face-pack pictures: <pack>-<eye|mouth>-<number>
		private static readonly System.Text.RegularExpressions.Regex FaceName = new System.Text.RegularExpressions.Regex(@"^([a-z]\d{3}(?:_\d{3})?)-(eye|mouth)-(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		private static readonly List<(string Pack, string Part, int Number, string Key)> _faceNames = new List<(string, string, int, string)>();
		private static readonly Dictionary<string, OpenFF.Graphics.FacePack> _facePacks = new Dictionary<string, OpenFF.Graphics.FacePack>(StringComparer.OrdinalIgnoreCase);
		private static Dictionary<string, string> _faceTwins;
		private static readonly object _faceLock = new object();

		/// <summary>The mods' textures folders scanned (the first root that has a name wins, as with files).</summary>
		public static void Register(IEnumerable<string> roots)
		{
			_files.Clear();
			_decoded.Clear();
			_pinned.Clear();
			_told.Clear();
			lock (_faceLock) { _faceNames.Clear(); _facePacks.Clear(); _faceTwins = null; }
			int count = 0;
			foreach (string root in roots ?? Array.Empty<string>())
			{
				string folder = Path.Combine(root, "textures");
				if (!Directory.Exists(folder)) continue;
				foreach (string file in Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
				{
					string key = Path.GetFileNameWithoutExtension(file);
					if (_files.ContainsKey(key)) continue;
					_files[key] = file;
					count++;
					System.Text.RegularExpressions.Match face = FaceName.Match(key);
					if (face.Success && int.TryParse(face.Groups[3].Value, out int faceNumber))
						lock (_faceLock) _faceNames.Add((face.Groups[1].Value, face.Groups[2].Value.ToLowerInvariant(), faceNumber, key));
					if (Pinned(key, out string package, out string texture))
					{
						if (!_pinned.TryGetValue(texture, out List<(string Package, string Key)> list)) _pinned[texture] = list = new List<(string, string)>();
						list.Add((package, key));
					}
				}
			}
			if (count > 0) Log.Write(LogChannel.General, "textures: " + count + " PNG(s) of the mods' own in place of the game's (textures/<name>.png; " + _pinned.Count + " texture name(s) pinned to a package; " + _faceNames.Count + " expression picture(s) named <pack>-<part>-<number>)");
		}

		/// <summary>A file name that says its package: "files_j104.ntxp.eye" or "j104.nmdp.eye" (package, a dot, the texture), or "j104.eye" (crystal tex's).</summary>
		private static bool Pinned(string key, out string package, out string texture)
		{
			package = texture = null;
			int cut = key.IndexOf(".ntxp.", StringComparison.OrdinalIgnoreCase);
			if (cut < 0) cut = key.IndexOf(".nmdp.", StringComparison.OrdinalIgnoreCase);
			if (cut > 0 && key.Length > cut + 6)
			{
				package = key.Substring(0, cut);
				texture = key.Substring(cut + 6);
				return true;
			}
			int dot = key.IndexOf('.');
			if (dot > 0 && dot == key.LastIndexOf('.') && key.Length > dot + 1)
			{
				package = key.Substring(0, dot);
				texture = key.Substring(dot + 1);
				return true;
			}
			return false;
		}

		public static bool Any => _files.Count > 0;

		/// <summary>A package's bytes remembered with the file they were read from (the game's own name for it, "files/j104.ntxp.lz").</summary>
		public static void NoteSource(Array data, string filename)
		{
			try
			{
				if (data is byte[] bytes && !string.IsNullOrEmpty(filename))
				{
					_sources.Remove(bytes);
					_sources.Add(bytes, filename);
				}
			}
			catch (Exception) { }
		}

		/// <summary>The file the bytes were read from, or null.</summary>
		public static string SourceOf(byte[] data)
		{
			try { return data != null && _sources.TryGetValue(data, out string name) ? name : null; }
			catch (Exception) { return null; }
		}

		/// <summary>"files/j104.ntxp.lz" -> "j104": the package's own name, whatever folder it was read from and whether it is a texture or a model package.</summary>
		private static string Stem(string filename)
		{
			string name = Path.GetFileName(filename.Replace('\\', '/'));
			int dot = name.IndexOf('.');
			return dot > 0 ? name.Substring(0, dot) : name;
		}

		/// <summary>
		/// The picture for a texture name (and the file of the package it is in, when known), decoded to
		/// RGBA at its own size; false when no mod gives one. A PNG pinned to the package comes first,
		/// then "<name>.png" for any package.
		/// </summary>
		public static bool TryGet(string texture, string package, out int width, out int height, out byte[] rgba)
		{
			width = height = 0; rgba = null;
			if (_files.Count == 0 || string.IsNullOrEmpty(texture)) return false;
			string key = null;
			// a face pack's set (the package is "face:<pack>#<number>"): the mod's picture for that pack and number, or for the same picture elsewhere
			if (package != null && package.StartsWith("face:", StringComparison.OrdinalIgnoreCase)) key = FaceKeyFor(texture, package);
			if (key == null && _pinned.TryGetValue(texture, out List<(string Package, string Key)> pinned))
			{
				if (!string.IsNullOrEmpty(package))
				{
					string stem = Stem(package);
					foreach ((string pkg, string pkey) in pinned)
					{
						if (string.Equals(pkg, stem, StringComparison.OrdinalIgnoreCase) || pkg.EndsWith("_" + stem, StringComparison.OrdinalIgnoreCase)) { key = pkey; break; }
					}
				}
				else if (_told.Add(texture))
				{
					Log.Write(LogChannel.File, "textures: " + texture + " - the package it is read from is not known here, so a PNG pinned to a package is not used for it");
				}
			}
			if (key == null && _files.ContainsKey(texture)) key = texture;
			if (key == null) return false;
			if (!_decoded.TryGetValue(key, out (int W, int H, byte[] Rgba) got))
			{
				got = Decode(_files[key]);
				_decoded[key] = got;
				if (got.Rgba != null) Log.Write(LogChannel.File, "textures: " + texture + (package != null ? " of " + package : "") + " drawn from " + Path.GetFileName(_files[key]) + " (" + got.W + "x" + got.H + ")");
			}
			if (got.Rgba == null) return false;
			width = got.W; height = got.H; rgba = got.Rgba;
			return true;
		}

		/// <summary>The file key of the mod's picture for a face pack's set ("face:j101_001#3", part "eye"): the one named for that very
		/// pack and number, else the one named for the first set of the game's packs holding the same picture; null when the mod has none.</summary>
		private static string FaceKeyFor(string part, string package)
		{
			try
			{
				int hash = package.LastIndexOf('#');
				if (hash < 6 || !int.TryParse(package.Substring(hash + 1), out int number)) return null;
				string pack = package.Substring(5, hash - 5);
				part = (part ?? "").ToLowerInvariant();
				string own = pack + "-" + part + "-" + number;
				if (_files.ContainsKey(own)) return own;
				lock (_faceLock)
				{
					if (_faceNames.Count == 0) return null;
					string signature = FacePackOf(pack)?.Signature(number, part);
					if (signature == null) return null;
					string model = pack.Length >= 4 ? pack.Substring(0, 4) : pack;   // a model's packs only: n591's and j101's closed mouth are the same picture, drawn apart
					if (_faceTwins == null)
					{
						Dictionary<string, string> twins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						List<(string Pack, string Part, int Number, string Key)> names = new List<(string, string, int, string)>(_faceNames);
						names.Sort((a, b) => { int c = string.Compare(a.Pack, b.Pack, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : a.Number.CompareTo(b.Number); });
						foreach ((string namePack, string namePart, int nameNumber, string nameKey) in names)
						{
							string sig = FacePackOf(namePack)?.Signature(nameNumber, namePart);
							if (sig != null) twins.TryAdd((namePack.Length >= 4 ? namePack.Substring(0, 4) : namePack) + "/" + namePart + "/" + sig, nameKey);
						}
						_faceTwins = twins;
					}
					return _faceTwins.TryGetValue(model + "/" + part + "/" + signature, out string twin) ? twin : null;
				}
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "textures: " + package + " - " + ex.Message);
				return null;
			}
		}

		/// <summary>A game face pack by name (j101_001), read once (callers hold _faceLock).</summary>
		private static OpenFF.Graphics.FacePack FacePackOf(string pack)
		{
			if (_facePacks.TryGetValue(pack, out OpenFF.Graphics.FacePack faces)) return faces;
			try
			{
				GlobalScope.ds.CFile file = new GlobalScope.ds.CFile();
				string name = pack + ".face";
				uint size = file.getSize(name);
				if (size > 0)
				{
					byte[] data = new byte[size];
					if (file.load(data, name)) faces = OpenFF.Graphics.FacePack.Read(data);
				}
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "textures: " + pack + ".face - " + ex.Message); }
			_facePacks[pack] = faces;
			return faces;
		}

		private static (int, int, byte[]) Decode(string path)
		{
			try
			{
				int[] image = ImageDecoder.Decode(File.ReadAllBytes(path));
				if (image == null || image.Length < 2) return (0, 0, null);
				int w = image[0], h = image[1];
				byte[] rgba = new byte[w * h * 4];
				for (int i = 0; i < w * h; i++)
				{
					uint packed = (uint)image[i + 2];        // XNA's packing: R in the low byte, then G, B, A
					rgba[i * 4] = (byte)packed;
					rgba[i * 4 + 1] = (byte)(packed >> 8);
					rgba[i * 4 + 2] = (byte)(packed >> 16);
					rgba[i * 4 + 3] = (byte)(packed >> 24);
				}
				return (w, h, rgba);
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "textures: " + Path.GetFileName(path) + " does not read: " + ex.Message);
				return (0, 0, null);
			}
		}
	}
}
