
//*********************************************************************************************************
// Written by Matthew Monroe for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Based on code written by Dave Clark in 2009
//
// Last modified 11/13/2011
//*********************************************************************************************************
using System;
using System.Data.SqlClient;
using System.Data;
using System.Windows.Forms;

namespace PkgFolderCreateManager
{
	class clsFolderCreateTask : clsDbTask, ITaskParams
	{
		//*********************************************************************************************************
		// Provides database access and tools for one folder create task
		//**********************************************************************************************************

		#region "Constants"
			protected const string SP_NAME_SET_COMPLETE = "SetFolderCreateTaskComplete";
			protected const string SP_NAME_REQUEST_TASK = "RequestFolderCreateTask";
		#endregion

		#region "Class variables"
			int m_TaskID = 0;
			string m_TaskParametersXML = string.Empty;
		#endregion

		#region "Properties"
			public string TaskParametersXML {
				get {
					if (string.IsNullOrEmpty(m_TaskParametersXML))
						return string.Empty;
					else
						return m_TaskParametersXML;
				}
			}

		#endregion

			#region "Constructors"
			/// <summary>
			/// Class constructor
			/// </summary>
			/// <param name="mgrParams">Manager params for use by class</param>
			public clsFolderCreateTask(IMgrParams mgrParams)
				: base(mgrParams)
			{
				m_JobParams.Clear();
			}
		#endregion

		#region "Methods"
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
				else
				{
					return string.Empty;
				}
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
					string msg = "Exception adding parameter: " + paramName + ", Value: " + paramValue;
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg, ex);
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
				EnumRequestTaskResult retVal;
				m_TaskID = 0;

				retVal = RequestTaskDetailed();
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
				string msg;
				SqlCommand myCmd = new SqlCommand();
				EnumRequestTaskResult outcome = EnumRequestTaskResult.NoTaskFound;
				int retVal = 0;
				string strProductVersion = Application.ProductVersion;
				if (strProductVersion == null) strProductVersion = "??";

