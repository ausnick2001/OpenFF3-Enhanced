// The HD textures, offered on the first start: when no mod in the mods folder carries them, the client asks (TexturesScreen)
// whether to download them, then downloads the pack, checks it, unpacks it into mods\FF3 HD Textures and starts itself again
// (a mod's files are taken in once, as the client starts - Restart).
//
// The pack is described by one small file, pack.json, a release asset (Tools/make-texture-pack.py makes it with the zips):
//   { "version": "1.0", "folder": "FF3 HD Textures", "size": <bytes to download>, "installedSize": <bytes on disk>,
//     "parts": [ { "name": "hd-textures-1.zip", "size": ..., "sha256": "..." } ],     // each under GitHub's 2 GiB a file
//     "duplicates": { "d06_02.d06_all_01": "d06_01.d06_all_01", ... } }
// A part's "url" may be given; without one it is beside pack.json. The "duplicates" are textures that are the very same picture
// as another's: the zips hold each picture once, and after unpacking a copy is made under every other name (so about half of
// what is installed is not downloaded). Every part is checked against its SHA-256 before anything is unpacked.
//
// Only before a game, only for FF3, never under a drive or a trace, never once the player said "Don't ask again" (settings.json
// "hdTextures": "no"), and silently nothing when the pack cannot be reached (offline, or no release yet).
//   --get-textures      offer it even if declined or already there
//   --no-textures-prompt never offer it this run
//   --textures-url=<url|file>  read the pack's pack.json from here (a test's; the parts may be files beside it)

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenFF.Content;

namespace OpenFF.Client
{
	internal static class HdTextures
	{
		/// <summary>Where pack.json is: the fork's release tagged "hd-textures". The fork: ausnick2001/OpenFF3-Enhanced.</summary>
		public const string PackUrl = "https://github.com/ausnick2001/OpenFF3-Enhanced/releases/download/hd-textures/pack.json";

		public const string DefaultFolder = "FF3 HD Textures";

		public enum Stage { Idle, Checking, Offer, Downloading, Verifying, Unpacking, Done, Failed }

		public sealed class Part { public string Name, Url, Sha256; public long Size; }

		public sealed class Pack
		{
			public string Version = "", Folder = DefaultFolder;
			public long Size, InstalledSize;
			public List<Part> Parts = new List<Part>();
			public Dictionary<string, string> Duplicates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		public static Stage Now { get; private set; } = Stage.Idle;
		public static Pack Found { get; private set; }
		public static string Error { get; private set; }
		/// <summary>The download's bytes so far and in all (every part), and its rate in bytes a second; while unpacking, the files so far and in all.</summary>
		public static long Got, Total;
		public static double Rate;
		/// <summary>The part being downloaded (from 1) and how many there are.</summary>
		public static int PartNow, PartCount;

		private static CancellationTokenSource _cancel;
		private static readonly object _sync = new object();

		private static string Source => Options.Get("textures-url") ?? PackUrl;

		/// <summary>The textures are in the mods folder already: a mod there with a textures folder of hundreds of PNGs (whatever its name).</summary>
		public static bool Installed
		{
			get
			{
				try
				{
					string mods = ModsFolder.Beside(AppContext.BaseDirectory);
					if (!Directory.Exists(mods)) return false;
					foreach (string dir in Directory.EnumerateDirectories(mods))
					{
						string textures = Path.Combine(dir, "textures");
						if (Directory.Exists(textures) && Directory.EnumerateFiles(textures, "*.png").Take(300).Count() >= 300) return true;
					}
				}
				catch (Exception) { }
				return false;
			}
		}

		/// <summary>Whether this run may offer the pack at all (the start's conditions; the pack itself still has to be reachable).</summary>
		private static bool Wanted()
		{
			if (Options.Get("no-textures-prompt") != null) return false;
			if (!string.IsNullOrEmpty(Options.Get("drive")) || !string.IsNullOrEmpty(Options.Get("trace"))) return false;
			if (string.Equals(Options.Get("game"), "ff4", StringComparison.OrdinalIgnoreCase)) return false;
			if (Options.Get("textures-url") == null && PackUrl.Contains("/OWNER/")) return false;    // not set up yet
			bool forced = Options.Get("get-textures") != null;
			if (forced) return true;
			return !string.Equals(DisplaySettings.Current.HdTextures, "no", StringComparison.OrdinalIgnoreCase) && !Installed;
		}

