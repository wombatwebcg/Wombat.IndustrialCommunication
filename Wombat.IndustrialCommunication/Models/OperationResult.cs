using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wombat.IndustrialCommunication
{

    /// <summary>
    /// 操作结果的类，只带有成功标志和错误信息 -> The class that operates the result, with only success flags and error messages
    /// </summary>
    /// <remarks>
    /// 当 <see cref="IsSuccess"/> 为 True 时，忽略 <see cref="Message"/> 及 <see cref="ErrorCode"/> 的值
    /// </remarks>
    /// 
    public class OperationResult
    {
        #region Constructor

        /// <summary>
        /// 实例化一个默认的结果对象
        /// </summary>
        /// 

        public OperationResult()
        {

        }



        /// <summary>
        /// 指示本次访问是否成功
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 指示操作是否被取消
        /// </summary>
        public bool IsCancelled { get; set; } = false;


        private string _message;

        /// <summary>
        /// 具体的错误描述
        /// </summary>
        /// 
        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                AddMessage2List();
            }
        }

        /// <summary>
        /// 具体的错误代码
        /// </summary>
        /// 
        public int ErrorCode { get; set; } = 10000;


        /// <summary>
        /// 详细异常
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 耗时（毫秒）
        /// </summary>
        public double? TimeConsuming { get; internal set; }


        public List<string> Requsts { get; set; } = new List<string>();

        /// <summary>
        /// 响应报文
        /// </summary>
        public List<string> Responses { get; set; } = new List<string>();


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public OperationResult Complete()
        {
            TimeConsuming = (DateTime.Now - InitialTime).TotalMilliseconds;
            return this;
        }


        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime InitialTime { get; protected set; } = DateTime.Now;


        /// <summary>
        /// 异常集合
        /// </summary>
        public List<string> OperationInfo { get; private set; } = new List<string>();

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult SetInfo(OperationResult result)
        {
            IsSuccess = result.IsSuccess;
            Message = result.Message;
            ErrorCode = result.ErrorCode;
            Exception = result.Exception;
            InitialTime = result.InitialTime;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            return this;
        }



        /// <summary>
        /// 添加异常到异常集合
        /// </summary>
        private void AddMessage2List()
        {
            if (!OperationInfo.Contains(Message)& Message != null & Message != string.Empty)
            {
                    OperationInfo.Add(Message);

            }
        }



        #endregion








        #region Static Method

        /*****************************************************************************************************
         * 
         *    主要是方便获取到一些特殊状态的结果对象
         * 
         ******************************************************************************************************/

        public static OperationResult CreateFailedResult()
        {
            return new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = StringResources.Language.ExceptionMessage,
            };
        }

        public static OperationResult CreateFailedResult(string message)
        {
            return new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = message
            };
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有一个参数对象
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">类型的值对象</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>()
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = StringResources.Language.ExceptionMessage,
            };
        }




        /// <summary>
        /// 创建并返回一个失败的返回结果对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="错误信息"></param>
        /// <returns></returns>
        public static OperationResult<T> CreateFailedResult<T>(string message)
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = message,
            };
        }


        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有一个参数对象
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">类型的值对象</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>(T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = StringResources.Language.ExceptionMessage,
                ResultValue = value
            };
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T">目标数据类型</typeparam>
        /// <param name="orgin">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T> CreateFailedResult<T>(OperationResult orgin)
        {
            var result = new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Exception = orgin.Exception,
                Responses = orgin.Responses,
                Requsts = orgin.Requsts,
                InitialTime = orgin.InitialTime,
                Message = orgin.Message

            };
            return result.Complete();
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T">目标数据类型</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T> CreateFailedResult<T>(OperationResult orgin, T value)
        {
            var result = new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Exception = orgin.Exception,
                Responses = orgin.Responses,
                Requsts = orgin.Requsts,
                InitialTime = orgin.InitialTime,
                Message = orgin.Message,
                ResultValue = value
            };
            return result.Complete();
        }



        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T1">目标数据类型一</typeparam>
        /// <typeparam name="T2">目标数据类型二</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2> CreateFailedResult<T1, T2>(OperationResult result)
        {
            return new OperationResult<T1, T2>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Exception = result.Exception,
                Responses = result.Responses,
                Requsts = result.Requsts,
                InitialTime = result.InitialTime,
                Message = result.Message
            };
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T1">目标数据类型一</typeparam>
        /// <typeparam name="T2">目标数据类型二</typeparam>
        /// <typeparam name="T3">目标数据类型三</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2, T3> CreateFailedResult<T1, T2, T3>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3>()
            {
                IsSuccess = false,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            };
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T1">目标数据类型一</typeparam>
        /// <typeparam name="T2">目标数据类型二</typeparam>
        /// <typeparam name="T3">目标数据类型三</typeparam>
        /// <typeparam name="T4">目标数据类型四</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2, T3, T4> CreateFailedResult<T1, T2, T3, T4>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3, T4>()
            {
                IsSuccess = false,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            };
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T1">目标数据类型一</typeparam>
        /// <typeparam name="T2">目标数据类型二</typeparam>
        /// <typeparam name="T3">目标数据类型三</typeparam>
        /// <typeparam name="T4">目标数据类型四</typeparam>
        /// <typeparam name="T5">目标数据类型五</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2, T3, T4, T5> CreateFailedResult<T1, T2, T3, T4, T5>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3, T4, T5>()
            {
                IsSuccess = false,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            };
        }


        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T1">目标数据类型一</typeparam>
        /// <typeparam name="T2">目标数据类型二</typeparam>
        /// <typeparam name="T3">目标数据类型三</typeparam>
        /// <typeparam name="T4">目标数据类型四</typeparam>
        /// <typeparam name="T5">目标数据类型五</typeparam>
        /// <typeparam name="T6">目标数据类型六</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2, T3, T4, T5, T6> CreateFailedResult<T1, T2, T3, T4, T5, T6>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3, T4, T5, T6>()
            {
                IsSuccess = false,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            };
        }



        /// <summary>
        /// 创建并返回一个成功的结果对象
        /// </summary>
        /// <returns>成功的结果对象</returns>
        public static OperationResult CreateSuccessResult()
        {
            return new OperationResult()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
            };
        }


        /// <summary>
        /// 创建并返回一个成功的结果对象
        /// </summary>
        /// <returns>成功的结果对象</returns>
        public static OperationResult CreateSuccessResult(string message)
        {
            return new OperationResult()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = message,
            };
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有一个参数对象
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">类型的值对象</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult<T> CreateSuccessResult<T>()
        {
            return new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
            }.Complete();
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有一个参数对象
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">类型的值对象</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult<T> CreateSuccessResult<T>(T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                ResultValue = value
            }.Complete();
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有一个参数对象
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">类型的值对象</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult<T> CreateSuccessResult<T>(OperationResult result, T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Exception = result.Exception,
                Responses = result.Responses,
                Requsts = result.Requsts,
                InitialTime = result.InitialTime,
                Message = result.Message,
                ResultValue = value
            }.Complete();
        }
        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有两个参数对象
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <param name="value1">类型一对象</param>
        /// <param name="value2">类型二对象</param>
        /// <returns>成的结果对象</returns>
        public static OperationResult<T1, T2> CreateSuccessResult<T1, T2>(T1 value1, T2 value2)
        {
            return new OperationResult<T1, T2>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                Value1 = value1,
                Value2 = value2,
            };
        }


        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有三个参数对象
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <typeparam name="T3">第三个参数类型</typeparam>
        /// <param name="value1">类型一对象</param>
        /// <param name="value2">类型二对象</param>
        /// <param name="value3">类型三对象</param>
        /// <returns>成的结果对象</returns>
        public static OperationResult<T1, T2, T3> CreateSuccessResult<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            return new OperationResult<T1, T2, T3>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                Value1 = value1,
                Value2 = value2,
                Value3 = value3,
            };
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有四个参数对象
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <typeparam name="T3">第三个参数类型</typeparam>
        /// <typeparam name="T4">第四个参数类型</typeparam>
        /// <param name="value1">类型一对象</param>
        /// <param name="value2">类型二对象</param>
        /// <param name="value3">类型三对象</param>
        /// <param name="value4">类型四对象</param>
        /// <returns>成的结果对象</returns>
        public static OperationResult<T1, T2, T3, T4> CreateSuccessResult<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            return new OperationResult<T1, T2, T3, T4>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                Value1 = value1,
                Value2 = value2,
                Value3 = value3,
                Value4 = value4,
            };
        }


        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有五个参数对象
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <typeparam name="T3">第三个参数类型</typeparam>
        /// <typeparam name="T4">第四个参数类型</typeparam>
        /// <typeparam name="T5">第五个参数类型</typeparam>
        /// <param name="value1">类型一对象</param>
        /// <param name="value2">类型二对象</param>
        /// <param name="value3">类型三对象</param>
        /// <param name="value4">类型四对象</param>
        /// <param name="value5">类型五对象</param>
        /// <returns>成的结果对象</returns>
        public static OperationResult<T1, T2, T3, T4, T5> CreateSuccessResult<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
        {
            return new OperationResult<T1, T2, T3, T4, T5>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                Value1 = value1,
                Value2 = value2,
                Value3 = value3,
                Value4 = value4,
                Value5 = value5,
            };
        }

        /// <summary>
        /// 创建并返回一个成功的结果对象，并带有六个参数对象
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <typeparam name="T3">第三个参数类型</typeparam>
        /// <typeparam name="T4">第四个参数类型</typeparam>
        /// <typeparam name="T5">第五个参数类型</typeparam>
        /// <typeparam name="T6">第六个参数类型</typeparam>
        /// <param name="value1">类型一对象</param>
        /// <param name="value2">类型二对象</param>
        /// <param name="value3">类型三对象</param>
        /// <param name="value4">类型四对象</param>
        /// <param name="value5">类型五对象</param>
        /// <param name="value6">类型六对象</param>
        /// <returns>成的结果对象</returns>
        public static OperationResult<T1, T2, T3, T4, T5, T6> CreateSuccessResult<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
        {
            return new OperationResult<T1, T2, T3, T4, T5, T6>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                Value1 = value1,
                Value2 = value2,
                Value3 = value3,
                Value4 = value4,
                Value5 = value5,
                Value6 = value6,
            };
        }

        #region Extended Factory Methods - 扩展工厂方法

        /// <summary>
        /// 从异常对象创建并返回一个失败的结果对象
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <returns>失败的结果对象</returns>
        public static OperationResult CreateFailedResult(Exception exception)
        {
            return new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = exception?.Message ?? StringResources.Language.ExceptionMessage,
                Exception = exception
            };
        }

        /// <summary>
        /// 从异常对象创建并返回一个失败的泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="exception">异常对象</param>
        /// <returns>失败的泛型结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>(Exception exception)
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = exception?.Message ?? StringResources.Language.ExceptionMessage,
                Exception = exception
            };
        }

        /// <summary>
        /// 从异常对象创建并返回一个带值的失败泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="exception">异常对象</param>
        /// <param name="value">结果值</param>
        /// <returns>失败的泛型结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>(Exception exception, T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = exception?.Message ?? StringResources.Language.ExceptionMessage,
                Exception = exception,
                ResultValue = value
            };
        }

        /// <summary>
        /// 创建并返回一个带自定义错误代码的失败结果对象
        /// </summary>
        /// <param name="errorCode">错误代码</param>
        /// <param name="message">错误消息</param>
        /// <returns>失败的结果对象</returns>
        public static OperationResult CreateFailedResult(int errorCode, string message)
        {
            return new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Message = message ?? StringResources.Language.ExceptionMessage
            };
        }

        /// <summary>
        /// 创建并返回一个带自定义错误代码的失败泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="errorCode">错误代码</param>
        /// <param name="message">错误消息</param>
        /// <param name="value">结果值</param>
        /// <returns>失败的泛型结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>(int errorCode, string message, T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Message = message ?? StringResources.Language.ExceptionMessage,
                ResultValue = value
            };
        }

        /// <summary>
        /// 创建并返回一个带异常和自定义错误代码的失败结果对象
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>失败的结果对象</returns>
        public static OperationResult CreateFailedResult(Exception exception, int errorCode)
        {
            return new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                Message = exception?.Message ?? StringResources.Language.ExceptionMessage,
                Exception = exception
            };
        }

        /// <summary>
        /// 创建并返回一个带请求响应数据的失败结果对象（适用于通信场景）
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="request">请求数据</param>
        /// <param name="response">响应数据</param>
        /// <returns>失败的结果对象</returns>
        public static OperationResult CreateFailedResult(string message, string request, string response)
        {
            var result = new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = message ?? StringResources.Language.ExceptionMessage
            };

            if (!string.IsNullOrEmpty(request))
                result.Requsts.Add(request);
            if (!string.IsNullOrEmpty(response))
                result.Responses.Add(response);

            return result;
        }

        /// <summary>
        /// 创建并返回一个带请求响应数据的失败泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="message">错误消息</param>
        /// <param name="request">请求数据</param>
        /// <param name="response">响应数据</param>
        /// <param name="value">结果值</param>
        /// <returns>失败的泛型结果对象</returns>
        public static OperationResult<T> CreateFailedResult<T>(string message, string request, string response, T value)
        {
            var result = new OperationResult<T>()
            {
                IsSuccess = false,
                ErrorCode = -1,
                Message = message ?? StringResources.Language.ExceptionMessage,
                ResultValue = value
            };

            if (!string.IsNullOrEmpty(request))
                result.Requsts.Add(request);
            if (!string.IsNullOrEmpty(response))
                result.Responses.Add(response);

            return result;
        }

        /// <summary>
        /// 创建并返回一个带请求响应数据的成功结果对象（适用于通信场景）
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <param name="response">响应数据</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult CreateSuccessResult(string request, string response)
        {
            var result = new OperationResult()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText
            };

            if (!string.IsNullOrEmpty(request))
                result.Requsts.Add(request);
            if (!string.IsNullOrEmpty(response))
                result.Responses.Add(response);

            return result;
        }

        /// <summary>
        /// 创建并返回一个带值和自定义消息的成功泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="value">结果值</param>
        /// <param name="message">成功消息</param>
        /// <returns>成功的泛型结果对象</returns>
        public static OperationResult<T> CreateSuccessResult<T>(T value, string message)
        {
            return new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = message ?? StringResources.Language.SuccessText,
                ResultValue = value
            }.Complete();
        }

        /// <summary>
        /// 创建并返回一个带值和请求响应数据的成功泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="value">结果值</param>
        /// <param name="request">请求数据</param>
        /// <param name="response">响应数据</param>
        /// <returns>成功的泛型结果对象</returns>
        public static OperationResult<T> CreateSuccessResult<T>(T value, string request, string response)
        {
            var result = new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = StringResources.Language.SuccessText,
                ResultValue = value
            };

            if (!string.IsNullOrEmpty(request))
                result.Requsts.Add(request);
            if (!string.IsNullOrEmpty(response))
                result.Responses.Add(response);

            return result.Complete();
        }

        /// <summary>
        /// 创建并返回一个带自定义成功代码的成功结果对象
        /// </summary>
        /// <param name="errorCode">成功代码（通常为0或正数）</param>
        /// <param name="message">成功消息</param>
        /// <returns>成功的结果对象</returns>
        public static OperationResult CreateSuccessResult(int errorCode, string message)
        {
            return new OperationResult()
            {
                IsSuccess = true,
                ErrorCode = errorCode,
                Message = message ?? StringResources.Language.SuccessText
            };
        }

        /// <summary>
        /// 根据条件创建成功或失败的结果对象
        /// </summary>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="message">消息</param>
        /// <returns>结果对象</returns>
        public static OperationResult CreateResult(bool isSuccess, string message)
        {
            return new OperationResult()
            {
                IsSuccess = isSuccess,
                ErrorCode = isSuccess ? 0 : -1,
                Message = message ?? (isSuccess ? StringResources.Language.SuccessText : StringResources.Language.ExceptionMessage)
            };
        }

        /// <summary>
        /// 根据条件创建成功或失败的泛型结果对象
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="value">结果值</param>
        /// <param name="message">消息</param>
        /// <returns>泛型结果对象</returns>
        public static OperationResult<T> CreateResult<T>(bool isSuccess, T value, string message)
        {
            return new OperationResult<T>()
            {
                IsSuccess = isSuccess,
                ErrorCode = isSuccess ? 0 : -1,
                Message = message ?? (isSuccess ? StringResources.Language.SuccessText : StringResources.Language.ExceptionMessage),
                ResultValue = value
            };
        }

        /// <summary>
        /// 从异常对象智能创建失败结果，自动提取异常信息
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <returns>失败的结果对象</returns>
        public static OperationResult CreateFromException(Exception exception)
        {
            if (exception == null)
                return CreateFailedResult(StringResources.Language.ExceptionMessage);

            var result = new OperationResult()
            {
                IsSuccess = false,
                ErrorCode = exception.HResult != 0 ? exception.HResult : -1,
                Message = exception.Message,
                Exception = exception
            };

            // 如果有内部异常，添加到操作信息中
            if (exception.InnerException != null)
            {
                result.OperationInfo.Add($"内部异常: {exception.InnerException.Message}");
            }

            return result;
        }

        #endregion

        #endregion



    }


    /// <summary>
    /// 操作结果的泛型类，允许带一个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T">泛型类</typeparam>
    /// 

    public class OperationResult<T> : OperationResult 
    {
        #region Constructor

        /// <summary>
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult() : base()
        {

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(T data) 
        {
            ResultValue = data;
        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message)=> { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result,T data)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            ResultValue = data;
        }





        /// <summary>
        /// 用户自定义的泛型数据
        /// </summary>
        public T ResultValue { get; set; }


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T> Complete()
        {
            base.Complete();
            return this;

        }


        #endregion

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T> SetInfo(OperationResult<T> result)
        {
            ResultValue = result.ResultValue;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public  OperationResult<T> SetInfo(OperationResult result,T data)
        {
            base.SetInfo(result);
            ResultValue = data;
            return this;
        }

    }
    /// <summary>
    /// 操作结果的泛型类，允许带两个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T1">泛型类</typeparam>
    /// <typeparam name="T2">泛型类</typeparam>
    public class OperationResult<T1, T2> : OperationResult
    {
        #region Constructor


        public OperationResult()
        {

        }




        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1,T2 data2)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            Value1 = data1;
            Value2 = data2;

        }


        #endregion

        /// <summary>
        /// 用户自定义的泛型数据1
        /// </summary>
        public T1 Value1 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T2 Value2 { get; set; }

        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T1, T2> Complete()
        {
            base.Complete();
            return this;

        }



        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T1,T2> SetInfo(OperationResult<T1,T2> result)
        {
            Value1 = result.Value1;
            Value2 = result.Value2;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T1, T2> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

    }


    /// <summary>
    /// 操作结果的泛型类，允许带两个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T1">泛型类</typeparam>
    /// <typeparam name="T2">泛型类</typeparam>
    /// <typeparam name="T3">泛型类</typeparam>
    public class OperationResult<T1, T2,T3> : OperationResult
    {
        #region Constructor




        public OperationResult()
        {

        }








        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2,T3 data3)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            Value1 = data1;
            Value2 = data2;
            Value3 = data3;

        }


        #endregion

        /// <summary>
        /// 用户自定义的泛型数据1
        /// </summary>
        public T1 Value1 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T2 Value2 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T3 Value3 { get; set; }


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T1, T2,T3> Complete()
        {
            base.Complete();
            return this;

        }



        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T1, T2,T3> SetInfo(OperationResult<T1, T2,T3> result)
        {
            Value1 = result.Value1;
            Value2 = result.Value2;
            Value3 = result.Value3;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T1, T2,T3> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

    }



    /// <summary>
    /// 操作结果的泛型类，允许带两个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T1">泛型类</typeparam>
    /// <typeparam name="T2">泛型类</typeparam>
    /// <typeparam name="T3">泛型类</typeparam>
    /// <typeparam name="T4">泛型类</typeparam>
    public class OperationResult<T1, T2, T3,T4> : OperationResult
    {
        #region Constructor


        public OperationResult()
        {

        }






        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            Value1 = data1;
            Value2 = data2;
            Value3 = data3;
            Value4 = data4;

        }


        #endregion

        /// <summary>
        /// 用户自定义的泛型数据1
        /// </summary>
        public T1 Value1 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T2 Value2 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据3
        /// </summary>
        public T3 Value3 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据4
        /// </summary>
        public T4 Value4 { get; set; }

        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T1, T2, T3,T4> Complete()
        {
            base.Complete();
            return this;

        }


        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T1, T2, T3, T4> SetInfo(OperationResult<T1, T2, T3, T4> result)
        {
            Value1 = result.Value1;
            Value2 = result.Value2;
            Value3 = result.Value3;
            Value4 = result.Value4;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T1, T2, T3, T4> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

    }


    /// <summary>
    /// 操作结果的泛型类，允许带两个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T1">泛型类</typeparam>
    /// <typeparam name="T2">泛型类</typeparam>
    /// <typeparam name="T3">泛型类</typeparam>
    /// <typeparam name="T4">泛型类</typeparam>
    /// <typeparam name="T5">泛型类</typeparam>
    public class OperationResult<T1, T2, T3, T4,T5> : OperationResult
    {
        #region Constructor


        public OperationResult()
        {

        }


        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4, T5 data5)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            Value1 = data1;
            Value2 = data2;
            Value3 = data3;
            Value4 = data4;
            Value5 = data5;

        }


        #endregion

        /// <summary>
        /// 用户自定义的泛型数据1
        /// </summary>
        public T1 Value1 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T2 Value2 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据3
        /// </summary>
        public T3 Value3 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据4
        /// </summary>
        public T4 Value4 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据4
        /// </summary>
        public T5 Value5 { get; set; }


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T1, T2, T3, T4,T5> Complete()
        {
            base.Complete();
            return this;

        }



        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T1, T2, T3, T4, T5> SetInfo(OperationResult<T1, T2, T3, T4, T5> result)
        {
            Value1 = result.Value1;
            Value2 = result.Value2;
            Value3 = result.Value3;
            Value4 = result.Value4;
            Value5 = result.Value5;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T1, T2, T3, T4, T5> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

    }


    /// <summary>
    /// 操作结果的泛型类，允许带两个用户自定义的泛型对象，推荐使用这个类
    /// </summary>
    /// <typeparam name="T1">泛型类</typeparam>
    /// <typeparam name="T2">泛型类</typeparam>
    /// <typeparam name="T3">泛型类</typeparam>
    /// <typeparam name="T4">泛型类</typeparam>
    /// <typeparam name="T5">泛型类</typeparam>
    /// <typeparam name="T6">泛型类</typeparam>
    public class OperationResult<T1, T2, T3, T4, T5, T6> : OperationResult
    {
        #region Constructor

        public OperationResult()
        {

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4, T5 data5, T6 data6)
        {
            this.ErrorCode = result.ErrorCode;
            this.Exception = result.Exception;
            this.InitialTime = result.InitialTime;
            this.IsSuccess = result.IsSuccess;
            this.Message = result.Message;
            result.OperationInfo.ForEach((message) => { OperationInfo.Add(message); });
            this.Requsts = result.Requsts;
            this.Responses = result.Responses;
            Value1 = data1;
            Value2 = data2;
            Value3 = data3;
            Value4 = data4;
            Value5 = data5;
            Value6 = data6;

        }


        #endregion

        /// <summary>
        /// 用户自定义的泛型数据1
        /// </summary>
        public T1 Value1 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据2
        /// </summary>
        public T2 Value2 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据3
        /// </summary>
        public T3 Value3 { get; set; }


        /// <summary>
        /// 用户自定义的泛型数据4
        /// </summary>
        public T4 Value4 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据5
        /// </summary>
        public T5 Value5 { get; set; }

        /// <summary>
        /// 用户自定义的泛型数据6
        /// </summary>
        public T6 Value6 { get; set; }


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T1, T2, T3, T4, T5, T6> Complete()
        {
            base.Complete();
            return this;

        }



        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public OperationResult<T1, T2, T3, T4, T5, T6> SetInfo(OperationResult<T1, T2, T3, T4, T5, T6> result)
        {
            Value1 = result.Value1;
            Value2 = result.Value2;
            Value3 = result.Value3;
            Value4 = result.Value4;
            Value5 = result.Value5;
            Value6 = result.Value6;
            base.SetInfo(result);
            return this;
        }

        /// <summary>
        /// 设置异常信息和Succeed状态
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public new OperationResult<T1, T2, T3, T4, T5, T6> SetInfo(OperationResult result)
        {
            base.SetInfo(result);
            return this;
        }

    }



}





