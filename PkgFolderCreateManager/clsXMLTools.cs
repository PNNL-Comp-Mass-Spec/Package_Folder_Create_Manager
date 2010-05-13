﻿
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/29/2009
//
// Last modified 06/29/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
	public class clsXMLTools
	{
		//*********************************************************************************************************
		// Tools for parsing input XML
		//**********************************************************************************************************

		#region "Methods"
			/// <summary>
			/// Converts command XML string into a dictionary of strings
			/// </summary>
			/// <param name="InputXML">XML string to parse</param>
			/// <returns>String dictionary of command sections</returns>
			public static StringDictionary ParseCommandXML(string InputXML)
			{
				StringDictionary returnDict = new StringDictionary();

				XmlDocument doc = new XmlDocument();
				doc.LoadXml(InputXML);

				try
				{
					returnDict.Add("package",doc.SelectSingleNode("//package").InnerText);
					returnDict.Add("path_local_root", doc.SelectSingleNode("//path_local_root").InnerText);
					returnDict.Add("path_shared_root", doc.SelectSingleNode("//path_shared_root").InnerText);
					returnDict.Add("path_folder", doc.SelectSingleNode("//path_folder").InnerText);
					//returnDict.Add("cmd", doc.SelectSingleNode("//cmd").InnerText);

					return returnDict;
				}
				catch (Exception Ex)
				{
					throw new Exception("", Ex);	// Message parameter left blank because it is handled at higher level
				}
			}	// End sub

			/// <summary>
			/// Converts broadcast XML string into a dictionary of strings
			/// </summary>
			/// <param name="InputXML">XML string to parse</param>
			/// <returns>String dictionary of broadcast sections</returns>
			public static clsBroadcastCmd ParseBroadcastXML(string InputXML)
			{
				clsBroadcastCmd returnedData = new clsBroadcastCmd();

				try
				{
					XmlDocument doc = new XmlDocument();
					doc.LoadXml(InputXML);

					// Get list of managers this command applies to
					foreach (XmlNode xn in doc.SelectNodes("//Managers/*"))
					{
						returnedData.MachineList.Add(xn.InnerText);
					}

					// Get command contained in message
					returnedData.MachCmd = doc.SelectSingleNode("//Message").InnerText;

					// Return the parsing results
					return returnedData;
				}
				catch (Exception Ex)
				{
					throw new Exception("Exception while parsing broadcast string", Ex);
				}
			}	// End sub
		#endregion
	}	// End class
}	// End namespace
