using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace OpenFF.Client
{
	/// <summary>
	/// PORT: where a game step's time went (--log-stalls): the places in the step that take a Lap record the time since the
	/// last Mark/Lap under their name, and a stalled frame's log line lists the ones that took a few milliseconds.
	/// </summary>
	internal static class StepProbe
	{
		private static readonly Dictionary<string, double> Parts = new Dictionary<string, double>();
		private static long _mark;

		public static void Reset() { Parts.Clear(); }

		public static void Mark() { _mark = Stopwatch.GetTimestamp(); }

		public static void Lap(string name)
		{
			long now = Stopwatch.GetTimestamp();
			double ms = (now - _mark) * 1000.0 / Stopwatch.Frequency;
			_mark = now;
			Parts.TryGetValue(name, out double before);
			Parts[name] = before + ms;
		}

		/// <summary>The parts of this frame's step that took 3 ms or more, longest first, as "name 41, name 5".</summary>
		public static string Describe()
		{
			var big = new List<KeyValuePair<string, double>>();
			foreach (var part in Parts) if (part.Value >= 3.0) big.Add(part);
			if (big.Count == 0) return "";
			big.Sort((a, b) => b.Value.CompareTo(a.Value));
			var text = new StringBuilder(", in:");
			foreach (var part in big) text.Append(' ').Append(part.Key).Append(' ').Append(part.Value.ToString("0")).Append(',');
			return text.ToString().TrimEnd(',');
		}
	}
}
