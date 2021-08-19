﻿
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//*********************************************************************************************************

using System;
using System.Diagnostics;
using System.Xml;
using System.IO;
using PRISM;
using PRISM.Logging;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Provides tools for creating and updating a task status file
    /// </summary>
    internal class clsStatusFile : EventNotifier
    {
        // Ignore Spelling: yyyy-MM-dd, hh:mm:ss tt, T_Mgrs

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

        private DateTime mLastFileWriteTime;

        private int mWritingErrorCountSaved;

        private readonly clsMessageHandler mMsgHandler;

        private int mMessageQueueExceptionCount;

        /// <summary>
        /// Status file path
        /// </summary>
        public string FileNamePath { get; set; }

        /// <summary>
        /// Manager name
        /// </summary>
        public string MgrName { get; set; }

        /// <summary>
        /// Manager status
        /// </summary>
        public EnumMgrStatus MgrStatus { get; set; } = EnumMgrStatus.Stopped;

        /// <summary>
        /// Overall CPU utilization of all threads
        /// </summary>
        public int CpuUtilization { get; set; }

        /// <summary>
        /// Step tool name
        /// </summary>
        public string Tool { get; set; }

        /// <summary>
        /// Task status
        /// </summary>
        public EnumTaskStatus TaskStatus { get; set; } = EnumTaskStatus.No_Task;

        /// <summary>
        /// Task start time (UTC-based)
        /// </summary>
        public DateTime TaskStartTime { get; set; }

        /// <summary>
        /// Progress (value between 0 and 100)
        /// </summary>
        public float Progress { get; set; }

        /// <summary>
        /// Current task
        /// </summary>
        public string CurrentOperation { get; set; }

        /// <summary>
        /// Task status detail
        /// </summary>
        public EnumTaskStatusDetail TaskStatusDetail { get; set; } = EnumTaskStatusDetail.No_Task;

        /// <summary>
        /// Job number
        /// </summary>
        public int JobNumber { get; set; }

        /// <summary>
        /// Step number
        /// </summary>
        public int JobStep { get; set; }

        /// <summary>
        /// Dataset name
        /// </summary>
        public string Dataset { get; set; }

        /// <summary>
        /// Most recent job info
        /// </summary>
        public string MostRecentJobInfo { get; set; }

        /// <summary>
        /// When true, the status XML is being sent to the manager status message queue
        /// </summary>
        public bool LogToMsgQueue { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsStatusFile(string statusFilePath, clsMessageHandler msgHandler)
        {
            FileNamePath = statusFilePath;
            TaskStartTime = DateTime.UtcNow;

            mMsgHandler = msgHandler;

            ClearCachedInfo();
        }

        /// <summary>
        /// Clears cached status info
        /// </summary>
        public void ClearCachedInfo()
        {
            Progress = 0;
            Dataset = string.Empty;
            JobNumber = 0;
            JobStep = 0;
            Tool = string.Empty;
        }

        /// <summary>
        /// Converts the manager status enum to a string value
        /// </summary>
        /// <param name="statusEnum">An IStatusFile.EnumMgrStatus object</param>
        /// <returns>String representation of input object</returns>
        private string ConvertMgrStatusToString(EnumMgrStatus statusEnum)
        {
            return statusEnum.ToString("G");
        }

        /// <summary>
        /// Converts the task status enum to a string value
        /// </summary>
        /// <param name="statusEnum">An IStatusFile.EnumTaskStatus object</param>
        /// <returns>String representation of input object</returns>
        private string ConvertTaskStatusToString(EnumTaskStatus statusEnum)
        {
            return statusEnum.ToString("G");
        }

        /// <summary>
        /// Converts the task status enum to a string value
        /// </summary>
        /// <param name="statusEnum">An IStatusFile.EnumTaskStatusDetail object</param>
        /// <returns>String representation of input object</returns>
        private string ConvertTaskStatusDetailToString(EnumTaskStatusDetail statusEnum)
        {
            return statusEnum.ToString("G");
        }

        /// <summary>
        /// Return the ProcessID of the Analysis manager
        /// </summary>
        public int GetProcessID()
        {
            return Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Get the directory path for the status file tracked by FileNamePath
        /// </summary>
        private string GetStatusFileDirectory()
        {
            var statusFileDirectory = Path.GetDirectoryName(FileNamePath);

            return statusFileDirectory ?? ".";
        }

        /// <summary>
        /// Writes the status file
        /// </summary>
        public void WriteStatusFile()
        {
            var lastUpdate = DateTime.UtcNow;
            var runTimeHours = GetRunTime();
            var processId = GetProcessID();

            const int cpuUtilization = 0;
            const float freeMemoryMB = 0;

            string xmlText;

            try
            {
                xmlText = GenerateStatusXML(this, lastUpdate, processId, cpuUtilization, freeMemoryMB, runTimeHours);

                WriteStatusFileToDisk(xmlText);
            }
            catch (Exception ex)
            {
                var msg = "Error generating status info: " + ex.Message;
                OnWarningEvent(msg);
                xmlText = string.Empty;
            }

            if (LogToMsgQueue)
            {
                // Send the XML text to a message queue
                LogStatusToMessageQueue(xmlText);
            }
        }

        private string GenerateStatusXML(
            clsStatusFile status,
            DateTime lastUpdate,
            int processId,
            int cpuUtilization,
            float freeMemoryMB,
            float runTimeHours)
        {
            // Note that we use this instead of using .ToString("o")
            // because .NET includes 7 digits of precision for the milliseconds,
            // and SQL Server only allows 3 digits of precision
            const string ISO_8601_DATE = "yyyy-MM-ddTHH:mm:ss.fffK";

            const string LOCAL_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

            // Create a new memory stream in which to write the XML
            var memStream = new MemoryStream();

            using var writer = new XmlTextWriter(memStream, System.Text.Encoding.UTF8);

            writer.Formatting = Formatting.Indented;
            writer.Indentation = 2;

            // Create the XML document in memory
            writer.WriteStartDocument(true);
            writer.WriteComment("Package Folder Create manager status");

            // Root level element
            writer.WriteStartElement("Root");
            writer.WriteStartElement("Manager");
            writer.WriteElementString("MgrName", status.MgrName);
            writer.WriteElementString("MgrStatus", status.ConvertMgrStatusToString(status.MgrStatus));

            writer.WriteComment("Local status log time: " + lastUpdate.ToLocalTime().ToString(LOCAL_TIME_FORMAT));
            writer.WriteComment("Local last start time: " + status.TaskStartTime.ToLocalTime().ToString(LOCAL_TIME_FORMAT));

            // Write out times in the format 2017-07-06T23:23:14.337Z
            writer.WriteElementString("LastUpdate", lastUpdate.ToUniversalTime().ToString(ISO_8601_DATE));

            writer.WriteElementString("LastStartTime", status.TaskStartTime.ToUniversalTime().ToString(ISO_8601_DATE));

            writer.WriteElementString("CPUUtilization", cpuUtilization.ToString("##0.0"));
            writer.WriteElementString("FreeMemoryMB", freeMemoryMB.ToString("##0.0"));
            writer.WriteElementString("ProcessID", processId.ToString());
            writer.WriteStartElement("RecentErrorMessages");

            foreach (var errMsg in clsStatusData.ErrorQueue)
            {
                writer.WriteElementString("ErrMsg", errMsg);
            }

            writer.WriteEndElement(); // RecentErrorMessages
            writer.WriteEndElement(); // Manager

            writer.WriteStartElement("Task");
            writer.WriteElementString("Tool", status.Tool);
            writer.WriteElementString("Status", status.ConvertTaskStatusToString(status.TaskStatus));
            writer.WriteElementString("Duration", runTimeHours.ToString("0.00"));
            writer.WriteElementString("DurationMinutes", (runTimeHours * 60).ToString("0.0"));
            writer.WriteElementString("Progress", status.Progress.ToString("##0.00"));
            writer.WriteElementString("CurrentOperation", status.CurrentOperation);

            writer.WriteStartElement("TaskDetails");
            writer.WriteElementString("Status", status.ConvertTaskStatusDetailToString(status.TaskStatusDetail));
            writer.WriteElementString("Job", status.JobNumber.ToString());
            writer.WriteElementString("Step", status.JobStep.ToString());
            writer.WriteElementString("Dataset", status.Dataset);
            writer.WriteElementString("MostRecentLogMessage", clsStatusData.MostRecentLogMessage);
            writer.WriteElementString("MostRecentJobInfo", status.MostRecentJobInfo);
            writer.WriteEndElement(); // TaskDetails
            writer.WriteEndElement(); // Task
            writer.WriteEndElement(); // Root

            // Close out the XML document (but do not close writer yet)
            writer.WriteEndDocument();
            writer.Flush();

            // Now use a StreamReader to copy the XML text to a string variable
            memStream.Seek(0, SeekOrigin.Begin);
            var srMemoryStreamReader = new StreamReader(memStream);
            var xmlText = srMemoryStreamReader.ReadToEnd();

            srMemoryStreamReader.Close();
            memStream.Close();

            return xmlText;
        }

        private void WriteStatusFileToDisk(string xmlText)
        {
            const int MIN_FILE_WRITE_INTERVAL_SECONDS = 2;

            if (!(DateTime.UtcNow.Subtract(mLastFileWriteTime).TotalSeconds >= MIN_FILE_WRITE_INTERVAL_SECONDS))
                return;

            // We will write out the Status XML to a temporary file, then rename the temp file to the primary file

            if (FileNamePath == null)
                return;

            var tempStatusFilePath = Path.Combine(GetStatusFileDirectory(), Path.GetFileNameWithoutExtension(FileNamePath) + "_Temp.xml");

            mLastFileWriteTime = DateTime.UtcNow;

            var success = WriteStatusFileToDisk(tempStatusFilePath, xmlText);
            if (success)
            {
                try
                {
                    File.Copy(tempStatusFilePath, FileNamePath, true);
                }
                catch (Exception ex)
                {
                    // Copy failed
                    // Log a warning that the file copy failed
                    OnWarningEvent("Unable to copy temporary status file to the final status file (" + Path.GetFileName(tempStatusFilePath) +
                                   " to " + Path.GetFileName(FileNamePath) + "):" + ex.Message);
                }

                try
                {
                    File.Delete(tempStatusFilePath);
                }
                catch (Exception ex)
                {
                    // Delete failed
                    // Log a warning that the file delete failed
                    OnWarningEvent("Unable to delete temporary status file (" + Path.GetFileName(tempStatusFilePath) + "): " + ex.Message);
                }
            }
            else
            {
                // Error writing to the temporary status file; try the primary file
                WriteStatusFileToDisk(FileNamePath, xmlText);
            }
        }

        private bool WriteStatusFileToDisk(string statusFilePath, string xmlText)
        {
            const int WRITE_FAILURE_LOG_THRESHOLD = 5;

            bool success;

            try
            {
                // Write out the XML text to a file
                // If the file is in use by another process, then the writing will fail
                using (var writer = new StreamWriter(new FileStream(statusFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.WriteLine(xmlText);
                }

                // Reset the error counter
                mWritingErrorCountSaved = 0;

                success = true;
            }
            catch (Exception ex)
            {
                // Increment the error counter
                mWritingErrorCountSaved++;

                if (mWritingErrorCountSaved >= WRITE_FAILURE_LOG_THRESHOLD)
                {
                    // 5 or more errors in a row have occurred
                    // Post an entry to the log, only when writingErrorCountSaved is 5, 10, 20, 30, etc.
                    if (mWritingErrorCountSaved == WRITE_FAILURE_LOG_THRESHOLD || mWritingErrorCountSaved % 10 == 0)
                    {
                        var msg = "Error writing status file " + Path.GetFileName(statusFilePath) + ": " + ex.Message;
                        OnWarningEvent(msg);
                    }
                }
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Updates status file
        /// (Overload to update when completion percentage is the only change)
        /// </summary>
        /// <param name="percentComplete">Job completion percentage (value between 0 and 100)</param>
        public void UpdateAndWrite(float percentComplete)
        {
            Progress = percentComplete;
            WriteStatusFile();
        }

        /// <summary>
        /// Updates status file
        /// (Overload to update file when status and completion percentage change)
        /// </summary>
        /// <param name="status">Job status enum</param>
        /// <param name="percentComplete">Job completion percentage (value between 0 and 100)</param>
        public void UpdateAndWrite(EnumTaskStatusDetail status, float percentComplete)
        {
            TaskStatusDetail = status;
            Progress = percentComplete;

            WriteStatusFile();
        }

        /// <summary>
        /// Sets status file to show manager not running
        /// </summary>
        /// <param name="mgrError">TRUE if manager not running due to error; FALSE otherwise</param>
        public void UpdateStopped(bool mgrError)
        {
            ClearCachedInfo();

            if (mgrError)
            {
                MgrStatus = EnumMgrStatus.Stopped_Error;
            }
            else
            {
                MgrStatus = EnumMgrStatus.Stopped;
            }

            TaskStatus = EnumTaskStatus.No_Task;
            TaskStatusDetail = EnumTaskStatusDetail.No_Task;

            WriteStatusFile();
        }

        /// <summary>
        /// Updates status file to show manager disabled
        /// </summary>
        /// <param name="disabledLocally">TRUE if manager disabled locally, otherwise FALSE</param>
        public void UpdateDisabled(bool disabledLocally)
        {
            ClearCachedInfo();

            if (disabledLocally)
            {
                MgrStatus = EnumMgrStatus.Disabled_Local;
            }
            else
            {
                MgrStatus = EnumMgrStatus.Disabled_MC;
            }

            TaskStatus = EnumTaskStatus.No_Task;
            TaskStatusDetail = EnumTaskStatusDetail.No_Task;

            WriteStatusFile();
        }

        /// <summary>
        /// Writes the status to the message queue
        /// </summary>
        /// <param name="strStatusXML">A string containing the XML to write</param>
        protected void LogStatusToMessageQueue(string strStatusXML)
        {
            if (mMsgHandler == null)
                return;

            try
            {
                mMsgHandler.SendMessage(strStatusXML);
                mMessageQueueExceptionCount = 0;
            }
            catch (Exception ex)
            {
                // From Matt in October 2014
                // These exceptions occur quite often with the folder create manager on Proto-3; I don't know why
                // Occasionally the status message gets through, but usually it does not
                // Thus, I set LogStatusToMessageQueue to False for the FolderCreate managers in the Manager Control DB

                // SELECT MT.MT_TypeName, M.M_Name, PT.ParamName, PV.*
                // FROM T_ParamValue PV INNER JOIN
                //      T_Mgrs M ON PV.MgrID = M.M_ID INNER JOIN
                //      T_MgrTypes MT ON M.M_TypeID = MT.MT_TypeID INNER JOIN
                //      T_ParamType PT ON PV.TypeID = PT.ParamID
                // WHERE (PT.ParamName = 'LogStatusToMessageQueue') AND (MT.MT_TypeName = 'FolderCreate')

                mMessageQueueExceptionCount++;
                var msg = "Exception sending status message to broker; count = " + mMessageQueueExceptionCount;

                if (DateTime.Now.TimeOfDay.Hours == 0 && DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes <= 5)
                {
                    // The time of day is between 12:00 am and 12:10 am, so write the full exception to the log
                    LogError(msg, ex);
                }
                else
                {
                    if (mMessageQueueExceptionCount < 5 || mMessageQueueExceptionCount % 20 == 0)
                        LogError(msg);
                }
            }
        }

        /// <summary>
        /// Total time the job has been running
        /// </summary>
        /// <returns>Number of hours manager has been processing job</returns>
        private float GetRunTime()
        {
            return (float)DateTime.UtcNow.Subtract(TaskStartTime).TotalHours;
        }

        /// <summary>
        /// Initializes the status from a file, if file exists
        /// </summary>
        public void InitStatusFromFile()
        {
            // Verify status file exists
            if (!File.Exists(FileNamePath)) return;

            // Get data from status file
            try
            {
                var XmlStr = File.ReadAllText(FileNamePath);
                // Convert to an XML document
                var Doc = new XmlDocument();
                Doc.LoadXml(XmlStr);

                // Get the most recent log message
                clsStatusData.MostRecentLogMessage = Doc.SelectSingleNode("//Task/TaskDetails/MostRecentLogMessage")?.InnerText;

                // Get the most recent job info
                MostRecentJobInfo = Doc.SelectSingleNode("//Task/TaskDetails/MostRecentJobInfo")?.InnerText;

                var recentErrorMessages = Doc.SelectNodes("//Manager/RecentErrorMessages/ErrMsg");

                if (recentErrorMessages != null)
                {
                    // Get the error messages
                    foreach (XmlNode Xn in recentErrorMessages)
                    {
                        clsStatusData.AddErrorMessage(Xn.InnerText);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Exception reading status file", ex);
            }
        }

        private void LogError(string message, Exception ex = null)
        {
            LogTools.LogError(message, ex);
        }
    }
}
