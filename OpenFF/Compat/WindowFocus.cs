// Brings the game's window to the front when it starts and is not the active one.
//
// Started from Steam's Big Picture mode, Windows does not hand a new program the foreground (Big Picture keeps it), so the
// game ran behind it and, being inactive, got no pad input until the player Alt+Tabbed to it. Asking for the foreground
// from the program's own thread is refused in that case; attaching to the foreground window's input thread for the call is
// the usual way round it. This tries for a few seconds after the start and only until the game has been the active window
// once, so a player who switches away on purpose is never pulled back.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenFF.Client
{
	internal static class WindowFocus
	{
		private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

		[DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
		[DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
		[DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
		[DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int max);
		[DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")] private static extern bool AttachThreadInput(uint thread, uint other, bool attach);
		[DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
		[DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
		[DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
		[DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
		[DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

		private const uint GW_OWNER = 4;
		private const int SW_RESTORE = 9;
		private const int Tries = 20;
		private const double GapSeconds = 0.5;

		private static bool _done;
		private static int _tried;
		private static DateTime _last = DateTime.MinValue;

		/// <summary>Called each frame; does nothing once the game has been the active window, or after a few tries.</summary>
		public static void Update(Microsoft.Xna.Framework.Game game)
		{
			if (_done || game == null || !OperatingSystem.IsWindows()) return;
			try
			{
				if (game.IsActive)
				{
					_done = true;
					if (_tried > 0) Log.Write(LogChannel.General, "window: in front after " + _tried + " request(s)");
					return;
				}
				DateTime now = DateTime.UtcNow;
				if ((now - _last).TotalSeconds < GapSeconds) return;
				_last = now;
				if (_tried >= Tries)
				{
					_done = true;
					Log.Write(LogChannel.General, "window: still not the active one after " + _tried + " requests; left alone");
					return;
				}
				IntPtr window = FindWindow(game.Window.Title);
				if (window == IntPtr.Zero) return;
				_tried++;
				Raise(window);
			}
			catch (Exception ex)
			{
				_done = true;
				Log.Write(LogChannel.General, "window: bringing to front: " + ex.Message);
			}
		}

		/// <summary>This process's visible, unowned window with the game's title.</summary>
		private static IntPtr FindWindow(string title)
		{
			uint mine = (uint)Environment.ProcessId;
			IntPtr found = IntPtr.Zero;
			StringBuilder text = new StringBuilder(256);
			EnumWindows(delegate(IntPtr window, IntPtr parameter)
			{
				GetWindowThreadProcessId(window, out uint pid);
				if (pid != mine || !IsWindowVisible(window) || GetWindow(window, GW_OWNER) != IntPtr.Zero) return true;
				text.Clear();
				GetWindowTextW(window, text, text.Capacity);
				if (text.ToString() != title) return true;
				found = window;
				return false;
			}, IntPtr.Zero);
			return found;
		}

		private static void Raise(IntPtr window)
		{
			IntPtr front = GetForegroundWindow();
			uint frontThread = front == IntPtr.Zero ? 0 : GetWindowThreadProcessId(front, out _);
			uint mine = GetCurrentThreadId();
			bool attached = frontThread != 0 && frontThread != mine && AttachThreadInput(mine, frontThread, true);
			try
			{
				if (IsIconic(window)) ShowWindow(window, SW_RESTORE);
				BringWindowToTop(window);
				SetForegroundWindow(window);
			}
			finally
			{
				if (attached) AttachThreadInput(mine, frontThread, false);
			}
		}
	}
}
