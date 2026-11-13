using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using OpenFF.Platform;
using OpenFF.Resources;

internal static partial class GlobalScope
{
	public static partial class btl
	{
		public class BattleTurnExecute : BaseBattle
		{
			public enum TURN_PHASE
			{
				TURN_IDLE,
				TURN_SORT_PHASE,
				TURN_COVER_FIRE,
				TURN_NPC_PHASE,
				TURN_EXECUTE_PHASE,
				TURN_PHASE_MAX
			}

			public enum RESULT_TYPE
			{
				RESULT_WIN = 1,
				RESULT_LOSE = 2,
				RESULT_ESCAPE = 4
			}

			public const TURN_PHASE TURN_IDLE = TURN_PHASE.TURN_IDLE;

			public const TURN_PHASE TURN_SORT_PHASE = TURN_PHASE.TURN_SORT_PHASE;

			public const TURN_PHASE TURN_COVER_FIRE = TURN_PHASE.TURN_COVER_FIRE;

			public const TURN_PHASE TURN_NPC_PHASE = TURN_PHASE.TURN_NPC_PHASE;

			public const TURN_PHASE TURN_EXECUTE_PHASE = TURN_PHASE.TURN_EXECUTE_PHASE;

			public const TURN_PHASE TURN_PHASE_MAX = TURN_PHASE.TURN_PHASE_MAX;

			public const RESULT_TYPE RESULT_WIN = RESULT_TYPE.RESULT_WIN;

			public const RESULT_TYPE RESULT_LOSE = RESULT_TYPE.RESULT_LOSE;

			public const RESULT_TYPE RESULT_ESCAPE = RESULT_TYPE.RESULT_ESCAPE;

			private TURN_PHASE turnPhase_;

			private int actionCharacterNumber_;

			private int nowActionCharacter_;

			private int startCounter_;

			private int result_;

			private bool playerSubWindowCreate_;

			private TurnSystem turn_ = new TurnSystem();

			private BattleNpcManager npcManager_ = new BattleNpcManager();

			// PORT: turn by turn (OpenFF.Client.BattleFlow). The round's speed order is worked out first; each combatant
			// is dealt with at their own turn: a hero's command window opens (the game's own, BattleSetupPlayer for one
			// hero), a monster chooses its action (BattleSetupEnemy.decide), and the turn plays at once.
			private BattleSetupPlayer setupPlayer_;

			private BattleSetupEnemy setupEnemy_;

			/// <summary>A hero's command window is open.</summary>
			private bool commandRunning_;

			/// <summary>Who has had their choice made this turn (a hero's window, a monster's pick), so it is not asked twice.</summary>
			private BaseBattleCharacter decidedFor_;

			/// <summary>How the battle opened for this round: a back attack takes the heroes' turn, a first strike the monsters'.</summary>
			private BATTLE_OPENING_TYPE roundOpening_;

			public void attach(BattleSetupPlayer setupPlayer, BattleSetupEnemy setupEnemy)
			{
				setupPlayer_ = setupPlayer;
				setupEnemy_ = setupEnemy;
			}

			public override void initialize(BattleSystem B)
			{
				commandRunning_ = false;
				decidedFor_ = null;
				roundOpening_ = OutsideToBattle.getInstance().battleOpeningType();
				turn_.initializeAll();
				turnPhase_ = TURN_PHASE.TURN_SORT_PHASE;
				actionCharacterNumber_ = 0;
				nowActionCharacter_ = 0;
				startCounter_ = 10;
				playerSubWindowCreate_ = false;
				turn_.setPhase(TurnSystem.Phase.Initialize);
				turn_.setPlayerWindow(B.playerWindow());
				turn_.setGeographyManager(B.geographyManager());
				turn_.setSummonDataManager(B.summonDataManager());
				turn_.setState(0);
				turn_.setCharacterManager(B.characterManager());
				// PORT: the glide from the command view to the fight's; auto battle kept the camera on the fight already
				// (the glide moves by steps from the command view, and from elsewhere it would overshoot).
				// (Turn by turn, the camera does not glide: it stays on the command view throughout.)
				if (!OpenFF.Client.BattleFlow.Active && !OutsideToBattle.getInstance().isFreeMode() && OutsideToBattle.getInstance().battleCamera() != BATTLE_CAMERA.MAIN_CAMERA)
				{
					OutsideToBattle.getInstance().setBattleCamera(BATTLE_CAMERA.MOVE_CAMERA);
					battleDisplay.setMoveFrame(18);
				}
			}

			/// <summary>
			/// PORT: turn by turn: between turns, deals with whoever is next in the order - a hero is asked for their command
			/// (a frame of their window each call), a monster chooses its action. True while a hero's window has the frame.
			/// </summary>
			private BattlePlayer stepBack_;

