
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 09/15/2009
//*********************************************************************************************************

using System.Collections.Generic;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Interface for step task parameters
    /// </summary>
    public interface ITaskParams
    {

        #region "Properties"

        Dictionary<string, string> TaskDictionary { get; }

        #endregion

        #region "Methods"

        string GetParam(string name);
        bool AddAdditionalParameter(string paramName, string paramValue);
        void SetParam(string keyName, string value);

        #endregion
    }
}
