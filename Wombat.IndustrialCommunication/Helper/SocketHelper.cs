﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Wombat.IndustrialCommunication
{
   public static class SocketHelper
    {
        /// <summary>
        /// 安全关闭
        /// </summary>
        /// <param name="socket"></param>
        internal static void SafeClose(this Socket socket)
        {
            try
            {
                if (socket?.Connected ?? false) socket?.Shutdown(SocketShutdown.Both);//正常关闭连接
            }
            catch { }

            try
            {
                socket?.Close();
            }
            catch { }
        }

    }
}
