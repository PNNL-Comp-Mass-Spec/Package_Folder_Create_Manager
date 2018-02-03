
//*********************************************************************************************************
// Written by Matthew Monroe for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Based on code written by Dave Clark in 2009
//*********************************************************************************************************

using System;
using System.Data.SqlClient;
using System.Data;

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
        public clsFolderCreateTask(IMgrParams mgrParams)
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
                var spCmd = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = SP_NAME_REQUEST_TASK
                };

                spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
                spCmd.Parameters.Add(new SqlParameter("@processorName", SqlDbType.VarChar, 128)).Value = ManagerName;
                spCmd.Parameters.Add(new SqlParameter("@taskID", SqlDbType.Int)).Direction = ParameterDirection.Output;
                spCmd.Parameters.Add(new SqlParameter("@parameters", SqlDbType.VarChar, 4000)).Direction = ParameterDirection.Output;
                spCmd.Parameters.Add(new SqlParameter("@message", SqlDbType.VarChar, 512)).Direction = ParameterDirection.Output;
                spCmd.Parameters.Add(new SqlParameter("@infoOnly", SqlDbType.TinyInt)).Value = 0;
                spCmd.Parameters.Add(new SqlParameter("@taskCountToPreview", SqlDbType.Int)).Value = 10;

                if (!mConnectionInfoLogged)
                {
                    var msg = "clsCaptureTask.RequestTaskDetailed(), connection string: " + m_ConnStr;
                    LogDebug(msg);

                    var paramListHeader = "clsCaptureTask.RequestTaskDetailed(), printing param list";
                    LogDebug(paramListHeader);

                    PrintCommandParams(spCmd);

                    mConnectionInfoLogged = true;
                }

                // Execute the SP
                var resCode = m_PipelineDBProcedureExecutor.ExecuteSP(spCmd, out _);

                switch (resCode)
                {
                    case RET_VAL_OK:
                        // No errors found in SP call, so see if any step tasks were found
                        mTaskID = (int)spCmd.Parameters["@taskID"].Value;

                        mTaskParametersXML = (string)spCmd.Parameters["@parameters"].Value;

                        outcome = EnumRequestTaskResult.TaskFound;
                        break;
                    case RET_VAL_TASK_NOT_AVAILABLE:
                        // No jobs found
                        outcome = EnumRequestTaskResult.NoTaskFound;
                        break;
                    default:
                        // There was an SP error
                        var errMsg = "clsFolderCreateTask.RequestTaskDetailed(), SP execution error " + resCode +
                            "; Msg text = " + (string)spCmd.Parameters["@message"].Value;

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
        /// <returns>TRUE for sucesss; FALSE for failure</returns>
        public bool SetFolderCreateTaskComplete(string spName, string connStr, int compCode, string compMsg, int evalCode)
        {
            try
            {

                // Setup for execution of the stored procedure
                var myCmd = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = spName
                };

                myCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
                myCmd.Parameters.Add(new SqlParameter("@taskID", SqlDbType.Int)).Value = mTaskID;
                myCmd.Parameters.Add(new SqlParameter("@completionCode", SqlDbType.Int)).Value = compCode;
                myCmd.Parameters.Add(new SqlParameter("@message", SqlDbType.VarChar, 512)).Direction = ParameterDirection.Output;

                LogDebug("Calling stored procedure " + spName);

                var msg = "Parameters: TaskID=" + myCmd.Parameters["@taskID"].Value +
                          ", completionCode=" + myCmd.Parameters["@completionCode"].Value;

                LogDebug(msg);

                // Execute the SP
                var resCode = m_PipelineDBProcedureExecutor.ExecuteSP(myCmd);

                if (resCode == 0)
                {
                    return true;
                }

                var errorMsg = "Error " + resCode + " setting task complete; Message = " + (string)myCmd.Parameters["@message"].Value;
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
