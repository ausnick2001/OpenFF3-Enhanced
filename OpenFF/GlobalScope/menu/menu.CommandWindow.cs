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
		public class CommandWindow : sys2d.Window
		{
			public enum COMMAND_WINDOW_SELECT
			{
				cwsOFF,
				cwsON,
				cwsDECIDED,
				cwsMAX,
				cwsINVALID
			}

			public class cwDATA
			{
				public enum cwdSTATE
				{
					cwdsOFF,
					cwdsON,
					cwdsMAX
				}

				public ds.Vector2<short> Position = new ds.Vector2<short>();

				public ds.Vector2<short> Size = new ds.Vector2<short>();
			}

			public const COMMAND_WINDOW_SELECT cwsOFF = COMMAND_WINDOW_SELECT.cwsOFF;

			public const COMMAND_WINDOW_SELECT cwsON = COMMAND_WINDOW_SELECT.cwsON;

			public const COMMAND_WINDOW_SELECT cwsDECIDED = COMMAND_WINDOW_SELECT.cwsDECIDED;

			public const COMMAND_WINDOW_SELECT cwsMAX = COMMAND_WINDOW_SELECT.cwsMAX;

			public const COMMAND_WINDOW_SELECT cwsINVALID = COMMAND_WINDOW_SELECT.cwsINVALID;

			public const int NO_MOVE = 0;

			public const int PUSH_ON = 1;

			public const int PUSH_OFF = 2;

			public static sys2d.Sprite3d g_WindowSprite = new sys2d.Sprite3d();

			public static int cwSTRING_MAX = 16;

			public static int cwMESSAGE_START = 11;

			private COMMAND_WINDOW_SELECT m_SelectState;

			private int m_MessageId;

			private pl.ABILITY_ID m_CommandId;

			private sys2d.Sprite3d m_Window = new sys2d.Sprite3d();

			private BasicWindow m_PushWindow = new BasicWindow();

			private BasicWindow[] commandWindow_ = new BasicWindow[8];

			// PORT: the command list as one panel (OpenFF.Client.BattleFlow.PanelLook).
			private BasicWindow panel_ = new BasicWindow();

			private bool panelMade_;

			private int[] messageId_ = new int[8];

			private int pushState_;

			public cwDATA commandWindowData(int i)
			{
				// PORT: the rows and the Run Away window come from the battle_hud screen of BattleDefine.xbn (OpenFF.Client.BattleHud) - the game's own numbers unless a mod moved them.
				cwDATA cwDATA2 = new cwDATA();
				OpenFF.Client.BattleHud.Rect row = OpenFF.Client.BattleHud.CommandRow(i);
				cwDATA2.Position.vx = (short)row.X;
				cwDATA2.Position.vy = (short)row.Y;
				cwDATA2.Size.vx = (short)row.Width;
				cwDATA2.Size.vy = (short)row.Height;
				return cwDATA2;
			}

			public CommandWindow()
			{
				for (int i = 0; i < commandWindow_.Length; i++)
				{
					commandWindow_[i] = new BasicWindow();
				}
				Initialize();
			}

			public static void cwInitializeSystem()
			{
			}

			public static void cwReleaseSystem()
			{
				g_WindowSprite.Release();
			}

			public override void Initialize()
			{
				base.Initialize();
				m_SelectState = COMMAND_WINDOW_SELECT.cwsINVALID;
				m_MessageId = -1;
				m_CommandId = pl.ABILITY_ID.ABILITY_ERR_ID;
				SetShow(show: false, user: true);
				pushState_ = 0;
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					commandWindow_[i].Initialize();
				}
				for (int j = 0; j < pl.BATTLE_COMMAND_MAX; j++)
				{
					messageId_[j] = -1;
				}
				setShowCommand(show: false);
			}

			public void execute()
			{
				if (!IsShow())
				{
					return;
				}
				if (panelMade_ && commandWindow_[0].IsShow()) DrawRowLines();
				ds.g_TouchPanel.getPoint(out var x, out var y);
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					ds.Vector2<short> positionUL = commandWindow_[i].GetPositionUL();
					ds.Vector2<short> size = commandWindow_[i].GetSize();
					if (ds.g_TouchPanel.isTouch() && ds.g_TouchPanel.getDispPoint().drag == 0 && positionUL.vx <= x && x <= positionUL.vx + size.vx && positionUL.vy <= y && y <= positionUL.vy + size.vy)
					{
						NNS_G2dDragHilight(positionUL.vx, positionUL.vy, size.vx, size.vy, 0);
					}
				}
			}

			/// <summary>PORT: where the rows meet, handed to the overlay that draws a line there (OpenFF.Client.ListLines), each step the list is up.</summary>
			private void DrawRowLines()
			{
				System.Collections.Generic.List<(int, int, int)> lines = new System.Collections.Generic.List<(int, int, int)>();
				for (int j = 1; j < pl.BATTLE_COMMAND_MAX; j++)
				{
					cwDATA d = commandWindowData(j);
					lines.Add((d.Position.vx, d.Position.vx + d.Size.vx, d.Position.vy));
				}
				OpenFF.Client.ListLines.Set(lines, () => panelMade_ && panel_.IsShow() && commandWindow_[0].IsShow());   // the panel's own showing, not the list's: the panel is up from the step it is built
			}

			public void createCommandWindow()
			{
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					commandWindow_[i].bwCreateUL(sys2d.DS2D_OBJ_PLANE.DS2D_OBJ_PLANE_MAIN3D, commandWindowData(i).Position, commandWindowData(i).Size, 3);
					commandWindow_[i].SetShow(show: true, user: true);
				}
				// PORT: one navy panel behind the list in place of a box a row - the rows' own windows drawn at no opacity.
				if (panelMade_) { panel_.Release(); panelMade_ = false; }
				if (OpenFF.Client.BattleFlow.PanelLook)
				{
					int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
					for (int j = 0; j < pl.BATTLE_COMMAND_MAX; j++)
					{
						cwDATA d = commandWindowData(j);
						left = Math.Min(left, d.Position.vx);
						top = Math.Min(top, d.Position.vy);
						right = Math.Max(right, d.Position.vx + d.Size.vx);
						bottom = Math.Max(bottom, d.Position.vy + d.Size.vy);
						commandWindow_[j].SetLook(0f, null);
					}
					ds.Vector2<short> at = new ds.Vector2<short>();
					ds.Vector2<short> size = new ds.Vector2<short>();
					at.vx = (short)(left - 2);
					at.vy = (short)(top - 2);
					size.vx = (short)(right - left + 4);
					size.vy = (short)(bottom - top + 4);
					panel_.bwCreateUL(sys2d.DS2D_OBJ_PLANE.DS2D_OBJ_PLANE_MAIN3D, at, size, 3);
					panel_.SetPanelStyle(BasicWindow.NavyFill, 29);
					panel_.SetShow(show: true, user: true);
					panelMade_ = true;
					OpenFF.Client.Log.Write(OpenFF.Client.LogChannel.File, "command list: built at step " + OpenFF.Client.GameHost.StepsTaken + (IsShow() ? " (shown)" : " (not yet shown)"));
					if (commandWindow_[0].IsShow()) DrawRowLines();   // PORT: the panel is up from here, so the lines come up with it (whether or not the list itself is marked shown yet)
				}
			}

			public void createCommandMessage(pl.ABILITY_ID _id, int i)
			{
				releaseCommandMessage(i);
				// PORT: a command of the mods' own (an id past the game's table) is named by its ladder step.
				if (OpenFF.Data.ModJobs.IsOwnCommand((int)_id))
				{
					messageId_[i] = dgs.msg.CMessageSys.getInstance().Main().createMessage(OpenFF.Client.BattleCommands.NameOf((int)_id), (ushort)(commandWindowData(i).Position.vx + OpenFF.Client.BattleHud.CommandTextOffset(i).X), (ushort)(commandWindowData(i).Position.vy + OpenFF.Client.BattleHud.CommandTextOffset(i).Y), dgs.msg.CMessageMng.MSD_HANDLE_KIND.MSD_HANDLE_KIND_COMMON, dgs.msg.CMessageMng.MSF_HANDLE_KIND.MSF_HANDLE_KIND_8x8);
				}
				else
				{
					messageId_[i] = dgs.msg.CMessageSys.getInstance().Main().createMessage((uint)pl.PlayerParty.instance().abilityList((int)_id).nameId_, (ushort)(commandWindowData(i).Position.vx + OpenFF.Client.BattleHud.CommandTextOffset(i).X), (ushort)(commandWindowData(i).Position.vy + OpenFF.Client.BattleHud.CommandTextOffset(i).Y), dgs.msg.CMessageMng.MSD_HANDLE_KIND.MSD_HANDLE_KIND_COMMON, dgs.msg.CMessageMng.MSF_HANDLE_KIND.MSF_HANDLE_KIND_8x8);
				}
				dgs.DGSMessage dGSMessage = dgs.msg.CMessageSys.getInstance().Main().Message(messageId_[i]);
				dGSMessage.setDisplaySpeed(byte.MaxValue);
				dGSMessage.setDisplayWait(0);
				dGSMessage.setStyle(520u);
				dGSMessage.setMessageColor(dgs.TXT_COLOR.TXT_COLOR_WHITE);
			}

			public void releaseCommandMessage(int i)
			{
				if (messageId_[i] != -1)
				{
					dgs.msg.CMessageSys.getInstance().Main().releaseMessage(messageId_[i]);
					messageId_[i] = -1;
				}
			}

			public void releaseCommandMessageAll()
			{
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					releaseCommandMessage(i);
				}
			}

			public void setShowCommand(bool show)
			{
				base.SetShow(show, user: true);
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					commandWindow_[i].SetShow(show, user: true);
				}
				if (panelMade_) panel_.SetShow(show, user: true);
				if (show) OpenFF.Client.Log.Write(OpenFF.Client.LogChannel.File, "command list: shown at step " + OpenFF.Client.GameHost.StepsTaken + (panelMade_ ? "" : " (panel not made)"));
				if (show && panelMade_ && commandWindow_[0].IsShow()) DrawRowLines();   // PORT: the lines the very step the list is shown, not the step after (execute), so they come up with it
				if (!show)
				{
					OpenFF.Client.ListLines.Clear();   // PORT: the row lines go the moment the list does
					releaseCommandMessageAll();
				}
			}

			public void setMessageColor(int i, int color)
			{
				dgs.DGSMessage dGSMessage = dgs.msg.CMessageSys.getInstance().Main().Message(messageId_[i]);
				if (color == 0)
				{
					dGSMessage.setMessageColor(dgs.TXT_COLOR.TXT_COLOR_WHITE);
				}
				else
				{
					dGSMessage.setMessageColor(dgs.TXT_COLOR.TXT_UCOLOR_4);
				}
			}

			public void moveMessage(int i)
			{
				dgs.DGSMessage dGSMessage = dgs.msg.CMessageSys.getInstance().Main().Message(messageId_[i]);
				short x;
				short y;
				switch (pushState_)
				{
				case 0:
					pushState_ = 1;
					break;
				case 1:
					dGSMessage.position(out x, out y);
					x++;
					y++;
					dGSMessage.setPosition(x, y, erase: true);
					pushState_ = 2;
					break;
				case 2:
					dGSMessage.position(out x, out y);
					x--;
					y--;
					dGSMessage.setPosition(x, y, erase: true);
					pushState_ = 0;
					break;
				}
			}

			public override void SetShow(bool show, bool user)
			{
				base.SetShow(show, user: true);
				m_PushWindow.SetShow(show: false, user: true);
				m_Window.SetShow(show);
				if (!show && m_MessageId != -1)
				{
					dgs.msg.CMessageSys.getInstance().Main().releaseMessage(m_MessageId);
					m_MessageId = -1;
				}
			}

			public override void Release()
			{
				Kill();
			}

			public override void Kill()
			{
				base.Kill();
				m_PushWindow.Release();
				m_Window.Release();
				sys2d.DS2DManager.d2dGetInstance().d2dDeleteSprite(m_Window);
				for (int i = 0; i < pl.BATTLE_COMMAND_MAX; i++)
				{
					commandWindow_[i].Release();
				}
				if (panelMade_) { panel_.Release(); panelMade_ = false; }
				OpenFF.Client.ListLines.Clear();
				releaseCommandMessageAll();
				if (m_MessageId != -1)
				{
					dgs.msg.CMessageSys.getInstance().Main().releaseMessage(m_MessageId);
					m_MessageId = -1;
				}
				Initialize();
			}

			public bool isShowCommand()
			{
				return commandWindow_[0].IsShow();
			}

			public int pushState()
			{
				return pushState_;
			}

			public pl.ABILITY_ID cwGetCommand()
			{
				return m_CommandId;
			}

			public COMMAND_WINDOW_SELECT cwGetSelectState()
			{
				return m_SelectState;
			}

			public override void SetPriority(byte pri)
			{
				m_Priority = pri;
				m_Window.SetPriority(pri);
			}

			protected override void SetDepth(int depth)
			{
				m_Depth = depth;
				m_Window.SetDepth(depth);
			}
		}
	}
}