			/// <summary>PORT: turn by turn: the hero who acted from where they stepped forward to choose steps back, once the turn is over, if the action left them there. True while that plays.</summary>
			private bool stepBackWait()
			{
				if (stepBack_ != null)
				{
					if (!stepBack_.isPlayerActionEnd())
					{
						return true;
					}
					stepBack_ = null;
					return false;
				}
				BattlePlayer forward = BattleSetupPlayer.forward_;
				if (forward == null || !OpenFF.Client.BattleFlow.Active || !OpenFF.Client.BattleFlow.QuickSteps)
				{
					return false;
				}
				if (turn_.counterCharacter() != null || (object)turn_.nowCharacter() != (object)forward)
				{
					return false;
				}
				if (!forward.isPlayerActionEnd())
				{
					return false;
				}
				BattleSetupPlayer.forward_ = null;
				if (!forward.isEnable() || forward.condition().isDeath())
				{
					return false;
				}
				VecFx32 now = new VecFx32();
				characterMng.getPosition(forward.characterMngId(), now);
				VecFx32 home = forward.rootPosition();
				if (Math.Abs(now.x - home.x) < 2048 && Math.Abs(now.z - home.z) < 2048)
				{
					return false;   // the action brought them home (an attack runs back)
				}
				forward.setNextPlayerActionId(BATTLE_ACTION_TYPE.DBA_BACK);
				stepBack_ = forward;
				return true;
			}

			private bool turnBasedStep(BattleSystem B)
			{
				if (commandRunning_)
				{
					setupPlayer_.executeSingle(B);
					if (setupPlayer_.SingleDone)
					{
						setupPlayer_.finishSingle(B);
						commandRunning_ = false;
					}
					return true;
				}
				if (turnPhase_ != TURN_PHASE.TURN_EXECUTE_PHASE || turn_.phase() != TurnSystem.Phase.Initialize || turn_.counterCharacter() != null || actionCharacterNumber_ == nowActionCharacter_)
				{
					return false;
				}
				BaseBattleCharacter next = B.characterManager().order(nowActionCharacter_);
				if (next == null || next == decidedFor_)
				{
					return false;
				}
				decidedFor_ = next;
				if (next.breed() == 0)
				{
					BattlePlayer hero = static_cast<BattlePlayer>(next);
					if (roundOpening_ == BATTLE_OPENING_TYPE.BACK_ATTACK)
					{
						// Struck from behind: the heroes lose this round (the game's setup gave them no command).
						if (setupPlayer_.isCommand(hero))
						{
							hero.setActionId(0);
						}
						return false;
					}
					if (B.characterManager().monsterParty().aliveNumber() == 0)
					{
						return false;   // the fight is won: nothing to ask
					}
					int slot = setupPlayer_.slotOf(B, hero);
					if (slot < 0 || !setupPlayer_.isCommand(hero))
					{
						return false;   // cannot choose (down, asleep, in the air, confused...): their turn plays as it stands
					}
					setupPlayer_.beginSingle(B, slot);
					commandRunning_ = true;
					return true;
				}
				setupEnemy_.decide(static_cast<BattleMonster>(next), roundOpening_);
				return false;
			}

			public override void terminate(BattleSystem B)
			{
			}

			public override void execute(BattleSystem B)
			{
				if (OpenFF.Client.BattleFlow.Active)
				{
					// PORT: turn by turn: a hero's window may have the frame. The camera never moves: the command view (the one the
					// battle opens into) stays for the menus and for the turns alike, so a turn starts the moment it is chosen.
					if (turnBasedStep(B))
					{
						return;
					}
				}
				else if (OutsideToBattle.getInstance().battleCamera() != BATTLE_CAMERA.MAIN_CAMERA && !OutsideToBattle.getInstance().isFreeMode())
				{
					return;
				}
				if (!playerSubWindowCreate_)
				{
					playerSubWindowCreate_ = true;
				}
				if (startCounter_-- != 0)
				{
					return;
				}
				startCounter_ = 0;
				switch (turnPhase_)
				{
				case TURN_PHASE.TURN_SORT_PHASE:
					sortCharacter(B.characterManager());
					turnPhase_ = TURN_PHASE.TURN_COVER_FIRE;
					break;
				case TURN_PHASE.TURN_COVER_FIRE:
					if (turn_.coverFire())
					{
						npcManager_.setup();
						turnPhase_ = TURN_PHASE.TURN_NPC_PHASE;
					}
					break;
				case TURN_PHASE.TURN_NPC_PHASE:
					actNpc(B);
					break;
				case TURN_PHASE.TURN_EXECUTE_PHASE:
					OutsideToBattle.getInstance().setBattleOpeningType(BATTLE_OPENING_TYPE.CALC_BATTLE_OPENING_TYPE);
					turnExecute(B);
					break;
				}
			}

