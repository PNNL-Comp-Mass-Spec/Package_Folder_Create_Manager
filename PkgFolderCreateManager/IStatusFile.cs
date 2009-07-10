
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//
// Last modified 06/18/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PkgFolderCreateManager
{

	#region "Enums"
		//Status constants
		public enum EnumMgrStatus : short
		{
			Stopped,
			Stopped_Error,
			Running,
			Disabled_Local,
			Disabled_MC
		}

		public enum EnumTaskStatus : short
		{
			Stopped,
			Requesting,
			Running,
			Closing,
			Failed,
			No_Task
		}

		public enum EnumTaskStatusDetail : short
		{
			Retrieving_Resources,
			Running_Tool,
			Packaging_Results,
			Delivering_Results,
			No_Task
		}
	#endregion

	interface IStatusFile
	{
		//*********************************************************************************************************
		// Interface used by classes that create and update task status file
		//**********************************************************************************************************


		#region "Properties"
			string FileNamePath { get;set; }
			string MgrName { get; set; }
			EnumMgrStatus MgrStatus { get; set; }
			DateTime LastStartTime { get; set; }
			int CpuUtilization { get; set; }
			string Tool { get; set; }
			EnumTaskStatus TaskStatus { get; set; }
			Single Duration { get; set; }
			Single Progress { get; set; }
			string CurrentOperation { get; set; }
			EnumTaskStatusDetail TaskStatusDetail { get; set; }
			int JobNumber { get; set; }
			int JobStep { get; set; }
			string Dataset { get; set; }
			string MostRecentJobInfo { get; set; }
			int SpectrumCount { get; set; }
			bool LogToMsgQueue { get; set; }
		#endregion

		#region "Methods"
			void WriteStatusFile();
			void UpdateAndWrite(Single PercentComplete);
			void UpdateAndWrite(EnumTaskStatusDetail Status, Single PercentComplete);
			void UpdateAndWrite(EnumTaskStatusDetail Status, Single PercentComplete, int DTACount);
			void UpdateStopped(bool MgrError);
			void UpdateDisabled(bool Local);
		#endregion
	}	// End interface
}	// End namespace
