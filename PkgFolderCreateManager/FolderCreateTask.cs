
//*********************************************************************************************************
// Written by Matthew Monroe for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Based on code written by Dave Clark in 2009
//*********************************************************************************************************

using System;
using System.Data;
using PRISM.AppSettings;
using PRISMDatabaseUtils;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Provides database access and tools for one folder create task
    /// </summary>
    internal class FolderCreateTask : DbTask, ITaskParams
    {
        protected const string SP_NAME_REQUEST_TASK = "request_folder_create_task";
        protected const string SP_NAME_SET_COMPLETE = "set_folder_create_task_complete";

        private int mTaskID;

        private string mTaskParametersXML = string.Empty;

        private bool mConnectionInfoLogged;

        /// <summary>
        /// XML with information on the directory to create
        /// </summary>
        /// <remarks>See CreateDirectory for example XML</remarks>
        public string TaskParametersXML => string.IsNullOrEmpty(mTaskParametersXML) ? string.Empty : mTaskParametersXML;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mgrParams">Manager params for use by class</param>
        public FolderCreateTask(MgrSettings mgrParams)
            : base(mgrParams)
        {
            mTaskID = 0;
            mJobParams.Clear();
        }

        /// <summary>
        /// Adds a parameter
        /// </summary>
        /// <param name="paramName">Name of parameter</param>
        /// <param name="paramValue">Value for parameter</param>
        /// <returns>RUE for success, FALSE for error</returns>
        public bool AddAdditionalParameter(string paramName, string paramValue)
        {
            try
            {
                mJobParams.Add(paramName, paramValue);
                return true;
            }
            catch (Exception ex)
            {
                var msg = "Exception adding parameter: " + paramName + ", Value: " + paramValue;
                LogError(msg, ex);
                return false;
            }
        }

        /// <summary>
        /// Prefix the stored procedure name using "sw." if the connection string is for a PostgreSQL server
        /// </summary>
        /// <param name="procedureName"></param>
        /// <returns>Stored procedure name to use</returns>
        private string AddSchemaIfPostgres(string procedureName)
        {
            var serverType = DbToolsFactory.GetServerTypeFromConnectionString(mConnectingString);

            return serverType == DbServerTypes.PostgreSQL
                ? string.Format("sw.{0}", procedureName)
                : procedureName;
        }

        /// <summary>
        /// Gets a stored parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <returns>Parameter value if found, otherwise empty string</returns>
        public string GetParam(string name)
        {
            return mJobParams.TryGetValue(name, out var param) ? param : string.Empty;
        }

        /// <summary>
        /// Stores a parameter
        /// </summary>
        /// <param name="keyName">Parameter key</param>
        /// <param name="value">Parameter value</param>
        public void SetParam(string keyName, string value)
        {
            value ??= string.Empty;
            mJobParams[keyName] = value;
        }

        /// <summary>
        /// Wrapper for requesting a task from the database
        /// </summary>
        /// <returns>Enum indicating if task was found</returns>
        public override EnumRequestTaskResult RequestTask()
        {
            mTaskID = 0;

            var retVal = RequestTaskDetailed();

            mTaskWasAssigned = retVal switch
            {
                EnumRequestTaskResult.TaskFound => true,
                EnumRequestTaskResult.NoTaskFound => false,
                _ => false
            };

            return retVal;
        }

        /// <summary>
        /// Detailed step request
        /// </summary>
        /// <returns>RequestTaskResult enum</returns>
        private EnumRequestTaskResult RequestTaskDetailed()
        {
            var spName = AddSchemaIfPostgres(SP_NAME_REQUEST_TASK);

            try
            {
                // Set up the command object prior to SP execution
                var cmd = mPipelineDBProcedureExecutor.CreateCommand(spName, CommandType.StoredProcedure);

                // Define parameter for procedure's return value
                // If querying a Postgres DB, mPipelineDBProcedureExecutor will auto-change "@return" to "_returnCode"
                var returnParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);

                mPipelineDBProcedureExecutor.AddParameter(cmd, "@processorName", SqlType.VarChar, 128, ManagerName);
                var taskParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@taskID", SqlType.Int, ParameterDirection.InputOutput);
                var taskParamsParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@parameters", SqlType.VarChar, 4000, ParameterDirection.InputOutput);
                var messageParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@message", SqlType.VarChar, 512, ParameterDirection.InputOutput);

                if (mPipelineDBProcedureExecutor.DbServerType == DbServerTypes.PostgreSQL)
                    mPipelineDBProcedureExecutor.AddParameter(cmd, "@infoOnly", SqlType.Boolean).Value = false;
                else
                    mPipelineDBProcedureExecutor.AddParameter(cmd, "@infoOnly", SqlType.TinyInt).Value = 0;

                mPipelineDBProcedureExecutor.AddParameter(cmd, "@taskCountToPreview", SqlType.Int).Value = 10;

                if (!mConnectionInfoLogged)
                {
                    var msg = "CaptureTask.RequestTaskDetailed(), connection string: " + mConnectingString;
                    LogDebug(msg);

                    LogDebug("CaptureTask.RequestTaskDetailed(), printing param list");

                    PrintCommandParams(cmd);

                    mConnectionInfoLogged = true;
                }

                // Execute the SP
                mPipelineDBProcedureExecutor.ExecuteSP(cmd, out _);

                var returnCode = DBToolsBase.GetReturnCode(returnParam);

                switch (returnCode)
                {
                    case RET_VAL_OK:
                        // No errors found in SP call, so see if any step tasks were found
                        mTaskID = (int)taskParam.Value;
                        mTaskParametersXML = (string)taskParamsParam.Value;
                        return EnumRequestTaskResult.TaskFound;

                    case RET_VAL_TASK_NOT_AVAILABLE:
                        // No jobs found
                        return EnumRequestTaskResult.NoTaskFound;

                    default:
                        // There was an SP error
                        var errMsg = string.Format(
                            "FolderCreateTask.RequestTaskDetailed(), SP execution error {0}; Message text = {1}",
                            returnCode,
                            string.IsNullOrWhiteSpace((string)messageParam.Value) ? "Unknown error" : messageParam.Value);

                        LogError(errMsg);
                        return EnumRequestTaskResult.ResultError;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format("Exception requesting folder create task using {0}", spName);
                LogError(errorMsg, ex);
                return EnumRequestTaskResult.ResultError;
            }
        }

        /// <summary>
        /// Closes a folder creation task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        public override void CloseTask(EnumCloseOutType taskResult)
        {
            CloseTask(taskResult, string.Empty, EnumEvalCode.EVAL_CODE_SUCCESS);
        }

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        public override void CloseTask(EnumCloseOutType taskResult, string closeoutMsg)
        {
            CloseTask(taskResult, closeoutMsg, EnumEvalCode.EVAL_CODE_SUCCESS);
        }

        /// <summary>
        /// Closes a capture pipeline task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        /// <param name="closeoutMsg">Message related to task closeout</param>
        /// <param name="evalCode">Enum representing evaluation results</param>
        public override void CloseTask(EnumCloseOutType taskResult, string closeoutMsg, EnumEvalCode evalCode)
        {
            // Note that closeoutMsg and evalCode are unused

            if (!SetFolderCreateTaskComplete((int)taskResult))
            {
                var msg = "Error setting task complete in database, task_id " + mTaskID;
                LogError(msg);
            }
            else
            {
                var msg = "Successfully set task complete in database, task_id " + mTaskID;
                LogDebug(msg);
            }
        }

        /// <summary>
        /// Call procedure to set the folder create task complete
        /// </summary>
        /// <param name="compCode">Integer representation of completion code</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetFolderCreateTaskComplete(int compCode)
        {
            var spName = AddSchemaIfPostgres(SP_NAME_SET_COMPLETE);

            try
            {
                // Setup for execution of the stored procedure
                var cmd = mPipelineDBProcedureExecutor.CreateCommand(spName, CommandType.StoredProcedure);

                // Define parameter for procedure's return value
                // If querying a Postgres DB, mPipelineDBProcedureExecutor will auto-change "@return" to "_returnCode"
                var returnParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);

                mPipelineDBProcedureExecutor.AddParameter(cmd, "@taskID", SqlType.Int).Value = mTaskID;
                mPipelineDBProcedureExecutor.AddParameter(cmd, "@completionCode", SqlType.Int).Value = compCode;
                var messageParam = mPipelineDBProcedureExecutor.AddParameter(cmd, "@message", SqlType.VarChar, 512, ParameterDirection.Output);

                LogDebug(string.Format("Calling stored procedure {0}", spName));

                LogDebug(string.Format("Parameters: TaskID={0}, completionCode={1}", mTaskID, compCode));

                // Execute the SP
                mPipelineDBProcedureExecutor.ExecuteSP(cmd);

                var returnCode = DBToolsBase.GetReturnCode(returnParam);

                if (returnCode == 0)
                {
                    return true;
                }

                var errorMsg = string.Format(
                    "Error {0} setting task complete: {1}",
                    returnCode,
                    string.IsNullOrWhiteSpace((string)messageParam.Value) ? "Unknown error" : messageParam.Value);

                LogError(errorMsg);

                return false;
            }
            catch (Exception ex)
            {
                var errorMsg = string.Format("Exception setting folder create task complete using {0}", spName);
                LogError(errorMsg, ex);
                return false;
            }
        }
    }
}
