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
		public class MBJobName : MenuBehavior
		{
			public static dgs.UniqueNumber MBJobName_UN = new dgs.UniqueNumber();

			private dgs.DGSMessage message;

			// PORT: the job's level after the name on the main menu's party panels ("LV. 5").
			private dgs.DGSMessage levelMessage;

			public MBJobName()
			{
				message = null;
			}

			~MBJobName()
			{
				mbjReleaseMessage();
			}

			public override void bmInitialize(Medget M)
			{
				XbnNodeList xbnNodeList = new XbnNodeList();
				XbnNode firstNodeByTagNameFromChildren = M.node().getFirstNodeByTagNameFromChildren(TRANSCODE("behavior"));
				firstNodeByTagNameFromChildren.getNodesByTagNameFromChildren(TRANSCODE("parameter"), xbnNodeList);
				byte player_num = 0;
				if (xbnNodeList.size() > 0)
				{
					player_num = (byte)xbnNodeList[0].nodeValueInt();
				}
				if (xbnNodeList.size() > 1)
				{
					int num = xbnNodeList[1].nodeValueInt();
					if (num >= 12)
					{
						flagOn(4);
					}
				}
				mbjnChangePlayerNumber(player_num);
			}

			public override void bmBehave(Medget M)
			{
			}

			public override void bmFinalize(Medget M)
			{
				mbjReleaseMessage();
			}

			public override void bmSuspend(Medget M)
			{
				if (message != null)
				{
					if (message.activity())
					{
						flagOn(2);
					}
					message.setActivity(b: false);
					levelMessage?.setActivity(b: false);
				}
			}

			public override void bmResume(Medget M)
			{
				if (message != null)
				{
					message.setActivity(flagCheck(2));
					levelMessage?.setActivity(flagCheck(2));
				}
			}

			public void mbjnChangePlayerNumber(byte player_num)
			{
				mbjReleaseMessage();
				dgs.DGSMessageManager dGSMessageManager = null;
				dGSMessageManager = ((ownerMedget.display() != 1) ? dgs.msg.CMessageSys.getInstance().Sub() : dgs.msg.CMessageSys.getInstance().Main());
				message = dGSMessageManager.createMessage((uint)(FIRST_JOBNAME_ID + pl.PlayerParty.instance().player(player_num).jobManager()
					.nowJob()), dgs.INVALID_MSDHANDLE, (!flagCheck(4)) ? 1 : 0);
				if (message != null)
				{
					message.setDisplaySpeed(byte.MaxValue);
					message.setDisplayWait(0);
					message.setPosition(ownerMedget.x(), ownerMedget.y(), erase: true);
					try
					{
						if (wmenu.CWMenuManager.Instance().GetKind() == wmenu.CWMenuMemberBase.WMENU_KIND.WMENU_KIND_MAIN_MENU)
						{
							pl.Player player = pl.PlayerParty.instance().player(player_num);
							int type = player.jobManager().nowJob();
							int value = player.jobManager().job((pl.JOB_TYPE)type).skill().skillLevel().get();
							value = OpenFF.Client.ProgressionLayer.JobMenuLevel(player.playerId(), type, value);
							string digits = "";
							dgs.msg.CMessageSys.getInstance().changeValueFont(value, out digits);
							levelMessage = dGSMessageManager.createMessage("LV. " + digits, 1);
							if (levelMessage != null)
							{
								ds.Vector2<short> size = new ds.Vector2<short>(0, 0);
								message.getTextSize(size);
								levelMessage.setDisplaySpeed(byte.MaxValue);
								levelMessage.setDisplayWait(0);
								levelMessage.setPosition((short)(ownerMedget.x() + size.vx + 8), ownerMedget.y(), erase: true);
							}
						}
					}
					catch (System.Exception ex)
					{
						OpenFF.Client.Log.Write(OpenFF.Client.LogChannel.General, "menus: job level on the party panel: " + ex.Message);
					}
				}
			}

			public new static int classIdentifier()
			{
				return MBJobName_UN.number();
			}

			public override object queryInterface(int class_id)
			{
				if (class_id == classIdentifier())
				{
					return this;
				}
				return null;
			}

			public void mbjReleaseMessage()
			{
				if (levelMessage != null)
				{
					levelMessage.release();
					levelMessage = null;
				}
				if (message != null)
				{
					message.release();
					message = null;
				}
			}
		}
	}
}
