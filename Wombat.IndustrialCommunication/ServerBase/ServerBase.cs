using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;




namespace Wombat.IndustrialCommunication
{


    abstract public class ServerBase : IServer, IDisposable 
    {




        public ServerBase()
        {
        }

        public void UseLogger(ILogger logger)
        {
            throw new NotImplementedException();
        }

        public OperationResult Listen()
        {
            if (!IsListening)
            {
                var result = DoListen();
                return result;
            }
            else
            {
                return new OperationResult() { IsSuccess = true, Message = "已存在连接" };
            }
        }


        public OperationResult Shutdown()
        {
            return DoShutdown();
        }

        abstract internal OperationResult DoShutdown();


        abstract internal OperationResult DoListen();


        public abstract bool IsListening { get; }


        public EndianFormat DataFormat { get; set; } = EndianFormat.ABCD;

        public bool IsReverse { get; set; } = false;

        public abstract string Version { get; }
        public ILogger Logger { get; set; }

        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan WaiteInterval { get; set; } = TimeSpan.FromMilliseconds(10);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMilliseconds(500);




        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Shutdown();
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
