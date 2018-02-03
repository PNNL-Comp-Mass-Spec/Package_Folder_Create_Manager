﻿using System;

namespace PkgFolderCreateManager
{
    public abstract class clsLoggerBase
    {
        /// <summary>
        /// Log a debug message
        /// </summary>
        /// <param name="statusMessage"></param>
        /// <param name="writeToLog"></param>
        protected static void LogDebug(string statusMessage, bool writeToLog = true)
        {
            clsUtilityMethods.ReportDebug(statusMessage, writeToLog);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="logToDb">When true, log the message to the database and the local log file</param>
        protected static void LogError(string errorMessage, bool logToDb = false)
        {
            clsUtilityMethods.LogError(errorMessage, logToDb);
        }

        /// <summary>
        /// Log an error message and exception
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="ex">Exception to log</param>
        protected static void LogError(string errorMessage, Exception ex)
        {
            clsUtilityMethods.LogError(errorMessage, ex);
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="warningMessage">Warning message</param>
        /// <param name="logToDb">When true, log the message to the database and the local log file</param>
        protected static void LogWarning(string warningMessage, bool logToDb = false)
        {
            clsUtilityMethods.LogWarning(warningMessage, logToDb);
        }

        /// <summary>
        /// Shows information about an exception at the console and in the log file
        /// </summary>
        /// <param name="errorMessage">Error message (do not include ex.message)</param>
        /// <param name="ex">Exception</param>
        protected static void ReportStatus(string errorMessage, Exception ex)
        {
            clsUtilityMethods.ReportStatus(errorMessage, ex);
        }

        /// <summary>
        /// Show a status message at the console and optionally include in the log file
        /// </summary>
        /// <param name="statusMessage">Status message</param>
        /// <param name="isDebug">True if a debug level message</param>
        protected static void ReportStatus(string statusMessage, bool isDebug = false)
        {
            clsUtilityMethods.ReportStatus(statusMessage, isDebug);
        }

    }
}
