
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/19/2009
//
// Last modified 06/19/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MessageLogger;
using System.Xml;
using System.Xml.XPath;
using MQCore;

namespace PkgFolderCreateManager
{
	class clsControlMessageTools
	{
		//*********************************************************************************************************
		// Handles receiving control commands from message broker
		//**********************************************************************************************************

		#region "Constants"
		#endregion

		#region "Class variables"
			private IMgrParams m_MgrSettings = null;
			private SimpleTopicSubscriber m_TopSubscribe;
		#endregion

		#region "Events"
		#endregion

		#region "Event handlers"
			private void subscriber_OnMessageReceived(string processor, string message)
			{
				//TODO: Parse the message and take appropriate action
			}

			void connection_OnConnectionException(Exception e)
			{
				string s = "=== Connection Exception ===" + Environment.NewLine;
				MessageBox.Show(s + e.Message);
			}
		#endregion

		#region "Properties"
		#endregion

		#region "Methods"
			/// <summary>
			/// Constructor
			/// </summary>
			/// <param name="MgrSettings">Mgr settings object</param>
			public clsControlMessageTools(clsMgrSettings MgrSettings)
			{
				m_MgrSettings = MgrSettings;
			}	// End sub

			public bool Initialize()
			{
				// Set up a message broker connection for receiving folder creation commands
				if (!InitializeMessageConnection())
				{
					return false;
				}

				// Initialization successful
				return true;
			}	// End sub

			/// <summary>
			/// Initializes a connection to the control command message broker
			/// </summary>
			private bool InitializeMessageConnection()
			{
				// initialize the connection parameter fields
				string msgBrokerURL = m_MgrSettings.GetParam("ControlQueueURI");
				string msgTopicName = m_MgrSettings.GetParam("ControlQueueTopic");

				// get a unique name for the message client
				DateTime tn = DateTime.Now; // Alternative: System.Guid.NewGuid().ToString();
				string strClientID = System.Net.Dns.GetHostEntry("localhost").HostName + '_' + tn.Ticks.ToString();

				if (this.m_TopSubscribe != null)
				{
					this.m_TopSubscribe.Dispose();
				}
				try
				{
					this.m_TopSubscribe = new SimpleTopicSubscriber(msgTopicName, msgBrokerURL, ref strClientID);
					this.m_TopSubscribe.OnMessageReceived += new MessageReceivedDelegate(subscriber_OnMessageReceived);
					this.m_TopSubscribe.OnConnectionException += new ConnectionExceptionDelegate(connection_OnConnectionException);
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile,clsLogTools.LogLevels.INFO,"Control broker connected");

					// Everything worked!
					return true;
				}
				catch (Exception Ex)
				{
					string ErrMsg = "Exception initializing connection to control broker";
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, ErrMsg, Ex);
					return false;
				}
			}	// End sub
		#endregion
	}	// End class
}	// End namespace
