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

    }
}
