using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.Network
{
    public enum CommunicationResultStatus
    {
        /// <summary>
        /// 默认状态，表示尚未开始等待或设置结果。
        /// </summary>
        Default,

        /// <summary>
        /// 等待中，表示当前对象正在等待一个结果。
        /// </summary>
        Waiting,

        /// <summary>
        /// 已完成，表示等待的结果已成功设置。
        /// </summary>
        Completed,

        /// <summary>
        /// 超时，表示等待超过了指定的时间。
        /// </summary>
        Timeout,

        /// <summary>
        /// 被取消，表示等待被取消了。
        /// </summary>
        Canceled,

        /// <summary>
        /// 已被释放，表示对象已被释放，无法继续使用。
        /// </summary>
        Disposed
    }
}
