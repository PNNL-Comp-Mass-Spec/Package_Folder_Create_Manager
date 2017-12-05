﻿
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy 
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 09/15/2009
//
// Last modified 09/15/2009
//*********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;

namespace PkgFolderCreateManager
{
    public interface ITaskParams
    {
        //*********************************************************************************************************
        // Interface for step task parameters
        //**********************************************************************************************************

        #region "Properties"
            StringDictionary TaskDictionary { get; }
        #endregion

        #region "Methods"
        string GetParam(string name);
            bool AddAdditionalParameter(string paramName, string paramValue);
            void SetParam(string keyName, string value);
        #endregion
    }    // End interface
}    // End namespace
