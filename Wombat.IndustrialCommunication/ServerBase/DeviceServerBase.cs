using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 设备服务器基类
    /// </summary>
    public abstract class DeviceServerBase : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        protected DeviceServerBase()
        {
        }

        /// <summary>
        /// 数据格式
        /// </summary>
        public EndianFormat DataFormat { get; set; } = EndianFormat.ABCD;

        /// <summary>
        /// 是否反转
        /// </summary>
        public bool IsReverse { get; set; } = false;

        /// <summary>
        /// 版本信息
        /// </summary>
        public abstract string Version { get; }

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                }

                _disposed = true;
            }
        }
    }
} 