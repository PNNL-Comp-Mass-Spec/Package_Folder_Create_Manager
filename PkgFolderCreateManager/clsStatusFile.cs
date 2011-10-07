
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/18/2009
//
// Last modified 06/18/2009
//						- 08/14/2009 (DAC) - Added additional parameters and methods for status reporting
//						- 08/21/2009 (DAC) - Added duration in minutes to status output
//*********************************************************************************************************
using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;

namespace PkgFolderCreateManager
{
	class clsStatusFile : IStatusFile
	{
		//*********************************************************************************************************
		// Provides tools for creating and updating a task status file
		//**********************************************************************************************************

		#region "Class variables"
			string m_FileNamePath;
			clsMessageHandler m_MsgHandler;
		#endregion

		#region "Properties"
			public string FileNamePath { get; set; }

			public string MgrName { get; set; }

			public EnumMgrStatus MgrStatus { get; set; }

			public DateTime LastStartTime { get; set; }

			public int CpuUtilization { get; set; }

			public string Tool { get; set; }

			public EnumTaskStatus TaskStatus { get; set; }

			public Single Duration { get; set; }

			public Single Progress { get; set; }

			public string CurrentOperation { get; set; }

			public EnumTaskStatusDetail TaskStatusDetail { get; set; }

			public int JobNumber { get; set; }

			public int JobStep { get; set; }

			public string Dataset { get; set; }

			public string MostRecentJobInfo { get; set; }

			public int SpectrumCount { get; set; }

			public bool LogToMsgQueue { get; set; }
		#endregion

		#region "Constructors"
			/// <summary>
			/// Constructor
			/// </summary>
			public clsStatusFile(string FileLocation, clsMessageHandler MsgHandler)
			{
				m_FileNamePath = FileLocation;
				m_MsgHandler = MsgHandler;
				LastStartTime = System.DateTime.Now;
				Progress = 0;
				SpectrumCount = 0;
				Dataset = "";
				JobNumber = 0;
				Tool = "";
			}	// End sub
		#endregion

		#region "Methods"
			/// <summary>
			/// Converts the manager status enum to a string value
			/// </summary>
			/// <param name="StatusEnum">An EnumMgrStatus object</param>
			/// <returns>String representation of input object</returns>
			private string ConvertMgrStatusToString(EnumMgrStatus StatusEnum)
			{
				return StatusEnum.ToString("G");
			}	// End sub

			/// <summary>
			/// Converts the task status enum to a string value
			/// </summary>
			/// <param name="StatusEnum">An EnumTaskStatus object</param>
			/// <returns>String representation of input object</returns>
			private string ConvertTaskStatusToString(EnumTaskStatus StatusEnum)
			{
				return StatusEnum.ToString("G");
			}	// End sub

			/// <summary>
			/// Converts the task detail status enum to a string value
			/// </summary>
			/// <param name="StatusEnum">An EnumTaskStatusDetail object</param>
			/// <returns>String representation of input object</returns>
			private string ConvertTaskDetailStatusToString(EnumTaskStatusDetail StatusEnum)
			{
				return StatusEnum.ToString("G");
			}	// End sub

