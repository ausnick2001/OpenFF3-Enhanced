// Asking where the game is, when it cannot be found.
//
// The client looks for the Steam or GOG install on its own (Launch.cs, SteamInstalls.cs). A copy kept somewhere
// unusual - another drive's own folder, a library Steam does not list, a portable copy - is not found, and
// before this the client just closed with nothing on screen. Now it says so and shows a folder picker; the
// folder chosen is remembered (launch.json), so it is asked once.
//
// Windows: the shell's own folder dialog. Linux: zenity or kdialog, whichever the desktop has (the Steam Deck's
// desktop mode has both). Anywhere else, or a refusal, the start fails as it did, with the --content hint.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OpenFF.Content;

namespace OpenFF.Client
{
	internal static class GamePicker
	{
		private const string Title = "OpenFF3 Enhanced";

		private const string Explain =
			"OpenFF3 Enhanced could not find your copy of Final Fantasy III (3D Remake).\n\n" +
			"Click OK and choose the game's folder - the one that holds FF3_Win32.exe, for example " +
			"...\\steamapps\\common\\FINAL FANTASY III. It is remembered, so you are only asked once.";

		/// <summary>
		/// Explains, then lets the player choose the game's folder until it is one or they give up. True when a folder
		/// was chosen and remembered (Launch.ResolveRoot now returns it).
		/// </summary>
		public static bool Ask()
		{
			// A test run with nobody there to answer does not wait for a dialog.
			if (!string.IsNullOrEmpty(Options.Get("drive")) || !string.IsNullOrEmpty(Options.Get("trace")))
			{
				return false;
			}

			try
			{
				if (!Say(Explain, retry: false))
				{
					return false;
				}
				while (true)
				{
					string chosen = Choose();
					if (string.IsNullOrEmpty(chosen))
					{
						return false;
					}
					if (Launch.Remember(chosen))
					{
						Log.Write(LogChannel.General, "launch: the game folder was chosen by hand: " + chosen);
						return true;
					}
					if (!Say("That folder does not look like Final Fantasy III's. It should hold the game's files folder " +
						"(and FF3_Win32.exe).\n\nChoose another?", retry: true))
					{
						return false;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Write(LogChannel.General, "launch: the folder picker failed: " + ex.Message);
				return false;
			}
		}

		// ---- Windows

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
		private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct BrowseInfo
		{
			public IntPtr Owner;
			public IntPtr Root;
			public IntPtr DisplayName;
			public string Title;
			public uint Flags;
			public IntPtr Callback;
			public IntPtr Param;
			public int Image;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHBrowseForFolderW")]
		private static extern IntPtr SHBrowseForFolder(ref BrowseInfo info);

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetPathFromIDListW")]
		private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder path);

		[DllImport("ole32.dll")]
		private static extern void CoTaskMemFree(IntPtr pointer);

		/// <summary>A message; OK/Cancel (or Retry/Cancel). False when the player cancels.</summary>
		private static bool Say(string text, bool retry)
		{
			if (OperatingSystem.IsWindows())
			{
				// MB_OKCANCEL = 1, MB_RETRYCANCEL = 5; MB_ICONINFORMATION = 0x40, MB_ICONQUESTION = 0x20.
				int answer = MessageBox(IntPtr.Zero, text, Title, retry ? 5u | 0x20u : 1u | 0x40u);
				return answer == 1 || answer == 4;
			}
			if (OperatingSystem.IsLinux())
			{
				// The dialog's own title says what is wanted; a message first only where there is a tool for it.
				return true;
			}
			Console.Error.WriteLine(text);
			return false;
		}

		/// <summary>The folder the player picks, or null.</summary>
		private static string Choose()
		{
			if (OperatingSystem.IsWindows())
			{
				string result = null;
				// The shell's dialog wants a single-threaded apartment; the game's own thread is not one.
				Thread thread = new Thread(() => result = ChooseWindows());
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
				return result;
			}
			if (OperatingSystem.IsLinux())
			{
				string prompt = "Choose the Final Fantasy III (3D Remake) folder - the one holding FF3_Win32.exe";
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				return Run("zenity", "--file-selection --directory --title=\"" + prompt + "\" --filename=\"" + home + "/\"")
					?? Run("kdialog", "--title \"" + Title + "\" --getexistingdirectory \"" + home + "\"");
			}
			return null;
		}

		private static string ChooseWindows()
		{
			IntPtr name = Marshal.AllocHGlobal(520 * 2);
			try
			{
				BrowseInfo info = new BrowseInfo
				{
					DisplayName = name,
					Title = "Choose the Final Fantasy III (3D Remake) folder - the one holding FF3_Win32.exe",
					// BIF_RETURNONLYFSDIRS 0x1 | BIF_EDITBOX 0x10 | BIF_NEWDIALOGSTYLE 0x40: a resizable dialog with a path box to paste into.
					Flags = 0x1 | 0x10 | 0x40
				};
				IntPtr pidl = SHBrowseForFolder(ref info);
				if (pidl == IntPtr.Zero)
				{
					return null;
				}
				try
				{
					StringBuilder path = new StringBuilder(520);
					return SHGetPathFromIDList(pidl, path) ? path.ToString() : null;
				}
				finally
				{
					CoTaskMemFree(pidl);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(name);
			}
		}

		// ---- Linux

		/// <summary>A tool's one line of output, or null when it is not installed, is cancelled or prints nothing.</summary>
		private static string Run(string tool, string arguments)
		{
			try
			{
				ProcessStartInfo start = new ProcessStartInfo(tool, arguments)
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false
				};
				using Process process = Process.Start(start);
				if (process == null)
				{
					return null;
				}
				string output = process.StandardOutput.ReadToEnd().Trim();
				process.WaitForExit();
				return process.ExitCode == 0 && output.Length > 0 ? output : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
