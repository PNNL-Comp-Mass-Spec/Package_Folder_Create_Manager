
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//
// Last modified 06/18/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PkgFolderCreateManager
{
    static class Program
    {
        //*********************************************************************************************************
        // Class that starts application execution
        //**********************************************************************************************************

        #region "Class variables"
            static clsMainProg m_MainProcess = null;
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
                catch (Exception Err)
                {
                    // Report any exceptions not handled at a lover leve to the system application log
                    ErrMsg = "Critical exception starting application: " + Err.Message;
                    System.Diagnostics.EventLog ev = new System.Diagnostics.EventLog("Application", ".", "DMS_PkgFolderCreate");
                    System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.EventLogTraceListener("DMS_PkgFolderCreate"));
                    System.Diagnostics.Trace.WriteLine(ErrMsg);
                    ev.Close();
                }

                //            Application.Run();
            }    // End sub
        #endregion
    }    // End class
}    // End namespace
