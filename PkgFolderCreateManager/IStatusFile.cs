
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//*********************************************************************************************************

namespace PkgFolderCreateManager
{

    #region "Enums"

    /// <summary>
    /// Manager status constants
    /// </summary>
    public enum EnumMgrStatus : short
    {
        Stopped,
        Stopped_Error,
        Running,
        Disabled_Local,
        Disabled_MC
    }

    /// <summary>
    /// Task status constants
    /// </summary>
    public enum EnumTaskStatus : short
    {
        Stopped,
        Requesting,
        Running,
        Closing,
        Failed,
        No_Task
    }

    /// <summary>
    /// Task status detail constants
    /// </summary>
    public enum EnumTaskStatusDetail : short
    {
        Retrieving_Resources,
        Running_Tool,
        Packaging_Results,
        Delivering_Results,
        No_Task
    }

    #endregion

    /// <summary>
    /// Interface used by classes that create and update task status file
    /// </summary>
    interface IStatusFile
    {

        #region "Properties"

        /// <summary>
        /// Status file path
        /// </summary>
        string FileNamePath { get; set; }

        /// <summary>
        /// Manager name
        /// </summary>
        string MgrName { get; set; }

        /// <summary>
        /// Manager status
        /// </summary>
        EnumMgrStatus MgrStatus { get; set; }

        /// <summary>
        /// Overall CPU utilization of all threads
        /// </summary>
        /// <remarks></remarks>
        int CpuUtilization { get; set; }

        /// <summary>
        /// Step tool name
        /// </summary>
        string Tool { get; set; }

        /// <summary>
        /// Task status
        /// </summary>
        EnumTaskStatus TaskStatus { get; set; }
        /// <summary>
        /// Progress (value between 0 and 100)
        /// </summary>
        float Progress { get; set; }

        /// <summary>
        /// Current task
        /// </summary>
        string CurrentOperation { get; set; }

        /// <summary>
        /// Task status detail
        /// </summary>
        EnumTaskStatusDetail TaskStatusDetail { get; set; }

        /// <summary>
        /// Job number
        /// </summary>
        int JobNumber { get; set; }

        /// <summary>
        /// Step number
        /// </summary>
        int JobStep { get; set; }

        /// <summary>
        /// Dataset name
        /// </summary>
        string Dataset { get; set; }

        /// <summary>
        /// Most recent job info
        /// </summary>
        string MostRecentJobInfo { get; set; }
        /// <summary>
        /// When true, the status XML is being sent to the manager status message queue
        /// </summary>
        bool LogToMsgQueue { get; set;  }

        #endregion

        #region "Methods"

        void WriteStatusFile();
        /// <summary>
        /// Updates status file
        /// </summary>
        /// <param name="percentComplete">Job completion percentage (value between 0 and 100)</param>
        /// <remarks></remarks>
        void UpdateAndWrite(float percentComplete);

        /// <summary>
        /// Updates status file
        /// </summary>
        /// <param name="status">Job status enum</param>
        /// <param name="percentComplete">Job completion percentage (value between 0 and 100)</param>
        /// <remarks></remarks>
        void UpdateAndWrite(EnumTaskStatusDetail status, float percentComplete);
        void UpdateStopped(bool mgrError);
        void UpdateDisabled(bool disabledLocally);
        void InitStatusFromFile();

        #endregion
    }
}