		/// <summary>As the client starts: pack.json fetched in the background; the offer comes up on the title once it is in.</summary>
		public static void CheckAtStart()
		{
			if (!Wanted()) return;
			lock (_sync) { Now = Stage.Checking; }
			Task.Run(async () =>
			{
				try
				{
					string json = await ReadText(Source, CancellationToken.None);
					Pack pack = Parse(json, Source);
					if (pack.Parts.Count == 0) throw new InvalidDataException("pack.json lists no parts");
					Found = pack;
					Now = Stage.Offer;
					Log.Write(LogChannel.General, "textures: HD pack " + pack.Version + " offered (" + (pack.Size / 1048576) + " MB to download)");
				}
				catch (Exception ex)
				{
					// Offline, or no release yet: said nowhere.
					Now = Stage.Idle;
					Log.Write(LogChannel.General, "textures: pack not offered - " + ex.Message);
				}
			});
		}

		private static Pack Parse(string json, string source)
		{
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;
			Pack pack = new Pack { Version = Str(root, "version") ?? "", Folder = Str(root, "folder") ?? DefaultFolder };
			if (pack.Folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || pack.Folder.StartsWith(".")) pack.Folder = DefaultFolder;
			pack.Size = Num(root, "size");
			pack.InstalledSize = Num(root, "installedSize");
			if (root.TryGetProperty("parts", out JsonElement parts))
			{
				foreach (JsonElement e in parts.EnumerateArray())
				{
					Part part = new Part { Name = Str(e, "name") ?? "", Url = Str(e, "url"), Sha256 = Str(e, "sha256") ?? "", Size = Num(e, "size") };
					if (part.Name.Length == 0 || part.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) continue;
					part.Url ??= Beside(source, part.Name);
					pack.Parts.Add(part);
				}
			}
			if (pack.Size <= 0) pack.Size = pack.Parts.Sum(p => p.Size);
			if (root.TryGetProperty("duplicates", out JsonElement dups) && dups.ValueKind == JsonValueKind.Object)
			{
				foreach (JsonProperty d in dups.EnumerateObject())
				{
					string to = d.Value.GetString();
					// Names only: nothing with a path in it is ever written outside the textures folder.
					if (!string.IsNullOrEmpty(to) && SafeName(d.Name) && SafeName(to)) pack.Duplicates[d.Name] = to;
				}
			}
			return pack;
		}

		private static bool SafeName(string name) => name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains("..");