			/// <summary>
			/// Writes the status file
			/// </summary>
			public void WriteStatusFile()
			{
				//Writes a status file for external monitor to read

				System.Xml.XmlDocument XDocument = default(System.Xml.XmlDocument);
				XmlTextWriter XWriter = default(XmlTextWriter);

				MemoryStream MemStream = default(MemoryStream);
				StreamReader MemStreamReader = default(StreamReader);

				string XMLText = string.Empty;

				//Set up the XML writer
				try
				{
					XDocument = new System.Xml.XmlDocument();

					//Create a memory stream to write the document in
					MemStream = new MemoryStream();
					XWriter = new XmlTextWriter(MemStream, System.Text.Encoding.UTF8);

					XWriter.Formatting = Formatting.Indented;
					XWriter.Indentation = 2;

					//Write the file
					XWriter.WriteStartDocument(true);
					//Root level element
					XWriter.WriteStartElement("Root");
					XWriter.WriteStartElement("Manager");
					XWriter.WriteElementString("MgrName", MgrName);
					XWriter.WriteElementString("MgrStatus", ConvertMgrStatusToString(MgrStatus));
					XWriter.WriteElementString("LastUpdate", System.DateTime.Now.ToString());
					XWriter.WriteElementString("LastStartTime", LastStartTime.ToString());
					XWriter.WriteElementString("CPUUtilization", CpuUtilization.ToString());
					XWriter.WriteElementString("FreeMemoryMB", "0");
					XWriter.WriteStartElement("RecentErrorMessages");
					foreach (string ErrMsg in clsStatusData.ErrorQueue)
					{
						XWriter.WriteElementString("ErrMsg", ErrMsg);
					}
					XWriter.WriteEndElement();		//Error messages
					XWriter.WriteEndElement();		//Manager section

					XWriter.WriteStartElement("Task");
					XWriter.WriteElementString("Tool", Tool);
					XWriter.WriteElementString("Status", ConvertTaskStatusToString(TaskStatus));
					XWriter.WriteElementString("Duration", Duration.ToString("##0.0"));
					XWriter.WriteElementString("DurationMinutes", (60.0F * Duration).ToString("##0.0"));
					XWriter.WriteElementString("Progress", Progress.ToString("##0.00"));
					XWriter.WriteElementString("CurrentOperation", CurrentOperation);
					XWriter.WriteStartElement("TaskDetails");
					XWriter.WriteElementString("Status", ConvertTaskDetailStatusToString(TaskStatusDetail));
					XWriter.WriteElementString("Job", JobNumber.ToString());
					XWriter.WriteElementString("Step", JobStep.ToString());
					XWriter.WriteElementString("Dataset", Dataset);
					XWriter.WriteElementString("MostRecentLogMessage", clsStatusData.MostRecentLogMessage);
					XWriter.WriteElementString("MostRecentJobInfo", MostRecentJobInfo);
					XWriter.WriteElementString("SpectrumCount", SpectrumCount.ToString());
					XWriter.WriteEndElement();		//Task details section
					XWriter.WriteEndElement();		//Task section
					XWriter.WriteEndElement();		//Root section

					//Close the document, but don't close the writer yet
					XWriter.WriteEndDocument();
					XWriter.Flush();

					//Use a streamreader to copy the XML text to a string variable
					MemStream.Seek(0, SeekOrigin.Begin);
					MemStreamReader = new System.IO.StreamReader(MemStream);
					XMLText = MemStreamReader.ReadToEnd();

					MemStreamReader.Close();
					MemStream.Close();

					//Since the document is now in a string, we can close the XWriter
					XWriter.Close();
					XWriter = null;
					GC.Collect();
					GC.WaitForPendingFinalizers();

					//Write the output file
					StreamWriter OutFile = default(StreamWriter);
					try
					{
						OutFile = new StreamWriter(new FileStream(m_FileNamePath, FileMode.Create, FileAccess.Write, FileShare.Read));
						OutFile.WriteLine(XMLText);
						OutFile.Close();
					}
					catch (Exception ex)
					{
						//TODO: Figure out appropriate action
					}
				}
				catch
				{
					//TODO: Figure out appropriate action
				}

				//Log to a message queue
				if (LogToMsgQueue)
				{
					LogStatusToMessageQueue(XMLText);
				}
			} // End sub

