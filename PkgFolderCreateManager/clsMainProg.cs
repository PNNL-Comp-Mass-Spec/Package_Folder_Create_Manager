
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/16/2009
//
// Last modified 06/16/2009
//                        - 08/14/2009 (DAC) - Modified logging and status reporting
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
            clsFolderCreateTask m_Task;

            private int m_TaskRequestErrorCount = 0;
        #endregion

        #region "Methods"

            private bool CBoolSafe(string Value, bool bDefaultValue) {
                bool bValue;

                if (bool.TryParse(Value, out bValue))
                    return bValue;
                else
                    return bDefaultValue;
            }

            public bool CheckDBQueue() {
                bool success = true;
                bool bContinueLooping = true;

                try {

                    while (bContinueLooping) {

                        clsDbTask.EnumRequestTaskResult taskReturn = m_Task.RequestTask();
                        switch (taskReturn) {
                            case clsDbTask.EnumRequestTaskResult.NoTaskFound:
                                bContinueLooping = false;
                                break;

                            case clsDbTask.EnumRequestTaskResult.ResultError:
                                // Problem with task request; Errors are logged by request method
                                m_TaskRequestErrorCount++;
                                bContinueLooping = false;
                                success = false;
                                break;

                            case clsDbTask.EnumRequestTaskResult.TaskFound:

                                string sErrorMessage = string.Empty;

                                success = CreateFolder(m_Task.TaskParametersXML, out sErrorMessage, "T_Data_Folder_Create_Queue");

                                if (success) {
                                    m_Task.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_SUCCESS);
                                    bContinueLooping = true;
                                } else {
                                    m_Task.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_FAILED, sErrorMessage);
                                    bContinueLooping = false;
                                }

                                break;

                            default:
                                //Shouldn't ever get here!
                                success = false;
                                bContinueLooping = false;
                                break;

                        }    // End switch (taskReturn)

                    } // While Loop

                } catch (Exception ex) {
                    string msg = "Exception requesting and processing task";
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg, ex);

                    return false;
                }

                return success;
            }

            protected bool CreateFolder(string cmdText, out string sErrorMessage, string Source) {

                StringDictionary cmdParams = null;
                sErrorMessage = string.Empty;

                // Parse the received string
                try {
                    cmdParams = clsXMLTools.ParseCommandXML(cmdText);
                } catch (Exception ex) {
                    sErrorMessage = "Exception parsing XML command string";
                    string msg = sErrorMessage + ": " + cmdText + Environment.NewLine;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg, ex);
                    m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
                    m_StatusFile.WriteStatusFile();
                    return false;
                }

                // Make the folder
                if (cmdParams == null) {
                    sErrorMessage = "cmdParams is null; Cannot create folder";
                    string msg = sErrorMessage + " for string " + cmdText;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg);
                    m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
                    m_StatusFile.WriteStatusFile();
                    return false;
                }

                try {
            
                    m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.Running_Tool;

                    string dumStr = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; Package " + cmdParams["package"];
                    m_StatusFile.MostRecentJobInfo = dumStr;
                    m_StatusFile.WriteStatusFile();

                    clsFolderTools.CreateFolder(m_MgrSettings.GetParam("perspective"), cmdParams, Source);

                    m_StatusFile.JobNumber = 0;
                    m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
                    m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
                    m_StatusFile.WriteStatusFile();

                } catch (Exception ex) {
                    sErrorMessage = "Exception calling clsFolderTools.CreateFolder";
                    string msg = sErrorMessage + " with XML command string: " + cmdText;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg, ex);
                    m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
                    m_StatusFile.WriteStatusFile();
                    return false;
                }

                return true;

            }

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
                string logFileName = m_MgrSettings.GetParam("logfilename");
                int debugLevel = int.Parse(m_MgrSettings.GetParam("debuglevel"));
                clsLogTools.CreateFileLogger(logFileName,debugLevel);
                string logCnStr = m_MgrSettings.GetParam("connectionstring");
                string moduleName = m_MgrSettings.GetParam("modulename");
                clsLogTools.CreateDbLogger(logCnStr,moduleName);

                //Make the initial log entry
                string myMsg = "=== Started Package Folder Creation Manager V" + Application.ProductVersion + " ===== ";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, myMsg);

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
                FileInfo fInfo = new FileInfo(Application.ExecutablePath);
                string statusFileNameLoc = Path.Combine(fInfo.DirectoryName, "Status.xml");
                m_StatusFile = new clsStatusFile(statusFileNameLoc,m_MsgHandler);
                {
                    m_StatusFile.LogToMsgQueue = CBoolSafe(m_MgrSettings.GetParam("LogStatusToMessageQueue"), false);
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
                string msg = "clsMainProgram.OnMsgHandler_BroadcastReceived: Broadcast message received: " + cmdText;
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.DEBUG,msg);

                clsBroadcastCmd recvCmd;

                // Parse the received message
                try
                {
                    recvCmd = clsXMLTools.ParseBroadcastXML(cmdText);
                }
                catch (Exception Ex)
                {
                    msg = "Exception while parsing broadcast data";
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg,Ex);
                    return;
                }

                // Determine if the message applies to this machine
                if (!recvCmd.MachineList.Contains(m_MgrSettings.GetParam("MgrName")))
                {
                    // Received command doesn't apply to this manager
                    msg = "Received command not applicable to this manager instance";
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);
                    return;
                }

                // Get the command and take appropriate action
                switch (recvCmd.MachCmd.ToLower())
                {
                    case "shutdown":
                        msg = "Shutdown message received";
                        clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);
                        m_BroadcastCmdType = BroadcastCmdType.Shutdown;
                        m_Running = false;
                        break;
                    case "readconfig":
                        msg = "Reload config message received";
                        clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);
                        m_BroadcastCmdType = BroadcastCmdType.ReadConfig;
                        m_Running = false;
                        break;
                    default:
                        // Invalid command received; do nothing except log it
                        msg = "Invalid broadcast command received: " + cmdText;
                        clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, msg);
                        break;
                }
            }    // End sub
            
            /// <summary>
            /// Handles receipt of command to make a directory
            /// </summary>
            /// <param name="cmdText">XML string containing command</param>
            void OnMsgHandler_CommandReceived(string cmdText)
            {
                try {

                    string msg = "clsMainProgram.OnMsgHandler_OnMsgHandler_CommandReceived: Command message received: " + cmdText;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);

                    m_StatusFile.TaskStatus = EnumTaskStatus.Running;
                    m_StatusFile.WriteStatusFile();

                    string sErrorMessage;
                    bool bSuccess;

                    bSuccess = CreateFolder(cmdText, out sErrorMessage, "ActiveMQ Broker");

                    if (!bSuccess) {
                        clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Error calling CreateFolder: " + sErrorMessage);
                    }

                } catch (Exception ex) {
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Error in OnMsgHandler_CommandReceived", ex);
                }

            }    // End sub

            /// <summary>
            /// Start looping while awaiting control or folder creation command
            /// </summary>
            public void DoFolderCreation()
            {
                const int DB_Query_Interval_Seconds = 30;

                string logMsg;
                DateTime lastLoopRun = DateTime.UtcNow;
                DateTime lastDBQuery = DateTime.UtcNow.Subtract(new TimeSpan(0, 0, DB_Query_Interval_Seconds));

                m_Task = new clsFolderCreateTask(m_MgrSettings);

                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Starting DoFolderCreation()");
                m_MgrActive = CBoolSafe(m_MgrSettings.GetParam("mgractive"), false);

                bool bCheckDBQueue = CBoolSafe(m_MgrSettings.GetParam("CheckDataFolderCreateQueue"), false);

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
                        //    Otherwise, it might be several days between log entries.
                        if (DateTime.Compare(DateTime.UtcNow, lastLoopRun.AddHours(24)) > 0)
                        {
                            lastLoopRun = DateTime.UtcNow;
                            logMsg = "Manager running";
                            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, logMsg);
                        }
                    }

                    if (bCheckDBQueue && DateTime.UtcNow.Subtract(lastDBQuery).TotalSeconds >= DB_Query_Interval_Seconds) {
                        CheckDBQueue();
                        lastDBQuery = System.DateTime.UtcNow;
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
            }    // End sub

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
            }    // End sub

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
            }    // End sub

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
            }    // End sub
        #endregion
    }    // End class
}    // End nameapace

