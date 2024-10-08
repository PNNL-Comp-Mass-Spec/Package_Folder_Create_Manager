﻿
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
using PRISM;
using PRISM.AppSettings;
using PRISM.Logging;
using PRISMDatabaseUtils;
using PRISMDatabaseUtils.AppSettings;
using PRISMDatabaseUtils.Logging;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Main program class for application
    /// </summary>
    internal class MainProgram : LoggerBase
    {
        // Ignore Spelling: cmd, dd, HH:mm:ss, yyyy

        private const string DEFAULT_BASE_LOGFILE_NAME = @"Logs\FolderCreate";

        private enum BroadcastCmdType
        {
            Shutdown,
            ReadConfig
        }

        private MgrSettings mMgrSettings;
        private readonly string mMgrExeName;
        private readonly string mMgrDirectoryPath;
        private StatusFile mStatusFile;

        /// <summary>
        /// Message queue handler
        /// </summary>
        private MessageHandler mMsgHandler;

        private bool mRunning;
        private bool mMgrActive;
        private BroadcastCmdType mBroadcastCmdType;
        private FolderCreateTask mTask;

        /// <summary>
        /// When true, show additional messages at the console
        /// </summary>
        public bool TraceMode { get; set; }

        public MainProgram()
        {
            var exeInfo = new FileInfo(AppUtils.GetAppPath());
            mMgrExeName = exeInfo.Name;
            mMgrDirectoryPath = exeInfo.DirectoryName;
        }

        public bool CheckDBQueue()
        {
            var success = true;
            var continueLooping = true;

            try
            {
                while (continueLooping)
                {
                    switch (mTask.RequestTask())
                    {
                        case DbTask.EnumRequestTaskResult.NoTaskFound:
                            continueLooping = false;
                            break;

                        case DbTask.EnumRequestTaskResult.ResultError:
                            // Problem with task request; Errors are logged by request method
                            continueLooping = false;
                            success = false;
                            break;

                        case DbTask.EnumRequestTaskResult.TaskFound:

                            success = CreateDirectory(mTask.TaskParametersXML, out var sErrorMessage, "T_Data_Folder_Create_Queue");

                            if (success)
                            {
                                mTask.CloseTask(DbTask.EnumCloseOutType.CLOSEOUT_SUCCESS);
                            }
                            else
                            {
                                mTask.CloseTask(DbTask.EnumCloseOutType.CLOSEOUT_FAILED, sErrorMessage);
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

        /// <summary>
        /// Initializes the database logger in static class PRISM.Logging.LogTools
        /// </summary>
        /// <remarks>Supports both SQL Server and Postgres connection strings</remarks>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="moduleName">Module name used by logger</param>
        /// <param name="traceMode">When true, show additional debug messages at the console</param>
        /// <param name="logLevel">Log threshold level</param>
        private void CreateDbLogger(
            string connectionString,
            string moduleName,
            bool traceMode = false,
            BaseLogger.LogLevels logLevel = BaseLogger.LogLevels.INFO)
        {
            var databaseType = DbToolsFactory.GetServerTypeFromConnectionString(connectionString);

            DatabaseLogger dbLogger = databaseType switch
            {
                DbServerTypes.MSSQLServer => new SQLServerDatabaseLogger(),
                DbServerTypes.PostgreSQL => new PostgresDatabaseLogger(),
                _ => throw new Exception("Unsupported database connection string: should be SQL Server or Postgres")
            };

            dbLogger.ChangeConnectionInfo(moduleName, connectionString);

            LogTools.SetDbLogger(dbLogger, logLevel, traceMode);
        }

        /// <summary>
        /// Create a directory
        /// </summary>
        /// <param name="cmdText">XML settings (see below for example XML)</param>
        /// <param name="errorMessage"></param>
        /// <param name="source"></param>
        /// <returns>True if successful, false if an error</returns>
        protected bool CreateDirectory(string cmdText, out string errorMessage, string source)
        {
            // ReSharper disable CommentTypo

            // Example contents of cmdText

            // <root>
            // <package>264</package>
            // <Path_Local_Root>F:\DataPkgs</Path_Local_Root>
            // <Path_Shared_Root>\\protoapps\DataPkgs\</Path_Shared_Root>
            // <Path_Folder>2011\Public\264_PNWRCE_Dengue_iTRAQ</Path_Folder>
            // <cmd>add</cmd>
            // <Source_DB>DMS_Data_Package</Source_DB>
            // <Source_Table>T_Data_Package</Source_Table>
            // </root>

            // ReSharper restore CommentTypo

            Dictionary<string, string> cmdParams;
            errorMessage = string.Empty;

            // Parse the received string
            try
            {
                cmdParams = XMLTools.ParseCommandXML(cmdText);
            }
            catch (Exception ex)
            {
                errorMessage = "Exception parsing XML command string";
                var msg = errorMessage + ": " + cmdText + Environment.NewLine;
                LogError(msg, ex);
                mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
                return false;
            }

            // Make the Directory
            if (cmdParams == null)
            {
                errorMessage = "cmdParams is null; Cannot create Directory";
                var msg = errorMessage + " for string " + cmdText;
                LogError(msg);
                mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
                return false;
            }

            try
            {
                mStatusFile.TaskStatusDetail = StatusFile.EnumTaskStatusDetail.Running_Tool;

                mStatusFile.MostRecentJobInfo = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; Package " + cmdParams["package"];
                mStatusFile.WriteStatusFile();

                FolderTools.CreateDirectory(mMgrSettings.GetParam("perspective"), cmdParams, source);

                mStatusFile.JobNumber = 0;
                mStatusFile.TaskStatusDetail = StatusFile.EnumTaskStatusDetail.No_Task;
                mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.No_Task;
                mStatusFile.WriteStatusFile();
            }
            catch (Exception ex)
            {
                errorMessage = "Exception calling FolderTools.CreateDirectory";
                var msg = errorMessage + " with XML command string: " + cmdText;
                LogError(msg, ex);
                mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.Failed;
                mStatusFile.WriteStatusFile();
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
            // Define the default logging info
            // This will get updated below
            LogTools.CreateFileLogger(DEFAULT_BASE_LOGFILE_NAME, BaseLogger.LogLevels.DEBUG);

            // Create a database logger connected to the DMS database on prismdb2 (previously, Manager_Control on Proteinseqs)

            // Once the initial parameters have been successfully read,
            // we remove this logger than make a new one using the connection string read from the Manager Control DB
            string defaultDmsConnectionString;

            // Open PkgFolderCreateManager.exe.db.config to look for setting MgrCnfgDbConnectStr, so we know which server to log to by default
            var dmsConnectionStringFromConfig = GetXmlConfigDefaultMgrConnectionString();

            if (string.IsNullOrWhiteSpace(dmsConnectionStringFromConfig))
            {
                // Use the hard-coded default that points to the DMS database on prismdb2 (previously, DMS5 on Gigasax)
                defaultDmsConnectionString = Properties.Settings.Default.MgrCnfgDbConnectStr;
            }
            else
            {
                // Use the connection string from PkgFolderCreateManager.exe.config
                defaultDmsConnectionString = dmsConnectionStringFromConfig;
            }

            var hostName = System.Net.Dns.GetHostName();
            var applicationName = "PkgFolderCreateManager_" + hostName;
            var defaultDbLoggerConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(defaultDmsConnectionString, applicationName);

            CreateDbLogger(defaultDbLoggerConnectionString, "PkgFolderCreate: " + hostName, TraceMode);

            // Get the manager settings
            try
            {
                mMgrSettings = new MgrSettingsDB
                {
                    TraceMode = TraceMode
                };

                var localSettings = LoadMgrSettingsFromFile();

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
                // Failures are logged by MgrSettings to local emergency log file
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

            // This connection string points to the DMS database on prismdb2 (previously, DMS_Pipeline on Gigasax)
            var logCnStr = mMgrSettings.GetParam("ConnectionString");
            var moduleName = mMgrSettings.GetParam("ModuleName");

            var dbLoggerConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(logCnStr, mMgrSettings.ManagerName);

            CreateDbLogger(dbLoggerConnectionString, moduleName);

            LogTools.MessageLogged += MessageLoggedHandler;

            // Make the initial log entry
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var msg = "=== Started Package Folder Creation Manager V" + appVersion + " === ";

            LogTools.LogMessage(msg);

            // Set up the message queue handler
            // The handler is unused in 2023 since manager parameter LogStatusToMessageQueue is False and OnMsgHandler_CommandReceived and OnMsgHandler_BroadcastReceived are no longer used

            mMsgHandler = new MessageHandler
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

            // Connect message handler events (retired in 2023)
            // mMsgHandler.CommandReceived += OnMsgHandler_CommandReceived;
            // mMsgHandler.BroadcastReceived += OnMsgHandler_BroadcastReceived;

            // Set up the status file class
            var statusFileNameLoc = mMgrDirectoryPath == null ? "Status.xml" : Path.Combine(mMgrDirectoryPath, "Status.xml");

            mStatusFile = new StatusFile(statusFileNameLoc, mMsgHandler)
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
        /// Extract the value DefaultDMSConnString from PkgFolderCreateManager.exe.config (or from PkgFolderCreateManager.exe.db.config)
        /// </summary>
        private string GetXmlConfigDefaultMgrConnectionString()
        {
            return GetXmlConfigFileSetting("MgrCnfgDbConnectStr");
        }

        /// <summary>
        /// Extract the value for the given setting from PkgFolderCreateManager.exe.config
        /// If the setting name is MgrCnfgDbConnectStr or DefaultDMSConnString, first checks file PkgFolderCreateManager.exe.db.config
        /// </summary>
        /// <remarks>Uses a simple text reader in case the file has malformed XML</remarks>
        /// <returns>Setting value if found, otherwise an empty string</returns>
        private string GetXmlConfigFileSetting(string settingName)
        {
            if (string.IsNullOrWhiteSpace(settingName))
                throw new ArgumentException("Setting name cannot be blank", nameof(settingName));

            var configFilePaths = new List<string>();

            if (settingName.Equals("MgrCnfgDbConnectStr", StringComparison.OrdinalIgnoreCase) ||
                settingName.Equals("DefaultDMSConnString", StringComparison.OrdinalIgnoreCase))
            {
                configFilePaths.Add(Path.Combine(mMgrDirectoryPath, mMgrExeName + ".db.config"));
            }

            configFilePaths.Add(Path.Combine(mMgrDirectoryPath, mMgrExeName + ".config"));

            var mgrSettings = new MgrSettings();
            RegisterEvents(mgrSettings);

            var valueFound = mgrSettings.GetXmlConfigFileSetting(configFilePaths, settingName, out var settingValue);

            if (valueFound)
                return settingValue;

            return string.Empty;
        }

        /// <summary>
        /// Loads the initial settings from application config file AnalysisManagerProg.exe.config
        /// </summary>
        /// <remarks>This method is public because CodeTest uses it</remarks>
        /// <returns>String dictionary containing initial settings if successful; null on error</returns>
        public Dictionary<string, string> LoadMgrSettingsFromFile()
        {
            // Note: When you are editing this project using the Visual Studio IDE, if you edit the values
            //  ->My Project>Settings.settings, then when you run the program (from within the IDE), it
            //  will update file AnalysisManagerProg.exe.config with your settings
            // The manager will exit if the "UsingDefaults" value is "True", thus you need to have
            //  "UsingDefaults" be "False" to run (and/or debug) the application

            // We should be able to load settings auto-magically using "Properties.Settings.Default.MgrCnfgDbConnectStr" and "Properties.Settings.Default.MgrName"
            // But that mechanism only works if the AnalysisManagerProg.exe.config is of the form:
            //   <applicationSettings>
            //     <AnalysisManagerProg.Properties.Settings>
            //       <setting name="MgrActive_Local" serializeAs="String">

            // Older VB.NET based versions of the AnalysisManagerProg.exe.config file have:
            //   <applicationSettings>
            //     <My.MySettings>
            //       <setting name="MgrActive_Local" serializeAs="String">

            // Method ReadMgrSettingsFile() works with both versions of the .exe.config file

            // Construct the path to the config document
            var configFilePath = Path.Combine(mMgrDirectoryPath, mMgrExeName + ".config");

            var mgrSettings = (mMgrSettings as MgrSettingsDB)?.LoadMgrSettingsFromFile(configFilePath);

            if (mgrSettings == null)
                return null;

            // Manager Config DB connection string
            if (!mgrSettings.ContainsKey(MgrSettings.MGR_PARAM_MGR_CFG_DB_CONN_STRING))
            {
                mgrSettings.Add(MgrSettings.MGR_PARAM_MGR_CFG_DB_CONN_STRING, Properties.Settings.Default.MgrCnfgDbConnectStr);
            }

            // Manager active flag
            if (!mgrSettings.ContainsKey(MgrSettings.MGR_PARAM_MGR_ACTIVE_LOCAL))
            {
                mgrSettings.Add(MgrSettings.MGR_PARAM_MGR_ACTIVE_LOCAL, "False");
            }

            // Manager name
            // The manager name may contain $ComputerName$
            // If it does, InitializeMgrSettings in MgrSettings will replace "$ComputerName$ with the local host name
            if (!mgrSettings.ContainsKey(MgrSettings.MGR_PARAM_MGR_NAME))
            {
                mgrSettings.Add(MgrSettings.MGR_PARAM_MGR_NAME, "LoadMgrSettingsFromFile__Undefined_manager_name");
            }

            // Default settings in use flag
            if (!mgrSettings.ContainsKey(MgrSettings.MGR_PARAM_USING_DEFAULTS))
            {
                mgrSettings.Add(MgrSettings.MGR_PARAM_USING_DEFAULTS, Properties.Settings.Default.UsingDefaults);
            }

            if (TraceMode)
            {
                ShowTrace("Settings loaded from " + PathUtils.CompactPathString(configFilePath, 60));
                MgrSettings.ShowDictionaryTrace(mgrSettings);
            }

            return mgrSettings;
        }

        /// <summary>
        /// Handles broadcast messages for control of the manager
        /// </summary>
        /// <param name="cmdText">Text of received message</param>
        [Obsolete("Deprecated in 2023")]
        // ReSharper disable once UnusedMember.Local
        private void OnMsgHandler_BroadcastReceived(string cmdText)
        {
            var msg = "MainProgram.OnMsgHandler_BroadcastReceived: Broadcast message received: " + cmdText;
            LogDebug(msg);

            BroadcastCmd receivedCmd;

            // Parse the received message
            try
            {
                receivedCmd = XMLTools.ParseBroadcastXML(cmdText);
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
        [Obsolete("Deprecated in 2023")]
        // ReSharper disable once UnusedMember.Local
        private void OnMsgHandler_CommandReceived(string cmdText)
        {
            try
            {
                var msg = "MainProgram.OnMsgHandler_OnMsgHandler_CommandReceived: Command message received: " + cmdText;
                LogDebug(msg);

                mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.Running;
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

            mTask = new FolderCreateTask(mMgrSettings);

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
            StatusData.MostRecentLogMessage = timeStamp + "; " + message + "; " + logLevel;

            if (logLevel <= BaseLogger.LogLevels.ERROR)
            {
                StatusData.AddErrorMessage(timeStamp + "; " + message + "; " + logLevel);
            }
        }

        /// <summary>
        /// Shortcut to set startup status
        /// </summary>
        private void SetStartupStatus()
        {
            mStatusFile.MgrStatus = StatusFile.EnumMgrStatus.Running;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = string.Empty;
            mStatusFile.TaskStatusDetail = StatusFile.EnumTaskStatusDetail.No_Task;
        }

        /// <summary>
        /// Shortcut to set shutdown status
        /// </summary>
        private void SetNormalShutdownStatus()
        {
            mStatusFile.MgrStatus = StatusFile.EnumMgrStatus.Stopped;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = string.Empty;
            mStatusFile.TaskStatusDetail = StatusFile.EnumTaskStatusDetail.No_Task;
        }

        /// <summary>
        /// Shortcut to set status if manager disabled through manager control db
        /// </summary>
        private void SetMCDisabledStatus()
        {
            mStatusFile.MgrStatus = StatusFile.EnumMgrStatus.Disabled_MC;
            mStatusFile.Tool = "NA";
            mStatusFile.TaskStatus = StatusFile.EnumTaskStatus.No_Task;
            mStatusFile.Dataset = "NA";
            mStatusFile.CurrentOperation = string.Empty;
            mStatusFile.TaskStatusDetail = StatusFile.EnumTaskStatusDetail.No_Task;
        }

        /// <summary>
        /// Show a trace message only if TraceMode is true
        /// </summary>
        /// <param name="message"></param>
        /// <param name="emptyLinesBeforeMessage"></param>
        private void ShowTrace(string message, int emptyLinesBeforeMessage = 1)
        {
            if (!TraceMode)
                return;

            ShowTraceMessage(message, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Show a message at the console, preceded by a time stamp
        /// </summary>
        /// <param name="message"></param>
        /// <param name="emptyLinesBeforeMessage"></param>
        public static void ShowTraceMessage(string message, int emptyLinesBeforeMessage = 1)
        {
            BaseLogger.ShowTraceMessage(message, false, "  ", emptyLinesBeforeMessage);
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