				try
				{
					//Set up the command object prior to SP execution
					{
						myCmd.CommandType = CommandType.StoredProcedure;
						myCmd.CommandText = SP_NAME_REQUEST_TASK;
						myCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
						myCmd.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

						myCmd.Parameters.Add(new SqlParameter("@processorName", SqlDbType.VarChar, 128));
						myCmd.Parameters["@processorName"].Direction = ParameterDirection.Input;
						myCmd.Parameters["@processorName"].Value = m_MgrParams.GetParam("MgrName");

						myCmd.Parameters.Add(new SqlParameter("@taskID", SqlDbType.Int));
						myCmd.Parameters["@taskID"].Direction = ParameterDirection.Output;

						myCmd.Parameters.Add(new SqlParameter("@parameters", SqlDbType.VarChar, 4000));
						myCmd.Parameters["@parameters"].Direction = ParameterDirection.Output;
						myCmd.Parameters["@parameters"].Value = "";

						myCmd.Parameters.Add(new SqlParameter("@message", SqlDbType.VarChar, 512));
						myCmd.Parameters["@message"].Direction = ParameterDirection.Output;
						myCmd.Parameters["@message"].Value = "";

						myCmd.Parameters.Add(new SqlParameter("@infoOnly", SqlDbType.TinyInt));
						myCmd.Parameters["@infoOnly"].Direction = ParameterDirection.Input;
						myCmd.Parameters["@infoOnly"].Value = 0;

						myCmd.Parameters.Add(new SqlParameter("@taskCountToPreview", SqlDbType.Int));
						myCmd.Parameters["@taskCountToPreview"].Direction = ParameterDirection.Input;
						myCmd.Parameters["@taskCountToPreview"].Value = 10;
					}

					msg = "clsCaptureTask.RequestTaskDetailed(), connection string: " + m_BrokerConnStr;
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);
					msg = "clsCaptureTask.RequestTaskDetailed(), printing param list";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);
					PrintCommandParams(myCmd);

					//Execute the SP
					retVal = ExecuteSP(myCmd, m_ConnStr);

					switch (retVal)
					{
						case RET_VAL_OK:
							//No errors found in SP call, so see if any step tasks were found
							m_TaskID = (int)myCmd.Parameters["@taskID"].Value;

							m_TaskParametersXML = (string)myCmd.Parameters["@parameters"].Value;

							outcome = EnumRequestTaskResult.TaskFound;
							break;
						case RET_VAL_TASK_NOT_AVAILABLE:
							//No jobs found
							outcome = EnumRequestTaskResult.NoTaskFound;
							break;
						default:
							//There was an SP error
							msg = "clsFolderCreateTask.RequestTaskDetailed(), SP execution error " + retVal.ToString();
							msg += "; Msg text = " + (string)myCmd.Parameters["@message"].Value;
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg);
							outcome = EnumRequestTaskResult.ResultError;
							break;
					}
				}
				catch (System.Exception ex)
				{
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Exception requesting folder create task: " + ex.Message);
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
				string msg;
				int compCode = (int)taskResult;

                if (!SetFolderCreateTaskComplete(SP_NAME_SET_COMPLETE, m_ConnStr, (int)taskResult, closeoutMsg, (int)evalCode))
				{
					msg = "Error setting task complete in database, task_id " + m_TaskID.ToString();
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.ERROR,msg);
				}
				else
				{
					msg = msg = "Successfully set task complete in database, task_id " + m_TaskID.ToString();
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.DEBUG,msg);
				}
			}

			/// <summary>
			/// Database calls to set a folder create task complete
			/// </summary>
			/// <param name="SpName">Name of SetComplete stored procedure</param>
			/// <param name="CompletionCode">Integer representation of completion code</param>
			/// <param name="ConnStr">Db connection string</param>
			/// <returns>TRUE for sucesss; FALSE for failure</returns>
			public bool SetFolderCreateTaskComplete(string spName, string connStr, int compCode, string compMsg, int evalCode)
			{
				string msg;
				bool Outcome = false;
				int ResCode = 0;

                try
                {

                    //Setup for execution of the stored procedure
                    SqlCommand MyCmd = new SqlCommand();
                    {
                        MyCmd.CommandType = CommandType.StoredProcedure;
                        MyCmd.CommandText = spName;
                        MyCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
                        MyCmd.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

                        MyCmd.Parameters.Add(new SqlParameter("@taskID", SqlDbType.Int));
                        MyCmd.Parameters["@taskID"].Direction = ParameterDirection.Input;
						MyCmd.Parameters["@taskID"].Value = m_TaskID;

                        MyCmd.Parameters.Add(new SqlParameter("@completionCode", SqlDbType.Int));
                        MyCmd.Parameters["@completionCode"].Direction = ParameterDirection.Input;
                        MyCmd.Parameters["@completionCode"].Value = compCode;

                        MyCmd.Parameters.Add(new SqlParameter("@message", SqlDbType.VarChar, 512));
                        MyCmd.Parameters["@message"].Direction = ParameterDirection.Output;
                    }

                    msg = "Calling stored procedure " + spName;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);

					msg = "Parameters: TaskID=" + MyCmd.Parameters["@taskID"].Value +
									", completionCode=" + MyCmd.Parameters["@completionCode"].Value;

                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, msg);


                    //Execute the SP
                    ResCode = ExecuteSP(MyCmd, connStr);

                    if (ResCode == 0)
                    {
                        Outcome = true;
                    }
                    else
                    {
                        msg = "Error " + ResCode.ToString() + " setting task complete";
                        msg += "; Message = " + (string)MyCmd.Parameters["@message"].Value;
                        Outcome = false;
                    }
                }
                catch (Exception ex)
                {
                    msg = "Exception calling stored procedure " + spName;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg, ex);
                    Outcome = false;
                }

				return Outcome;
			}
		#endregion
	}	// End class
}	// End namespace
