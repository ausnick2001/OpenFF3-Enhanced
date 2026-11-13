using System;
using System.Runtime.InteropServices;

namespace OpenFF.Client;

// MonoGame's Texture2D constructor and SetData each end in glFinish. With VSync the driver keeps about
// three frames queued, so that glFinish waits for all of them to be shown: ~50 ms for the first time any
// model, text or picture is uploaded (--log-stalls: tex-new 16 + tex-setdata 33, whatever the size).
// Finishing right after each Present keeps the queue empty, so those same calls return at once. The
// wait moves into this call, where the swap would have blocked for VSync anyway.
// "--no-gpu-drain" turns it off, to compare.
internal static class GpuDrain
{
	private static bool _off = Options.Get("no-gpu-drain") != null || !OperatingSystem.IsWindows();
	private static bool _logged;

	[DllImport("opengl32.dll", EntryPoint = "glFinish")]
	private static extern void glFinish();

	public static void Run()
	{
		if (_off) return;
		try
		{
			glFinish();
			if (!_logged) { _logged = true; Log.Write(LogChannel.General, "pacing: GPU drained after each present (--no-gpu-drain turns it off)"); }
		}
		catch (Exception e)
		{
			_off = true;
			Log.Write(LogChannel.General, "pacing: GPU drain unavailable - " + e.GetType().Name);
		}
	}
}
