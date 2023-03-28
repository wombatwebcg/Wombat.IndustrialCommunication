using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using System;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication
{






    abstract public class BaseModel : IBaseModel, IDisposable 
    {




        public BaseModel()
        {

        }

        public ILogger Logger { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(1500);

        public TimeSpan WaiteInterval { get; set; } = TimeSpan.FromMilliseconds(10);


        public bool IsPrintCommand { get; set; } = false;

        public virtual void UseLogger()
        {
            LogHelper.Build();
            Logger = new SerilogLoggerFactory(LogHelper.Log).CreateLogger<BaseModel>();
            IsPrintCommand = true;
        }


        public bool IsClearCacheBeforeRead { get; set; } = true;

        public bool IsClearCacheAfterRead { get; set; } = true;

        public bool IsBaseStreamFlush { get; set; } = true;

        public bool IsUseLongConnect { get; set; } = true;

        /// <summary>
        /// 警告日志委托
        /// 为了可用性，会对异常网络进行重试。此类日志通过委托接口给出去。
        /// </summary>
        public  LoggerDelegate WarningLog { get; set; }


        abstract protected OperationResult DoConnect();

        abstract protected OperationResult DoDisconnect();

        public abstract bool IsConnect { get; }

        public int OperationReTryTimes { get; set; } = 2;

        public EndianFormat DataFormat { get; set; } = EndianFormat.ABCD;

        public bool IsReverse { get; set; } = true;

        public OperationResult Connect()
        {
            if (!IsConnect)
            {
                var result = DoConnect();
                return result;
            }
            else
            {
                return new OperationResult() { IsSuccess = true, Message = "已存在连接" };
            }
        }

        public OperationResult Disconnect()
        {
            return DoDisconnect();

        }





        /// <summary>
        /// 发送报文，并获取响应报文
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
       public  abstract OperationResult<byte[]> SendPackageReliable(byte[] command);



        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command">发送命令</param>
        /// <returns></returns>
       public abstract OperationResult<byte[]> SendPackageSingle(byte[] command);



        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Disconnect();
                    // TODO: 释放托管状态(托管对象)
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~CommonExecution()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }

}
