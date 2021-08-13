
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/16/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PRISM.AppSettings;
using PRISM.Logging;
using PRISMDatabaseUtils;
using PRISMDatabaseUtils.AppSettings;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Main program class for application
    /// </summary>
    internal class clsMainProg : clsLoggerBase
    {
        // Ignore Spelling: cmd, dd, yyyy, HH:mm:ss

        private const string DEFAULT_BASE_LOGFILE_NAME = @"Logs\FolderCreate";

        private enum BroadcastCmdType
        {
            Shutdown,
            ReadConfig
        }

        private MgrSettings mMgrSettings;
        private clsStatusFile mStatusFile;
        private clsMessageHandler mMsgHandler;
        private bool mRunning;
        private bool mMgrActive;
        private BroadcastCmdType mBroadcastCmdType;
        private clsFolderCreateTask mTask;

        public bool CheckDBQueue()
        {
            var success = true;
            var continueLooping = true;

            try
            {
                while (continueLooping)
                {
                    var taskReturn = mTask.RequestTask();
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

                            success = CreateDirectory(mTask.TaskParametersXML, out var sErrorMessage, "T_Data_Folder_Create_Queue");

                            if (success)
                            {
                                mTask.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_SUCCESS);
                            }
                            else
                            {
                                mTask.CloseTask(clsDbTask.EnumCloseOutType.CLOSEOUT_FAILED, sErrorMessage);
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
                LogError("Exception requesting and processing task", ex);
                return false;
            }

            return success;
        }

        protected bool CreateDirectory(string cmdText, out string errorMessage, string source)
        {
            Dictionary<string, string> cmdParams;
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
                mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
                return false;
            }

            // Make the Directory
            if (cmdParams == null)
            {
                errorMessage = "cmdParams is null; Cannot create Directory";
                var msg = errorMessage + " for string " + cmdText;
                LogError(msg);
                mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
                return false;
            }

            try
            {
                mStatusFile.TaskStatusDetail = clsStatusFile.EnumTaskStatusDetail.Running_Tool;

                var dumStr = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; Package " + cmdParams["package"];
                mStatusFile.MostRecentJobInfo = dumStr;
                mStatusFile.WriteStatusFile();

                clsFolderTools.CreateDirectory(mMgrSettings.GetParam("perspective"), cmdParams, source);

                mStatusFile.JobNumber = 0;
                mStatusFile.TaskStatusDetail = clsStatusFile.EnumTaskStatusDetail.No_Task;
                mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.No_Task;
                mStatusFile.WriteStatusFile();
            }
            catch (Exception ex)
            {
                errorMessage = "Exception calling clsFolderTools.CreateDirectory";
                var msg = errorMessage + " with XML command string: " + cmdText;
                LogError(msg, ex);
                mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
                return false;
            }

            return true;
        }

        private Dictionary<string, string> GetLocalManagerSettings()
        {
            var localSettings = new Dictionary<string, string>
            {
                {MgrSettings.MGR_PARAM_MGR_CFG_DB_CONN_STRING, Properties.Settings.Default.MgrCnfgDbConnectStr},
                {MgrSettings.MGR_PARAM_MGR_ACTIVE_LOCAL, Properties.Settings.Default.MgrActive_Local},
                {MgrSettings.MGR_PARAM_MGR_NAME, Properties.Settings.Default.MgrName},
                {MgrSettings.MGR_PARAM_USING_DEFAULTS, Properties.Settings.Default.UsingDefaults}
            };

            return localSettings;
        }

        /// <summary>
        /// Initializes the manager
        /// </summary>
        /// <returns>TRUE for success, FALSE for failure</returns>
        public bool InitMgr()
        {
            // Define the default logging info
            // This will get updated below
            LogTools.CreateFileLogger(DEFAULT_BASE_LOGFILE_NAME, BaseLogger.LogLevels.DEBUG);

            // Create a database logger connected to the Manager Control DB
            // Once the initial parameters have been successfully read,
            // we remove this logger than make a new one using the connection string read from the Manager Control DB
            var defaultDmsConnectionString = Properties.Settings.Default.MgrCnfgDbConnectStr;

            var hostName = System.Net.Dns.GetHostName();
            var applicationName = "PkgFolderCreateManager_" + hostName;
            var defaultDbLoggerConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(defaultDmsConnectionString, applicationName);

            LogTools.CreateDbLogger(defaultDbLoggerConnectionString, "FolderCreate: " + hostName);

            // Get the manager settings
            try
            {
                var localSettings = GetLocalManagerSettings();

                mMgrSettings = new MgrSettingsDB {
                    TraceMode = false
                };
                RegisterEvents(mMgrSettings);
                mMgrSettings.CriticalErrorEvent += ErrorEventHandler;

                var success = mMgrSettings.LoadSettings(localSettings, true);
                if (!success)
                {
                    if (string.Equals(mMgrSettings.ErrMsg, MgrSettings.DEACTIVATED_LOCALLY))
                        throw new ApplicationException(MgrSettings.DEACTIVATED_LOCALLY);

                    throw new ApplicationException("Unable to initialize manager settings class: " + mMgrSettings.ErrMsg);
                }
            }
            catch
            {
                // Failures are logged by clsMgrSettings to local emergency log file
                return false;
            }

            // Setup the loggers
            var logFileNameBase = mMgrSettings.GetParam("LogFilename", "FolderCreate");

            BaseLogger.LogLevels logLevel;
            if (int.TryParse(mMgrSettings.GetParam("DebugLevel"), out var debugLevel))
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
            var logCnStr = mMgrSettings.GetParam("ConnectionString");
            var moduleName = mMgrSettings.GetParam("ModuleName");

            var dbLoggerConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(logCnStr, mMgrSettings.ManagerName);

            LogTools.CreateDbLogger(dbLoggerConnectionString, moduleName);

            LogTools.MessageLogged += MessageLoggedHandler;

            // Make the initial log entry
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var msg = "=== Started Package Folder Creation Manager V" + appVersion + " === ";

            LogTools.LogMessage(msg);

            // Setup the message queue
            mMsgHandler = new clsMessageHandler
            {
                BrokerUri = mMgrSettings.GetParam("MessageQueueURI"),
                CommandQueueName = mMgrSettings.GetParam("ControlQueueName"),
                BroadcastTopicName = mMgrSettings.GetParam("BroadcastQueueTopic"),
                StatusTopicName = mMgrSettings.GetParam("MessageQueueTopicMgrStatus"),
                MgrSettings = mMgrSettings
            };

            if (!mMsgHandler.Init())
            {
                // Most error messages provided by .Init method, but debug message is here for program tracking
                LogDebug("Message handler init error");
                return false;
            }

            LogDebug("Message handler initialized");

            // Connect message handler events
            mMsgHandler.CommandReceived += OnMsgHandler_CommandReceived;
            mMsgHandler.BroadcastReceived += OnMsgHandler_BroadcastReceived;

            // Setup the status file class
            var appPath = PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.GetAppPath();
            var fInfo = new FileInfo(appPath);

            string statusFileNameLoc;
            if (fInfo.DirectoryName == null)
                statusFileNameLoc = "Status.xml";
            else
                statusFileNameLoc = Path.Combine(fInfo.DirectoryName, "Status.xml");

            mStatusFile = new clsStatusFile(statusFileNameLoc, mMsgHandler)
            {
                LogToMsgQueue = mMgrSettings.GetParam("LogStatusToMessageQueue", false),
                MgrName = mMgrSettings.ManagerName
            };

            RegisterEvents(mStatusFile);

            mStatusFile.InitStatusFromFile();

            SetStartupStatus();
            mStatusFile.WriteStatusFile();

            LogDebug("Status file init complete");

            // Register the listeners for the message handler
            mMsgHandler.RegisterListeners();

            // Everything worked
            return true;
        }

        /// <summary>
        /// Handles broadcast messages for control of the manager
        /// </summary>
        /// <param name="cmdText">Text of received message</param>
        private void OnMsgHandler_BroadcastReceived(string cmdText)
        {
            var msg = "clsMainProgram.OnMsgHandler_BroadcastReceived: Broadcast message received: " + cmdText;
            LogDebug(msg);

            clsBroadcastCmd receivedCmd;

            // Parse the received message
            try
            {
                receivedCmd = clsXMLTools.ParseBroadcastXML(cmdText);
            }
            catch (Exception ex)
            {
                msg = "Exception while parsing broadcast data";
                LogError(msg, ex);
                return;
            }

            // Determine if the message applies to this machine
            if (!receivedCmd.MachineList.Contains(mMgrSettings.ManagerName))
            {
                // Received command doesn't apply to this manager
                msg = "Received command not applicable to this manager instance";
                LogDebug(msg);
                return;
            }

            // Get the command and take appropriate action
            switch (receivedCmd.MachCmd.ToLower())
            {
                case "shutdown":
                    msg = "Shutdown message received";
                    LogMessage(msg);
                    mBroadcastCmdType = BroadcastCmdType.Shutdown;
                    mRunning = false;
                    break;
                case "ReadConfig":
                    msg = "Reload config message received";
                    LogMessage(msg);
                    mBroadcastCmdType = BroadcastCmdType.ReadConfig;
                    mRunning = false;
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
        private void OnMsgHandler_CommandReceived(string cmdText)
        {
            try
            {
                var msg = "clsMainProgram.OnMsgHandler_OnMsgHandler_CommandReceived: Command message received: " + cmdText;
                LogDebug(msg);

                mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.Running;
                mStatusFile.WriteStatusFile();

                var bSuccess = CreateDirectory(cmdText, out var sErrorMessage, "ActiveMQ Broker");

                if (!bSuccess)
                {
                    LogError("Error calling CreateDirectory: " + sErrorMessage);
                }
            }
            catch (Exception ex)
            {
                LogError("Error in OnMsgHandler_CommandReceived", ex);
            }
        }

        /// <summary>
        /// Start looping while awaiting control or directory creation command
        /// </summary>
        public void DoDirectoryCreation()
        {
            const int DB_Query_Interval_Seconds = 30;

            string logMsg;
            var lastLoopRun = DateTime.UtcNow;
            var lastDBQuery = DateTime.UtcNow.Subtract(new TimeSpan(0, 0, DB_Query_Interval_Seconds));

            mTask = new clsFolderCreateTask(mMgrSettings);

            LogDebug("Starting DoDirectoryCreation()");
            mMgrActive = mMgrSettings.GetParam("MgrActive", false);

            var checkDBQueue = mMgrSettings.GetParam("CheckDataFolderCreateQueue", false);

            if (!checkDBQueue)
            {
                LogWarning("Manager parameter CheckDataFolderCreateQueue is false; the database will not be contacted");
            }

            mRunning = mMgrActive;
            var logCount = 0;
            while (mRunning)
            {
                logCount++;
                if (logCount > 60)
                {
                    // Update status every 60 seconds
                    mStatusFile.WriteStatusFile();
                    logCount = 0;

                    // If it has been > 24 hours since last log entry, tell the log that everything's OK.
                    // Otherwise, it might be several days between log entries.
                    if (DateTime.Compare(DateTime.UtcNow, lastLoopRun.AddHours(24)) > 0)
                    {
                        lastLoopRun = DateTime.UtcNow;
                        logMsg = "Manager running";
                        LogMessage(logMsg);
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

            LogDebug("Exiting DoDirectoryCreation()");

            // Determine what caused exit from directory creation loop and take appropriate action
            switch (mBroadcastCmdType)
            {
                case BroadcastCmdType.ReadConfig:
                    // TODO: Add code for reloading the configuration
                    break;
                case BroadcastCmdType.Shutdown:
                    // Shutdown command was received
                    if (mMgrActive)
                    {
                        // Exit command was received
                        LogDebug("Shutdown cmd received");
                        SetNormalShutdownStatus();
                        mStatusFile.WriteStatusFile();
                        mMsgHandler.Dispose();
                    }
                    else
                    {
                        // Manager is disabled through MC database
                        logMsg = "Disabled via Manager Control database";
                        LogWarning(logMsg);
                        SetMCDisabledStatus();
                        mStatusFile.WriteStatusFile();
                        mMsgHandler.Dispose();
                    }

                    logMsg = "=== Exiting Package Folder Creation Manager ===";
                    LogMessage(logMsg);
                    break;
                default:
                    logMsg = "DoDirectoryCreation(); Invalid command type received: " + mBroadcastCmdType.ToString();
                    LogError(logMsg);
                    break;
            }
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
            mStatusFile.MgrStatus = clsStatusFile.EnumMgrStatus.Running;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = "";
            mStatusFile.TaskStatusDetail = clsStatusFile.EnumTaskStatusDetail.No_Task;
        }

        /// <summary>
        /// Shortcut to set shutdown status
        /// </summary>
        private void SetNormalShutdownStatus()
        {
            mStatusFile.MgrStatus = clsStatusFile.EnumMgrStatus.Stopped;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = "";
            mStatusFile.TaskStatusDetail = clsStatusFile.EnumTaskStatusDetail.No_Task;
        }

        /// <summary>
        /// Shortcut to set status if manager disabled through manager control db
        /// </summary>
        private void SetMCDisabledStatus()
        {
            mStatusFile.MgrStatus = clsStatusFile.EnumMgrStatus.Disabled_MC;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = clsStatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = "";
            mStatusFile.TaskStatusDetail = clsStatusFile.EnumTaskStatusDetail.No_Task;
        }

        private void RegisterEvents(IEventNotifier sourceClass, bool writeDebugEventsToLog = true)
        {
            if (writeDebugEventsToLog)
            {
                sourceClass.DebugEvent += DebugEventHandler;
            }
            else
            {
                sourceClass.DebugEvent += DebugEventHandlerConsoleOnly;
            }

            sourceClass.StatusEvent += StatusEventHandler;
            sourceClass.ErrorEvent += ErrorEventHandler;
            sourceClass.WarningEvent += WarningEventHandler;
            // sourceClass.ProgressUpdate += ProgressUpdateHandler;
        }

        private void DebugEventHandlerConsoleOnly(string statusMessage)
        {
            LogDebug(statusMessage, writeToLog: false);
        }

        private void DebugEventHandler(string statusMessage)
        {
            LogDebug(statusMessage);
        }
        private void ErrorEventHandler(string message, Exception ex)
        {
            LogError(message);
        }

        private void StatusEventHandler(string message)
        {
            LogMessage(message);
        }

        private void WarningEventHandler(string message)
        {
            LogWarning(message);
        }
    }
}

