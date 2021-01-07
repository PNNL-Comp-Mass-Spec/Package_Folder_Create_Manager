
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//*********************************************************************************************************

using System;
using PRISM;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Class that starts application execution
    /// </summary>
    internal static class Program
    {
        #region "Class variables"

        private static clsMainProg m_MainProcess;

        #endregion

        #region "Methods"

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            // Start the main program running
            try
            {
                if (m_MainProcess == null)
                {
                    m_MainProcess = new clsMainProg();
                    if (!m_MainProcess.InitMgr())
                    {
                        PRISM.Logging.FileLogger.FlushPendingMessages();
                        return;
                    }
                    m_MainProcess.DoDirectoryCreation();
                }
            }
            catch (Exception ex)
            {
                var errMsg = "Critical exception starting application: " + ex.Message;
                ConsoleMsgUtils.ShowWarning(errMsg + "; " + StackTraceFormatter.GetExceptionStackTrace(ex));
                ConsoleMsgUtils.ShowWarning("Exiting clsMainProcess.Main with error code = 1");
            }

            PRISM.Logging.FileLogger.FlushPendingMessages();
        }

        #endregion
    }
}
