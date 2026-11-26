// FF3 test switches: --opening=back|pre forces every battle's opening; --full-party fills the party to four heroes.
// Tests' switches: run them with --save-dir on a throwaway folder.

using System;

namespace OpenFF.Client
{
	internal static class TestParty
	{
		private static bool _done;

		/// <summary>The opening a battle gets: the forced one under --opening, else the one given.</summary>
		public static GlobalScope.btl.BATTLE_OPENING_TYPE ForcedOpening(GlobalScope.btl.BATTLE_OPENING_TYPE given)
		{
			string want = Options.Get("opening");
			if (string.IsNullOrEmpty(want)) return given;
			if (want.StartsWith("b", StringComparison.OrdinalIgnoreCase)) return GlobalScope.btl.BATTLE_OPENING_TYPE.BACK_ATTACK;
			if (want.StartsWith("p", StringComparison.OrdinalIgnoreCase) || want.StartsWith("i", StringComparison.OrdinalIgnoreCase)) return GlobalScope.btl.BATTLE_OPENING_TYPE.INITIATLVE_ATTACK;
			return given;
		}

		public static void Update()
		{
			if (_done || Options.Get("full-party") == null || GameProfile.IsFf4) return;
			try
			{
				if (!EngineApi.InWorld) return;
				GlobalScope.pl.PlayerParty party = GlobalScope.pl.PlayerParty.instance();
				if (party.player(0) == null || !party.player(0).isEnable()) return;
				_done = true;
				for (byte id = 0; id < 4; id++)
				{
					bool added = party.addPlayer(id);
					Log.Write(LogChannel.General, "test: --full-party: hero " + id + (added ? " joined" : " already in, or refused"));
				}
			}
			catch (Exception ex)
			{
				_done = true;
				Log.Write(LogChannel.General, "test: --full-party: " + ex.Message);
			}
		}
	}
}
