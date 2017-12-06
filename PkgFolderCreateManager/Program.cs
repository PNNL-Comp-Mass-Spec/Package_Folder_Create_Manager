
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//*********************************************************************************************************

using System;
using System.Windows.Forms;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Class that starts application execution
    /// </summary>
    static class Program
    {

        #region "Class variables"

        static clsMainProg m_MainProcess;
        static string ErrMsg;

        #endregion

        #region "Methods"
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start the main program running
            try
            {
                if (m_MainProcess == null)
                {
                    m_MainProcess = new clsMainProg();
                    if (!m_MainProcess.InitMgr())
                    {
                        return;
                    }
                    m_MainProcess.DoFolderCreation();
                }
            }
            catch (Exception ex)
            {
                // Report any exceptions not handled at a lover leve to the system application log
                ErrMsg = "Critical exception starting application: " + ex.Message;
                var ev = new System.Diagnostics.EventLog("Application", ".", "DMS_PkgFolderCreate");
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.EventLogTraceListener("DMS_PkgFolderCreate"));
                System.Diagnostics.Trace.WriteLine(ErrMsg);
                ev.Close();
            }

        }
        #endregion
    }
}
