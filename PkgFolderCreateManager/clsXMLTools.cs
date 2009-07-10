
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
					returnDict.Add("local", doc.SelectSingleNode("//local").InnerText);
					returnDict.Add("share", doc.SelectSingleNode("//share").InnerText);
					returnDict.Add("year", doc.SelectSingleNode("//year").InnerText);
					returnDict.Add("team", doc.SelectSingleNode("//team").InnerText);
					returnDict.Add("folder", doc.SelectSingleNode("//folder").InnerText);
					returnDict.Add("cmd", doc.SelectSingleNode("//cmd").InnerText);

					return returnDict;
				}
				catch (Exception Ex)
				{
					throw new Exception("Exception parsing command string", Ex);
				}
			}	// End sub

			/// <summary>
			/// Converts broadcast XML string into a dictionary of strings
			/// </summary>
			/// <param name="InputXML">XML string to parse</param>
			/// <returns>String dictionary of broadcast sections</returns>
			public static StringDictionary ParseBroadcast(string InputXML)
			{
				return null;
			}	// End sub
		#endregion
	}	// End class
}	// End namespace
