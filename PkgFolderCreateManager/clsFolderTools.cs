
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 07/07/2009
//
// Last modified 07/07/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
	public class clsFolderTools
	{
		//*********************************************************************************************************
		// Performs folder creation
		//**********************************************************************************************************

		#region "Constants"
			private const bool WARN_IF_FOLDER_EXISTS = true;
			private const bool NO_WARN_IF_FOLDER_EXISTS = false;
		#endregion

		#region "Methods"
			/// <summary>
			/// Creates specified folder
			/// </summary>
			/// <param name="folderParams">String dictionary containing parameters for folder creation</param>
			public static void CreateFolder(string perspective, StringDictionary folderParams)
			{
				string msg = "Processing command for package " + folderParams["package"];
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);

				//// Test for add or update
				//if (folderParams["cmd"].ToLower() != "add")
				//{
				//   // Ignore the command if it isn't an "add"
				//   msg = "Package " + folderParams["package"] + ", command '" + folderParams["cmd"] + "' not supported. Message ignored";
				//   clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, msg);
				//   return;
				//}
				//else
				//{
				//   msg = "Creating folder for package " + folderParams["package"];
				//   clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);
				//}
				
				// Determine if client or server perspective and initialize path
				string folderPath;
				if (perspective.ToLower() == "client")
				{
					folderPath = folderParams["path_shared_root"];
				}
				else
				{
					folderPath = folderParams["path_local_root"];
				}

				// Determine if root-level folder exists; error out if not present
				if (!Directory.Exists(folderPath))
				{
					msg = "Root folder " + folderPath + " not found";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg);
					return;
				}

				// Parse folder string
				string[] pathParts = folderParams["path_folder"].Split(new string[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);

				// Create desired path, one subfolder at a time
				for (int indx = 0; indx < pathParts.Length; indx++)
				{
					folderPath = Path.Combine(folderPath, pathParts[indx]);
					if (!CreateFolderIfNotFound(folderPath, NO_WARN_IF_FOLDER_EXISTS))
					{
						// Couldn't create folder, so exit
						// Error reporting handled within called function
						return;
					}
				}
			}	// End sub

			/// <summary>
			/// Creates spacified folder
			/// </summary>
			/// <param name="FolderName">Name of folder to create</param>
			/// <returns>TRUE for success; FALSE otherwise</returns>
			private static bool CreateFolderIfNotFound(string FolderName, bool WarnIfExists)
			{
				if (Directory.Exists(FolderName))
				{
					// Folder exists
					if (WarnIfExists)
					{
						string Msg = "Folder " + FolderName + " already exists";
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, Msg);
					}
					return true;
				}

				// Folder not found, so try to create it
				try
				{
					Directory.CreateDirectory(FolderName);
					string Msg = "Folder " + FolderName + " created";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);
					return true;
				}
				catch (Exception Ex)
				{
					string Msg = "Exception creating folder " + FolderName;
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, Msg, Ex);
					return false;
				}
			}	// End sub
		#endregion
	}	// End class
}	// End namespace
