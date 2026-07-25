using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
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
