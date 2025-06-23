using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.Network
{
    /// <summary>
    /// 等待数据对象
    /// </summary>
    /// <typeparam name="T"></typeparam>

    /// <summary>
    /// 用于等待通信结果的类，支持同步和异步等待，并支持取消和超时功能。
    /// </summary>
    /// <typeparam name="T">等待结果的类型。</typeparam>
    public class CommunicationResult<T> : IDisposable
    {
        private readonly AutoResetEvent _waitHandle;
        private readonly object _lock = new object();
        private volatile CommunicationResultStatus _status;
        private TaskCompletionSource<T> _tcs;
        private CancellationTokenRegistration _tokenRegistration;
        private bool disposedValue;

        /// <summary>
        /// 初始化 <see cref="CommunicationResult{T}"/> 类的新实例。
        /// </summary>
        public CommunicationResult()
        {
            _waitHandle = new AutoResetEvent(false);
            Reset();
        }

        /// <summary>
        /// 获取当前通信的状态。
        /// </summary>
        public CommunicationResultStatus Status => _status;

        /// <summary>
        /// 获取通信结果。
        /// </summary>
        public T Result { get; private set; }

        /// <summary>
        /// 取消当前的等待操作。
        /// </summary>
        public void Cancel()
        {
            lock (_lock)
            {
                if (_status == CommunicationResultStatus.Waiting)
                {
                    _status = CommunicationResultStatus.Canceled;
                    _waitHandle.Set();
                    _tcs?.TrySetCanceled();
                }
            }
        }

        /// <summary>
        /// 重置状态和结果，使对象可以被重新使用。
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _tokenRegistration.Dispose();
                _status = CommunicationResultStatus.Default;
                Result = default;
                _waitHandle.Reset();
                _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>
        /// 设置状态为完成，并解除所有等待线程。
        /// </summary>
        /// <returns>如果成功设置状态，则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool Set()
        {
            lock (_lock)
            {
                if (_status == CommunicationResultStatus.Waiting)
                {
                    _status = CommunicationResultStatus.Completed;
                    _waitHandle.Set();
                    _tcs?.TrySetResult(Result);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 设置结果并解除所有等待线程。
        /// </summary>
        /// <param name="result">要设置的结果。</param>
        /// <returns>如果成功设置结果，则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool Set(T result)
        {
            lock (_lock)
            {
                if (_status == CommunicationResultStatus.Waiting)
                {
                    Result = result;
                    _status = CommunicationResultStatus.Completed;
                    _waitHandle.Set();
                    _tcs?.TrySetResult(result);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 设置取消令牌，使等待可以被取消。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                lock (_lock)
                {
                    _tokenRegistration.Dispose();
                    _tokenRegistration = cancellationToken.Register(Cancel);
                }
            }
        }

        /// <summary>
        /// 同步等待指定时间。
        /// </summary>
        /// <param name="timeout">超时时间。</param>
        /// <returns>等待的结果状态。</returns>
        public CommunicationResultStatus Wait(TimeSpan timeout)
        {
            return Wait((int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// 同步等待指定毫秒数。
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间（毫秒）。</param>
        /// <returns>等待的结果状态。</returns>
        public CommunicationResultStatus Wait(int timeoutMilliseconds)
        {
            if (_waitHandle.WaitOne(timeoutMilliseconds))
            {
                return _status;
            }

            lock (_lock)
            {
                if (_status == CommunicationResultStatus.Waiting)
                {
                    _status = CommunicationResultStatus.Timeout;
                }
            }
            return _status;
        }

        /// <summary>
        /// 异步等待结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务，返回等待的结果。</returns>
        public async Task<T> WaitAsync(CancellationToken cancellationToken = default)
        {
            SetCancellationToken(cancellationToken);
            return await _tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// 释放资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _status = CommunicationResultStatus.Disposed;
                    _tokenRegistration.Dispose();
                    _waitHandle.Dispose();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// 释放托管和非托管资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
