// The help line across the top of the battle while the command list is open: what the command under the hand does,
// as the spell and item lists already do for theirs. The game has no text for commands, so the words are here -
// a few for the common ones - and any more go in command-help.txt beside OpenFF3 Enhanced.exe, one "Name=What it does" a line
// (the name as the list shows it). A command with neither shows no line (and the log says which, once).

using System;
using System.Collections.Generic;
using System.IO;

namespace OpenFF.Client
{
	internal static class CommandHelp
	{
		private static readonly Dictionary<int, string> ById = new Dictionary<int, string>
		{
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_ATTACK, "Attack one enemy with your equipped weapon." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_ESCAPE, "Try to run away from the battle." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_GUARD, "Brace for the turn to take less damage." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_ITEM, "Use an item, or switch weapons and shields." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_BLACK_MAGIC, "Cast black magic: attack spells." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_WHITE_MAGIC, "Cast white magic: healing and support spells." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_MAGIC, "Cast a spell." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_STEAL, "Try to steal an item from an enemy." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_SUMMON, "Call a summoned monster to fight." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_SONG, "Play a song with your harp." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_JUMP, "Leap out of battle, then strike next turn for heavy damage." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_KNOCK_OVER, "Stronger attacks at the cost of lower defense for the turn." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_COVER, "Reduces physical damage taken for the turn, more strongly than Guard." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_TAKE_A_POWDER, "A more reliable attempt to escape from battle." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_OVERISSUE, "Attacks four random enemies four times." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_ROLL_UP, "Builds power for the next attack; backfires on a third use in a row." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_PROVOCATION, "Taunts enemies into attacking you and lowers their defense." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_POISE, "Reduces physical damage taken, responds with powerful counterattack." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_DARK, "Sacrifices 20% of current HP to damage all enemies." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_CHECK, "Reveals a target's weaknesses and HP, and strips its positive effects." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_GEOGRAPHY, "Casts a random Terrain spell based on the surrounding area." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_PITCH, "Hurls a weapon from your inventory at an enemy; the weapon is lost." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_EQUIP, "Switches out currently-equipped weapons and shields." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_FRONT, "Move to the front row: hit harder, take more damage." },
			{ (int)GlobalScope.pl.ABILITY_ID.ABILITY_BACK, "Move to the back row: take less damage, hit less hard." },
		};

		// The mods' own commands, by the name their ladder shows.
		private static readonly Dictionary<string, string> OwnByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "Zeninage", "Throws gil at every enemy: more gil, more damage. The party pays." },
		};

		// The commands everyone knows: the Minimal setting of Battle descriptions shows no line for these.
		private static readonly HashSet<int> Obvious = new HashSet<int>
		{
			(int)GlobalScope.pl.ABILITY_ID.ABILITY_ATTACK, (int)GlobalScope.pl.ABILITY_ID.ABILITY_ESCAPE, (int)GlobalScope.pl.ABILITY_ID.ABILITY_GUARD,
			(int)GlobalScope.pl.ABILITY_ID.ABILITY_ITEM, (int)GlobalScope.pl.ABILITY_ID.ABILITY_BLACK_MAGIC, (int)GlobalScope.pl.ABILITY_ID.ABILITY_WHITE_MAGIC,
			(int)GlobalScope.pl.ABILITY_ID.ABILITY_MAGIC, (int)GlobalScope.pl.ABILITY_ID.ABILITY_STEAL, (int)GlobalScope.pl.ABILITY_ID.ABILITY_SUMMON,
			(int)GlobalScope.pl.ABILITY_ID.ABILITY_EQUIP,
		};

		private static Dictionary<string, string> _byName;
		private static readonly HashSet<string> _noted = new HashSet<string>();
		private static string _shown;

		private static Dictionary<string, string> ByName
		{
			get
			{
				if (_byName != null) return _byName;
				_byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				try
				{
					string file = Path.Combine(AppContext.BaseDirectory, "command-help.txt");
					if (File.Exists(file))
					{
						foreach (string line in File.ReadAllLines(file))
						{
							int eq = line.IndexOf('=');
							if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
							_byName[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
						}
						Log.Write(LogChannel.General, "battle: " + _byName.Count + " command help line(s) from command-help.txt");
					}
				}
				catch (Exception ex) { Log.Write(LogChannel.General, "battle: command-help.txt: " + ex.Message); }
				return _byName;
			}
		}

		private static string Describe(int id, string name)
		{
			if (name != null && ByName.TryGetValue(name, out string own)) return own;
			if (ById.TryGetValue(id, out string text)) return text;
			if (name != null && OwnByName.TryGetValue(name, out string mine)) return mine;
			if (name != null && _noted.Add(name)) Log.Write(LogChannel.General, "battle: no help line for the command '" + name + "' (ability " + id + "); add 'name=text' to command-help.txt");
			return null;
		}

		/// <summary>Called each frame the command list is up: the line for the command under the hand.</summary>
		public static void Update(int id, string name)
		{
			try
			{
				GlobalScope.btl.HelpWindow help = GlobalScope.btl.Battle2DManager.instance().helpWindow();
				if (_shown != null && !help.isActive()) _shown = null;
				// Battle descriptions: off shows no line; minimal none for the obvious commands.
				int mode = Qol.BattleDescriptions;
				if (mode == 0 || (mode == 1 && Obvious.Contains(id))) { Release(); return; }
				string text = Describe(id, name);
				if (text == null) { Release(); return; }
				if (text == _shown) return;
				if (_shown != null) help.releaseHelpWindow();
				help.setMsdHandle(0);
				help.createHelpWindow(text, 0, 1);
				_shown = text;
			}
			catch (Exception ex) { Log.Write(LogChannel.General, "battle: command help: " + ex.Message); }
		}

		/// <summary>The command list is left (a choice, a cancel): the line goes.</summary>
		public static void Release()
		{
			if (_shown == null) return;
			_shown = null;
			try { GlobalScope.btl.Battle2DManager.instance().helpWindow().releaseHelpWindow(); } catch (Exception) { }
		}
	}
}
