using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Core;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication
{

    //}
    /// <summary>
    /// 日记记录委托定义
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ex"></param>
    public delegate void LoggerDelegate(string name, Exception ex = null);

    public interface IClient
    {
        ILog Logger { get; set; }

        EndianFormat DataFormat { get; set; }

        TimeSpan Timeout { get; set; }

        TimeSpan ReceiveTimeout { get; set; }

        TimeSpan SendTimeout { get; set; }


        TimeSpan WaiteInterval { get; set; }

        int OperationReTryTimes { get; set; }

        void UseLogger();


        bool IsUseLongConnect { get; set; }

        bool Connected { get; }

        OperationResult Connect();

        OperationResult Disconnect();

        Task<OperationResult> ConnectAsync();

        Task<OperationResult> DisconnectAsync();



    }
}
