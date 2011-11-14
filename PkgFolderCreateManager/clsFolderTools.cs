
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
				string[] pathParts;
				int XMLParamVersion = 0;

				if (folderParams.ContainsKey("Path_Shared_Root")) {
					// New-style XML
					// folderParams will have entries named: package, Path_Local_Root, Path_Shared_Root, and Path_Folder

					XMLParamVersion = 1;

					if (perspective.ToLower() == "client") {
						folderPath = folderParams["Path_Shared_Root"];
					} else {
						folderPath = folderParams["Path_Local_Root"];
					}


				} else {
					// Old-style XML
					// folderParams will have entries named: package, local, share, year, team, folder, and ID

					XMLParamVersion = 0;

					if (perspective.ToLower() == "client") {
						folderPath = folderParams["share"];
					} else {
						folderPath = folderParams["local"];
					}

				}

				// Determine if root-level folder exists; error out if not present
				if (!Directory.Exists(folderPath))
				{
					msg = "Root folder " + folderPath + " not found";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, msg);
					return;
				}

				if (XMLParamVersion == 1) {
					// Parse folder string
					pathParts = folderParams["Path_Folder"].Split(new string[] { @"\" }, StringSplitOptions.RemoveEmptyEntries);
				} else {
					pathParts = new string[3];
					pathParts[0] = folderParams["team"];
					pathParts[1] = folderParams["year"];
					pathParts[2] = folderParams["folder"];
				}

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
			/// Creates specified folder
			/// </summary>
			/// <param name="FolderName">Name of folder to create</param>
			/// <returns>TRUE for success; FALSE otherwise</returns>
			private static bool CreateFolderIfNotFound(string FolderName, bool WarnIfExists)
			{
				if (Directory.Exists(FolderName))
				{
					// Folder exists
					string Msg = "Folder " + FolderName + " already exists";
					if (WarnIfExists) {
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, Msg);
					} else {
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);
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