			public void releaseData()
			{
				npcManager_.cleanup();
			}

			public void sortCharacter(BattleCharacterManager BCM)
			{
				BCM.clearOrder();
				ds.Vector<OrderParameter, ds.FastErasePolicy<OrderParameter>> vector = new ds.Vector<OrderParameter, ds.FastErasePolicy<OrderParameter>>(10);
				vector.clear();
				MoveOrderFormula moveOrderFormula = new MoveOrderFormula();
				BattleParty battleParty = BCM.playerParty();
				for (int i = 0; i < 4; i++)
				{
					if (!battleParty.battlePlayer(i).isEnable())
					{
						continue;
					}
					OrderParameter orderParameter = new OrderParameter();
					orderParameter.breed = battleParty.battlePlayer(i).breed();
					orderParameter._id = i;
					orderParameter.dex = moveOrderFormula.moveDexterityPlayer(battleParty.battlePlayer(i));
					orderParameter.dex += (int)ds.RandomNumber.logic32((uint)(orderParameter.dex + 1));
					if (!OpenFF.Client.BattleFlow.Active && (battleParty.battlePlayer(i).actionId() == 3 || battleParty.battlePlayer(i).actionId() == 13 || battleParty.battlePlayer(i).actionId() == 12 || battleParty.battlePlayer(i).actionId() == 23 || battleParty.battlePlayer(i).actionId() == 20))   // PORT: turn by turn, the commands are not chosen yet when the order is made: no first place for Guard, Cover and the like - speed alone
					{
						orderParameter.dex += 10000;
					}
					if (vector.empty())
					{
						vector.insert(0, orderParameter);
					}
					else
					{
						bool flag = false;
						for (int j = 0; j < vector.size(); j++)
						{
							if (orderParameter.dex > vector.at(j).dex)
							{
								vector.insert(j, orderParameter);
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							vector.push_back(orderParameter);
						}
					}
					actionCharacterNumber_++;
				}
				BattleMonsterParty battleMonsterParty = BCM.monsterParty();
				for (int i = 0; i < 6; i++)
				{
					if (!battleMonsterParty.battleMonster(i).isEnable())
					{
						continue;
					}
					OrderParameter orderParameter2 = new OrderParameter();
					orderParameter2.breed = battleMonsterParty.battleMonster(i).breed();
					orderParameter2._id = i;
					orderParameter2.dex = moveOrderFormula.moveDexterityMonster(battleMonsterParty.battleMonster(i));
					orderParameter2.dex += (int)ds.RandomNumber.logic32((uint)(orderParameter2.dex + 1));
					if (vector.empty())
					{
						vector.insert(0, orderParameter2);
					}
					else
					{
						bool flag2 = false;
						for (int j = 0; j < vector.size(); j++)
						{
							if (orderParameter2.dex > vector.at(j).dex)
							{
								vector.insert(j, orderParameter2);
								flag2 = true;
								break;
							}
						}
						if (!flag2)
						{
							vector.push_back(orderParameter2);
						}
					}
					actionCharacterNumber_++;
				}
				for (int i = 0; i < actionCharacterNumber_; i++)
				{
					if (vector.at(i).breed == 0)
					{
						BCM.setOrder(i, battleParty.battlePlayer(vector.at(i)._id));
					}
					else if (vector.at(i).breed == 1)
					{
						BCM.setOrder(i, battleMonsterParty.battleMonster(vector.at(i)._id));
					}
				}
				if (vector.size() != actionCharacterNumber_)
				{
					OS_Printf("リストのサイズと actionCharacterNumber_ が違う値です\n");
				}
			}

			public void actNpc(BattleSystem B)
			{
				npcManager_.execute(B);
				if (npcManager_.isEnd())
				{
					turnPhase_ = TURN_PHASE.TURN_EXECUTE_PHASE;
				}
			}

			public void turnExecute(BattleSystem B)
			{
				if (turn_.phase() == TurnSystem.Phase.Initialize)
				{
					if (turn_.counterCharacter() != null)
					{
						turn_.setNowCharacter(turn_.counterCharacter());
					}
					else
					{
						turn_.setNowCharacter(B.characterManager().order(nowActionCharacter_));
					}
					turn_.setCharacterManager(B.characterManager());
					turn_.initializeTurn();
					if (turn_.phase() != TurnSystem.Phase.Terminate)
					{
						turn_.setPhase(TurnSystem.Phase.Execute);
					}
				}
				else if (turn_.phase() == TurnSystem.Phase.Execute)
				{
					turn_.executeTurn();
				}
				else if (turn_.phase() == TurnSystem.Phase.MonsterExecute)
				{
					turn_.monsterExecute();
				}
				else if (turn_.phase() == TurnSystem.Phase.Terminate)
				{
					if (stepBackWait())
					{
						return;
					}
					turn_.terminateTurn();
					turn_.clearCheckFlagAll();
					if (OpenFF.Client.BattleFlow.Active && !OutsideToBattle.getInstance().isFreeMode())
					{
						// PORT: turn by turn: back on the command view after a turn (a summon's last shot leaves the camera on the fight's view).
						battleDisplay.stateBattleCamera();
					}
					if (turn_.counterCharacter() != null)
					{
						if (turn_.counterCharacter().isActionEnd())
						{
							turn_.setCounterCharacter(null);
						}
					}
					else if (turn_.nowCharacter() != null)
					{
						if (turn_.nowCharacter().isActionEnd())
						{
							nowActionCharacter_++;
							decidedFor_ = null;   // PORT: turn by turn: the next in the order is asked afresh
						}
						else if (turn_.nowCharacter().breed() == 1)
						{
							BattleSetupEnemy battleSetupEnemy = new BattleSetupEnemy();
							BattleMonster monster = static_cast<BattleMonster>(turn_.nowCharacter());
							battleSetupEnemy.selectAction(monster);
						}
					}
					else
					{
						nowActionCharacter_++;
						decidedFor_ = null;
					}
					if (turn_.isPlayerEscape())
					{
						turn_.terminateTurn();
						setPhase(Phase.Terminate);
						if (isTurnEnd(B.characterManager()))
						{
							sendResultTypeToOutside();
							B.setNextState(BATTLE_SYSTEM_STATE.ENDING);
						}
						else
						{
							BattleMain battleMain = static_cast<BattleMain>(B.battle());
							B.turnCount_++;
							battleMain.setNextBattleMainState(BATTLE_MAIN_STATE.SETUP_PLAYER);
						}
						return;
					}
					if (actionCharacterNumber_ == nowActionCharacter_)
					{
						turn_.setPhase(TurnSystem.Phase.CommonExecute);
					}
					else
					{
						turn_.setPhase(TurnSystem.Phase.Initialize);
					}
				}
				if (actionCharacterNumber_ == nowActionCharacter_ && turn_.phase() == TurnSystem.Phase.CommonExecute && turn_.commonExecute())
				{
					turn_.terminateTurn();
					setPhase(Phase.Terminate);
					if (isTurnEnd(B.characterManager()))
					{
						sendResultTypeToOutside();
						B.setNextState(BATTLE_SYSTEM_STATE.ENDING);
					}
					else
					{
						BattleMain battleMain2 = static_cast<BattleMain>(B.battle());
						B.turnCount_++;
						battleMain2.setNextBattleMainState(BATTLE_MAIN_STATE.SETUP_PLAYER);
					}
				}
			}

			public bool isTurnEnd(BattleCharacterManager BCM)
			{
				result_ = 0;
				for (int i = 0; i < 4; i++)
				{
					BattlePlayer battlePlayer = BCM.playerParty().battlePlayer(i);
					if (battlePlayer != null)
					{
						if (battlePlayer.isBattle())
						{
							result_ |= 1;
						}
						if (battlePlayer.flag(PLAYER_FLAG.PF_ESCAPE) && turn_.isPlayerEscape())
						{
							result_ |= 4;
							return true;
						}
					}
				}
				for (int i = 0; i < 6; i++)
				{
					BattleMonster battleMonster = BCM.monsterParty().battleMonster(i);
					if (battleMonster != null && battleMonster.isBattle())
					{
						result_ |= 2;
						break;
					}
				}
				if ((result_ & 1) != 0 && (result_ & 2) != 0)
				{
					return false;
				}
				if (result_ == 0)
				{
					result_ |= 2;
				}
				return true;
			}

			public void sendResultTypeToOutside()
			{
				BATTLE_RESULT battleResult = BATTLE_RESULT.ERR_RESULT;
				if ((result_ & 4) != 0)
				{
					battleResult = BATTLE_RESULT.PLAYER_ESCAPE;
				}
				else if ((result_ & 1) != 0)
				{
					battleResult = BATTLE_RESULT.WIN;
				}
				else if ((result_ & 2) != 0)
				{
					battleResult = BATTLE_RESULT.LOSE;
				}
				BattleToOutside.getInstance().setBattleResult(battleResult);
			}
		}
	}
}
