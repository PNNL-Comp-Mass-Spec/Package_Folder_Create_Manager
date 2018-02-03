
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 09/10/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.Data;

namespace PkgFolderCreateManager
{
    /// <summary>
    ///  Base class for handling task-related data
    /// </summary>
    abstract class clsDbTask : clsLoggerBase
    {

        #region "Enums"

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

        #endregion

        #region "Constants"

        protected const int RET_VAL_OK = 0;
        protected const int RET_VAL_TASK_NOT_AVAILABLE = 53000;

        #endregion

        #region "Class variables"

        protected IMgrParams m_MgrParams;
        protected string m_ConnStr;
        protected string m_BrokerConnStr;
        protected StringCollection m_ErrorList = new StringCollection();
        protected bool m_TaskWasAssigned = false;
        protected Dictionary<string, string> m_JobParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region "Properties"

        public bool TaskWasAssigned => m_TaskWasAssigned;

        public Dictionary<string, string> TaskDictionary => m_JobParams;

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="MgrParams"></param>
        protected clsDbTask(IMgrParams MgrParams)
        {
            m_MgrParams = MgrParams;
            m_ConnStr = m_MgrParams.GetParam("ConnectionString");
            m_BrokerConnStr = m_MgrParams.GetParam("brokerconnectionstring");
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
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="outTable">NOTHING when called; if SP successful, contains data table on return</param>
        /// <param name="connStr">Db connection string</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        protected virtual int ExecuteSP(SqlCommand spCmd, ref DataTable outTable, string connStr)
        {
            // If this value is in error msg, then exception occurred before ResCode was set
            var resCode = -9999;

            var myTimer = new System.Diagnostics.Stopwatch();
            var retryCount = 3;

            m_ErrorList.Clear();
            while (retryCount > 0)
            {
                // Multiple retry loop for handling SP execution failures
                try
                {
                    using (var cn = new SqlConnection(connStr))
                    {
                        cn.InfoMessage += OnInfoMessage;
                        using (var da = new SqlDataAdapter())
                        {
                            using (var ds = new DataSet())
                            {
                                // NOTE: The connection has to be added here because it didn't exist at the time the command object was created
                                spCmd.Connection = cn;
                                // Change command timeout from 30 second default in attempt to reduce SP execution timeout errors
                                spCmd.CommandTimeout = int.Parse(m_MgrParams.GetParam("cmdtimeout"));
                                da.SelectCommand = spCmd;
                                myTimer.Start();
                                da.Fill(ds);
                                myTimer.Stop();
                                resCode = (int)da.SelectCommand.Parameters["@Return"].Value;
                                if (outTable != null && ds.Tables.Count > 0) outTable = ds.Tables[0];
                            }    // ds
                        }    //de
                        cn.InfoMessage -= OnInfoMessage;
                    }    // cn
                    LogErrorEvents();
                    break;
                }
                catch (Exception ex)
                {
                    myTimer.Stop();
                    retryCount -= 1;
                    var msg = "clsDBTask.ExecuteSP(), exception filling data adapter, " + ex.Message + ". ResCode = " + resCode + ". Retry count = " + retryCount;
                    PRISM.ConsoleMsgUtils.ShowWarning(msg);
                    LogError(msg);
                }
                finally
                {
                    // Log debugging info (but don't show it at the console)
                    var debugMsg = "SP execution time: " + (myTimer.ElapsedMilliseconds / 1000.0).ToString("##0.000") + " seconds for SP " + spCmd.CommandText;
                    clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, debugMsg);

                    // Reset the connection timer
                    myTimer.Reset();
                }

                // Wait 10 seconds before retrying
                System.Threading.Thread.Sleep(10000);
            }

            if (retryCount < 1)
            {
                // Too many retries, log and return error
                var msg = "Excessive retries executing SP " + spCmd.CommandText;
                LogError(msg);
                return -1;
            }

            return resCode;
        }

        /// <summary>
        /// Debugging routine for printing SP calling params
        /// </summary>
        /// <param name="inpCmd">SQL command object containing params</param>
        protected virtual void PrintCommandParams(SqlCommand inpCmd)
        {
            // Verify there really are command paramters
            if (inpCmd == null) return;

            if (inpCmd.Parameters.Count < 1) return;

            var msg = "";

            foreach (SqlParameter myParam in inpCmd.Parameters)
            {
                msg += Environment.NewLine + string.Format("  Name= {0,-20}, Value= {1}", myParam.ParameterName, DbCStr(myParam.Value));
            }

            var writeToLog = m_DebugLevel >= 5;
            LogDebug("Parameter list:" + msg, writeToLog);
        }

        protected virtual bool FillParamDict(DataTable dt)
        {
            // Verify valid datatable
            if (dt == null)
            {
                var msg = "clsDbTask.FillParamDict(): No parameter table";
                LogError(msg);
                return false;
            }

            // Verify at least one row present
            if (dt.Rows.Count < 1)
            {
                var msg = "clsDbTask.FillParamDict(): No parameters returned by request SP";
                LogError(msg);
                return false;
            }

            // Fill string dictionary with parameter values
            m_JobParams.Clear();

            try
            {
                foreach (DataRow currRow in dt.Rows)
                {
                    var myKey = currRow[dt.Columns["Parameter"]] as string;
                    if (myKey is null)
                        continue;

                    var myVal = currRow[dt.Columns["Value"]] as string;
                    m_JobParams.Add(myKey, myVal);
                }
                return true;
            }
            catch (Exception ex)
            {
                var msg = "clsDbTask.FillParamDict(): Exception reading task parameters";
                LogError(msg, ex);
                return false;
            }
        }

        protected string DbCStr(object InpObj)
        {
            // If input object is DbNull, returns "", otherwise returns String representation of object
            if (InpObj == null || ReferenceEquals(InpObj, DBNull.Value))
            {
                return "";
            }
            return InpObj.ToString();
        }

        protected float DbCSng(object InpObj)
        {
            // If input object is DbNull, returns 0.0, otherwise returns Single representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0.0F;
            }
            return (float)InpObj;
        }

        protected double DbCDbl(object InpObj)
        {
            // If input object is DbNull, returns 0.0, otherwise returns Double representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0.0;
            }
            return (double)InpObj;
        }

        protected int DbCInt(object InpObj)
        {
            // If input object is DbNull, returns 0, otherwise returns Integer representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0;
            }
            return (int)InpObj;
        }

        protected long DbCLng(object InpObj)
        {
            // If input object is DbNull, returns 0, otherwise returns Integer representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0;
            }
            return (long)InpObj;
        }

        protected decimal DbCDec(object InpObj)
        {
            // If input object is DbNull, returns 0, otherwise returns Decimal representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0;
            }
            return (decimal)InpObj;
        }

        protected short DbCShort(object InpObj)
        {
            // If input object is DbNull, returns 0, otherwise returns Short representation of object
            if (ReferenceEquals(InpObj, DBNull.Value))
            {
                return 0;
            }
            return (short)InpObj;
        }

        #endregion

        #region "Event handlers"

        {
        }

        #endregion
    }
}
