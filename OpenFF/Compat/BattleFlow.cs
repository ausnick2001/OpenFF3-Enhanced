// Turn-by-turn battles for FF3.
//
// The game's own battle asks every hero for a command first, then plays the whole round out in speed
// order. With this on, the round's speed order is worked out first and each combatant is dealt with at
// their own turn: a hero's command menu opens when it is their turn, they choose, and they act at once;
// a monster chooses its action when its turn comes and acts; then the next in the order, and so on.
// Everyone still acts once a round, the round still ends with the poison and other end-of-round effects,
// and every status that counts rounds counts them as it did.
//
// On by default for FF3. `--classic-battle` (or FF3_CLASSIC_BATTLE=1) plays the game's own order of play.
// A battle two clients compute together (IBattle.Shared) always plays the classic way, since its random
// rolls are seeded per round and the commands travel over the wire a round at a time (BattleSync).

namespace OpenFF.Client
{
	internal static class BattleFlow
	{
		/// <summary>Whether the battle now running plays turn by turn. Fixed when the battle's main phase begins.</summary>
		public static bool Active { get; private set; }

		/// <summary>Whether a battle starting now would play turn by turn.</summary>
		public static bool Wanted
		{
			get
			{
				if (GameProfile.IsFf4) return false;
				if (BattleSync.Shared != null) return false;
				return Options.Get("classic-battle") == null;
			}
		}

		/// <summary>BattleMain.initialize: the battle's rounds begin; decide how they will be played.</summary>
		public static void Begin()
		{
			Active = Wanted;
			Log.Write(LogChannel.General, "battle: " + (Active ? "turn by turn" : "classic order of play"));
		}

		/// <summary>BattleSystem.terminate: the battle is over.</summary>
		public static void End()
		{
			Active = false;
		}
	}
}
