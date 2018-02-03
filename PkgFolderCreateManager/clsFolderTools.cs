
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 07/07/2009
//*********************************************************************************************************

using System;
using System.IO;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Performs folder creation
    /// </summary>
    public class clsFolderTools : clsLoggerBase
    {

        #region "Constants"

        private const bool WARN_IF_FOLDER_EXISTS = true;
        private const bool NO_WARN_IF_FOLDER_EXISTS = false;

        #endregion

        #region "Methods"

        /// <summary>
        /// Creates specified folder
        /// </summary>
        /// <param name="perspective"></param>
        /// <param name="folderParams">String dictionary containing parameters for folder creation</param>
        /// <param name="source"></param>
        public static void CreateFolder(string perspective, StringDictionary folderParams, string source)
        {
            var msg = "Processing command for package " + folderParams["package"] + " (Source = " + source + ")";
            ReportStatus(msg);

            // // Test for add or update
            // if (folderParams["cmd"].ToLower() != "add")
            // {
            //   // Ignore the command if it isn't an "add"
            //   msg = "Package " + folderParams["package"] + ", command '" + folderParams["cmd"] + "' not supported. Message ignored";
            //   LogWarning(msg);
            //   return;
            // }
            // else
            // {
            //   msg = "Creating folder for package " + folderParams["package"];
            //   LogInfo(msg);
            // }

            // Determine if client or server perspective and initialize path
            string folderPath;
            string[] pathParts;
            int XMLParamVersion;

            if (folderParams.ContainsKey("Path_Shared_Root"))
            {
                // New-style XML
                // folderParams will have entries named: package, Path_Local_Root, Path_Shared_Root, and Path_Folder

                XMLParamVersion = 1;

                if (perspective.ToLower() == "client")
                {
                    folderPath = folderParams["Path_Shared_Root"];
                }
                else
                {
                    folderPath = folderParams["Path_Local_Root"];
                }


            }
            else
            {
                // Old-style XML
                // folderParams will have entries named: package, local, share, year, team, folder, and ID

                XMLParamVersion = 0;

                if (perspective.ToLower() == "client")
                {
                    folderPath = folderParams["share"];
                }
                else
                {
                    folderPath = folderParams["local"];
                }

            }

            // Determine if root-level folder exists; error out if not present
            if (!Directory.Exists(folderPath))
            {
                msg = "Root folder " + folderPath + " not found";
                LogError(msg);
                return;
            }

            if (XMLParamVersion == 1)
            {
                // Parse folder string
                pathParts = folderParams["Path_Folder"].Split(new[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                pathParts = new string[3];
                pathParts[0] = folderParams["team"];
                pathParts[1] = folderParams["year"];
                pathParts[2] = folderParams["folder"];
            }

            // Create desired path, one subfolder at a time
            for (var indx = 0; indx < pathParts.Length; indx++)
            {
                folderPath = Path.Combine(folderPath, pathParts[indx]);
                bool bLogIfAlreadyExists;
                if (indx == pathParts.Length - 1)
                    bLogIfAlreadyExists = true;
                else
                    bLogIfAlreadyExists = false;

                if (!CreateFolderIfNotFound(folderPath, NO_WARN_IF_FOLDER_EXISTS, bLogIfAlreadyExists))
                {
                    // Couldn't create folder, so exit
                    // Error reporting handled within called function
                    return;
                }
            }
        }

        /// <summary>
        /// Creates specified folder
        /// </summary>
        /// <param name="folderPath">Path to the folder to create</param>
        /// <param name="warnIfExists">When true, log a warning if the directory exiss</param>
        /// <param name="logIfExists">When true, log a status message if the directory exists</param>
        /// <returns>TRUE for success; FALSE otherwise</returns>
        private static bool CreateFolderIfNotFound(string folderPath, bool warnIfExists, bool logIfExists)
        {
            if (Directory.Exists(folderPath))
            {
                // Folder exists
                var msg = "Folder " + folderPath + " already exists";
                if (warnIfExists)
                {
                    LogWarning(msg);
                }
                else
                {
                    if (logIfExists)
                        ReportStatus(msg);
                }
                return true;
            }

            // Folder not found, so try to create it
            try
            {
                Directory.CreateDirectory(folderPath);
                var msg = "Folder " + folderPath + " created";
                ReportStatus(msg);
                return true;
            }
            catch (Exception ex)
            {
                var msg = "Exception creating folder " + folderPath;
                LogError(msg, ex);
                return false;
            }
        }

        #endregion
    }
}
