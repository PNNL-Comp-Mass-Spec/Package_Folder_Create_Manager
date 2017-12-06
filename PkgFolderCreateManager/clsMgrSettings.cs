
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/16/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Class for loading, storing and accessing manager parameters.
    /// </summary>
    /// <remarks>
    ///  Loads initial settings from local config file, then checks to see if remainder of settings should be
    ///  loaded or manager set to inactive. If manager active, retrieves remainder of settings from manager
    ///  parameters database.
    /// </remarks>
    public class clsMgrSettings : IMgrParams
    {

        #region "Class variables"

        Dictionary<string, string> m_MgrParams;
        bool m_MCParamsLoaded;

        #endregion

        #region "Methods"

        public clsMgrSettings()
        {
            if (!LoadSettings())
            {
                throw new ApplicationException("Unable to initialize manager settings class");
            }
        }

        public bool LoadSettings()
        {

            // If the param dictionary exists, it needs to be cleared out
            if (m_MgrParams != null)
            {
                m_MgrParams.Clear();
                m_MgrParams = null;
            }

            // Get settings from config file
            m_MgrParams = LoadMgrSettingsFromFile();

            // Test the settings retrieved from the config file
            if (!CheckInitialSettings(m_MgrParams))
            {
                // Error logging handled by CheckInitialSettings
                return false;
            }

            // Determine if manager is deactivated locally
            if (!bool.Parse(m_MgrParams["MgrActive_Local"]))
            {
                var msg = "Manager deactivated locally";
                PRISM.ConsoleMsgUtils.ShowWarning(msg);
                LogWarning(msg);
                return false;
            }

            // Get remaining settings from database
            if (!LoadMgrSettingsFromDB(m_MgrParams))
            {
                // Error logging handled by LoadMgrSettingsFromDB
                return false;
            }

            // Set flag indicating params have been loaded from MC db
            m_MCParamsLoaded = true;

            // No problems found
            return true;
        }

        private Dictionary<string, string> LoadMgrSettingsFromFile()
        {
            // Load initial settings into string dictionary for return
            var msgParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Manager config db connection string
            var conStr = PkgFolderCreateManager.Properties.Settings.Default.MgrCnfgDbConnectStr;
            msgParams.Add("MgrCnfgDbConnectStr", conStr);

            // Manager active flag
            var mgrActiveLocal = PkgFolderCreateManager.Properties.Settings.Default.MgrActive_Local;
            msgParams.Add("MgrActive_Local", mgrActiveLocal);

            // Manager name
            var mgrName = PkgFolderCreateManager.Properties.Settings.Default.MgrName;
            msgParams.Add("MgrName", mgrName);

            // Default settings in use flag
            var usingDefaults = PkgFolderCreateManager.Properties.Settings.Default.UsingDefaults;
            msgParams.Add("UsingDefaults", usingDefaults);

            return msgParams;
        }

        private bool CheckInitialSettings(IDictionary<string, string> mgrParams)
        {
            // Verify manager settings dictionary exists
            if (mgrParams == null)
            {
                WriteErrorToSystemLog("clsMgrSettings.CheckInitialSettings(); Manager parameter dictionary is null");
                return false;
            }

            if (!mgrParams.TryGetValue("UsingDefaults", out var usingDefaults) || string.IsNullOrEmpty(usingDefaults))
            {
                WriteErrorToSystemLog("clsMgrSettings.CheckInitialSettings(); usingDefaults manager parameter not defined");
                return false;
            }

            // Verify intact config file was found
            if (bool.Parse(usingDefaults))
            {
                WriteErrorToSystemLog("clsMgrSettings.CheckInitialSettings(); Config file problem, default settings being used");
                return false;
            }

            // No problems found
            return true;
        }

        public bool LoadMgrSettingsFromDB(Dictionary<string, string> mgrSettings)
        {
            // Requests manager parameters from database. Input string specifies view to use. Performs retries if necessary.
            short retryCount = 3;

            if (!m_MgrParams.TryGetValue("MgrName", out var managerName))
            {
                WriteErrorMsg("Manager parameter MgrName is not defined");
                return false;
            }

            var sqlStr = "SELECT ParameterName, ParameterValue FROM V_MgrParams WHERE ManagerName = '" + managerName + "'";

            // Get a table containing data for job
            DataTable dt = null;

            if (!mgrSettings.TryGetValue("MgrCnfgDbConnectStr", out var connectionString))
            {
                WriteErrorMsg("Manager setting MgrCnfgDbConnectStr is not defined");
                return false;
            }

            // Get a datatable holding the parameters for one manager
            while (retryCount > 0)
            {
                try
                {
                    using (var cn = new SqlConnection(connectionString))
                    {
                        using (var da = new SqlDataAdapter(sqlStr, cn))
                        {
                            using (var ds = new DataSet())
                            {
                                da.Fill(ds);
                                dt = ds.Tables[0];
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    retryCount -= 1;
                    var msg = "Exception getting manager settings from database: " + ex.Message + ", RetryCount = " + retryCount;

                    WriteErrorMsg(msg);
                    // Delay for 5 seconds before trying again
                    System.Threading.Thread.Sleep(5000);
                }
            }

            // If loop exited due to errors, return false
            if (retryCount < 1)
            {
                var msg = "Excessive failures attempting to retrieve manager settings from database";
                WriteErrorMsg(msg);
                dt?.Dispose();
                return false;
            }

            if (dt == null)
                return false;

            // Verify at least one row returned
            if (dt.Rows.Count < 1)
            {
                // Wrong number of rows returned
                var msg = "Settings not found for manager " + managerName + "; connection string " + connectionString;
                WriteErrorMsg(msg);
                dt.Dispose();
                return false;
            }

            // Fill a string dictionary with the manager parameters that have been found
            try
            {
                foreach (DataRow currentRow in dt.Rows)
                {
                    // Add the column heading and value to the dictionary
                    var paramKey = DbCStr(currentRow[dt.Columns["ParameterName"]]);
                    var paramVal = DbCStr(currentRow[dt.Columns["ParameterValue"]]);
                    if (m_MgrParams.ContainsKey(paramKey))
                    {
                        m_MgrParams[paramKey] = paramVal;
                    }
                    else
                    {
                        m_MgrParams.Add(paramKey, paramVal);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                var msg = "Exception parsing manager settings retrieved from the database: " + ex.Message;
                WriteErrorMsg(msg);
                return false;
            }
            finally
            {
                dt.Dispose();
            }
        }

        public string GetParam(string itemKey)
        {
            if (m_MgrParams.ContainsKey(itemKey))
                return m_MgrParams[itemKey];

            return String.Empty;
        }

        public void SetParam(string itemKey, string itemValue)
        {
            m_MgrParams[itemKey] = itemValue;
        }

        private string DbCStr(object inpObj)
        {
            if (inpObj == null)
            {
                return "";
            }
            return inpObj.ToString();
        }

        private void LogWarning(string message)
        {
            PRISM.ConsoleMsgUtils.ShowWarning(message);
            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, message);
        }

        private void WriteErrorMsg(string message)
        {

            if (m_MCParamsLoaded)
            {
                PRISM.ConsoleMsgUtils.ShowError(message);
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, message);
            }
            else
            {
                WriteErrorToSystemLog(message);
            }
        }

        private void WriteErrorToSystemLog(string message)
        {
            PRISM.ConsoleMsgUtils.ShowError(message);
            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogSystem, clsLogTools.LogLevels.ERROR, message);
        }

        #endregion
    }
}
