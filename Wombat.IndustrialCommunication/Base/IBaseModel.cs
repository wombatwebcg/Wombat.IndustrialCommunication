using Microsoft.Extensions.Logging;
using System;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication
{

    //}
    /// <summary>
    /// 日记记录委托定义
    /// </summary>
    /// <param name="name"></param>
    /// <param name="ex"></param>
    public delegate void LoggerDelegate(string name, Exception ex = null);

    public interface IBaseModel
    {
        ILogger Logger { get; set; }

        EndianFormat DataFormat { get; set; }

        TimeSpan Timeout { get; set; }

        TimeSpan WaiteInterval { get; set; }

        int OperationReTryTimes { get; set; }

        bool IsUseLog { get; set; }

        void UseLogger();

        bool IsClearCacheBeforeRead { get; set; }

        bool IsClearCacheAfterRead { get; set; }

        bool IsBaseStreamFlush { get; set; }

        bool IsUseLongConnect { get; set; }

        bool IsConnect { get; }

        OperationResult Connect();

        OperationResult Disconnect();



    }
}
