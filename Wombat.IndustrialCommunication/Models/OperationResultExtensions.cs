using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
   public static class OperationResultExtensions
    {

        public static OperationResult Copy(this OperationResult orgin)
        {
            var result = new OperationResult()
            {
                IsSuccess = orgin.IsSuccess,
                ErrorCode = orgin.ErrorCode,
                Message = orgin.Message,
                Exception = orgin.Exception,
                Requsts = orgin.Requsts,
                Responses = orgin.Responses,
            };
            orgin.OperationInfo.ForEach((message) => { result.OperationInfo.Add(message); });
            return result;
        }


        public static OperationResult<T> Copy<T>(this OperationResult<T> orgin)
        {
            var result = new OperationResult<T>()
            {
                IsSuccess = orgin.IsSuccess,
                ErrorCode = orgin.ErrorCode,
                Message = orgin.Message,
                Exception = orgin.Exception,
                Requsts = orgin.Requsts,
                Responses = orgin.Responses,
                Value = orgin.Value
            };
            orgin.OperationInfo.ForEach((message) => { result.OperationInfo.Add(message); });
            return result;
        }

        public static OperationResult<T1,T2> Copy<T1, T2>(this OperationResult<T1, T2> orgin)
        {
            var result = new OperationResult<T1,T2>()
            {
                IsSuccess = orgin.IsSuccess,
                ErrorCode = orgin.ErrorCode,
                Message = orgin.Message,
                Exception = orgin.Exception,
                Requsts = orgin.Requsts,
                Responses = orgin.Responses,
                Value1 = orgin.Value1,
                Value2 = orgin.Value2
            };
            orgin.OperationInfo.ForEach((message) => { result.OperationInfo.Add(message); });
            return result;
        }

    }
}
