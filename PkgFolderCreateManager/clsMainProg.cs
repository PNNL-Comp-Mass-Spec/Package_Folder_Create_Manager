
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/16/2009
//*********************************************************************************************************

using System;
using System.IO;
using System.Collections.Specialized;
using System.Reflection;
using PRISM.Logging;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Main program class for application
    /// </summary>
    class clsMainProg
    {

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
        private bool m_Running;
        private bool m_MgrActive;
        private BroadcastCmdType m_BroadcastCmdType;
        clsFolderCreateTask m_Task;

        #endregion

        #region "Methods"

        public bool CheckDBQueue()
        {
            var success = true;
            var continueLooping = true;

            try
            {

                while (continueLooping)
                {

                    var taskReturn = m_Task.RequestTask();
                    switch (taskReturn)
                    {
                        case clsDbTask.EnumRequestTaskResult.NoTaskFound:
                            continueLooping = false;
                            break;

                        case clsDbTask.EnumRequestTaskResult.ResultError:
                            // Problem with task request; Errors are logged by request method
                            continueLooping = false;
                            success = false;
                            break;

                        case clsDbTask.EnumRequestTaskResult.TaskFound:

                            success = CreateFolder(m_Task.TaskParametersXML, out var sErrorMessage, "T_Data_Folder_Create_Queue");

                            if (success)
                            {
                                m_Task.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_SUCCESS);
                            }
                            else
                            {
                                m_Task.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_FAILED, sErrorMessage);
                                continueLooping = false;
                            }

                            break;

                        default:
                            // Shouldn't ever get here!
                            success = false;
                            continueLooping = false;
                            break;

                    }
                }

            }
            catch (Exception ex)
            {
                var msg = "Exception requesting and processing task";
                LogError(msg, ex);

                return false;
            }

            return success;
        }

        protected bool CreateFolder(string cmdText, out string errorMessage, string source)
        {

            StringDictionary cmdParams;
            errorMessage = string.Empty;

            // Parse the received string
            try
            {
                cmdParams = clsXMLTools.ParseCommandXML(cmdText);
            }
            catch (Exception ex)
            {
                errorMessage = "Exception parsing XML command string";
                var msg = errorMessage + ": " + cmdText + Environment.NewLine;
                LogError(msg, ex);
                m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
                m_StatusFile.WriteStatusFile();
                return false;
            }

            // Make the folder
            if (cmdParams == null)
            {
                errorMessage = "cmdParams is null; Cannot create folder";
                var msg = errorMessage + " for string " + cmdText;
                LogError(msg);
                m_StatusFile.TaskStatus = EnumTaskStatus.Failed;
                m_StatusFile.WriteStatusFile();
                return false;
            }

            try
            {

                m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.Running_Tool;

                var dumStr = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; Package " + cmdParams["package"];
                m_StatusFile.MostRecentJobInfo = dumStr;
                m_StatusFile.WriteStatusFile();

                clsFolderTools.CreateFolder(m_MgrSettings.GetParam("perspective"), cmdParams, source);

                m_StatusFile.JobNumber = 0;
                m_StatusFile.TaskStatusDetail = EnumTaskStatusDetail.No_Task;
                m_StatusFile.TaskStatus = EnumTaskStatus.No_Task;
                m_StatusFile.WriteStatusFile();

            }
            catch (Exception ex)
            {
                errorMessage = "Exception calling clsFolderTools.CreateFolder";
                var msg = errorMessage + " with XML command string: " + cmdText;
                LogError(msg, ex);
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
            // Get the manager settings
            try
            {
                m_MgrSettings = new clsMgrSettings();
            }
            catch
            {
                // Failures are logged by clsMgrSettings to local emergency log file
                return false;
            }

            // Setup the loggers
            var logFileNameBase = m_MgrSettings.GetParam("logfilename");
            if (string.IsNullOrWhiteSpace(logFileNameBase))
                logFileNameBase = "FolderCreate";

            BaseLogger.LogLevels logLevel;
            if (int.TryParse(m_MgrSettings.GetParam("debuglevel"), out var debugLevel))
            {
                logLevel = (BaseLogger.LogLevels)debugLevel;
            }
            else
            {
                logLevel = BaseLogger.LogLevels.INFO;
            }

            LogTools.CreateFileLogger(logFileNameBase, logLevel);

            // Typically:
            // Data Source=gigasax;Initial Catalog=DMS_Pipeline;Integrated Security=SSPI;
            var logCnStr = m_MgrSettings.GetParam("connectionstring");
            var moduleName = m_MgrSettings.GetParam("modulename");
            LogTools.CreateDbLogger(logCnStr, moduleName);

            LogTools.MessageLogged += MessageLoggedHandler;

            // Make the initial log entry
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var msg = "=== Started Package Folder Creation Manager V" + appVersion + " ===== ";

            LogTools.LogMessage(msg);

            // Setup the message queue
            m_MsgHandler = new clsMessageHandler();
            m_MsgHandler.BrokerUri = m_MsgHandler.BrokerUri = m_MgrSettings.GetParam("MessageQueueURI");
            m_MsgHandler.CommandQueueName = m_MgrSettings.GetParam("ControlQueueName");
            m_MsgHandler.BroadcastTopicName = m_MgrSettings.GetParam("BroadcastQueueTopic");
            m_MsgHandler.StatusTopicName = m_MgrSettings.GetParam("MessageQueueTopicMgrStatus");
            m_MsgHandler.MgrSettings = m_MgrSettings;

            if (!m_MsgHandler.Init())
            {
                // Most error messages provided by .Init method, but debug message is here for program tracking
                LogDebug("Message handler init error");
                return false;
            }

            LogDebug("Message handler initialized");

            // Connect message handler events
            m_MsgHandler.CommandReceived += OnMsgHandler_CommandReceived;
            m_MsgHandler.BroadcastReceived += OnMsgHandler_BroadcastReceived;

            // Setup the status file class
            var appPath = PRISM.FileProcessor.ProcessFilesOrFoldersBase.GetAppPath();
            var fInfo = new FileInfo(appPath);

            string statusFileNameLoc;
            if (fInfo.DirectoryName == null)
                statusFileNameLoc = "Status.xml";
            else
                statusFileNameLoc = Path.Combine(fInfo.DirectoryName, "Status.xml");

            m_StatusFile = new clsStatusFile(statusFileNameLoc, m_MsgHandler);
            {
                m_StatusFile.LogToMsgQueue = m_MgrSettings.GetParam("LogStatusToMessageQueue", false);
                m_StatusFile.MgrName = m_MgrSettings.ManagerName;
                m_StatusFile.InitStatusFromFile();
                SetStartupStatus();
                m_StatusFile.WriteStatusFile();
            }
            LogDebug("Status file init complete");

            // Register the listeners for the message handler
            m_MsgHandler.RegisterListeners();

            // Everything worked
            return true;
        }

        /// <summary>
        /// Handles broacast messages for control of the manager
        /// </summary>
        /// <param name="cmdText">Text of received message</param>
        void OnMsgHandler_BroadcastReceived(string cmdText)
        {
            var msg = "clsMainProgram.OnMsgHandler_BroadcastReceived: Broadcast message received: " + cmdText;
            LogDebug(msg);

            clsBroadcastCmd recvCmd;

            // Parse the received message
            try
            {
                recvCmd = clsXMLTools.ParseBroadcastXML(cmdText);
            }
            catch (Exception ex)
            {
                msg = "Exception while parsing broadcast data";
                LogError(msg, ex);
                return;
            }

            // Determine if the message applies to this machine
            if (!recvCmd.MachineList.Contains(m_MgrSettings.ManagerName))
            {
                // Received command doesn't apply to this manager
                msg = "Received command not applicable to this manager instance";
                LogDebug(msg);
                return;
            }

            // Get the command and take appropriate action
            switch (recvCmd.MachCmd.ToLower())
            {
                case "shutdown":
                    msg = "Shutdown message received";
                    LogInfo(msg);
                    m_BroadcastCmdType = BroadcastCmdType.Shutdown;
                    m_Running = false;
                    break;
                case "readconfig":
                    msg = "Reload config message received";
                    LogInfo(msg);
                    m_BroadcastCmdType = BroadcastCmdType.ReadConfig;
                    m_Running = false;
                    break;
                default:
                    // Invalid command received; do nothing except log it
                    msg = "Invalid broadcast command received: " + cmdText;
                    LogWarning(msg);
                    break;
            }
        }

        /// <summary>
        /// Handles receipt of command to make a directory
        /// </summary>
        /// <param name="cmdText">XML string containing command</param>
        void OnMsgHandler_CommandReceived(string cmdText)
        {
            try
            {

                var msg = "clsMainProgram.OnMsgHandler_OnMsgHandler_CommandReceived: Command message received: " + cmdText;
                LogDebug(msg);

                m_StatusFile.TaskStatus = EnumTaskStatus.Running;
                m_StatusFile.WriteStatusFile();

                var bSuccess = CreateFolder(cmdText, out var sErrorMessage, "ActiveMQ Broker");

                if (!bSuccess)
                {
                    LogError("Error calling CreateFolder: " + sErrorMessage);
                }

            }
            catch (Exception ex)
            {
                LogError("Error in OnMsgHandler_CommandReceived", ex);
            }

        }

        /// <summary>
        /// Start looping while awaiting control or folder creation command
        /// </summary>
        public void DoFolderCreation()
        {
            const int DB_Query_Interval_Seconds = 30;

            string logMsg;
            var lastLoopRun = DateTime.UtcNow;
            var lastDBQuery = DateTime.UtcNow.Subtract(new TimeSpan(0, 0, DB_Query_Interval_Seconds));

            m_Task = new clsFolderCreateTask(m_MgrSettings);

            LogDebug("Starting DoFolderCreation()");
            m_MgrActive = m_MgrSettings.GetParam("mgractive", false);

            var checkDBQueue = m_MgrSettings.GetParam("CheckDataFolderCreateQueue", false);

            if (!checkDBQueue)
            {
                LogWarning("Manager parameter CheckDataFolderCreateQueue is false; the database will not be contacted");
            }

            m_Running = m_MgrActive;
            var logCount = 0;
            while (m_Running)
            {
                logCount++;
                if (logCount > 60)
                {
                    // Update status every 60 seconds
                    m_StatusFile.WriteStatusFile();
                    logCount = 0;

                    // If it has been > 24 hours since last log entry, tell the log that everything's OK.
                    // Otherwise, it might be several days between log entries.
                    if (DateTime.Compare(DateTime.UtcNow, lastLoopRun.AddHours(24)) > 0)
                    {
                        lastLoopRun = DateTime.UtcNow;
                        logMsg = "Manager running";
                        LogInfo(logMsg);
                    }
                }

                if (checkDBQueue && DateTime.UtcNow.Subtract(lastDBQuery).TotalSeconds >= DB_Query_Interval_Seconds)
                {
                    CheckDBQueue();
                    lastDBQuery = DateTime.UtcNow;
                }

                // Pause 1 second
                System.Threading.Thread.Sleep(1000);
            }

            LogDebug("Exiting DoFolderCreation()");

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
                        LogDebug("Shutdown cmd received");
                        SetNormalShutdownStatus();
                        m_StatusFile.WriteStatusFile();
                        m_MsgHandler.Dispose();
                    }
                    else
                    {
                        // Manager is disabled through MC database
                        logMsg = "Disabled via Manager Control database";
                        LogWarning(logMsg);
                        SetMCDisabledStatus();
                        m_StatusFile.WriteStatusFile();
                        m_MsgHandler.Dispose();
                    }

                    logMsg = "=== Exiting Package Folder Creation Manager ===";
                    LogInfo(logMsg);
                    break;
                default:
                    logMsg = "clsMainProg.DoFolderCreation(); Invalid command type received: " + m_BroadcastCmdType.ToString();
                    LogError(logMsg);
                    break;
            }
        }

        private void LogDebug(string message)
        {
            LogTools.LogDebug(message);
        }

        private void LogInfo(string message)
        {
            LogTools.LogMessage(message);
        }

        private void LogWarning(string message)
        {
            LogTools.LogWarning(message);
        }

        private void LogError(string message, Exception ex = null)
        {
            LogTools.LogError(message, ex);
        }

        private void MessageLoggedHandler(string message, BaseLogger.LogLevels logLevel)
        {
            var timeStamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");

            // Update the status file data
            clsStatusData.MostRecentLogMessage = timeStamp + "; " + message + "; " + logLevel;

            if (logLevel <= BaseLogger.LogLevels.ERROR)
            {
                clsStatusData.AddErrorMessage(timeStamp + "; " + message + "; " + logLevel);
            }

        }

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
        }

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
        }

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
        }

        #endregion
    }
}

