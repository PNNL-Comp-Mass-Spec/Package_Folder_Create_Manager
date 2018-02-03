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

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Provides tools for creating and updating a task status file
    /// </summary>
    class clsStatusFile : clsEventNotifier, IStatusFile
    {

        #region "Class variables"

        private DateTime m_LastFileWriteTime;

        private int m_WritingErrorCountSaved;

        readonly clsMessageHandler m_MsgHandler;

        int m_MessageQueueExceptionCount;

        #endregion

        #region "Properties"

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
        // Current task
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

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public clsStatusFile(string statusFilePath, clsMessageHandler MsgHandler)
        {
            FileNamePath = statusFilePath;
            TaskStartTime = DateTime.UtcNow;

            m_MsgHandler = MsgHandler;

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
        /// <returns></returns>
        /// <remarks></remarks>
        public int GetProcessID()
        {
            var processID = Process.GetCurrentProcess().Id;
            return processID;
        }

        /// <summary>
        /// Get the folder path for the status file tracked by FileNamePath
        /// </summary>
        /// <returns></returns>
        private string GetStatusFileDirectory()
        {
            var statusFileDirectory = Path.GetDirectoryName(FileNamePath);

            if (statusFileDirectory == null)
                return ".";

            return statusFileDirectory;
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
            using (var xWriter = new XmlTextWriter(memStream, System.Text.Encoding.UTF8))
            {
                xWriter.Formatting = Formatting.Indented;
                xWriter.Indentation = 2;

                // Create the XML document in memory
                xWriter.WriteStartDocument(true);
                xWriter.WriteComment("Package Folder Create manager status");

                // Root level element
                xWriter.WriteStartElement("Root");
                xWriter.WriteStartElement("Manager");
                xWriter.WriteElementString("MgrName", status.MgrName);
                xWriter.WriteElementString("MgrStatus", status.ConvertMgrStatusToString(status.MgrStatus));

                xWriter.WriteComment("Local status log time: " + lastUpdate.ToLocalTime().ToString(LOCAL_TIME_FORMAT));
                xWriter.WriteComment("Local last start time: " + status.TaskStartTime.ToLocalTime().ToString(LOCAL_TIME_FORMAT));

                // Write out times in the format 2017-07-06T23:23:14.337Z
                xWriter.WriteElementString("LastUpdate", lastUpdate.ToUniversalTime().ToString(ISO_8601_DATE));

                xWriter.WriteElementString("LastStartTime", status.TaskStartTime.ToUniversalTime().ToString(ISO_8601_DATE));

                xWriter.WriteElementString("CPUUtilization", cpuUtilization.ToString("##0.0"));
                xWriter.WriteElementString("FreeMemoryMB", freeMemoryMB.ToString("##0.0"));
                xWriter.WriteElementString("ProcessID", processId.ToString());
                xWriter.WriteStartElement("RecentErrorMessages");

                foreach (var errMsg in clsStatusData.ErrorQueue)
                {
                    xWriter.WriteElementString("ErrMsg", errMsg);
                }

                xWriter.WriteEndElement(); // RecentErrorMessages
                xWriter.WriteEndElement(); // Manager

                xWriter.WriteStartElement("Task");
                xWriter.WriteElementString("Tool", status.Tool);
                xWriter.WriteElementString("Status", status.ConvertTaskStatusToString(status.TaskStatus));
                xWriter.WriteElementString("Duration", runTimeHours.ToString("0.00"));
                xWriter.WriteElementString("DurationMinutes", (runTimeHours * 60).ToString("0.0"));
                xWriter.WriteElementString("Progress", status.Progress.ToString("##0.00"));
                xWriter.WriteElementString("CurrentOperation", status.CurrentOperation);

                xWriter.WriteStartElement("TaskDetails");
                xWriter.WriteElementString("Status", status.ConvertTaskStatusDetailToString(status.TaskStatusDetail));
                xWriter.WriteElementString("Job", status.JobNumber.ToString());
                xWriter.WriteElementString("Step", status.JobStep.ToString());
                xWriter.WriteElementString("Dataset", status.Dataset);
                xWriter.WriteElementString("MostRecentLogMessage", clsStatusData.MostRecentLogMessage);
                xWriter.WriteElementString("MostRecentJobInfo", status.MostRecentJobInfo);
                xWriter.WriteEndElement(); // TaskDetails
                xWriter.WriteEndElement(); // Task
                xWriter.WriteEndElement(); // Root

                // Close out the XML document (but do not close XWriter yet)
                xWriter.WriteEndDocument();
                xWriter.Flush();

                // Now use a StreamReader to copy the XML text to a string variable
                memStream.Seek(0, SeekOrigin.Begin);
                var srMemoryStreamReader = new StreamReader(memStream);
                var xmlText = srMemoryStreamReader.ReadToEnd();

                srMemoryStreamReader.Close();
                memStream.Close();

                return xmlText;
            }

        }

        private void WriteStatusFileToDisk(string xmlText)
        {
            const int MIN_FILE_WRITE_INTERVAL_SECONDS = 2;

            if (!(DateTime.UtcNow.Subtract(m_LastFileWriteTime).TotalSeconds >= MIN_FILE_WRITE_INTERVAL_SECONDS))
                return;

            // We will write out the Status XML to a temporary file, then rename the temp file to the primary file

            if (FileNamePath == null)
                return;

            var tempStatusFilePath = Path.Combine(GetStatusFileDirectory(), Path.GetFileNameWithoutExtension(FileNamePath) + "_Temp.xml");

            m_LastFileWriteTime = DateTime.UtcNow;

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
                m_WritingErrorCountSaved = 0;

                success = true;

            }
            catch (Exception ex)
            {
                // Increment the error counter
                m_WritingErrorCountSaved += 1;

                if (m_WritingErrorCountSaved >= WRITE_FAILURE_LOG_THRESHOLD)
                {
                    // 5 or more errors in a row have occurred
                    // Post an entry to the log, only when writingErrorCountSaved is 5, 10, 20, 30, etc.
                    if (m_WritingErrorCountSaved == WRITE_FAILURE_LOG_THRESHOLD || m_WritingErrorCountSaved % 10 == 0)
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
        /// <param name="strStatusXML">A string contiaining the XML to write</param>
        protected void LogStatusToMessageQueue(string strStatusXML)
        {

            try
            {
                m_MsgHandler.SendMessage(strStatusXML);
                m_MessageQueueExceptionCount = 0;
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

                m_MessageQueueExceptionCount += 1;
                var msg = "Exception sending status message to broker; count = " + m_MessageQueueExceptionCount;

                if (DateTime.Now.TimeOfDay.Hours == 0 && DateTime.Now.TimeOfDay.Minutes >= 0 && DateTime.Now.TimeOfDay.Minutes <= 5)
                {
                    // The time of day is between 12:00 am and 12:10 am, so write the full exception to the log
                    LogError(msg, ex);
                }
                else
                {
                    if (m_MessageQueueExceptionCount < 5 || m_MessageQueueExceptionCount % 20 == 0)
                        LogError(msg);
                }
            }
        }

        /// <summary>
        /// Total time the job has been running
        /// </summary>
        /// <returns>Number of hours manager has been processing job</returns>
        /// <remarks></remarks>
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

                // Get the error messsages
                foreach (XmlNode Xn in Doc.SelectNodes("//Manager/RecentErrorMessages/ErrMsg"))
                {
                    clsStatusData.AddErrorMessage(Xn.InnerText);
                }
            }
            catch (Exception ex)
            {
                LogError("Exception reading status file", ex);
            }
        }

        private void LogError(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message);

            if (ex == null)
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.ERROR, message);
            else
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.ERROR, message, ex);

        }
        #endregion
    }
}
