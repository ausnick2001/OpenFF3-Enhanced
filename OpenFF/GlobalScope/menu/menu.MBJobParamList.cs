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
	public static partial class menu
	{
		public class MBJobParamList : MenuBehavior
		{
			public const int MESSAGE_NUM = 23;

			public const int ITEM_NUM = 22;

			public static dgs.UniqueNumber MBJobParamList_UN = new dgs.UniqueNumber();

			~MBJobParamList()
			{
			}

			public override void bmInitialize(Medget M)
			{
				scrollOffset = 0;
				reset();
				updataJobList();
				createItemMessage();
				MenuManager.getSingleton().initFocus(0);
			}

			public override void bmBehave(Medget M)
			{
				changeJobColor();
			}

			public override void bmFinalize(Medget M)
			{
				releaseItemMessage();
			}

			public void wmsRefresh()
			{
				releaseItemMessage();
				reset();
				updataJobList();
				createItemMessage();
			}

			public override void bmSuspend(Medget M)
			{
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					dgs.DGSMessage dGSMessage = null;
					dGSMessage = (dgs.DGSMessage)GET_MSG_JOB(medget);
					if (dGSMessage != null)
					{
						SET_ACTIVITY(medget, dGSMessage.activity() ? 1 : 0);
						dGSMessage.setActivity(b: false);
					}
					dGSMessage = (dgs.DGSMessage)GET_MSG_SKILL(medget);
					if (dGSMessage != null)
					{
						SET_ACTIVITY(medget, dGSMessage.activity() ? 1 : 0);
						dGSMessage.setActivity(b: false);
					}
				}
			}

			public override void bmResume(Medget M)
			{
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					dgs.DGSMessage dGSMessage = null;
					((dgs.DGSMessage)GET_MSG_JOB(medget))?.setActivity(GET_ACTIVITY(medget) != 0);
					((dgs.DGSMessage)GET_MSG_SKILL(medget))?.setActivity(GET_ACTIVITY(medget) != 0);
				}
				wmsRefresh();
			}

			public override bool bmDecide(Medget M)
			{
				int num = GET_JOB_NUMBER(M);
				OS_Printf("job_num %d\n", num);
				if (num < 0 || 23 <= num)
				{
					MenuManager.getSingleton().playSEBeep();
					return true;
				}
				MenuManager.getSingleton().SetTargetItemNo(num);
				for (Medget medget = M.childNode(); medget != null; medget = medget.nextSibling())
				{
					MenuManager.getSingleton().leaveFocusList(medget);
				}
				MenuManager.getSingleton().SetDecideButtonState(0);
				return true;
			}

			public override bool bmCancel(Medget M)
			{
				bool result = false;
				MenuManager.getSingleton().SetCancelButtonState(0);
				return result;
			}

			public override bool bmDirection(Medget M, int key)
			{
				// PORT: more jobs than the list has rows (22 jobs, 16 rows on this screen): Down on the last row or Up on the first scrolls the list.
				if (TryScroll(M))
				{
					return true;
				}
				return MenuManager.getSingleton().MoveCursor(M, PlaySe: true);
			}

			public override void bmActivate(Medget M)
			{
				if (M == ownerMedget)
				{
					MenuManager.getSingleton().SetTargetItemNo(0);
				}
			}

			public override void bmDeactivate(Medget M)
			{
			}

			public void reset()
			{
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					SET_JOB_NUMBER(medget, -1);
					SET_MSG_JOB(medget, null);
					SET_MSG_SKILL(medget, null);
				}
			}

			/// <summary>
			/// PORT: a row's text behaviour, or null. The game's own layout gives every row of the list
			/// one; a layout that does not - a menu file a mod or an install rewrote, a frame of its own
			/// among the rows - crashed the Job screen as it opened (a NullReferenceException here). Such
			/// a row still gets its job's name and level; it is only not lit or raised.
			/// </summary>
			private static MBText RowText(Medget medget)
			{
				return medget.behavior()?.queryInterface(MBText.classIdentifier()) as MBText;
			}

			public void createItemMessage()
			{
				string after = "";
				int num = 0;
				dgs.msg.CMessageMng.MSF_HANDLE_KIND font = dgs.msg.CMessageMng.MSF_HANDLE_KIND.MSF_HANDLE_KIND_8x8;
				dgs.DGSMessageManager dGSMessageManager = null;
				int targetCharNo = MenuManager.getSingleton().GetTargetCharNo();
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					if (GET_JOB_NUMBER(medget) >= 0)
					{
						dGSMessageManager = ((medget.display() != 0) ? dgs.msg.CMessageSys.getInstance().Main() : dgs.msg.CMessageSys.getInstance().Sub());
						dgs.DGSMessage dGSMessage = dGSMessageManager.createMessage((uint)(50105 + GET_JOB_NUMBER(medget)), MenuManager.getSingleton().GetMenuDataTextNo(), (int)font);
						if (dGSMessage != null)
						{
							dGSMessage.setPosition((short)(medget.x() + 4), (short)(medget.y() + (medget.height() - 12) / 2), erase: true);
							dGSMessage.setDisplaySpeed(byte.MaxValue);
							dGSMessage.setDisplayWait(0);
							SET_MSG_JOB(medget, dGSMessage);
							num = pl.PlayerParty.instance().player((byte)targetCharNo).jobManager()
								.job(static_cast<pl.JOB_TYPE>(GET_JOB_NUMBER(medget)))
								.skill()
								.skillLevel()
								.get();
							// PORT: a hero on the mods' mastery progression shows the job's ladder level here (FF5's job menu).
							num = OpenFF.Client.ProgressionLayer.JobMenuLevel(pl.PlayerParty.instance().player((byte)targetCharNo).playerId(), GET_JOB_NUMBER(medget), num);
							dgs.msg.CMessageSys.getInstance().changeValueFont(num, out after);
							dGSMessage = dGSMessageManager.createMessage(after, (int)font);
							if (dGSMessage != null)
							{
								dGSMessage.setPosition((short)(medget.x() + medget.width() - 4), (short)(medget.y() + (medget.height() - 12) / 2), erase: true);
								dGSMessage.setDisplaySpeed(byte.MaxValue);
								dGSMessage.setDisplayWait(0);
								dGSMessage.setStyle(dGSMessage.getStyle() | 0x20);
								SET_MSG_SKILL(medget, dGSMessage);
								MBText mBText = RowText(medget);
								mBText?.bmTextVisibility(v: true);
								mBText?.bmSetPriority(3);
								MenuManager.getSingleton().joinFocusList(medget);
							}
						}
					}
				}
			}

			public void releaseItemMessage()
			{
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					dgs.DGSMessage dGSMessage = null;
					((dgs.DGSMessage)GET_MSG_JOB(medget))?.release();
					SET_MSG_JOB(medget, null);
					((dgs.DGSMessage)GET_MSG_SKILL(medget))?.release();
					SET_MSG_SKILL(medget, null);
					RowText(medget)?.bmTextVisibility(v: false);
				}
			}

			/// <summary>The first job shown: the list scrolls over the jobs the hero can change to, a row (a line of columns) at a time.</summary>
			private int scrollOffset;

			/// <summary>Every job the hero can change to now, in the menu's order: opened, and not the one it has.</summary>
			private readonly List<int> eligibleJobs = new List<int>();

			/// <summary>Back to the top of the list (another hero's jobs).</summary>
			public void ResetScroll()
			{
				scrollOffset = 0;
			}

			public void updataJobList()
			{
				FillJobList(resetCursor: true);
			}

			private void FillJobList(bool resetCursor)
			{
				evt.CEventManager.getInstance().FlagMng().set(0u, (uint)evt.EVENT_JOB_FLAG[(int)job_menu_itemu_priority[0]]);
				pl.JOB_TYPE now = (pl.JOB_TYPE)pl.PlayerParty.instance().player((byte)MenuManager.getSingleton().GetTargetCharNo()).jobManager().nowJob();
				eligibleJobs.Clear();
				for (int i = 0; i < 23; i++)
				{
					if (job_menu_itemu_priority[i] != now && evt.CEventManager.getInstance().FlagMng().get(0u, (uint)evt.EVENT_JOB_FLAG[(int)job_menu_itemu_priority[i]]) != 0)
					{
						eligibleJobs.Add(static_cast<int>(job_menu_itemu_priority[i]));
					}
				}
				int rows = RowCount();
				int columns = ColumnCount();
				// Never past the last row that has a job, and always a whole row.
				int last = Math.Max(0, eligibleJobs.Count - rows);
				int most = (last + columns - 1) / columns * columns;
				if (scrollOffset > most)
				{
					scrollOffset = most;
				}
				if (scrollOffset < 0)
				{
					scrollOffset = 0;
				}
				int shown = 0;
				int slot = 0;
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					int index = scrollOffset + slot++;
					if (index < eligibleJobs.Count)
					{
						SET_JOB_NUMBER(medget, eligibleJobs[index]);
						shown++;
					}
					else
					{
						SET_JOB_NUMBER(medget, -1);
					}
				}
				if (!resetCursor)
				{
					return;
				}
				if (shown == 0)
				{
					MenuManager.getSingleton().GetCursor2d().SetShow(show: false);
				}
				else
				{
					MenuManager.getSingleton().SetTargetItemNo(0);
				}
			}

			/// <summary>How many rows (child medgets) the list has.</summary>
			private int RowCount()
			{
				int count = 0;
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					count++;
				}
				return count;
			}

			/// <summary>How many columns the rows are laid out in: the rows that start level with the first.</summary>
			private int ColumnCount()
			{
				Medget first = ownerMedget.childNode();
				if (first == null)
				{
					return 1;
				}
				int columns = 0;
				for (Medget medget = first; medget != null; medget = medget.nextSibling())
				{
					if (medget.y() == first.y())
					{
						columns++;
					}
				}
				return Math.Max(1, columns);
			}

			private Medget RowAt(int slot)
			{
				int at = 0;
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					if (at++ == slot)
					{
						return medget;
					}
				}
				return null;
			}

			/// <summary>Down on the last row, or Up on the first, with more jobs beyond: the list moves a row and the cursor stays where it is.</summary>
			public bool TryScroll(Medget M)
			{
				int pad = (int)ds.g_Pad.repeat();
				bool up = (pad & 0x40) != 0;
				bool down = !up && (pad & 0x80) != 0;
				if (!up && !down)
				{
					return false;
				}
				int rows = RowCount();
				int columns = ColumnCount();
				int slot = -1;
				int at = 0;
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					if (medget == M)
					{
						slot = at;
						break;
					}
					at++;
				}
				if (slot < 0)
				{
					return false;
				}
				if (down && slot >= rows - columns && scrollOffset + rows < eligibleJobs.Count)
				{
					scrollOffset += columns;
				}
				else if (up && slot < columns && scrollOffset > 0)
				{
					scrollOffset = Math.Max(0, scrollOffset - columns);
				}
				else
				{
					return false;
				}
				MenuManager.getSingleton().SetScrollType(MenuManager.SCROLL_TYPE.TYPE_WAIT);
				releaseItemMessage();
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					MenuManager.getSingleton().leaveFocusList(medget);
				}
				FillJobList(resetCursor: false);
				createItemMessage();
				// The cursor stays on its row; where that row has no job (the last, part-filled line), on the nearest one before it that has.
				Medget target = RowAt(slot);
				while (target != null && GET_JOB_NUMBER(target) < 0 && slot > 0)
				{
					target = RowAt(--slot);
				}
				if (target != null)
				{
					MenuManager.getSingleton().initFocus(target.myTag());
				}
				MenuManager.getSingleton().playSEMoveCursor();
				return true;
			}

			public void changeJobColor()
			{
				for (Medget medget = ownerMedget.childNode(); medget != null; medget = medget.nextSibling())
				{
					MBText mBText = RowText(medget);
					int num = ((mBText != null && mBText.bmIsPushed()) ? 1 : 0);
					int num2 = medget.y() + (medget.height() - 12) / 2 + num;
					dgs.DGSMessage dGSMessage = null;
					dGSMessage = (dgs.DGSMessage)GET_MSG_JOB(medget);
					if (dGSMessage != null)
					{
						dGSMessage.position(out var _, out var y);
						if (y != num2)
						{
							dGSMessage.setPosition((short)(medget.x() + 4 + num), (short)num2, erase: true);
							((dgs.DGSMessage)GET_MSG_SKILL(medget))?.setPosition((short)(medget.x() + medget.width() - 4 + num), (short)num2, erase: true);
						}
					}
				}
			}

			public new static int classIdentifier()
			{
				return MBJobParamList_UN.number();
			}

			public override object queryInterface(int class_id)
			{
				if (class_id == classIdentifier())
				{
					return this;
				}
				return null;
			}
		}
	}
}
