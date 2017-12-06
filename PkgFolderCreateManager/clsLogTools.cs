﻿
//*********************************************************************************************************
// Written by Dave Clark and Matthew Monroe for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 09/10/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using log4net;
using log4net.Appender;
using log4net.Util.TypeConverters;

// This assembly attribute tells Log4Net where to find the config file
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "Logging.config", Watch = true)]

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Class for handling logging via Log4Net
    /// </summary>
    public class clsLogTools
    {

        #region "Constants"

        private const string LOG_FILE_APPENDER = "FileAppender";

        /// <summary>
        /// Date format for log file names
        /// </summary>
        public const string LOG_FILE_DATECODE = "MM-dd-yyyy";

        private const string LOG_FILE_MATCH_SPEC = "??-??-????";

        private const string LOG_FILE_DATE_REGEX = @"(?<Month>\d+)-(?<Day>\d+)-(?<Year>\d{4,4})";

        private const string LOG_FILE_EXTENSION = ".txt";

        private const int OLD_LOG_FILE_AGE_THRESHOLD_DAYS = 32;

        #endregion

        #region "Enums"

        /// <summary>
        /// Log levels
        /// </summary>
        public enum LogLevels
        {
            /// <summary>
            /// Debug message
            /// </summary>
            DEBUG = 5,

            /// <summary>
            /// Informational message
            /// </summary>
            INFO = 4,

            /// <summary>
            /// Warning message
            /// </summary>
            WARN = 3,

            /// <summary>
            /// Error message
            /// </summary>
            ERROR = 2,

            /// <summary>
            /// Fatal error message
            /// </summary>
            FATAL = 1
        }

        /// <summary>
        /// Log types
        /// </summary>
        public enum LoggerTypes
        {
            /// <summary>
            /// Log to a log file
            /// </summary>
            LogFile,

            /// <summary>
            /// Log to the database and to the log file
            /// </summary>
            LogDb,

            /// <summary>
            /// Log to the system event log and to the log file
            /// </summary>
            LogSystem
        }

        #endregion

        #region "Class variables"

        private static readonly ILog m_FileLogger = LogManager.GetLogger("FileLogger");
        private static readonly ILog m_DbLogger = LogManager.GetLogger("DbLogger");
        private static readonly ILog m_SysLogger = LogManager.GetLogger("SysLogger");

        private static string m_FileDate = "";
        private static string m_BaseFileName = "";
        private static FileAppender m_FileAppender;

        #endregion

        #region "Properties"

        /// <summary>
        /// File path for the current log file used by the FileAppender
        /// </summary>
        public static string CurrentFileAppenderPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_FileAppender?.File))
                {
                    return string.Empty;
                }

                return m_FileAppender.File;
            }
        }

        /// <summary>
        /// Tells calling program file debug status
        /// </summary>
        /// <returns>TRUE if debug level enabled for file logger; FALSE otherwise</returns>
        /// <remarks></remarks>
        public static bool FileLogDebugEnabled => m_FileLogger.IsDebugEnabled;

        #endregion

        #region "Methods"

        /// <summary>
        /// Write a message to the logging system
        /// </summary>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        public static void WriteLog(LoggerTypes loggerType, LogLevels logLevel, string message)
        {
            WriteLogWork(loggerType, logLevel, message, null);
        }

        /// <summary>
        /// Write a message and exception to the logging system
        /// </summary>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        /// <param name="ex">Exception to be logged</param>
        public static void WriteLog(LoggerTypes loggerType, LogLevels logLevel, string message, Exception ex)
        {
            WriteLogWork(loggerType, logLevel, message, ex);
        }

        /// <summary>
        /// Write a message and possibly an exception to the logging system
        /// </summary>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        /// <param name="ex">Exception to be logged; null if no exception</param>
        private static void WriteLogWork(LoggerTypes loggerType, LogLevels logLevel, string message, Exception ex)
        {
            ILog myLogger;

            // Establish which logger will be used
            switch (loggerType)
            {
                case LoggerTypes.LogDb:
                    myLogger = m_DbLogger;
                    message = System.Net.Dns.GetHostName() + ": " + message;
                    break;
                case LoggerTypes.LogFile:
                    myLogger = m_FileLogger;

                    // Check to determine if a new file should be started
                    var testFileDate = DateTime.Now.ToString(LOG_FILE_DATECODE);
                    if (!string.Equals(testFileDate, m_FileDate))
                    {
                        m_FileDate = testFileDate;
                        ChangeLogFileName();
                    }
                    break;
                case LoggerTypes.LogSystem:
                    myLogger = m_SysLogger;
                    break;
                default:
                    throw new Exception("Invalid logger type specified");
            }

            // Update the status file data
            clsStatusData.MostRecentLogMessage = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; " + message + "; " + logLevel;

            if (myLogger == null)
                return;

            // Send the log message
            switch (logLevel)
            {
                case LogLevels.DEBUG:
                    if (myLogger.IsDebugEnabled)
                    {
                        if (ex == null)
                        {
                            myLogger.Debug(message);
                        }
                        else
                        {
                            myLogger.Debug(message, ex);
                        }
                    }
                    break;
                case LogLevels.ERROR:
                    clsStatusData.AddErrorMessage(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "; " + message + "; " + logLevel);
                    if (myLogger.IsErrorEnabled)
                    {
                        if (ex == null)
                        {
                            myLogger.Error(message);
                        }
                        else
                        {
                            myLogger.Error(message, ex);
                        }
                    }
                    break;
                case LogLevels.FATAL:
                    if (myLogger.IsFatalEnabled)
                    {
                        if (ex == null)
                        {
                            myLogger.Fatal(message);
                        }
                        else
                        {
                            myLogger.Fatal(message, ex);
                        }
                    }
                    break;
                case LogLevels.INFO:
                    if (myLogger.IsInfoEnabled)
                    {
                        if (ex == null)
                        {
                            myLogger.Info(message);
                        }
                        else
                        {
                            myLogger.Info(message, ex);
                        }
                    }
                    break;
                case LogLevels.WARN:
                    if (myLogger.IsWarnEnabled)
                    {
                        if (ex == null)
                        {
                            myLogger.Warn(message);
                        }
                        else
                        {
                            myLogger.Warn(message, ex);
                        }
                    }
                    break;
                default:
                    throw new Exception("Invalid log level specified");
            }
        }

        /// <summary>
        /// Update the log file's base name
        /// </summary>
        /// <param name="baseName"></param>
        /// <remarks>Will append today's date to the base name</remarks>
        public static void ChangeLogFileBaseName(string baseName)
        {
            m_BaseFileName = baseName;
            ChangeLogFileName();
        }

        /// <summary>
        /// Changes the base log file name
        /// </summary>
        public static void ChangeLogFileName()
        {
            m_FileDate = DateTime.Now.ToString(LOG_FILE_DATECODE);
            ChangeLogFileName(m_BaseFileName + "_" + m_FileDate + LOG_FILE_EXTENSION);
        }

        /// <summary>
        /// Changes the base log file name
        /// </summary>
        /// <param name="relativeFilePath">Log file base name and path (relative to program folder)</param>
        /// <remarks>This method is called by the Mage, Ascore, and Multialign plugins</remarks>
        public static void ChangeLogFileName(string relativeFilePath)
        {
            // Get a list of appenders
            var appendList = FindAppenders(LOG_FILE_APPENDER);
            if (appendList == null)
            {
                WriteLog(LoggerTypes.LogSystem, LogLevels.WARN, "Unable to change file name. No appender found");
                return;
            }

            foreach (var selectedAppender in appendList)
            {
                // Convert the IAppender object to a FileAppender instance
                if (!(selectedAppender is FileAppender appenderToChange))
                {
                    WriteLog(LoggerTypes.LogSystem, LogLevels.ERROR, "Unable to convert appender");
                    return;
                }

                // Change the file name and activate change
                appenderToChange.File = relativeFilePath;
                appenderToChange.ActivateOptions();
            }
        }

        /// <summary>
        /// Gets the specified appender
        /// </summary>
        /// <param name="appenderName">Name of appender to find</param>
        /// <returns>List(IAppender) objects if found; null otherwise</returns>
        private static IEnumerable<IAppender> FindAppenders(string appenderName)
        {

            // Get a list of the current loggers
            var loggerList = LogManager.GetCurrentLoggers();
            if (loggerList.GetLength(0) < 1)
                return null;

            // Create a List of appenders matching the criteria for each logger
            var retList = new List<IAppender>();
            foreach (var testLogger in loggerList)
            {
                foreach (var testAppender in testLogger.Logger.Repository.GetAppenders())
                {
                    if (testAppender.Name == appenderName)
                        retList.Add(testAppender);
                }
            }

            // Return the list of appenders, if any found
            if (retList.Count > 0)
            {
                return retList;
            }

            return null;
        }

        /// <summary>
        /// Sets the file logging level via an integer value (Overloaded)
        /// </summary>
        /// <param name="logLevel">Integer corresponding to level (1-5, 5 being most verbose)</param>
        public static void SetFileLogLevel(int logLevel)
        {
            var logLevelEnumType = typeof(LogLevels);

            // Verify input level is a valid log level
            if (!Enum.IsDefined(logLevelEnumType, logLevel))
            {
                WriteLog(LoggerTypes.LogFile, LogLevels.ERROR, "Invalid value specified for level: " + logLevel);
                return;
            }

            // Convert input integer into the associated enum
            var logLevelEnum = (LogLevels)Enum.Parse(logLevelEnumType, logLevel.ToString(CultureInfo.InvariantCulture));

            SetFileLogLevel(logLevelEnum);
        }

        /// <summary>
        /// Sets file logging level based on enumeration (Overloaded)
        /// </summary>
        /// <param name="logLevel">LogLevels value defining level (Debug is most verbose)</param>
        public static void SetFileLogLevel(LogLevels logLevel)
        {
            var logger = (log4net.Repository.Hierarchy.Logger)m_FileLogger.Logger;

            switch (logLevel)
            {
                case LogLevels.DEBUG:
                    logger.Level = logger.Hierarchy.LevelMap["DEBUG"];
                    break;
                case LogLevels.ERROR:
                    logger.Level = logger.Hierarchy.LevelMap["ERROR"];
                    break;
                case LogLevels.FATAL:
                    logger.Level = logger.Hierarchy.LevelMap["FATAL"];
                    break;
                case LogLevels.INFO:
                    logger.Level = logger.Hierarchy.LevelMap["INFO"];
                    break;
                case LogLevels.WARN:
                    logger.Level = logger.Hierarchy.LevelMap["WARN"];
                    break;
            }
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <param name="logFilePath"></param>
        private static void ArchiveOldLogs(string logFilePath)
        {
            var targetPath = "??";

            try
            {
                var currentLogFile = new FileInfo(logFilePath);

                var matchSpec = "*_" + LOG_FILE_MATCH_SPEC + LOG_FILE_EXTENSION;

                var logDirectory = currentLogFile.Directory;
                if (logDirectory == null)
                {
                    WriteLog(LoggerTypes.LogFile, LogLevels.WARN, "Error archiving old log files; cannot determine the parent directory of " + currentLogFile);
                    return;
                }

                var logFiles = logDirectory.GetFiles(matchSpec);

                var matcher = new Regex(LOG_FILE_DATE_REGEX, RegexOptions.Compiled);

                foreach (var logFile in logFiles)
                {
                    var match = matcher.Match(logFile.Name);

                    if (!match.Success)
                        continue;

                    var logFileYear = int.Parse(match.Groups["Year"].Value);
                    var logFileMonth = int.Parse(match.Groups["Month"].Value);
                    var logFileDay = int.Parse(match.Groups["Day"].Value);

                    var logDate = new DateTime(logFileYear, logFileMonth, logFileDay);

                    if (DateTime.Now.Subtract(logDate).TotalDays <= OLD_LOG_FILE_AGE_THRESHOLD_DAYS)
                        continue;

                    var targetDirectory = new DirectoryInfo(Path.Combine(logDirectory.FullName, logFileYear.ToString()));
                    if (!targetDirectory.Exists)
                        targetDirectory.Create();

                    targetPath = Path.Combine(targetDirectory.FullName, logFile.Name);

                    logFile.MoveTo(targetPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog(LoggerTypes.LogFile, LogLevels.ERROR, "Error moving old log file to " + targetPath, ex);
            }
        }

        /// <summary>
        /// Creates a file appender
        /// </summary>
        /// <param name="logFileNameBase">Base name for log file</param>
        /// <returns>A configured file appender</returns>
        private static FileAppender CreateFileAppender(string logFileNameBase)
        {
            m_FileDate = DateTime.Now.ToString(LOG_FILE_DATECODE);
            m_BaseFileName = logFileNameBase;

            var layout = new log4net.Layout.PatternLayout
            {
                ConversionPattern = "%date{MM/dd/yyyy HH:mm:ss}, %message, %level,%newline"
            };
            layout.ActivateOptions();

            var returnAppender = new FileAppender
            {
                Name = LOG_FILE_APPENDER,
                File = m_BaseFileName + "_" + m_FileDate + LOG_FILE_EXTENSION,
                AppendToFile = true,
                Layout = layout
            };

            returnAppender.ActivateOptions();

            return returnAppender;
        }

        /// <summary>
        /// Configures the file logger
        /// </summary>
        /// <param name="logFileName">Base name for log file</param>
        /// <param name="logLevel">Debug level for file logger (1-5, 5 being most verbose)</param>
        public static void CreateFileLogger(string logFileName, int logLevel)
        {
            var curLogger = (log4net.Repository.Hierarchy.Logger)m_FileLogger.Logger;
            m_FileAppender = CreateFileAppender(logFileName);
            curLogger.AddAppender(m_FileAppender);

            ArchiveOldLogs(m_FileAppender.File);

            SetFileLogLevel(logLevel);
        }

        /// <summary>
        /// Configures the file logger
        /// </summary>
        /// <param name="logFileName">Base name for log file</param>
        /// <param name="logLevel">Debug level for file logger</param>
        public static void CreateFileLogger(string logFileName, LogLevels logLevel)
        {
            CreateFileLogger(logFileName, (int)logLevel);
        }

        /// <summary>
        /// Configures the database logger
        /// </summary>
        /// <param name="connStr">Database connection string</param>
        /// <param name="moduleName">Module name used by logger</param>
        public static void CreateDbLogger(string connStr, string moduleName)
        {
            var curLogger = (log4net.Repository.Hierarchy.Logger)m_DbLogger.Logger;
            curLogger.Level = log4net.Core.Level.Info;

            curLogger.AddAppender(CreateDbAppender(connStr, moduleName, "DbAppender"));

            if (m_FileAppender == null)
            {
                return;
            }

            var addFileAppender = true;
            foreach (var appender in curLogger.Appenders)
            {
                if (ReferenceEquals(appender, m_FileAppender))
                {
                    addFileAppender = false;
                    break;
                }
            }

            if (addFileAppender)
            {
                curLogger.AddAppender(m_FileAppender);
            }
        }

        /// <summary>
        /// Creates a database appender
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="moduleName">Module name used by logger</param>
        /// <param name="appenderName">Appender name</param>
        /// <returns>ADONet database appender</returns>
        public static AdoNetAppender CreateDbAppender(string connectionString, string moduleName, string appenderName)
        {
            var returnAppender = new AdoNetAppender
            {
                BufferSize = 1,
                ConnectionType = "System.Data.SqlClient.SqlConnection, System.Data, Version=1.0.3300.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                ConnectionString = connectionString,
                CommandType = CommandType.StoredProcedure,
                CommandText = "PostLogEntry",
                Name = appenderName
            };

            // Type parameter
            var typeParam = new AdoNetAppenderParameter
            {
                ParameterName = "@type",
                DbType = DbType.String,
                Size = 50,
                Layout = CreateLayout("%level")
            };
            returnAppender.AddParameter(typeParam);

            // Message parameter
            var msgParam = new AdoNetAppenderParameter
            {
                ParameterName = "@message",
                DbType = DbType.String,
                Size = 4000,
                Layout = CreateLayout("%message")
            };
            returnAppender.AddParameter(msgParam);

            // PostedBy parameter
            var postByParam = new AdoNetAppenderParameter
            {
                ParameterName = "@postedBy",
                DbType = DbType.String,
                Size = 128,
                Layout = CreateLayout(moduleName)
            };
            returnAppender.AddParameter(postByParam);

            returnAppender.ActivateOptions();

            return returnAppender;
        }

        /// <summary>
        /// Creates a layout object for a Db appender parameter
        /// </summary>
        /// <param name="layoutStr">Name of parameter</param>
        /// <returns></returns>
        private static log4net.Layout.IRawLayout CreateLayout(string layoutStr)
        {
            var layoutConvert = new log4net.Layout.RawLayoutConverter();
            var returnLayout = new log4net.Layout.PatternLayout
            {
                ConversionPattern = layoutStr
            };
            returnLayout.ActivateOptions();

            var retItem = (log4net.Layout.IRawLayout)layoutConvert.ConvertFrom(returnLayout);

            if (retItem == null)
            {
                throw new ConversionNotSupportedException("Error converting a PatternLayout to IRawLayout");
            }

            return retItem;
        }

        #endregion

    }
}
