
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
			/// <param name="FolderParams">String dictionary containing parameters for folder creation</param>
			public static void CreatePkgFolder(string Perspective, StringDictionary FolderParams)
			{
				string Msg = "Processing command for package " + FolderParams["package"];
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);

				// Test for add or update
				if (FolderParams["cmd"].ToLower() != "add")
				{
					// Ignore the command if it isn't an "add"
					Msg = "Package " + FolderParams["package"] + ", command '" + FolderParams["cmd"] + "' not supported. Message ignored";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.WARN, Msg);
					return;
				}
				else
				{
					Msg = "Creating folder for package " + FolderParams["package"];
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, Msg);
				}
				
				// Determine if client or server perspective and initialize path
				string FolderPath;
				if (Perspective.ToLower() == "client")
				{
					FolderPath = FolderParams["share"];
				}
				else
				{
					FolderPath=FolderParams["local"];
				}

				// Determine if team-level folder exists; create if necessary
				FolderPath = Path.Combine(FolderPath, FolderParams["team"]);
				if (!CreateFolderIfNotFound(FolderPath, NO_WARN_IF_FOLDER_EXISTS))
				{
					// Couldn't create folder, so exit
					// Error reporting handled within called function
					return;
				}

				// Determine if year-level folder exists; create if necessary
				FolderPath = Path.Combine(FolderPath, FolderParams["year"]);
				if (!CreateFolderIfNotFound(FolderPath, NO_WARN_IF_FOLDER_EXISTS))
				{
					// Couldn't create folder, so exit
					// Error reporting handled within called function
					return;
				}

				// Create the package folder
				//TODO: Does the pakage need to be padded with zeroes?
				FolderPath = Path.Combine(FolderPath, FolderParams["folder"]);
				if (!CreateFolderIfNotFound(FolderPath, WARN_IF_FOLDER_EXISTS))
				{
					// Couldn't create folder, so exit
					// Error reporting handled within called function
					return;
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
