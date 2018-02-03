﻿//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2010, Battelle Memorial Institute
// Created 09/14/2010
//
//*********************************************************************************************************

using System;
using PRISM;
using PRISM.Logging;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Holds static utility methods that are put here to avoid cluttering up other classes
    /// </summary>
    public static class clsUtilityMethods
    {

        #region "Methods"

        /// <summary>
        /// Convert bytes to Gigabytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static double BytesToGB(long bytes)
        {
            return bytes / 1024.0 / 1024.0 / 1024.0;
        }

        /// <summary>
        /// Convert string to bool; default false if an error
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool CBoolSafe(string value)
        {
            return CBoolSafe(value, false);
        }

        /// <summary>
        /// Convert a string value to a boolean
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static bool CBoolSafe(string value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (bool.TryParse(value, out var blnValue))
                return blnValue;

            return defaultValue;
        }

        /// <summary>
        /// Convert a string value to an integer
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static int CIntSafe(string value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (int.TryParse(value, out var intValue))
                return intValue;

            return defaultValue;
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="logToDb">When true, log the message to the database and the local log file</param>
        public static void LogError(string errorMessage, bool logToDb = false)
        {
            ConsoleMsgUtils.ShowError(errorMessage);

            var loggerType = logToDb ? clsLogTools.LoggerTypes.LogDb : clsLogTools.LoggerTypes.LogFile;
            clsLogTools.WriteLog(loggerType, BaseLogger.LogLevels.ERROR, errorMessage);
        }

        /// <summary>
        /// Log an error message and exception
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <param name="ex">Exception to log</param>
        public static void LogError(string errorMessage, Exception ex)
        {
            var formattedError = ConsoleMsgUtils.ShowError(errorMessage, ex);

            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, formattedError, ex);
        }

        /// <summary>
        /// Show a status message at the console and optionally include in the log file
        /// </summary>
        /// <param name="statusMessage">Status message</param>
        /// <param name="isError">True if this is an error</param>
        /// <param name="writeToLog">True to write to the log file; false to only display at console</param>
        public static void LogMessage(string statusMessage, bool isError = false, bool writeToLog = true)
        {
            if (isError)
            {
                ConsoleMsgUtils.ShowError(statusMessage, false);
            }
            else
            {
                Console.WriteLine(statusMessage);
            }

            if (!writeToLog)
                return;

            if (isError)
            {
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, statusMessage);
            }
            else
            {
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.INFO, statusMessage);
            }
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="warningMessage">Warning message</param>
        /// <param name="logToDb">When true, log the message to the database and the local log file</param>
        public static void LogWarning(string warningMessage, bool logToDb = false)
        {
            ConsoleMsgUtils.ShowWarning(warningMessage);

            var loggerType = logToDb ? clsLogTools.LoggerTypes.LogDb : clsLogTools.LoggerTypes.LogFile;
            clsLogTools.WriteLog(loggerType, BaseLogger.LogLevels.WARN, warningMessage);
        }

        /// <summary>
        /// Show a debug message, and optionally log to disk
        /// </summary>
        /// <param name="message"></param>
        /// <param name="writeToLog"></param>
        public static void LogDebug(string message, bool writeToLog = false)
        {
            ConsoleMsgUtils.ShowDebug(message);

            if (!writeToLog)
                return;

            var loggerType = clsLogTools.LoggerTypes.LogFile;
            clsLogTools.WriteLog(loggerType, BaseLogger.LogLevels.DEBUG, message);

        }


        #endregion
    }
}
