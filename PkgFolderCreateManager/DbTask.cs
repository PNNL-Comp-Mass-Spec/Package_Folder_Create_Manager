
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
    internal abstract class DbTask : LoggerBase
    {
        // Ignore Spelling: RET

        // ReSharper disable UnusedMember.Global

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

        // ReSharper restore UnusedMember.Global

        public enum EnumRequestTaskResult : short
        {
            TaskFound = 0,
            NoTaskFound = 1,
            ResultError = 2
        }

        // ReSharper disable InconsistentNaming

        protected const int RET_VAL_OK = 0;
        protected const int RET_VAL_TASK_NOT_AVAILABLE = 53000;

        // ReSharper restore InconsistentNaming

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

        // ReSharper disable once UnusedMember.Global
        public bool TaskWasAssigned => mTaskWasAssigned;

        public Dictionary<string, string> TaskDictionary => mJobParams;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mgrParams"></param>
        protected DbTask(MgrSettings mgrParams)
        {
            mMgrParams = mgrParams;
            ManagerName = mMgrParams.GetParam("MgrName", Environment.MachineName + "_Undefined-Manager");

            // This connection string points to the DMS database on prismdb2 (previously, DMS_Pipeline on Gigasax)
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
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract EnumRequestTaskResult RequestTask();

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract void CloseTask(EnumCloseOutType taskResult);

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract void CloseTask(EnumCloseOutType taskResult, string closeoutMsg);

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        /// <param name="evalCode">Enum representing evaluation results</param>
        // ReSharper disable once UnusedMemberInSuper.Global
        public abstract void CloseTask(EnumCloseOutType taskResult, string closeoutMsg, EnumEvalCode evalCode);

        /// <summary>
        /// Debugging routine for printing SP calling params
        /// </summary>
        /// <param name="cmd">SQL command object containing params</param>
        protected virtual void PrintCommandParams(DbCommand cmd)
        {
            // Verify there really are command parameters
            if (cmd == null)
                return;

            if (cmd.Parameters.Count < 1)
                return;

            var msg = new StringBuilder();
            msg.AppendLine("Parameter list:");

            foreach (DbParameter myParam in cmd.Parameters)
            {
                msg.AppendFormat("  Name= {0,-20}, Value= {1}", myParam.ParameterName, DbCStr(myParam.Value));
                msg.AppendLine();
            }

            var writeToLog = mDebugLevel >= 5;
            LogDebug(msg.ToString(), writeToLog);
        }

        private string DbCStr(object value)
        {
            // If input object is DbNull, returns string.Empty, otherwise returns String representation of object
            if (value == null || ReferenceEquals(value, DBNull.Value))
            {
                return string.Empty;
            }
            return value.ToString();
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
