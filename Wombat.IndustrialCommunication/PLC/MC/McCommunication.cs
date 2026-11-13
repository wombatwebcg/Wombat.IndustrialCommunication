using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Core;
using Wombat.IndustrialCommunication.Helpers;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 协议通信层：地址解析、元素个数到协议点数的换算、分帧、位打包与结果整理。
    /// 对外长度语义沿用基类约定：字访问为字节数，位访问为点数。
    /// </summary>
    public class McCommunication : DeviceDataReaderWriterBase
    {
        internal AsyncLock _lock = new AsyncLock();

        /// <summary>日志</summary>
        public ILogger Logger { get; set; }

        /// <summary>是否输出调试日志</summary>
        public bool EnableDebugLog { get; set; }

        /// <summary>构造通信层</summary>
        /// <param name="transport">MC 传输层</param>
        /// <param name="profile">帧/系列派生规则</param>
        public McCommunication(McEthernetTransport transport, McProfile profile) : base(transport)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));

            Profile = profile ?? throw new ArgumentNullException(nameof(profile));

            // MC 字数据为「低字在前、字内小端」，对应 DCBA
            DataFormat = EndianFormat.DCBA;
            IsReverse = false;
        }

        /// <summary>帧/系列派生规则</summary>
        public McProfile Profile { get; }

        /// <inheritdoc />
        public override string Version
        {
            get { return "MC 1.0"; }
        }

        /// <summary>MC 传输层</summary>
        protected McEthernetTransport McTransport
        {
            get { return (McEthernetTransport)Transport; }
        }

        /// <inheritdoc />
        protected internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length,
            DataTypeEnums dataType, bool isBit = false)
        {
            return ReadAsync(address, length, dataType, isBit, CancellationToken.None);
        }

        /// <inheritdoc />
        protected internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length,
            DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                return await ReadCoreAsync(address, length, isBit, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected internal override Task<OperationResult> WriteAsync(string address, byte[] data,
            DataTypeEnums dataType, bool isBit = false)
        {
            return WriteAsync(address, data, dataType, isBit, CancellationToken.None);
        }

        /// <inheritdoc />
        protected internal override async Task<OperationResult> WriteAsync(string address, byte[] data,
            DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!isBit)
                    return await WriteWordsCoreAsync(address, data, cancellationToken).ConfigureAwait(false);

                // 通用位写入路径拿不到「有几个点」，只能按打包字节数 × 8 推断；
                // 精确点数由 WriteAsync(bool) / WriteAsync(bool[]) 重载给出。
                return await WriteBitsCoreAsync(address, UnpackBits(data, data.Length * 8), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task<OperationResult> WriteAsync(string address, bool value)
        {
            using (await _lock.LockAsync().ConfigureAwait(false))
            {
                return await WriteBitsCoreAsync(address, new[] { value }, CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            if (value == null || value.Length == 0)
                return Failed(OperationFailureKind.Validation, "写入数据不能为空");

            using (await _lock.LockAsync().ConfigureAwait(false))
            {
                return await WriteBitsCoreAsync(address, value, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<OperationResult<byte[]>> ReadCoreAsync(string address, int length, bool isBit,
            CancellationToken cancellationToken)
        {
            if (length <= 0)
                return FailedBytes(OperationFailureKind.Validation, "读取长度必须大于 0");

            McAddress mcAddress;
            try
            {
                mcAddress = McAddress.Parse(address, Profile);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is OverflowException)
            {
                return FailedBytes(OperationFailureKind.InvalidAddress, ex.Message);
            }

            if (isBit)
            {
                if (!mcAddress.IsBitDevice)
                    return FailedBytes(OperationFailureKind.InvalidAddress,
                        $"软元件 {mcAddress.Device.Prefix} 为字软元件，不支持位访问");

                return await ReadBitsAsync(mcAddress, length, cancellationToken).ConfigureAwait(false);
            }

            if (mcAddress.IsBitDevice)
                return FailedBytes(OperationFailureKind.InvalidAddress,
                    $"软元件 {mcAddress.Device.Prefix} 为位软元件，不支持字访问");

            int elements = (length + 1) / 2;
            int pointsPerElement = mcAddress.WordsPerPoint;
            int maxPoints = Profile.GetMaxPoints(false);
            int elementsPerFrame = Math.Max(1, maxPoints / pointsPerElement);

            var buffer = new List<byte>(elements * pointsPerElement * 2);
            int consumed = 0;
            while (consumed < elements)
            {
                int frameElements = Math.Min(elementsPerFrame, elements - consumed);
                int framePoints = frameElements * pointsPerElement;
                var frameAddress = mcAddress.WithOffset(consumed);

                var frameResult = await ReadFrameAsync(frameAddress, framePoints, cancellationToken).ConfigureAwait(false);
                if (!frameResult.IsSuccess)
                    return frameResult;

                buffer.AddRange(frameResult.ResultValue);
                consumed += frameElements;
            }

            byte[] data = buffer.ToArray();
            if (pointsPerElement > 1)
            {
                // 长定时器每点 4 字：只把每个元素的当前值（第 1 字）按元素顺序压紧，
                // 调用方拿到的是「每元素 2 字节」，系统占用的字不混进结果
                var compact = new byte[elements * 2];
                for (int i = 0; i < elements; i++)
                    Array.Copy(data, i * pointsPerElement * 2, compact, i * 2, 2);
                data = compact;
            }

            if (data.Length > length)
            {
                var trimmed = new byte[length];
                Array.Copy(data, 0, trimmed, 0, length);
                data = trimmed;
            }

            var result = OperationResult.CreateSuccessResult(data);
            return result.Complete();
        }

        /// <summary>
        /// 位读取：1E 帧要求位设备头地址为 16 的倍数，因此先对齐再裁掉前导点。
        /// </summary>
        private async Task<OperationResult<byte[]>> ReadBitsAsync(McAddress address, int points,
            CancellationToken cancellationToken)
        {
            int lead = 0;
            var start = address;
            if (Profile.FrameType == McFrameType.A1E)
            {
                int misalignment = address.Address % 16;
                if (misalignment != 0)
                {
                    if (address.Address < misalignment)
                        return FailedBytes(OperationFailureKind.InvalidAddress,
                            $"A-1E 位访问要求头地址为 16 的倍数，{address.OriginalAddress} 无法对齐");

                    lead = misalignment;
                    start = address.WithOffset(-misalignment);
                }
            }

            var valuesResult = await ReadBitValuesAsync(start, lead + points, cancellationToken).ConfigureAwait(false);
            if (!valuesResult.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(valuesResult);

            return OperationResult.CreateSuccessResult(PackBits(valuesResult.ResultValue, lead, points)).Complete();
        }

        private async Task<OperationResult<bool[]>> ReadBitValuesAsync(McAddress start, int points,
            CancellationToken cancellationToken)
        {
            int maxPoints = Profile.GetMaxPoints(true);
            var values = new List<bool>(points);
            int consumed = 0;

            while (consumed < points)
            {
                int framePoints = Math.Min(maxPoints, points - consumed);
                var frameAddress = start.WithOffset(consumed);

                var frameResult = await ReadFrameAsync(frameAddress, framePoints, cancellationToken).ConfigureAwait(false);
                if (!frameResult.IsSuccess)
                    return OperationResult.CreateFailedResult<bool[]>(frameResult);

                values.AddRange(McCommonMethods.UnpackBitData(frameResult.ResultValue, framePoints));
                consumed += framePoints;
            }

            return OperationResult.CreateSuccessResult(values.ToArray()).Complete();
        }

        private async Task<OperationResult<byte[]>> ReadFrameAsync(McAddress address, int points,
            CancellationToken cancellationToken)
        {
            McReadRequest request;
            try
            {
                request = new McReadRequest(Profile, address, points);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return FailedBytes(OperationFailureKind.InvalidAddress, ex.Message);
            }

            DebugLog("[MC] 读请求 {Address} 点数 {Points}: {Frame}", address.OriginalAddress, points,
                ToHex(request.ProtocolMessageFrame));

            var response = await McTransport.UnicastReadMessageAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(response);

            var readResponse = (McReadResponse)response.ResultValue;
            DebugLog("[MC] 读响应 {Address}: {Frame}", address.OriginalAddress, ToHex(readResponse.ProtocolMessageFrame));

            var result = OperationResult.CreateSuccessResult(readResponse.Data);
            result.Requsts = response.Requsts;
            result.Responses = response.Responses;
            return result.Complete();
        }

        private async Task<OperationResult> WriteWordsCoreAsync(string address, byte[] data,
            CancellationToken cancellationToken)
        {
            if (data == null || data.Length == 0)
                return Failed(OperationFailureKind.Validation, "写入数据不能为空");

            McAddress mcAddress;
            try
            {
                mcAddress = McAddress.Parse(address, Profile);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is OverflowException)
            {
                return Failed(OperationFailureKind.InvalidAddress, ex.Message);
            }

            if (mcAddress.IsBitDevice)
                return Failed(OperationFailureKind.InvalidAddress,
                    $"软元件 {mcAddress.Device.Prefix} 为位软元件，不支持字写入");

            int elements = (data.Length + 1) / 2;
            int pointsPerElement = mcAddress.WordsPerPoint;
            int bytesPerElement = pointsPerElement * 2;
            int maxPoints = Profile.GetMaxPoints(false);
            int elementsPerFrame = Math.Max(1, maxPoints / pointsPerElement);

            int consumed = 0;
            while (consumed < elements)
            {
                int frameElements = Math.Min(elementsPerFrame, elements - consumed);
                int framePoints = frameElements * pointsPerElement;

                byte[] payload = new byte[frameElements * bytesPerElement];
                int available = Math.Min(payload.Length, data.Length - consumed * 2);
                if (available > 0)
                    Array.Copy(data, consumed * 2, payload, 0, available);

                var frameResult = await WriteFrameAsync(mcAddress.WithOffset(consumed), framePoints, payload,
                    cancellationToken).ConfigureAwait(false);
                if (!frameResult.IsSuccess)
                    return frameResult;

                consumed += frameElements;
            }

            return OperationResult.CreateSuccessResult().Complete();
        }

        private async Task<OperationResult> WriteBitsCoreAsync(string address, bool[] values,
            CancellationToken cancellationToken)
        {
            if (values == null || values.Length == 0)
                return Failed(OperationFailureKind.Validation, "写入数据不能为空");

            McAddress mcAddress;
            try
            {
                mcAddress = McAddress.Parse(address, Profile);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is OverflowException)
            {
                return Failed(OperationFailureKind.InvalidAddress, ex.Message);
            }

            if (!mcAddress.IsBitDevice)
                return Failed(OperationFailureKind.InvalidAddress,
                    $"软元件 {mcAddress.Device.Prefix} 为字软元件，不支持位写入");

            return await WriteBitValuesAlignedAsync(mcAddress, values, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// A-1E 位写同样要求头地址为 16 的倍数：先读回对齐块，替换目标点后再整块写入。
        /// </summary>
        private async Task<OperationResult> WriteBitValuesAlignedAsync(McAddress address, bool[] values,
            CancellationToken cancellationToken)
        {
            if (Profile.FrameType != McFrameType.A1E)
                return await WriteBitValuesAsync(address, values, cancellationToken).ConfigureAwait(false);

            int misalignment = address.Address % 16;
            if (misalignment == 0)
                return await WriteBitValuesAsync(address, values, cancellationToken).ConfigureAwait(false);

            if (address.Address < misalignment)
                return Failed(OperationFailureKind.InvalidAddress,
                    $"A-1E 位访问要求头地址为 16 的倍数，{address.OriginalAddress} 无法对齐");

            var alignedStart = address.WithOffset(-misalignment);
            var existing = await ReadBitValuesAsync(alignedStart, misalignment + values.Length, cancellationToken)
                .ConfigureAwait(false);
            if (!existing.IsSuccess)
                return existing;

            var merged = existing.ResultValue;
            Array.Copy(values, 0, merged, misalignment, values.Length);

            return await WriteBitValuesAsync(alignedStart, merged, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationResult> WriteBitValuesAsync(McAddress start, bool[] values,
            CancellationToken cancellationToken)
        {
            int maxPoints = Profile.GetMaxPoints(true);
            int consumed = 0;
            while (consumed < values.Length)
            {
                int framePoints = Math.Min(maxPoints, values.Length - consumed);
                var chunk = new bool[framePoints];
                Array.Copy(values, consumed, chunk, 0, framePoints);

                var frameResult = await WriteFrameAsync(start.WithOffset(consumed), framePoints,
                    McCommonMethods.PackBitData(chunk), cancellationToken).ConfigureAwait(false);
                if (!frameResult.IsSuccess)
                    return frameResult;

                consumed += framePoints;
            }

            return OperationResult.CreateSuccessResult().Complete();
        }

        private async Task<OperationResult> WriteFrameAsync(McAddress address, int points, byte[] data,
            CancellationToken cancellationToken)
        {
            McWriteRequest request;
            try
            {
                request = new McWriteRequest(Profile, address, points, data);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return Failed(OperationFailureKind.InvalidAddress, ex.Message);
            }

            DebugLog("[MC] 写请求 {Address} 点数 {Points}: {Frame}", address.OriginalAddress, points,
                ToHex(request.ProtocolMessageFrame));

            var response = await McTransport.UnicastWriteMessageAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                return OperationResult.CreateFailedResult(response);

            DebugLog("[MC] 写响应 {Address}: {Frame}", address.OriginalAddress,
                ToHex(((McWriteResponse)response.ResultValue).ProtocolMessageFrame));

            var result = OperationResult.CreateSuccessResult();
            result.Requsts = response.Requsts;
            result.Responses = response.Responses;
            return result.Complete();
        }

        /// <inheritdoc />
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(
            Dictionary<string, DataTypeEnums> addresses, CancellationToken cancellationToken = default)
        {
            List<McBatchHelper.McBatchAddressInfo> infos;
            string error;
            if (!McBatchHelper.TryParseAddresses(addresses, Profile, out infos, out error))
                return FailedBatch(OperationFailureKind.InvalidAddress, error);

            using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, (DataTypeEnums, object)>();

                foreach (var block in McBatchHelper.BuildBlocks(infos, Profile))
                {
                    if (block.IsBit)
                    {
                        var bitResult = await ReadBitValuesAsync(block.Start, block.WordCount, cancellationToken)
                            .ConfigureAwait(false);
                        if (!bitResult.IsSuccess)
                            return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(bitResult);

                        for (int i = 0; i < block.Addresses.Count; i++)
                        {
                            var info = block.Addresses[i];
                            values[info.OriginalAddress] = (DataTypeEnums.Bool, bitResult.ResultValue[block.Offsets[i]]);
                        }

                        continue;
                    }

                    var wordResult = await ReadFrameAsync(block.Start, block.WordCount, cancellationToken)
                        .ConfigureAwait(false);
                    if (!wordResult.IsSuccess)
                        return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(wordResult);

                    for (int i = 0; i < block.Addresses.Count; i++)
                    {
                        var info = block.Addresses[i];
                        values[info.OriginalAddress] = (info.DataType,
                            McBatchHelper.ExtractValue(wordResult.ResultValue, block.Offsets[i], info.DataType));
                    }
                }

                return OperationResult.CreateSuccessResult(values).Complete();
            }
        }

        /// <inheritdoc />
        public override async ValueTask<OperationResult> BatchWriteAsync(
            Dictionary<string, (DataTypeEnums, object)> addresses, CancellationToken cancellationToken = default)
        {
            if (addresses == null || addresses.Count == 0)
                return Failed(OperationFailureKind.Validation, "地址列表不能为空");

            var types = new Dictionary<string, DataTypeEnums>(addresses.Count);
            foreach (var pair in addresses)
                types[pair.Key] = pair.Value.Item1;

            List<McBatchHelper.McBatchAddressInfo> infos;
            string error;
            if (!McBatchHelper.TryParseAddresses(types, Profile, out infos, out error))
                return Failed(OperationFailureKind.InvalidAddress, error);

            using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var block in McBatchHelper.BuildBlocks(infos, Profile))
                {
                    if (block.IsBit)
                    {
                        var bitValues = new bool[block.Addresses.Count];
                        for (int i = 0; i < bitValues.Length; i++)
                            bitValues[i] = Convert.ToBoolean(addresses[block.Addresses[i].OriginalAddress].Item2);

                        var bitResult = await WriteBitValuesAlignedAsync(block.Start, bitValues, cancellationToken)
                            .ConfigureAwait(false);
                        if (!bitResult.IsSuccess)
                            return bitResult;

                        continue;
                    }

                    var payload = new List<byte>();
                    foreach (var info in block.Addresses)
                    {
                        byte[] itemBytes = McBatchHelper.ConvertValueToBytes(
                            addresses[info.OriginalAddress].Item2, info.DataType);
                        int expected = info.WordCount * 2;
                        if (itemBytes.Length < expected)
                        {
                            var padded = new byte[expected];
                            Array.Copy(itemBytes, padded, itemBytes.Length);
                            itemBytes = padded;
                        }

                        payload.AddRange(itemBytes);
                    }

                    var wordResult = await WriteFrameAsync(block.Start, block.WordCount, payload.ToArray(),
                        cancellationToken).ConfigureAwait(false);
                    if (!wordResult.IsSuccess)
                        return wordResult;
                }

                return OperationResult.CreateSuccessResult().Complete();
            }
        }

        /// <summary>把位值按「每字节 8 点、低位在前」打包，与基类 ToBool 的约定一致。</summary>
        internal static byte[] PackBits(bool[] values, int offset, int count)
        {
            var buffer = new byte[(count + 7) / 8];
            for (int i = 0; i < count; i++)
            {
                if (!values[offset + i])
                    continue;

                buffer[i / 8] |= (byte)(1 << (i % 8));
            }

            return buffer;
        }

        /// <summary>按「每字节 8 点、低位在前」解包。</summary>
        internal static bool[] UnpackBits(byte[] data, int count)
        {
            var values = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int index = i / 8;
                if (index >= data.Length)
                    break;

                values[i] = (data[index] & (1 << (i % 8))) != 0;
            }

            return values;
        }

        private static OperationResult Failed(OperationFailureKind kind, string message)
        {
            return new OperationResult { IsSuccess = false, FailureKind = kind, Message = message }.Complete();
        }

        private static OperationResult<byte[]> FailedBytes(OperationFailureKind kind, string message)
        {
            return new OperationResult<byte[]> { IsSuccess = false, FailureKind = kind, Message = message }.Complete();
        }

        private static OperationResult<Dictionary<string, (DataTypeEnums, object)>> FailedBatch(
            OperationFailureKind kind, string message)
        {
            return new OperationResult<Dictionary<string, (DataTypeEnums, object)>>
            {
                IsSuccess = false,
                FailureKind = kind,
                Message = message
            }.Complete();
        }

        private static string ToHex(byte[] data)
        {
            if (data == null)
                return string.Empty;

            var text = new string[data.Length];
            for (int i = 0; i < data.Length; i++)
                text[i] = data[i].ToString("X2");
            return string.Join(" ", text);
        }

        private void DebugLog(string message, params object[] args)
        {
            if (!EnableDebugLog)
                return;

            Logger?.LogDebug(message, args);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _lock?.Dispose();

            base.Dispose(disposing);
        }
    }
}
