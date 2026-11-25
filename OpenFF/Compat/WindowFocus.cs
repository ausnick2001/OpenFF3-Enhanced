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
		[DllImport("user32.dll", SetLastError = true)] private static extern bool SetForegroundWindow(IntPtr window);
		[DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
		[DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
		[DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
		[DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

		[StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
		[DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
		[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
		[DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr window);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int max);

		private const int GWL_EXSTYLE = -20;
		private const long WS_EX_TOPMOST = 8;
		private const uint GW_HWNDNEXT = 2;

		private static DateTime _diagStart = DateTime.MinValue, _diagLast = DateTime.MinValue;
		private static int _diagCount;

		/// <summary>For the first 15 seconds, once a second, the log says whether the game thinks it is active, which window is in front and what sits above the game's (to find out why a launch from Big Picture is left behind). Changes nothing.</summary>
		private static void Diagnose(Microsoft.Xna.Framework.Game game)
		{
			if (_diagCount >= 16 || game == null) return;
			DateTime now = DateTime.UtcNow;
			if (_diagStart == DateTime.MinValue) _diagStart = now;
			if ((now - _diagLast).TotalSeconds < 1) return;
			_diagLast = now; _diagCount++;
			try
			{
				IntPtr mine = FindWindow(game.Window.Title);
				IntPtr front = GetForegroundWindow();
				if (game.IsActive && mine != IntPtr.Zero && front == mine) return;   // in front: nothing to say
				StringBuilder line = new StringBuilder();
				line.Append("t=").Append((now - _diagStart).TotalSeconds.ToString("0.0")).Append(" active=").Append(game.IsActive);
				line.Append(" ours=").Append(mine == IntPtr.Zero ? "not found" : Describe(mine));
				line.Append(" front=").Append(front == IntPtr.Zero ? "none" : front == mine ? "ours" : Describe(front));
				if (mine != IntPtr.Zero && front != mine)
				{
					StringBuilder above = new StringBuilder();
					int shown = 0;
					IntPtr w = GetTopWindow(IntPtr.Zero);
					for (int i = 0; i < 200 && w != IntPtr.Zero && w != mine && shown < 5; i++, w = GetWindow(w, GW_HWNDNEXT))
					{
						if (!IsWindowVisible(w) || !GetWindowRect(w, out Rect r) || r.Right - r.Left < 300 || r.Bottom - r.Top < 200) continue;
						above.Append(shown++ > 0 ? "; " : "").Append(Describe(w));
					}
					line.Append(" above-ours=[").Append(above).Append(']');
				}
				Log.Write(LogChannel.General, "window: " + line);
			}
			catch (Exception ex) { _diagCount = 99; Log.Write(LogChannel.General, "window: diagnostic: " + ex.Message); }
		}

		private static string Describe(IntPtr window)
		{
			StringBuilder title = new StringBuilder(128), cls = new StringBuilder(128);
			GetWindowTextW(window, title, title.Capacity);
			GetClassNameW(window, cls, cls.Capacity);
			GetWindowThreadProcessId(window, out uint pid);
			GetWindowRect(window, out Rect r);
			bool topmost = ((long)GetWindowLongPtr(window, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
			return "'" + title + "' class=" + cls + " pid=" + pid + (pid == (uint)Environment.ProcessId ? "(this game)" : "") + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + "@" + r.Left + "," + r.Top
				+ (topmost ? " topmost" : "") + (IsIconic(window) ? " minimised" : "") + (IsWindowVisible(window) ? "" : " hidden");
		}

		private const uint GW_OWNER = 4;
		private const int SW_RESTORE = 9;
		private const int Tries = 40;
		private const double GapSeconds = 0.5;

		private static bool _done;
		private static int _tried;
		private static DateTime _last = DateTime.MinValue;

		/// <summary>Called each frame; does nothing once the game has been the active window, or after a few tries.</summary>
		public static void Update(Microsoft.Xna.Framework.Game game)
		{
			if (game == null || !OperatingSystem.IsWindows()) return;
			Diagnose(game);
			if (_done) return;
			try
			{
				DateTime now = DateTime.UtcNow;
				if ((now - _last).TotalSeconds < GapSeconds) return;
				_last = now;
				// The game reports itself active in its very first frames, before it has a window at all (and Big Picture takes the
				// front a moment later), so only the game's own window being there and active counts.
				IntPtr window = FindWindow(game.Window.Title);
				if (window == IntPtr.Zero) return;
				if (game.IsActive)
				{
					_done = true;
					Log.Write(LogChannel.General, "window: in front" + (_tried > 0 ? " after " + _tried + " request(s)" : " from the start"));
					return;
				}
				if (_tried >= Tries)
				{
					_done = true;
					Log.Write(LogChannel.General, "window: still not the active one after " + _tried + " requests; left alone");
					return;
				}
				_tried++;
				Raise(window, _tried);
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

		/// <summary>One request for the front. Odd tries attach to the foreground window's input thread; even tries tap Alt first (the other usual way past Windows' foreground lock). The result goes to the log so a launch that stays behind says which, if either, was refused.</summary>
		private static void Raise(IntPtr window, int attempt)
		{
			bool byAlt = attempt % 2 == 0;
			IntPtr front = GetForegroundWindow();
			uint frontThread = front == IntPtr.Zero ? 0 : GetWindowThreadProcessId(front, out _);
			uint mine = GetCurrentThreadId();
			bool attached = !byAlt && frontThread != 0 && frontThread != mine && AttachThreadInput(mine, frontThread, true);
			bool ok;
			int error = 0;
			try
			{
				if (IsIconic(window)) ShowWindow(window, SW_RESTORE);
				if (byAlt) keybd_event(0x12, 0, 0, UIntPtr.Zero);
				BringWindowToTop(window);
				ok = SetForegroundWindow(window);
				if (!ok) error = Marshal.GetLastWin32Error();
				if (byAlt) keybd_event(0x12, 0, 2, UIntPtr.Zero);
			}
			finally
			{
				if (attached) AttachThreadInput(mine, frontThread, false);
			}
			IntPtr now = GetForegroundWindow();
			StringBuilder title = new StringBuilder(128);
			if (now != IntPtr.Zero) GetWindowTextW(now, title, title.Capacity);
			if (attempt <= 12 || attempt % 10 == 0)
				Log.Write(LogChannel.General, "window: request " + attempt + " (" + (byAlt ? "alt key" : "input thread") + (attached ? ", attached" : "") + ") returned " + ok + (ok ? "" : " error " + error) + "; in front now: '" + title + "'" + (now == window ? " (ours)" : ""));
		}
	}
}
