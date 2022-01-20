
//*********************************************************************************************************
// Written by Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 07/10/2009
//*********************************************************************************************************

using System.Collections.Generic;

namespace PkgFolderCreateManager
{
    /// <summary>
    /// Class to hold data received from Broadcast command queue for control of manager
    /// </summary>
    internal class clsBroadcastCmd
    {
        /// <summary>
        /// List of machines the received message applies to
        /// </summary>
        public List<string> MachineList { get; set; } = new();

        /// <summary>
        /// The command that was broadcast
        /// </summary>
        public string MachCmd { get; set; }
    }
}
