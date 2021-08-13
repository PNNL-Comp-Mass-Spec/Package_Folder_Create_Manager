
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 09/10/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using PRISM.AppSettings;
using PRISMDatabaseUtils;

namespace PkgFolderCreateManager
{
    /// <summary>
    ///  Base class for handling task-related data
    /// </summary>
    internal abstract class clsDbTask : clsLoggerBase
    {
        public enum EnumCloseOutType : short
        {
            CLOSEOUT_SUCCESS = 0,
            CLOSEOUT_FAILED = 1,
            CLOSEOUT_NOT_READY = 2,
            CLOSEOUT_NEED_TO_ABORT_PROCESSING = 3
        }

        public enum EnumEvalCode : short
        {
            EVAL_CODE_SUCCESS = 0,
            EVAL_CODE_FAILED = 1,
            EVAL_CODE_NOT_EVALUATED = 2
        }

        public enum EnumRequestTaskResult : short
        {
            TaskFound = 0,
            NoTaskFound = 1,
            ResultError = 2
        }

        protected const int RET_VAL_OK = 0;
        protected const int RET_VAL_TASK_NOT_AVAILABLE = 53000;

        protected readonly MgrSettings mMgrParams;

        protected readonly string mConnectingString;

        protected bool mTaskWasAssigned;

        /// <summary>
        /// Debug level
        /// </summary>
        /// <remarks>4 means Info level (normal) logging; 5 for Debug level (verbose) logging</remarks>
        protected readonly int mDebugLevel;

        /// <summary>
        /// Job parameters
        /// </summary>
        protected readonly Dictionary<string, string> mJobParams = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stored procedure executor
        /// </summary>
        protected readonly IDBTools mPipelineDBProcedureExecutor;

        /// <summary>
        /// Manager name
        /// </summary>
        public string ManagerName { get; }

        public bool TaskWasAssigned => mTaskWasAssigned;

        public Dictionary<string, string> TaskDictionary => mJobParams;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mgrParams"></param>
        protected clsDbTask(MgrSettings mgrParams)
        {
            mMgrParams = mgrParams;
            ManagerName = mMgrParams.GetParam("MgrName", Environment.MachineName + "_Undefined-Manager");

            // Gigasax.DMS_Pipeline
            var connectionString = mMgrParams.GetParam("ConnectionString");

            mConnectingString = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, ManagerName);
            mPipelineDBProcedureExecutor = DbToolsFactory.GetDBTools(mConnectingString);

            mPipelineDBProcedureExecutor.ErrorEvent += PipelineDBProcedureExecutor_DBErrorEvent;

            // Cache the log level
            // 4 means Info level (normal) logging; 5 for Debug level (verbose) logging
            mDebugLevel = mgrParams.GetParam("DebugLevel", 4);
        }

        /// <summary>
        /// Requests a capture pipeline task
        /// </summary>
        /// <returns>RequestTaskResult enum specifying call result</returns>
        public abstract EnumRequestTaskResult RequestTask();

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        public abstract void CloseTask(EnumCloseOutType taskResult);

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        public abstract void CloseTask(EnumCloseOutType taskResult, string closeoutMsg);

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        /// <param name="evalCode">Enum representing evaluation results</param>
        public abstract void CloseTask(EnumCloseOutType taskResult, string closeoutMsg, EnumEvalCode evalCode);

        /// <summary>
        /// Debugging routine for printing SP calling params
        /// </summary>
        /// <param name="inpCmd">SQL command object containing params</param>
        protected virtual void PrintCommandParams(DbCommand inpCmd)
        {
            // Verify there really are command parameters
            if (inpCmd == null)
                return;

            if (inpCmd.Parameters.Count < 1)
                return;

            var msg = new StringBuilder();
            msg.AppendLine("Parameter list:");

            foreach (DbParameter myParam in inpCmd.Parameters)
            {
                msg.AppendFormat("  Name= {0,-20}, Value= {1}", myParam.ParameterName, DbCStr(myParam.Value));
                msg.AppendLine();
            }

            var writeToLog = mDebugLevel >= 5;
            LogDebug(msg.ToString(), writeToLog);
        }

        private string DbCStr(object InpObj)
        {
            // If input object is DbNull, returns "", otherwise returns String representation of object
            if (InpObj == null || ReferenceEquals(InpObj, DBNull.Value))
            {
                return string.Empty;
            }
            return InpObj.ToString();
        }

        private void PipelineDBProcedureExecutor_DBErrorEvent(string message, Exception ex)
        {
            var logToDb = message.Contains("permission was denied");

            if (logToDb)
                LogError(message, logToDb: true);
            else
                LogError(message, ex);
        }
    }
}
