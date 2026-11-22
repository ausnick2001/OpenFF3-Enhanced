// A test aid for battles, behind the debug overlay (F1): with the overlay up, a fight can have a status put on a party member to see how
// it looks (stone's grey bodies, poison's green, a toad, a mini...) without waiting for an enemy to do it.
//
//   Insert  the next status in the list      Home  the next party member
//   End     put the status on that member    Delete  clear every status from that member
//
// The overlay's legend shows the choice while a battle runs. The status goes on through the game's own routine for a spell's
// conditions (NewMagicFormula.setCondition), so it is the same as an enemy's doing it. Nothing here runs without the overlay, and
// nothing in a player's game ever turns it on by itself.

using System;
using Microsoft.Xna.Framework.Input;

namespace OpenFF.Client
{
	using btl = GlobalScope.btl;

	internal static class BattleDebug
	{
		private static readonly (string Name, short Mask)[] Statuses =
		{
			("Petrify", 0x08), ("Poison", 0x100), ("Blind", 0x80), ("Silence", 0x20), ("Toad", 0x10), ("Mini", 0x40),
			("Sleep", 0x02), ("Paralysis", 0x01), ("Confusion", 0x04), ("KO", 0x200)
		};

		private static int _status, _member;
		private static string _said = "";
		private static KeyboardState _previous;

		private static bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && !_previous.IsKeyDown(key);

		/// <summary>The overlay's line about it: the choice, and what the last key did.</summary>
		public static string Describe()
		{
			if (!InBattle()) return null;
			return "Insert status [" + Statuses[_status].Name + "]   Home member [" + (_member + 1) + "]   End inflict   Delete clear" + (_said.Length > 0 ? "   - " + _said : "");
		}

		private static bool InBattle()
		{
			try { return (GlobalScope.GAMEPART)GlobalScope.sys.FF3PartSys.getCurrentPart() == GlobalScope.GAMEPART.GAMEPART_BATTLE; }
			catch (Exception) { return false; }
		}

		/// <summary>Once a step of the battle (BattlePart): the keys, with the overlay up and the window in focus.</summary>
		public static void Tick(btl.BattleSystem system)
		{
			if (!DebugOverlay.Active || !FramePacer.WindowActive || GameProfile.IsFf4) { return; }
			KeyboardState now = Keyboard.GetState();
			try
			{
				if (Pressed(now, Keys.Insert)) { _status = (_status + 1) % Statuses.Length; _said = ""; }
				if (Pressed(now, Keys.Home)) { _member = (_member + 1) % 4; _said = ""; }
				if (Pressed(now, Keys.End)) Inflict(system, clear: false);
				if (Pressed(now, Keys.Delete)) Inflict(system, clear: true);
			}
			catch (Exception ex) { _said = "failed: " + ex.Message; Log.Write(LogChannel.General, "battle debug: " + ex); }
			_previous = now;
		}

		/// <summary>What the turn system does once a status has changed (TurnSystem, BattleParty.registerCharacterMng): the colour and effect of it on the body, and the motion it holds (stone stands still).</summary>
		private static void Refresh(btl.BattlePlayer p)
		{
			// A toad or a mini was only queued (changeCondition) by the spell's routine: the turn system turns the body at the action's end (TurnSystem), so here too.
			if (p.changeCondition().isFrog()) p.changeFrog(flag: false);
			if (p.changeCondition().isLilliput()) p.changeLilliput(flag: false);
			p.changeConditionEffect();
			p.setNextPlayerActionId(btl.BATTLE_ACTION_TYPE.DBA_CONDITION_MOTION);
		}

		private static void Inflict(btl.BattleSystem system, bool clear)
		{
			btl.BattlePlayer p = system.characterManager().playerParty().battlePlayer(_member);
			if (p == null || p.player() == null || !p.player().isEnable()) { _said = "no member " + (_member + 1); return; }
			if (clear)
			{
				p.condition().clearCondition();
				p.changeCondition().clearCondition();
				p.clearStoneInfo();
				Refresh(p);
				_said = "member " + (_member + 1) + " cleared";
			}
			else
			{
				// The member as its own attacker, so the spell's strength (intelligence over mind) comes to 1 and a timed status gets a short time.
				new btl.NewMagicFormula().setCondition(p, p, Statuses[_status].Mask, 0, false);
				Refresh(p);
				_said = Statuses[_status].Name + " on member " + (_member + 1);
			}
			Log.Write(LogChannel.General, "battle debug: " + _said);
		}
	}
}
