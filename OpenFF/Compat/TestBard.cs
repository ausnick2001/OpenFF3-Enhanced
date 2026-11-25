// --test-bard (FF3): the first hero a Bard with a harp in hand, as soon as the world is up - for trying the battle's Song list
// without playing to the Bard's crystal. A test's switch: run it with --save-dir on a throwaway folder.

using System;

namespace OpenFF.Client
{
	internal static class TestBard
	{
		private const int HarpId = 2301;   // the first harp in the item table (Bards only)

		private static bool _done;

		public static void Update()
		{
			if (_done || Options.Get("test-bard") == null || GameProfile.IsFf4) return;
			try
			{
				if (!EngineApi.InWorld) return;
				GlobalScope.pl.Player hero = GlobalScope.pl.PlayerParty.instance().player(0);
				if (hero == null || !hero.isEnable()) return;
				_done = true;
				hero.changeJob(GlobalScope.pl.JOB_TYPE.BARD);
				GlobalScope.pl.PlayerParty.instance().addItem(HarpId, 1);
				bool worn = OpenFF.Game.Party != null && OpenFF.Game.Party.Equip(0, HarpId);
				Log.Write(LogChannel.General, "test: --test-bard: " + hero.name() + " is a Bard" + (worn ? " with the harp" : ", the harp is in the bag (could not equip it)"));
			}
			catch (Exception ex)
			{
				_done = true;
				Log.Write(LogChannel.General, "test: --test-bard: " + ex.Message);
			}
		}
	}
}
