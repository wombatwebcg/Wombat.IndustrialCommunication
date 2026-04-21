using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    internal static class PointListOperationHelper
    {
        public static OperationResult<IList<DevicePointReadRequest>> NormalizeReadRequests(IEnumerable<DevicePointReadRequest> points)
        {
            if (points == null)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointReadRequest>>("点位读取列表不能为空");
            }

            var normalized = new List<DevicePointReadRequest>();
            foreach (var point in points)
            {
                if (point == null)
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointReadRequest>>("点位读取项不能为空");
                }

                if (string.IsNullOrWhiteSpace(point.Address))
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointReadRequest>>("点位地址不能为空");
                }

                if (point.DataType == DataTypeEnums.None)
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointReadRequest>>(string.Format("点位 {0} 未指定数据类型", point.Address));
                }

                normalized.Add(new DevicePointReadRequest
                {
                    Name = ResolvePointName(point.Name, point.Address),
                    Address = point.Address,
                    DataType = point.DataType,
                    Length = point.Length <= 0 ? 1 : point.Length,
                    EnableBatch = point.EnableBatch
                });
            }

            return OperationResult.CreateSuccessResult<IList<DevicePointReadRequest>>(normalized);
        }

        public static OperationResult<IList<DevicePointWriteRequest>> NormalizeWriteRequests(IEnumerable<DevicePointWriteRequest> points)
        {
            if (points == null)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointWriteRequest>>("点位写入列表不能为空");
            }

            var normalized = new List<DevicePointWriteRequest>();
            foreach (var point in points)
            {
                if (point == null)
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointWriteRequest>>("点位写入项不能为空");
                }

                if (string.IsNullOrWhiteSpace(point.Address))
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointWriteRequest>>("点位地址不能为空");
                }

                if (point.DataType == DataTypeEnums.None)
                {
                    return OperationResult.CreateFailedResult<IList<DevicePointWriteRequest>>(string.Format("点位 {0} 未指定数据类型", point.Address));
                }

                normalized.Add(new DevicePointWriteRequest
                {
                    Name = ResolvePointName(point.Name, point.Address),
                    Address = point.Address,
                    DataType = point.DataType,
                    Length = point.Length <= 0 ? 1 : point.Length,
                    EnableBatch = point.EnableBatch,
                    Value = point.Value
                });
            }

            return OperationResult.CreateSuccessResult<IList<DevicePointWriteRequest>>(normalized);
        }

        public static async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(IDeviceClient client, IList<DevicePointReadRequest> points)
        {
            if (client == null)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointReadResult>>("设备客户端不能为空");
            }

            var results = new List<DevicePointReadResult>();
            var failedMessages = new List<string>();
            var aggregate = new OperationResult<IList<DevicePointReadResult>>();

            if (points == null || points.Count == 0)
            {
                aggregate.ResultValue = results;
                aggregate.IsSuccess = true;
                aggregate.Message = "点位读取列表为空";
                return aggregate.Complete();
            }

            var batchReadRequest = BuildBatchReadRequest(points);
            var batchReadResult = default(OperationResult<Dictionary<string, (DataTypeEnums, object)>>);
            var useBatchReadResult = false;
            if (batchReadRequest.Count > 1)
            {
                batchReadResult = await client.BatchReadAsync(batchReadRequest).ConfigureAwait(false);
                MergeOperationTrace(aggregate, batchReadResult);
                if (batchReadResult.IsSuccess)
                {
                    useBatchReadResult = true;
                }
                else
                {
                    AddOperationInfo(aggregate, string.Format("批量读取失败，已回退逐点读取: {0}", batchReadResult.Message));
                }
            }

            foreach (var point in points)
            {
                OperationResult<object> pointResult;
                if (useBatchReadResult && batchReadRequest.ContainsKey(point.Address))
                {
                    pointResult = CreateBatchReadPointResult(point, batchReadResult);
                }
                else
                {
                    pointResult = await ReadSinglePointAsync(client, point).ConfigureAwait(false);
                    MergeOperationTrace(aggregate, pointResult);
                }

                var readResult = CreateReadResult(point, pointResult);
                results.Add(readResult);

                if (!pointResult.IsSuccess)
                {
                    failedMessages.Add(string.Format("{0}: {1}", point.Name, pointResult.Message));
                }
            }

            aggregate.ResultValue = results;
            aggregate.IsSuccess = failedMessages.Count == 0;
            aggregate.Message = failedMessages.Count == 0 ? StringResources.Language.SuccessText : string.Join("; ", failedMessages.ToArray());
            return aggregate.Complete();
        }

        public static async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(IDeviceClient client, IList<DevicePointWriteRequest> points)
        {
            if (client == null)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointWriteResult>>("设备客户端不能为空");
            }

            var results = new List<DevicePointWriteResult>();
            var failedMessages = new List<string>();
            var aggregate = new OperationResult<IList<DevicePointWriteResult>>();

            if (points == null || points.Count == 0)
            {
                aggregate.ResultValue = results;
                aggregate.IsSuccess = true;
                aggregate.Message = "点位写入列表为空";
                return aggregate.Complete();
            }

            var batchWriteRequest = BuildBatchWriteRequest(points);
            var batchWriteSucceededAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (batchWriteRequest.Count > 1)
            {
                var batchWriteResult = await client.BatchWriteAsync(batchWriteRequest).ConfigureAwait(false);
                MergeOperationTrace(aggregate, batchWriteResult);
                if (batchWriteResult.IsSuccess)
                {
                    foreach (var address in batchWriteRequest.Keys)
                    {
                        batchWriteSucceededAddresses.Add(address);
                    }
                }
                else
                {
                    AddOperationInfo(aggregate, string.Format("批量写入失败，已回退逐点写入: {0}", batchWriteResult.Message));
                }
            }

            foreach (var point in points)
            {
                OperationResult pointResult;
                if (batchWriteSucceededAddresses.Contains(point.Address))
                {
                    pointResult = OperationResult.CreateSuccessResult();
                }
                else
                {
                    pointResult = await WriteSinglePointAsync(client, point).ConfigureAwait(false);
                    MergeOperationTrace(aggregate, pointResult);
                }

                var writeResult = CreateWriteResult(point, pointResult);
                results.Add(writeResult);

                if (!pointResult.IsSuccess)
                {
                    failedMessages.Add(string.Format("{0}: {1}", point.Name, pointResult.Message));
                }
            }

            aggregate.ResultValue = results;
            aggregate.IsSuccess = failedMessages.Count == 0;
            aggregate.Message = failedMessages.Count == 0 ? StringResources.Language.SuccessText : string.Join("; ", failedMessages.ToArray());
            return aggregate.Complete();
        }

        private static async Task<OperationResult<object>> ReadSinglePointAsync(IDeviceClient client, DevicePointReadRequest point)
        {
            if (point.DataType == DataTypeEnums.String)
            {
                if (point.Length <= 0)
                {
                    return OperationResult.CreateFailedResult<object>(string.Format("点位 {0} 的字符串读取长度必须大于 0", point.Name));
                }

                var stringResult = await client.ReadStringAsync(point.Address, point.Length).ConfigureAwait(false);
                return stringResult.IsSuccess
                    ? new OperationResult<object>(stringResult, stringResult.ResultValue).Complete()
                    : OperationResult.CreateFailedResult<object>(stringResult);
            }

            if (point.Length > 1)
            {
                return await client.ReadAsync(point.DataType, point.Address, point.Length).ConfigureAwait(false);
            }

            return await client.ReadAsync(point.DataType, point.Address).ConfigureAwait(false);
        }

        private static async Task<OperationResult> WriteSinglePointAsync(IDeviceClient client, DevicePointWriteRequest point)
        {
            if (point.Value == null)
            {
                return OperationResult.CreateFailedResult(string.Format("点位 {0} 的写入值不能为空", point.Name));
            }

            if (point.DataType == DataTypeEnums.String)
            {
                var stringValue = Convert.ToString(point.Value, CultureInfo.InvariantCulture);
                if (stringValue == null)
                {
                    return OperationResult.CreateFailedResult(string.Format("点位 {0} 的字符串写入值不能为空", point.Name));
                }

                return await client.WriteAsync(point.Address, stringValue).ConfigureAwait(false);
            }

            var arrayValue = point.Value as Array;
            if (arrayValue != null)
            {
                return await client.WriteAsync(point.DataType, point.Address, ConvertToObjectArray(arrayValue)).ConfigureAwait(false);
            }

            return await client.WriteAsync(point.DataType, point.Address, point.Value).ConfigureAwait(false);
        }

        private static object[] ConvertToObjectArray(Array values)
        {
            var result = new object[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                result[i] = values.GetValue(i);
            }

            return result;
        }

        private static Dictionary<string, DataTypeEnums> BuildBatchReadRequest(IList<DevicePointReadRequest> points)
        {
            var result = new Dictionary<string, DataTypeEnums>(StringComparer.OrdinalIgnoreCase);
            var duplicateAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var point in points)
            {
                if (!CanUseBatchRead(point))
                {
                    continue;
                }

                if (result.ContainsKey(point.Address))
                {
                    duplicateAddresses.Add(point.Address);
                    result.Remove(point.Address);
                    continue;
                }

                result[point.Address] = point.DataType;
            }

            if (duplicateAddresses.Count > 0)
            {
                foreach (var address in duplicateAddresses)
                {
                    if (result.ContainsKey(address))
                    {
                        result.Remove(address);
                    }
                }
            }

            return result;
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildBatchWriteRequest(IList<DevicePointWriteRequest> points)
        {
            var result = new Dictionary<string, (DataTypeEnums, object)>(StringComparer.OrdinalIgnoreCase);
            var duplicateAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var point in points)
            {
                if (!CanUseBatchWrite(point))
                {
                    continue;
                }

                if (result.ContainsKey(point.Address))
                {
                    duplicateAddresses.Add(point.Address);
                    result.Remove(point.Address);
                    continue;
                }

                result[point.Address] = (point.DataType, point.Value);
            }

            if (duplicateAddresses.Count > 0)
            {
                foreach (var address in duplicateAddresses)
                {
                    if (result.ContainsKey(address))
                    {
                        result.Remove(address);
                    }
                }
            }

            return result;
        }

        private static bool CanUseBatchRead(DevicePointReadRequest point)
        {
            return point != null
                && point.EnableBatch
                && point.DataType != DataTypeEnums.String
                && point.Length <= 1;
        }

        private static bool CanUseBatchWrite(DevicePointWriteRequest point)
        {
            return point != null
                && point.EnableBatch
                && point.Value != null
                && point.DataType != DataTypeEnums.String
                && point.Length <= 1
                && !(point.Value is Array);
        }

        private static OperationResult<object> CreateBatchReadPointResult(DevicePointReadRequest point, OperationResult<Dictionary<string, (DataTypeEnums, object)>> batchReadResult)
        {
            if (batchReadResult == null || batchReadResult.ResultValue == null)
            {
                return OperationResult.CreateFailedResult<object>(string.Format("点位 {0} 的批量读取结果为空", point.Name));
            }

            (DataTypeEnums, object) batchValue;
            if (!batchReadResult.ResultValue.TryGetValue(point.Address, out batchValue))
            {
                return OperationResult.CreateFailedResult<object>(string.Format("点位 {0} 未在批量读取结果中返回", point.Name));
            }

            return new OperationResult<object>(batchReadResult, batchValue.Item2).Complete();
        }

        private static DevicePointReadResult CreateReadResult(DevicePointReadRequest point, OperationResult<object> pointResult)
        {
            return new DevicePointReadResult
            {
                Name = point.Name,
                Address = point.Address,
                DataType = point.DataType,
                Length = point.Length,
                IsSuccess = pointResult.IsSuccess,
                Message = pointResult.Message,
                Value = pointResult.IsSuccess ? pointResult.ResultValue : null
            };
        }

        private static DevicePointWriteResult CreateWriteResult(DevicePointWriteRequest point, OperationResult pointResult)
        {
            return new DevicePointWriteResult
            {
                Name = point.Name,
                Address = point.Address,
                DataType = point.DataType,
                Length = point.Length,
                IsSuccess = pointResult.IsSuccess,
                Message = pointResult.Message,
                Value = point.Value
            };
        }

        private static void MergeOperationTrace(OperationResult target, OperationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.Requsts != null && source.Requsts.Count > 0)
            {
                target.Requsts.AddRange(source.Requsts);
            }

            if (source.Responses != null && source.Responses.Count > 0)
            {
                target.Responses.AddRange(source.Responses);
            }

            if (source.OperationInfo != null && source.OperationInfo.Count > 0)
            {
                foreach (var info in source.OperationInfo.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    if (!target.OperationInfo.Contains(info))
                    {
                        target.OperationInfo.Add(info);
                    }
                }
            }
        }

        private static void AddOperationInfo(OperationResult target, string info)
        {
            if (target == null || string.IsNullOrWhiteSpace(info))
            {
                return;
            }

            if (!target.OperationInfo.Contains(info))
            {
                target.OperationInfo.Add(info);
            }
        }

        private static string ResolvePointName(string name, string address)
        {
            return string.IsNullOrWhiteSpace(name) ? address : name;
        }
    }
}
