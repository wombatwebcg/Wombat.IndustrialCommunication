using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.Model;

namespace Wombat.IndustrialCommunication.Iec104.ModbusBridge
{
    /// <summary>
    /// 桥接层的 104 命令出口。生产环境由 <see cref="Iec104MasterValueSource"/> 实现，
    /// 测试可注入假实现，不必真的建链。
    /// </summary>
    public interface IIec104CommandSink
    {
        /// <summary>只发选择帧，等待选择确认。</summary>
        Task<Iec104CommandResult> SelectSingleCommandAsync(int ioa, bool on, CancellationToken cancellationToken);

        /// <summary>发执行帧。已选择过时用 Direct，未选择时用 SelectAndExecute。</summary>
        Task<Iec104CommandResult> SingleCommandAsync(
            int ioa,
            bool on,
            Iec104CommandMode mode,
            CancellationToken cancellationToken);
    }
}
