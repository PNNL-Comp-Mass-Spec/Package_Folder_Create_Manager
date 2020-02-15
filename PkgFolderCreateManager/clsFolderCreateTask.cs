
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
    class clsFolderCreateTask : clsDbTask, ITaskParams
    {

        #region "Constants"

        protected const string SP_NAME_SET_COMPLETE = "SetFolderCreateTaskComplete";
        protected const string SP_NAME_REQUEST_TASK = "RequestFolderCreateTask";

        #endregion

        #region "Class variables"

        int mTaskID;
        string mTaskParametersXML = string.Empty;

        private bool mConnectionInfoLogged;
        #endregion

        #region "Properties"

        public string TaskParametersXML
        {
            get
            {
                if (string.IsNullOrEmpty(mTaskParametersXML))
                    return string.Empty;

                return mTaskParametersXML;
            }
        }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mgrParams">Manager params for use by class</param>
        public clsFolderCreateTask(MgrSettings mgrParams)
            : base(mgrParams)
        {
            mTaskID = 0;
            m_JobParams.Clear();
        }

        /// <summary>
        /// Gets a stored parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <returns>Parameter value if found, otherwise empty string</returns>
        public string GetParam(string name)
        {
            if (m_JobParams.ContainsKey(name))
            {
                return m_JobParams[name];
            }

            return string.Empty;
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
                m_JobParams.Add(paramName, paramValue);
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
        /// Stores a parameter
        /// </summary>
        /// <param name="keyName">Parameter key</param>
        /// <param name="value">Parameter value</param>
        public void SetParam(string keyName, string value)
        {
            if (value == null)
            {
                value = "";
            }
            m_JobParams[keyName] = value;
        }

        /// <summary>
        /// Wrapper for requesting a task from the database
        /// </summary>
        /// <returns>num indicating if task was found</returns>
        public override EnumRequestTaskResult RequestTask()
        {
            mTaskID = 0;

            var retVal = RequestTaskDetailed();
            switch (retVal)
            {
                case EnumRequestTaskResult.TaskFound:
                    m_TaskWasAssigned = true;
                    break;
                case EnumRequestTaskResult.NoTaskFound:
                    m_TaskWasAssigned = false;
                    break;
                default:
                    m_TaskWasAssigned = false;
                    break;
            }

            return retVal;
        }

        /// <summary>
        /// Detailed step request
        /// </summary>
        /// <returns>RequestTaskResult enum</returns>
        private EnumRequestTaskResult RequestTaskDetailed()
        {
            EnumRequestTaskResult outcome;

            try
            {
                // Set up the command object prior to SP execution
                var dbTools = m_PipelineDBProcedureExecutor;
                var cmd = dbTools.CreateCommand(SP_NAME_REQUEST_TASK, CommandType.StoredProcedure);

                dbTools.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);
                dbTools.AddParameter(cmd, "@processorName", SqlType.VarChar, 128, ManagerName);
                var taskParam = dbTools.AddParameter(cmd, "@taskID", SqlType.Int, ParameterDirection.Output);
                var taskParamsParam = dbTools.AddParameter(cmd, "@parameters", SqlType.VarChar, 4000, ParameterDirection.Output);
                var messageParam = dbTools.AddParameter(cmd, "@message", SqlType.VarChar, 512, ParameterDirection.Output);
                dbTools.AddParameter(cmd, "@infoOnly", SqlType.TinyInt).Value = 0;
                dbTools.AddParameter(cmd, "@taskCountToPreview", SqlType.Int).Value = 10;

                if (!mConnectionInfoLogged)
                {
                    var msg = "clsCaptureTask.RequestTaskDetailed(), connection string: " + m_ConnStr;
                    LogDebug(msg);

                    var paramListHeader = "clsCaptureTask.RequestTaskDetailed(), printing param list";
                    LogDebug(paramListHeader);

                    PrintCommandParams(cmd);

                    mConnectionInfoLogged = true;
                }

                // Execute the SP
                var resCode = m_PipelineDBProcedureExecutor.ExecuteSP(cmd, out _);

                switch (resCode)
                {
                    case RET_VAL_OK:
                        // No errors found in SP call, so see if any step tasks were found
                        mTaskID = (int)taskParam.Value;

                        mTaskParametersXML = (string)taskParamsParam.Value;

                        outcome = EnumRequestTaskResult.TaskFound;
                        break;
                    case RET_VAL_TASK_NOT_AVAILABLE:
                        // No jobs found
                        outcome = EnumRequestTaskResult.NoTaskFound;
                        break;
                    default:
                        // There was an SP error
                        var errMsg = "clsFolderCreateTask.RequestTaskDetailed(), SP execution error " + resCode +
                            "; Msg text = " + (string)messageParam.Value;

                        LogError(errMsg);
                        outcome = EnumRequestTaskResult.ResultError;
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError("Exception requesting folder create task: " + ex.Message);
                outcome = EnumRequestTaskResult.ResultError;
            }
            return outcome;
        }

        /// <summary>
        /// Closes a folder creation task (Overloaded)
        /// </summary>
        /// <param name="taskResult">Enum representing task state</param>
        public override void CloseTask(EnumCloseOutType taskResult)
        {
            CloseTask(taskResult, "", EnumEvalCode.EVAL_CODE_SUCCESS);
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
            if (!SetFolderCreateTaskComplete(SP_NAME_SET_COMPLETE, m_ConnStr, (int)taskResult, closeoutMsg, (int)evalCode))
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
        /// Database calls to set a folder create task complete
        /// </summary>
        /// <param name="spName">Name of SetComplete stored procedure</param>
        /// <param name="connStr">Db connection string</param>
        /// <param name="compCode">Integer representation of completion code</param>
        /// <param name="compMsg"></param>
        /// <param name="evalCode"></param>
        /// <returns>TRUE for success; FALSE for failure</returns>
        public bool SetFolderCreateTaskComplete(string spName, string connStr, int compCode, string compMsg, int evalCode)
        {
            try
            {

                // Setup for execution of the stored procedure
                var dbTools = m_PipelineDBProcedureExecutor;
                var cmd = dbTools.CreateCommand(spName, CommandType.StoredProcedure);

                dbTools.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);
                dbTools.AddParameter(cmd, "@taskID", SqlType.Int).Value = mTaskID;
                dbTools.AddParameter(cmd, "@completionCode", SqlType.Int).Value = compCode;
                var messageParam = dbTools.AddParameter(cmd, "@message", SqlType.VarChar, 512, ParameterDirection.Output);

                LogDebug("Calling stored procedure " + spName);

                var msg = "Parameters: TaskID=" + mTaskID +
                          ", completionCode=" + compCode;

                LogDebug(msg);

                // Execute the SP
                var resCode = m_PipelineDBProcedureExecutor.ExecuteSP(cmd);

                if (resCode == 0)
                {
                    return true;
                }

                var errorMsg = "Error " + resCode + " setting task complete; Message = " + messageParam.Value;
                LogError(errorMsg);
                return false;
            }
            catch (Exception ex)
            {
                var errorMsg = "Exception calling stored procedure " + spName;
                LogError(errorMsg, ex);
                return false;
            }

        }

        #endregion
    }
}