		private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
		private static long Num(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : 0;

		/// <summary>A file beside pack.json, in the same form (a URL, or a path).</summary>
		private static string Beside(string source, string name)
		{
			if (IsLocal(source)) return Path.Combine(Path.GetDirectoryName(LocalPath(source)) ?? ".", name);
			return source.Substring(0, source.LastIndexOf('/') + 1) + name;
		}

		private static bool IsLocal(string where) => where != null && (where.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || (!where.StartsWith("http:", StringComparison.OrdinalIgnoreCase) && !where.StartsWith("https:", StringComparison.OrdinalIgnoreCase)));
		private static string LocalPath(string where) => where.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(where).LocalPath : where;

		private static HttpClient Client()
		{
			HttpClient http = new HttpClient();
			http.DefaultRequestHeaders.UserAgent.ParseAdd("OpenFF3-Enhanced/" + Updates.Current.ToString(3));
			return http;
		}

		private static async Task<string> ReadText(string url, CancellationToken token)
		{
			if (IsLocal(url)) return await File.ReadAllTextAsync(LocalPath(url), token);
			using HttpClient http = Client();
			http.Timeout = TimeSpan.FromSeconds(15);
			return await http.GetStringAsync(url, token);
		}

		/// <summary>"Not now": no offer until the next start.</summary>
		public static void Later() { Now = Stage.Idle; Log.Write(LogChannel.General, "textures: not now"); }

		/// <summary>"Don't ask again": remembered in settings.json.</summary>
		public static void Decline()
		{
			DisplaySettings.Current.HdTextures = "no";
			try { DisplaySettings.Current.Save(); } catch (Exception) { }
			Now = Stage.Idle;
			Log.Write(LogChannel.General, "textures: declined for good");
		}

		/// <summary>Said back from a failure: the offer again (or nothing, without the pack).</summary>
		public static void Dismiss() { Now = Stage.Idle; }

		public static void Cancel() => _cancel?.Cancel();

		/// <summary>Downloads, checks and unpacks the pack in the background. Done when the mod is in place; the screen then restarts the client.</summary>
		public static void Install()
		{
			Pack pack = Found;
			if (pack == null) return;
			lock (_sync)
			{
				if (Now == Stage.Downloading || Now == Stage.Verifying || Now == Stage.Unpacking) return;
				Now = Stage.Downloading;
				Error = null;
				Got = 0; Total = pack.Size; Rate = 0; PartNow = 0; PartCount = pack.Parts.Count;
				_cancel = new CancellationTokenSource();
			}
			CancellationToken token = _cancel.Token;
			Task.Run(async () =>
			{
				string work = Path.Combine(Path.GetTempPath(), "OpenFF-textures");
				string mods = ModsFolder.Beside(AppContext.BaseDirectory);
				// A folder beginning with a dot is not a mod (ModsFolder.Load passes it over): the pack is unpacked there and moved in whole.
				string staging = Path.Combine(mods, ".hd-textures-staging");
				try
				{
					Directory.CreateDirectory(mods);
					EnoughRoom(work, pack.Size, mods, pack.InstalledSize);
					if (Directory.Exists(work)) Directory.Delete(work, true);
					Directory.CreateDirectory(work);
					if (Directory.Exists(staging)) Directory.Delete(staging, true);

					List<string> zips = new List<string>();
					for (int i = 0; i < pack.Parts.Count; i++)
					{
						PartNow = i + 1;
						string zip = Path.Combine(work, pack.Parts[i].Name);
						await Download(pack.Parts[i].Url, zip, token);
						zips.Add(zip);
					}

					Now = Stage.Verifying;
					for (int i = 0; i < pack.Parts.Count; i++)
					{
						string wanted = pack.Parts[i].Sha256 ?? "";
						if (wanted.Length == 0) throw new InvalidDataException("pack.json has no checksum for " + pack.Parts[i].Name);
						string have;
						using (FileStream f = File.OpenRead(zips[i])) have = Convert.ToHexString(await SHA256.HashDataAsync(f, token));
						if (!string.Equals(have, wanted, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(pack.Parts[i].Name + " does not match its checksum - nothing was installed");
					}
					Log.Write(LogChannel.General, "textures: " + pack.Parts.Count + " part(s) checked");

					Now = Stage.Unpacking;
					Directory.CreateDirectory(staging);
					string stagingFull = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
					Got = 0; Total = 0;
					foreach (string zip in zips) using (ZipArchive za = ZipFile.OpenRead(zip)) Total += za.Entries.Count;
					Total += pack.Duplicates.Count;
					foreach (string zip in zips)
					{
						using (ZipArchive za = ZipFile.OpenRead(zip))
						{
							foreach (ZipArchiveEntry entry in za.Entries)
							{
								token.ThrowIfCancellationRequested();
								string dest = Path.GetFullPath(Path.Combine(staging, entry.FullName));
								if (!dest.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("an entry of the pack points outside its folder: " + entry.FullName);
								if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); }
								else
								{
									Directory.CreateDirectory(Path.GetDirectoryName(dest));
									entry.ExtractToFile(dest, overwrite: true);
								}
								Got++;
							}
						}
						File.Delete(zip);
					}
					// The pictures stored once, copied under their other names.
					string textures = Path.Combine(staging, "textures");
					Directory.CreateDirectory(textures);
					foreach (KeyValuePair<string, string> d in pack.Duplicates)
					{
						token.ThrowIfCancellationRequested();
						string from = Path.Combine(textures, d.Value + ".png"), to = Path.Combine(textures, d.Key + ".png");
						if (File.Exists(from) && !File.Exists(to)) File.Copy(from, to);
						Got++;
					}
					File.WriteAllText(Path.Combine(staging, "pack-installed.json"), JsonSerializer.Serialize(new { version = pack.Version, installed = DateTime.UtcNow.ToString("o") }));

					string target = Path.Combine(mods, pack.Folder);
					if (Directory.Exists(target)) MoveInto(staging, target);
					else Directory.Move(staging, target);
					try { Directory.Delete(work, true); } catch (Exception) { }
					Log.Write(LogChannel.General, "textures: HD pack " + pack.Version + " installed in " + target);
					Now = Stage.Done;
				}
				catch (OperationCanceledException)
				{
					Cleanup(work, staging);
					Now = Stage.Offer;
					Log.Write(LogChannel.General, "textures: cancelled");
				}
				catch (Exception ex)
				{
					Cleanup(work, staging);
					Error = Plain(ex);
					Now = Stage.Failed;
					Log.Write(LogChannel.General, "textures: install failed - " + Error);
				}
			});
		}

		private static void Cleanup(string work, string staging)
		{
			try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception) { }
			try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (Exception) { }
		}

		private static void MoveInto(string from, string to)
		{
			foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories).ToList())
			{
				string dest = Path.Combine(to, Path.GetRelativePath(from, file));
				Directory.CreateDirectory(Path.GetDirectoryName(dest));
				File.Move(file, dest, overwrite: true);
			}
			Directory.Delete(from, true);
		}

