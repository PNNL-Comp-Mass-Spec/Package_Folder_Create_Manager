
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

        string FileNamePath { get; set; }
        string MgrName { get; set; }
        EnumMgrStatus MgrStatus { get; set; }
        int CpuUtilization { get; set; }
        string Tool { get; set; }
        EnumTaskStatus TaskStatus { get; set; }
        float Progress { get; set; }
        string CurrentOperation { get; set; }
        EnumTaskStatusDetail TaskStatusDetail { get; set; }
        int JobNumber { get; set; }
        int JobStep { get; set; }
        string Dataset { get; set; }
        string MostRecentJobInfo { get; set; }
        bool LogToMsgQueue { get; set; }

        #endregion

        #region "Methods"

        void WriteStatusFile();
        void UpdateAndWrite(float percentComplete);
        void UpdateAndWrite(EnumTaskStatusDetail status, float percentComplete);
        void UpdateStopped(bool mgrError);
        void UpdateDisabled(bool disabledLocally);
        void InitStatusFromFile();

        #endregion
    }
}
