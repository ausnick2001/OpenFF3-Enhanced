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
	public static partial class wmenu
	{
							public class CWMenuMemberBase : menu.MenuBehavior
							{
								public enum WMENU_KIND
								{
									WMENU_KIND_ITEM,
									WMENU_KIND_MAGIC,
									WMENU_KIND_EQUIP,
									WMENU_KIND_STATUS,
									WMENU_KIND_FORMATION,
									WMENU_KIND_JOB,
									WMENU_KIND_CONFIG,
									WMENU_KIND_HALF_SAVE,
									WMENU_KIND_SAVE,
									WMENU_KIND_MAIN_MENU,
									WMENU_KIND_PLANE,
									WMENU_KIND_MAGIC_LEARN,
									WMENU_KIND_CONFIG_2,
									WMENU_KIND_TIPS_LIST,
									WMENU_KIND_TIPS_TEXT,
									WMENU_KIND_MAX
								}

								public enum WMENU_PROCESS
								{
									WMENU_PROCESS_CSELECTINITIALIZE,
									WMENU_PROCESS_CSELECTRUN,
									WMENU_PROCESS_CSELECTTERMINATE,
									WMENU_PROCESS_CSELECTFADEOUT,
									WMENU_PROCESS_INITIALIZE,
									WMENU_PROCESS_RUN,
									WMENU_PROCESS_TERMINATE,
									WMENU_PROCESS_FADEOUT,
									WMENU_PROCESS_END
								}

								public const WMENU_KIND WMENU_KIND_ITEM = WMENU_KIND.WMENU_KIND_ITEM;

								public const WMENU_KIND WMENU_KIND_MAGIC = WMENU_KIND.WMENU_KIND_MAGIC;

								public const WMENU_KIND WMENU_KIND_EQUIP = WMENU_KIND.WMENU_KIND_EQUIP;

								public const WMENU_KIND WMENU_KIND_STATUS = WMENU_KIND.WMENU_KIND_STATUS;

								public const WMENU_KIND WMENU_KIND_FORMATION = WMENU_KIND.WMENU_KIND_FORMATION;

								public const WMENU_KIND WMENU_KIND_JOB = WMENU_KIND.WMENU_KIND_JOB;

								public const WMENU_KIND WMENU_KIND_CONFIG = WMENU_KIND.WMENU_KIND_CONFIG;

								public const WMENU_KIND WMENU_KIND_HALF_SAVE = WMENU_KIND.WMENU_KIND_HALF_SAVE;

								public const WMENU_KIND WMENU_KIND_SAVE = WMENU_KIND.WMENU_KIND_SAVE;

								public const WMENU_KIND WMENU_KIND_MAIN_MENU = WMENU_KIND.WMENU_KIND_MAIN_MENU;

								public const WMENU_KIND WMENU_KIND_PLANE = WMENU_KIND.WMENU_KIND_PLANE;

								public const WMENU_KIND WMENU_KIND_MAGIC_LEARN = WMENU_KIND.WMENU_KIND_MAGIC_LEARN;

								public const WMENU_KIND WMENU_KIND_CONFIG_2 = WMENU_KIND.WMENU_KIND_CONFIG_2;

								public const WMENU_KIND WMENU_KIND_TIPS_LIST = WMENU_KIND.WMENU_KIND_TIPS_LIST;

								public const WMENU_KIND WMENU_KIND_TIPS_TEXT = WMENU_KIND.WMENU_KIND_TIPS_TEXT;

								public const WMENU_KIND WMENU_KIND_MAX = WMENU_KIND.WMENU_KIND_MAX;

								public const WMENU_PROCESS WMENU_PROCESS_CSELECTINITIALIZE = WMENU_PROCESS.WMENU_PROCESS_CSELECTINITIALIZE;

								public const WMENU_PROCESS WMENU_PROCESS_CSELECTRUN = WMENU_PROCESS.WMENU_PROCESS_CSELECTRUN;

								public const WMENU_PROCESS WMENU_PROCESS_CSELECTTERMINATE = WMENU_PROCESS.WMENU_PROCESS_CSELECTTERMINATE;

								public const WMENU_PROCESS WMENU_PROCESS_CSELECTFADEOUT = WMENU_PROCESS.WMENU_PROCESS_CSELECTFADEOUT;

								public const WMENU_PROCESS WMENU_PROCESS_INITIALIZE = WMENU_PROCESS.WMENU_PROCESS_INITIALIZE;

								public const WMENU_PROCESS WMENU_PROCESS_RUN = WMENU_PROCESS.WMENU_PROCESS_RUN;

								public const WMENU_PROCESS WMENU_PROCESS_TERMINATE = WMENU_PROCESS.WMENU_PROCESS_TERMINATE;

								public const WMENU_PROCESS WMENU_PROCESS_FADEOUT = WMENU_PROCESS.WMENU_PROCESS_FADEOUT;

								public const WMENU_PROCESS WMENU_PROCESS_END = WMENU_PROCESS.WMENU_PROCESS_END;

								public static bool isEnd;

								protected bool doFinalize;

								public CWMenuMemberBase()
								{
								}

								public CWMenuMemberBase(string behavior_name)
									: base(behavior_name)
								{
								}

								public virtual bool cSelectInitialize()
								{
									return true;
								}

								public virtual void initialize()
								{
								}

								public virtual void run()
								{
								}

								public virtual void terminate()
								{
								}

								public bool isFinalize()
								{
									return doFinalize;
								}

								public void setFinalize(bool val)
								{
									doFinalize = val;
								}

								/// <summary>DS X (the pad's square, C on the keyboard) turns to the previous tab.</summary>
								public const int TAB_PREV_BUTTON = 0x400;

								/// <summary>DS Y (the pad's triangle, V on the keyboard) turns to the next tab.</summary>
								public const int TAB_NEXT_BUTTON = 0x800;

								/// <summary>
								/// PORT: a menu's tabs are the "mm_command" row the touch build tapped; a pad has no way
								/// to reach them. This turns to the neighbouring tab on a button edge and presses it the
								/// way a tap did - focus goes to the tab and the activate state is set - so the menu's own
								/// tap handling runs: the tab cursor moves to it and the list is re-focused. The caller
								/// says which tab is current (its tag under mm_command), which buttons turn back and
								/// forward (bits of ds.g_Pad.edge()), and any tags to skip (a tab that is an action, not
								/// a page). Returns true when a turn was made this frame.
								/// </summary>
								protected bool TurnTabs(int currentTag, int prevButtons, int nextButtons, params int[] skipTags)
								{
									int edge = ds.g_Pad.edge();
									int dir = 0;
									if ((edge & prevButtons) != 0)
									{
										dir = -1;
									}
									else if ((edge & nextButtons) != 0)
									{
										dir = 1;
									}
									if (dir == 0)
									{
										return false;
									}
									menu.MenuManager mgr = menu.MenuManager.getSingleton();
									menu.Medget root = mgr.root();
									menu.Medget row = root?.getNodeByID(TRANSCODE("mm_command"));
									if (row == null)
									{
										return false;
									}
									var tabs = new System.Collections.Generic.List<menu.Medget>();
									for (menu.Medget m = row.childNode(); m != null; m = m.nextSibling())
									{
										if (System.Array.IndexOf(skipTags, (int)m.myTag()) < 0)
										{
											tabs.Add(m);
										}
									}
									if (tabs.Count < 2)
									{
										return false;
									}
									int at = tabs.FindIndex(m => m.myTag() == currentTag);
									if (at < 0)
									{
										at = 0;
									}
									int to = at + dir;
									if (to < 0 || to >= tabs.Count)
									{
										return false;
									}
									mgr.playSEDecide();
									// By tag, as a tap does (MenuManager.execute): setFocuseMedget(Medget) hands the
									// list index to initFocus, which reads a tag, and lands elsewhere.
									mgr.initFocus(tabs[to].myTag());
									mgr.SetActivateButtonState(0);
									return true;
								}

								// PORT: pad route to a menu's tab row (Use / Sort / Key Items, Use / Learn / Remove / Change): the touch build
								// tapped them and a pad had no way there (the equipment menu had the same gap). Circle on the list steps up to
								// the row; left / right (or up / down) move along it, Cross takes the tab as a tap on it does, Circle again
								// leaves the menu.
								private int padTabAt = -1;

								private bool padTabSkip;

								/// <summary>The tab Cross last took from the row (-1 none); a menu reads and clears it.</summary>
								protected int PadTabPicked = -1;

								private System.Collections.Generic.List<menu.Medget> padTabList;

								protected void BeginPadTabs(int currentTag)
								{
									menu.MenuManager mgr = menu.MenuManager.getSingleton();
									menu.Medget row = mgr.root()?.getNodeByID(TRANSCODE("mm_command"));
									var tabs = new System.Collections.Generic.List<menu.Medget>();
									if (row != null)
									{
										for (menu.Medget m = row.childNode(); m != null; m = m.nextSibling())
										{
											tabs.Add(m);
										}
									}
									if (tabs.Count < 2)
									{
										mgr.playSECancel();
										CWMenuManager.Instance().SetNextKind(WMENU_KIND.WMENU_KIND_MAIN_MENU);
										CWMenuManager.Instance().SetProcState(WMENU_PROCESS.WMENU_PROCESS_TERMINATE);
										return;
									}
									padTabList = tabs;
									padTabAt = System.Math.Max(0, tabs.FindIndex(m => m.myTag() == currentTag));
									padTabSkip = true;
									CWMenuManager.Instance().GetDummyCursor().SetShow(show: false);
									mgr.inputPermission(b: false);
									mgr.GetCursor2d().SetShow(show: true);
									mgr.GetCursor2d().SetPositionI(tabs[padTabAt].cursorX(), tabs[padTabAt].cursorY());
									mgr.playSEMoveCursor();
								}

								/// <summary>One frame of the tab row: true while it holds the menu (the list is not read then).</summary>
								protected bool PadTabsStep()
								{
									if (padTabAt < 0)
									{
										return false;
									}
									if (padTabSkip)
									{
										padTabSkip = false;
										return true;
									}
									menu.MenuManager mgr = menu.MenuManager.getSingleton();
									int edge = ds.g_Pad.edge();
									if ((edge & 2) != 0 || CWMenuManager.Instance().GetMenuButton().TouchButtonB())
									{
										padTabAt = -1;
										mgr.inputPermission(b: true);
										mgr.playSECancel();
										CWMenuManager.Instance().SetNextKind(WMENU_KIND.WMENU_KIND_MAIN_MENU);
										CWMenuManager.Instance().SetProcState(WMENU_PROCESS.WMENU_PROCESS_TERMINATE);
										return true;
									}
									if ((edge & 1) != 0)
									{
										menu.Medget chosen = padTabList[padTabAt];
										PadTabPicked = chosen.myTag();
										padTabAt = -1;
										mgr.inputPermission(b: true);
										mgr.playSEDecide();
										// By tag, as a tap does (and as TurnTabs does): the menu's own tap handling carries it out.
										mgr.initFocus(chosen.myTag());
										mgr.SetActivateButtonState(0);
										return true;
									}
									int step = 0;
									if ((edge & 0x30) != 0)
									{
										step = ((edge & 0x10) != 0) ? 1 : -1;
									}
									else if ((edge & 0xC0) != 0)
									{
										step = ((edge & 0x80) != 0) ? 1 : -1;
									}
									if (step != 0)
									{
										padTabAt = (padTabAt + step + padTabList.Count) % padTabList.Count;
										mgr.GetCursor2d().SetPositionI(padTabList[padTabAt].cursorX(), padTabList[padTabAt].cursorY());
										mgr.playSEMoveCursor();
									}
									return true;
								}
							}
	}
}
