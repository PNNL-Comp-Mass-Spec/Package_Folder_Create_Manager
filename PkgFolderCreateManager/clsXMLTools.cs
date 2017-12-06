
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/29/2009
//*********************************************************************************************************

using System;
using System.Xml;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Tools for parsing input XML
    /// </summary>
    public class clsXMLTools
    {

        #region "Methods"

        /// <summary>
        /// Converts command XML string into a dictionary of strings
        /// </summary>
        /// <param name="InputXML">XML string to parse</param>
        /// <returns>String dictionary of command sections</returns>
        public static StringDictionary ParseCommandXML(string InputXML)
        {
            var returnDict = new StringDictionary();
            var XMLParamVersion = -1;

            var doc = new XmlDocument();
            doc.LoadXml(InputXML);

            try
            {
                returnDict.Add("package", doc.SelectSingleNode("//package")?.InnerText);

                var oNode = doc.SelectSingleNode("//Path_Local_Root");
                if (oNode != null)
                    XMLParamVersion = 1;

                if (XMLParamVersion == -1)
                {
                    oNode = doc.SelectSingleNode("//local");

                    if (oNode != null)
                        XMLParamVersion = 0;
                }

                if (XMLParamVersion == -1)
                    throw new Exception("Unrecognized XML Format; should contain node Path_Local_Root or node local");

                if (XMLParamVersion == 0)
                {
                    returnDict.Add("local", doc.SelectSingleNode("//local")?.InnerText);
                    returnDict.Add("share", doc.SelectSingleNode("//share")?.InnerText);
                    returnDict.Add("year", doc.SelectSingleNode("//year")?.InnerText);
                    returnDict.Add("team", doc.SelectSingleNode("//team")?.InnerText);
                    returnDict.Add("folder", doc.SelectSingleNode("//folder")?.InnerText);
                    // returnDict.Add("ID", doc.SelectSingleNode("//ID").InnerText);
                    // returnDict.Add("cmd", doc.SelectSingleNode("//cmd").InnerText);
                }

                if (XMLParamVersion == 1)
                {
                    returnDict.Add("Path_Local_Root", doc.SelectSingleNode("//Path_Local_Root")?.InnerText);
                    returnDict.Add("Path_Shared_Root", doc.SelectSingleNode("//Path_Shared_Root")?.InnerText);
                    returnDict.Add("Path_Folder", doc.SelectSingleNode("//Path_Folder")?.InnerText);
                    // returnDict.Add("cmd", doc.SelectSingleNode("//cmd").InnerText);
                }

                return returnDict;
            }
            catch (Exception ex)
            {
                throw new Exception("", ex);    // Message parameter left blank because it is handled at higher level
            }
        }

        /// <summary>
        /// Converts broadcast XML string into a dictionary of strings
        /// </summary>
        /// <param name="InputXML">XML string to parse</param>
        /// <returns>String dictionary of broadcast sections</returns>
        public static clsBroadcastCmd ParseBroadcastXML(string InputXML)
        {
            var returnedData = new clsBroadcastCmd();

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(InputXML);

                // Get list of managers this command applies to
                foreach (XmlNode xn in doc.SelectNodes("//Managers/*"))
                {
                    returnedData.MachineList.Add(xn.InnerText);
                }

                // Get command contained in message
                returnedData.MachCmd = doc.SelectSingleNode("//Message")?.InnerText;

                // Return the parsing results
                return returnedData;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception while parsing broadcast string", ex);
            }
        }

        #endregion
    }
}
