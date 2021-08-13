
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
        private static clsMainProg mMainProcess;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            // Start the main program running
            try
            {
                if (mMainProcess == null)
                {
                    mMainProcess = new clsMainProg();
                    if (!mMainProcess.InitMgr())
                    {
                        PRISM.Logging.FileLogger.FlushPendingMessages();
                        return;
                    }
                    mMainProcess.DoDirectoryCreation();
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
    }
}