		/// <summary>The download's room in the temp folder and the unpacked pack's in the mods folder - said before anything is fetched.</summary>
		private static void EnoughRoom(string work, long download, string mods, long installed)
		{
			try
			{
				string a = Path.GetPathRoot(Path.GetFullPath(work)), b = Path.GetPathRoot(Path.GetFullPath(mods));
				long need = installed + 200L * 1048576;
				long tempNeed = download + 100L * 1048576;
				bool same = string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
				if (same) { need += download; tempNeed = 0; }
				if (new DriveInfo(b).AvailableFreeSpace < need) throw new IOException("not enough room on " + b + " - it needs about " + Gb(need) + " GB free");
				if (!same && new DriveInfo(a).AvailableFreeSpace < tempNeed) throw new IOException("not enough room on " + a + " (the temp folder) - it needs about " + Gb(tempNeed) + " GB free");
			}
			catch (IOException) { throw; }
			catch (Exception) { /* a drive that will not say: carry on */ }
		}

		private static string Gb(long bytes) => (bytes / 1073741824.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

		private static async Task Download(string url, string to, CancellationToken token)
		{
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
			long lastBytes = Got;
			double lastTime = 0;
			Stream source;
			HttpClient http = null;
			HttpResponseMessage response = null;
			if (IsLocal(url)) source = File.OpenRead(LocalPath(url));
			else
			{
				http = Client();
				http.Timeout = Timeout.InfiniteTimeSpan;
				response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
				response.EnsureSuccessStatusCode();
				source = await response.Content.ReadAsStreamAsync(token);
			}
			try
			{
				using FileStream file = File.Create(to);
				byte[] buffer = new byte[1 << 17];
				int throttle = Options.GetInt("update-throttle", 0);
				while (true)
				{
					int read = await source.ReadAsync(buffer, 0, buffer.Length, token);
					if (read == 0) break;
					await file.WriteAsync(buffer, 0, read, token);
					Got += read;
					if (throttle > 0) await Task.Delay((int)(read * 1000L / (throttle * 1024L)), token);
					double now = clock.Elapsed.TotalSeconds;
					if (now - lastTime >= 0.5)
					{
						double rate = (Got - lastBytes) / (now - lastTime);
						Rate = Rate <= 0 ? rate : Rate * 0.6 + rate * 0.4;
						lastBytes = Got; lastTime = now;
					}
				}
			}
			finally
			{
				source.Dispose();
				response?.Dispose();
				http?.Dispose();
			}
		}

		private static string Plain(Exception ex) => ex is HttpRequestException || ex is TaskCanceledException ? "could not reach GitHub (" + ex.Message + ")" : ex.Message;
	}
}
