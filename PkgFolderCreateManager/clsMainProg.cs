
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/16/2009
//
// Last modified 06/16/2009
//						- 08/14/2009 (DAC) - Modified logging and status reporting
//*********************************************************************************************************
using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
	class clsMainProg
	{
		//*********************************************************************************************************
		// Main program class for application
		//**********************************************************************************************************

		#region "Enums"
			private enum BroadcastCmdType
			{
				Shutdown,
				ReadConfig,
				Invalid
			}
		#endregion

		#region "Class variables"
			private clsMgrSettings m_MgrSettings;
			private IStatusFile m_StatusFile;
			private clsMessageHandler m_MsgHandler;
			private bool m_Running = false;
			private bool m_MgrActive = false;
			private BroadcastCmdType m_BroadcastCmdType;
		#endregion

		#region "Methods"
			/// <summary>
			/// Initializes the manager
			/// </summary>
			/// <returns>TRUE for success, FALSE for failure</returns>
			public bool InitMgr()
			{
				//Get the manager settings
				try
				{
					m_MgrSettings = new clsMgrSettings();
				}
				catch
				{
					//Failures are logged by clsMgrSettings to local emergency log file
					return false;
				}

				//Setup the logger
				string LogFileName = m_MgrSettings.GetParam("logfilename");
				int debugLevel=int.Parse(m_MgrSettings.GetParam("debuglevel"));
				clsLogTools.CreateFileLogger(LogFileName,debugLevel);
				string logCnStr=m_MgrSettings.GetParam("connectionstring");
				string moduleName=m_MgrSettings.GetParam("modulename");
				clsLogTools.CreateDbLogger(logCnStr,moduleName);

				//Make the initial log entry
				string MyMsg = "=== Started Package Folder Creation Manager V" + Application.ProductVersion + " ===== ";
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, MyMsg);

				//Setup the message queue
				m_MsgHandler = new clsMessageHandler();
				m_MsgHandler.BrokerUri = m_MsgHandler.BrokerUri = m_MgrSettings.GetParam("MessageQueueURI");
				m_MsgHandler.CommandQueueName = m_MgrSettings.GetParam("ControlQueueName");
				m_MsgHandler.BroadcastTopicName = m_MgrSettings.GetParam("BroadcastQueueTopic");
				m_MsgHandler.StatusTopicName = m_MgrSettings.GetParam("MessageQueueTopicMgrStatus");
				m_MsgHandler.MgrSettings = m_MgrSettings;
				if (!m_MsgHandler.Init())
				{
					// Most error messages provided by .Init method, but debug message is here for program tracking
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Message handler init error");
					return false;
				}
				else
				{
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Message handler initialized");
				}

				//Connect message handler events
				m_MsgHandler.CommandReceived += new MessageProcessorDelegate(OnMsgHandler_CommandReceived);
				m_MsgHandler.BroadcastReceived += new MessageProcessorDelegate(OnMsgHandler_BroadcastReceived);

				//Setup the status file class
				FileInfo FInfo = new FileInfo(Application.ExecutablePath);
				string StatusFileNameLoc = Path.Combine(FInfo.DirectoryName, "Status.xml");
				m_StatusFile = new clsStatusFile(StatusFileNameLoc,m_MsgHandler);
				{
					m_StatusFile.LogToMsgQueue = bool.Parse(m_MgrSettings.GetParam("LogStatusToMessageQueue"));
					m_StatusFile.MgrName = m_MgrSettings.GetParam("MgrName");
					m_StatusFile.InitStatusFromFile();
					SetStartupStatus();
					m_StatusFile.WriteStatusFile();
				}
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Status file init complete");

				// Register the listeners for the message handler
				m_MsgHandler.RegisterListeners();

				//Everything worked
				return true;
			}

			/// <summary>
			/// Handles broacast messages for control of the manager
			/// </summary>
			/// <param name="cmdText">Text of received message</param>
			void OnMsgHandler_BroadcastReceived(string cmdText)
			{
				string Msg = "clsMainProgram.OnMsgHandler_BroadcastReceived: Broadcast message received: " + cmdText;
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.DEBUG,Msg);

				clsBroadcastCmd recvCmd;

				// Parse the received message
				try
				{
					recvCmd = clsXMLTools.ParseBroadcastXML(cmdText);
				}
				catch (Exception Ex)
				{
					Msg = "Exception while parsing broadcast data";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, Msg,Ex);
					return;
				}

				// Determine if the message applies to this machine
				if (!recvCmd.MachineList.Contains(m_MgrSettings.GetParam("MgrName")))
				{
					// Received command doesn't apply to this manager
					Msg = "Received command not applicable to this manager instance";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, Msg);
					return;
				}

				// Get the command and take appropriate action
				switch (recvCmd.MachCmd.ToLower())
				{
					case "shutdown":
						Msg = "Shutdown message received";
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);
						m_BroadcastCmdType = BroadcastCmdType.Shutdown;
						m_Running = false;
						break;
					case "readconfig":
						Msg = "Reload config message received";
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);
						m_BroadcastCmdType = BroadcastCmdType.ReadConfig;
						m_Running = false;
						break;
					default:
						// Invalid command received; do nothing except log it
						Msg = "Invalid broadcast command received: " + cmdText;
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, Msg);
						break;
				}
			}	// End sub
			
			/// <summary>
			/// Handles receipt of command to make a directory
			/// </summary>
			/// <param name="cmdText">XML string containing command</param>
			void OnMsgHandler_CommandReceived(string cmdText)
			{
				string Msg = "clsMainProgram.OnMsgHandler_OnMsgHandler_CommandReceived: Command message received: " + cmdText;
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, Msg);

				StringDictionary cmdParams = null;

				m_StatusFile.TaskStatus = EnumTaskStatus.Running;
				m_StatusFile.WriteStatusFile();

				// Parse the received string
				try
				{
					cmdParams = clsXMLTools.ParseCommandXML(cmdText);
				}
				catch (Exception Ex)
				{
					Msg = "Exception parsing XML command string: " + cmdText + Environment.NewLine;
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, Msg, Ex);
					m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
					m_StatusFile.WriteStatusFile();
					return;
				}

				// Make the folder
				if (cmdParams == null)
				{
					Msg = "cmdParams is null; Cannot create folder for string " + cmdText;
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.ERROR,Msg);
					m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
					m_StatusFile.WriteStatusFile();
					return;
				}
				m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.Running_Tool;
				m_StatusFile.JobNumber=Int32.Parse(cmdParams["package"]);
				string dumStr = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; Package " + cmdParams["package"];
				m_StatusFile.MostRecentJobInfo = dumStr;
				m_StatusFile.WriteStatusFile();
				
				clsFolderTools.CreatePkgFolder(m_MgrSettings.GetParam("perspective"), cmdParams);

				m_StatusFile.JobNumber = 0;
				m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
				m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
				m_StatusFile.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Start looping while awaiting control or folder creation command
			/// </summary>
			public void DoFolderCreation()
			{
				string logMsg;
				DateTime lastLoopRun = DateTime.Now;

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Starting DoFolderCreation()");
				m_MgrActive = Convert.ToBoolean(m_MgrSettings.GetParam("mgractive"));
				m_Running = m_MgrActive;
				int logCount = 0;
				while (m_Running)
				{
					logCount++;
					if (logCount > 60)
					{
						// Update status every 60 seconds
						 m_StatusFile.WriteStatusFile();
						logCount = 0;

						// If it has been > 24 hours since last log entry, tell the log that everything's OK.
						//	Otherwise, it might be seveal days between log entries.
						if (DateTime.Compare(DateTime.Now, lastLoopRun.AddHours(24)) > 0)
						{
							lastLoopRun = DateTime.Now;
							logMsg = "Manager running";
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, logMsg);
						}
					}
					// Pause 1 second
					System.Threading.Thread.Sleep(1000);
				}

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Exiting DoFolderCreation()");

				// Determine what caused exit from folder creation loop and take appropriate action
				switch (m_BroadcastCmdType)
				{
					case BroadcastCmdType.ReadConfig:
						// TODO: Add code for reloading the configuration
						break;
					case BroadcastCmdType.Shutdown:
						// Shutdown command was received
						if (m_MgrActive)
						{
							// Exit command was received
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Shutdown cmd received");
							SetNormalShutdownStatus();
							m_StatusFile.WriteStatusFile();
							m_MsgHandler.Dispose();
						}
						else
						{
							// Manager is disabled through MC database
							logMsg = "Disabled via Manager Control database";
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, logMsg);
							SetMCDisabledStatus();
							m_StatusFile.WriteStatusFile();
							m_MsgHandler.Dispose();
						}

						logMsg = "=== Exiting Package Folder Creation Manager ===";
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, logMsg);
						break;
					default:
						logMsg="clsMainProg.DoFolderCreation(); Invalid command type received: " + m_BroadcastCmdType.ToString();
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, logMsg);
						break;
				}
			}	// End sub

			/// <summary>
			/// Shortcut to set startup status
			/// </summary>
			private void SetStartupStatus()
			{
				m_StatusFile.MgrStatus = EnumMgrStatus.Running;
				m_StatusFile.Tool = "NA";
				m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
				m_StatusFile.Dataset = "NA";
				m_StatusFile.CurrentOperation = "";
				m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
			}	// End sub

			/// <summary>
			/// Shortcut to set shutdown status
			/// </summary>
			private void SetNormalShutdownStatus()
			{
				m_StatusFile.MgrStatus = EnumMgrStatus.Stopped;
				m_StatusFile.Tool = "NA";
				m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
				m_StatusFile.Dataset = "NA";
				m_StatusFile.CurrentOperation = "";
				m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
			}	// End sub

			/// <summary>
			/// Shortcut to set status if manager disabled through manager control db
			/// </summary>
			private void SetMCDisabledStatus()
			{
				m_StatusFile.MgrStatus = EnumMgrStatus.Disabled_MC;
				m_StatusFile.Tool = "NA";
				m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
				m_StatusFile.Dataset = "NA";
				m_StatusFile.CurrentOperation = "";
				m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
			}	// End sub
		#endregion
	}	// End class
}	// End nameapace

