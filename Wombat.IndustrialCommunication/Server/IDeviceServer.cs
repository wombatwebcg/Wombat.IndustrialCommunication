using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Server
{
    public interface IDeviceServer:IServer,IReadWrite
    {
        bool EnableSnapshotPersistence { get; set; }
        string SnapshotFilePath { get; set; }
        OperationResult DeleteSnapshot();
        OperationResult ResetDataAndDeleteSnapshot();
        void ConfigureSnapshotPersistence(string name = null);
    }
}
