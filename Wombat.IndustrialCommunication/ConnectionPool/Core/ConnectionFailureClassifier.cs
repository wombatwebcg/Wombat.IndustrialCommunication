using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    internal static class ConnectionFailureClassifier
    {
        public static bool IsRecoverable(OperationResult result)
        {
            if (result == null || result.IsCancelled)
            {
                return false;
            }

            switch (result.FailureKind)
            {
                case OperationFailureKind.Timeout:
                case OperationFailureKind.ConnectionClosed:
                case OperationFailureKind.Transport:
                    return true;
                case OperationFailureKind.Cancelled:
                case OperationFailureKind.Validation:
                case OperationFailureKind.Protocol:
                case OperationFailureKind.Business:
                    return false;
            }

            if (IsRecoverableException(result.Exception))
            {
                return true;
            }

            var message = result.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            message = message.ToLowerInvariant();
            return message.Contains("timeout")
                || message.Contains("timed out")
                || message.Contains("connection closed")
                || message.Contains("connection reset")
                || message.Contains("connection refused")
                || message.Contains("not connected")
                || message.Contains("broken pipe")
                || message.Contains("socket")
                || message.Contains("超时")
                || message.Contains("连接已关闭")
                || message.Contains("连接关闭")
                || message.Contains("连接断开")
                || message.Contains("连接中断")
                || message.Contains("连接被关闭")
                || message.Contains("远程主机强迫关闭")
                || message.Contains("套接字");
        }

        private static bool IsRecoverableException(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.InnerExceptions.Any(IsRecoverableException);
            }

            return exception is TimeoutException
                || exception is SocketException
                || exception is IOException
                || exception is ObjectDisposedException
                || IsRecoverableException(exception.InnerException);
        }
    }
}
