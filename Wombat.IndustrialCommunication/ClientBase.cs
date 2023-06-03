using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Core;
using Wombat.Infrastructure;


namespace Wombat.IndustrialCommunication
{


    abstract public class ClientBase : IClient, IDisposable 
    {




        public ClientBase()
        {

        }

        public ILog Logger { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(1500);

        public TimeSpan WaiteInterval { get; set; } = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// 获取或设置接收操作的超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// 获取或设置发送操作的超时时间
        /// </summary>
        public TimeSpan SendTimeout { get; set; } = TimeSpan.Zero;



        public virtual void UseLogger()
        {
            LogHelper.Build();
            Logger = new LoggerBuilder().UseConsoleLogger().UseFileLogger().CreateLogger();
        }


        public bool IsUseLongConnect { get; set; } = true;

        /// <summary>
        /// 警告日志委托
        /// 为了可用性，会对异常网络进行重试。此类日志通过委托接口给出去。
        /// </summary>
        public  LoggerDelegate WarningLog { get; set; }


        abstract internal OperationResult DoConnect();

        abstract internal OperationResult DoDisconnect();

        abstract internal Task<OperationResult> DoConnectAsync();

        abstract internal Task<OperationResult> DoDisconnectAsync();


        public abstract bool Connected { get; }

        public int OperationReTryTimes { get; set; } = 2;

        public EndianFormat DataFormat { get; set; } = EndianFormat.ABCD;

        public bool IsReverse { get; set; } = true;

        public OperationResult Connect()
        {
            if (!Connected)
            {
                var result = DoConnect();
                return result;
            }
            else
            {
                return new OperationResult() { IsSuccess = true, Message = "已存在连接" };
            }
        }
        public async Task<OperationResult> ConnectAsync()
        {
            if (!Connected)
            {
                var result = await DoConnectAsync();
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

        public async Task<OperationResult> DisconnectAsync()
        {
          return  await DoDisconnectAsync();
        }



        internal abstract OperationResult<byte[]> GetMessageContent(byte[] command);

        internal abstract ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command);

        internal abstract OperationResult<byte[]> InterpretAndExtractMessageData(byte[] command);

        internal abstract ValueTask<OperationResult<byte[]>> InterpretAndExtractMessageDataAsync(byte[] command);



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
