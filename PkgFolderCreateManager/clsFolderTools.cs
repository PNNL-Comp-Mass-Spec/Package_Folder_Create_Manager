
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 07/07/2009
//*********************************************************************************************************

using System;
using System.Collections.Generic;
using System.IO;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Performs folder creation
    /// </summary>
    public class clsFolderTools : clsLoggerBase
    {
        // Ignore Spelling: cmd

        #region "Constants"

        private const bool WARN_IF_DIRECTORY_EXISTS = true;
        private const bool NO_WARN_IF_DIRECTORY_EXISTS = false;

        #endregion

        #region "Methods"

        /// <summary>
        /// Creates specified directory
        /// </summary>
        /// <param name="perspective"></param>
        /// <param name="cmdParams">Dictionary containing parameters for directory creation</param>
        /// <param name="source"></param>
        public static void CreateDirectory(string perspective, IReadOnlyDictionary<string, string> cmdParams, string source)
        {
            var msg = "Processing command for package " + cmdParams["package"] + " (Source = " + source + ")";
            LogMessage(msg);

            // // Test for add or update
            // if (cmdParams["cmd"].ToLower() != "add")
            // {
            //   // Ignore the command if it isn't an "add"
            //   msg = "Package " + cmdParams["package"] + ", command '" + cmdParams["cmd"] + "' not supported. Message ignored";
            //   LogWarning(msg);
            //   return;
            // }
            // else
            // {
            //   msg = "Creating directory for package " + cmdParams["package"];
            //   LogInfo(msg);
            // }

            // Determine if client or server perspective and initialize path
            string directoryPath;
            string[] pathParts;
            int XMLParamVersion;

            if (cmdParams.ContainsKey("Path_Shared_Root"))
            {
                // New-style XML
                // cmdParams will have entries named: package, Path_Local_Root, Path_Shared_Root, and Path_Directory

                XMLParamVersion = 1;

                if (perspective.ToLower() == "client")
                {
                    directoryPath = cmdParams["Path_Shared_Root"];
                }
                else
                {
                    directoryPath = cmdParams["Path_Local_Root"];
                }


            }
            else
            {
                // Old-style XML
                // cmdParams will have entries named: package, local, share, year, team, directory, and ID

                XMLParamVersion = 0;

                if (perspective.ToLower() == "client")
                {
                    directoryPath = cmdParams["share"];
                }
                else
                {
                    directoryPath = cmdParams["local"];
                }

            }

            // Determine if root-level directory exists; error out if not present
            if (!Directory.Exists(directoryPath))
            {
                msg = "Root directory " + directoryPath + " not found";
                LogError(msg);
                return;
            }

            if (XMLParamVersion == 1)
            {
                // Parse directory string
                pathParts = cmdParams["Path_Directory"].Split(new[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                pathParts = new string[3];
                pathParts[0] = cmdParams["team"];
                pathParts[1] = cmdParams["year"];
                pathParts[2] = cmdParams["directory"];
            }

            // Create desired path, one subdirectory at a time
            for (var i = 0; i < pathParts.Length; i++)
            {
                directoryPath = Path.Combine(directoryPath, pathParts[i]);
                bool logIfAlreadyExists;
                if (i == pathParts.Length - 1)
                    logIfAlreadyExists = true;
                else
                    logIfAlreadyExists = false;

                if (!CreateDirectoryIfNotFound(directoryPath, NO_WARN_IF_DIRECTORY_EXISTS, logIfAlreadyExists))
                {
                    // Couldn't create directory, so exit
                    // Error reporting handled within called function
                    return;
                }
            }
        }

        /// <summary>
        /// Creates specified directory
        /// </summary>
        /// <param name="directoryPath">Path to the directory to create</param>
        /// <param name="warnIfExists">When true, log a warning if the directory exists</param>
        /// <param name="logIfExists">When true, log a status message if the directory exists</param>
        /// <returns>TRUE for success; FALSE otherwise</returns>
        private static bool CreateDirectoryIfNotFound(string directoryPath, bool warnIfExists, bool logIfExists)
        {
            if (Directory.Exists(directoryPath))
            {
                // Directory exists
                var msg = "Directory " + directoryPath + " already exists";
                if (warnIfExists)
                {
                    LogWarning(msg);
                }
                else
                {
                    if (logIfExists)
                        LogMessage(msg);
                }
                return true;
            }

            // Directory not found, so try to create it
            try
            {
                Directory.CreateDirectory(directoryPath);
                var msg = "Directory " + directoryPath + " created";
                LogMessage(msg);
                return true;
            }
            catch (Exception ex)
            {
                var msg = "Exception creating directory " + directoryPath;
                LogError(msg, ex);
                return false;
            }
        }

        #endregion
    }
}