			/// <summary>
			/// Updates status file (Overloaded)
			/// </summary>
			/// <param name="PercentComplete">Job completion percentage</param></param>
			public void UpdateAndWrite(float PercentComplete)
			{
				Progress = PercentComplete;

				this.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Updates status file (Overloaded)
			/// </summary>
			/// <param name="Status">Job status enum</param>
			/// <param name="PercentComplete">Job completion percentage</param></param></param>
			public void UpdateAndWrite(EnumTaskStatusDetail Status, float PercentComplete)
			{
				TaskStatusDetail = Status;
				Progress = PercentComplete;

				this.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Updates status file (Overloaded)
			/// </summary>
			/// <param name="Status">Job status enum</param>
			/// <param name="PercentComplete">Job completion percentage</param></param></param>
			/// <param name="DTACount">Number of DTA files found for Sequest analysis</param>
			public void UpdateAndWrite(EnumTaskStatusDetail Status, float PercentComplete, int DTACount)
			{
				TaskStatusDetail = Status;
				Progress = PercentComplete;
				SpectrumCount = DTACount;

				this.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Sets status file to show mahager not running
			/// </summary>
			/// <param name="MgrError">TRUE if manager not running due to error; FALSE otherwise</param>
			public void UpdateStopped(bool MgrError)
			{
				if (MgrError)
				{
					MgrStatus = EnumMgrStatus.Stopped_Error;
				}
				else
				{
					MgrStatus = EnumMgrStatus.Stopped;
				}
				Progress = 0;
				SpectrumCount = 0;
				Dataset = "";
				JobNumber = 0;
				Tool = "";
				TaskStatus = EnumTaskStatus.No_Task;
				TaskStatusDetail = EnumTaskStatusDetail.No_Task;

				this.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Updates status file to show manager disabled
			/// </summary>
			/// <param name="Local">TRUE if manager disabled locally, otherwise FALSE</param>
			public void UpdateDisabled(bool Local)
			{
				if (Local)
				{
					MgrStatus = EnumMgrStatus.Disabled_Local;
				}
				else
				{
					MgrStatus = EnumMgrStatus.Disabled_MC;
				}
				Progress = 0;
				SpectrumCount = 0;
				Dataset = "";
				JobNumber = 0;
				Tool = "";
				TaskStatus = EnumTaskStatus.No_Task;
				TaskStatusDetail = EnumTaskStatusDetail.No_Task;

				this.WriteStatusFile();
			}	// End sub

			/// <summary>
			/// Writes the status to the message queue
			/// </summary>
			/// <param name="strStatusXML">A string contiaining the XML to write</param>
			protected void LogStatusToMessageQueue(string strStatusXML)
			{
				DateTime dtLastFailureTime = System.DateTime.MinValue;

				try
				{
					m_MsgHandler.SendMessage(strStatusXML);
				}
				catch (Exception Ex)
				{
					string msg = "Exception sending status message to broker";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.ERROR,msg);
				}
			}	// End sub

			/// <summary>
			/// Initializes the status from a file, if file exists
			/// </summary>
			public void InitStatusFromFile()
			{
				string XmlStr;
				XmlDocument Doc;

				//Verify status file exists
				if (!File.Exists(m_FileNamePath)) return;

				//Get data from status file
				try
				{
					XmlStr = File.ReadAllText(m_FileNamePath);
					//Convert to an XML document
					Doc = new XmlDocument();
					Doc.LoadXml(XmlStr);

					//Get the most recent log message
					clsStatusData.MostRecentLogMessage = Doc.SelectSingleNode("//Task/TaskDetails/MostRecentLogMessage").InnerText;

					//Get the most recent job info
					MostRecentJobInfo = Doc.SelectSingleNode("//Task/TaskDetails/MostRecentJobInfo").InnerText;

					//Get the error messsages
					foreach (XmlNode Xn in Doc.SelectNodes("//Manager/RecentErrorMessages/ErrMsg"))
					{
						clsStatusData.AddErrorMessage(Xn.InnerText);
					}
				}
				catch (Exception ex)
				{
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Exception reading status file", ex);
					return;
				}
			}	// End sub
		#endregion
	}	// End class
}	// End namespace
