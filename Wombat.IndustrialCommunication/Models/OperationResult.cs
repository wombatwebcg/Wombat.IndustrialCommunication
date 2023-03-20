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
        public double? TimeConsuming { get; private set; }

        public string Requst { get; set; }

        /// <summary>
        /// 响应报文
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// 请求报文2
        /// </summary>
        public string Requst2 { get; set; }

        /// <summary>
        /// 响应报文2
        /// </summary>
        public string Response2 { get; set; }

        /// <summary>
        /// 结束时间统计
        /// </summary>
        public OperationResult EndTime()
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
        public List<string> MessageList { get; private set; } = new List<string>();

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
            foreach (var err in result.MessageList)
            {
                if (!MessageList.Contains(err))
                    MessageList.Add(err);
            }
            return this;
        }


        /// <summary>
        /// 添加异常到异常集合
        /// </summary>
        public void AddMessage2List()
        {
            if (!MessageList.Contains(Message))
            {
                if (Message != null & Message != string.Empty)
                    MessageList.Add(Message);

            }
        }

        public static OperationResult Assignment(OperationResult orgin)
        {
            var newOperationValue = new OperationResult()
            {
                IsSuccess = orgin.IsSuccess,
                ErrorCode = orgin.ErrorCode,
                Message = orgin.Message,
                Exception = orgin.Exception,
                InitialTime = orgin.InitialTime,
                Requst = orgin.Requst,
                Requst2 = orgin.Requst2,
                Response = orgin.Response,
                Response2 = orgin.Response2
            };
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    newOperationValue.MessageList.Add(message);
            }
            return newOperationValue;
        }


        #endregion

        #region Static Method

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
                Value = value
            };
        }

        /// <summary>
        /// 创建并返回一个失败的结果对象，该对象复制另一个结果对象的错误信息
        /// </summary>
        /// <typeparam name="T">目标数据类型</typeparam>
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T> CreateFailedResult<T>(OperationResult result)
        {
            return new OperationResult<T>()
            {
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            };
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
        /// <param name="result">之前的结果对象</param>
        /// <returns>带默认泛型对象的失败结果类</returns>
        public static OperationResult<T1, T2, T3> CreateFailedResult<T1, T2, T3>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3>()
            {
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
                Value = value
            };
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
            Value = data;
        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result,T data)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if(message!=null&message!=string.Empty)
                this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
            Value = data;
        }

        /// <summary>
        /// 请求报文
        /// </summary>



        /// <summary>
        /// 用户自定义的泛型数据
        /// </summary>
        public T Value { get; set; }


        /// <summary>
        /// 结束时间统计
        /// </summary>
        public new OperationResult<T> EndTime()
        {
            base.EndTime();
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
            Value = result.Value;
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
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult(OperationResult result) : base()
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1,T2 data2)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
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
        public new OperationResult<T1, T2> EndTime()
        {
            base.EndTime();
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
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult(OperationResult result) : base()
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2,T3 data3)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
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
        public new OperationResult<T1, T2,T3> EndTime()
        {
            base.EndTime();
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
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult(OperationResult result) : base()
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
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
        public new OperationResult<T1, T2, T3,T4> EndTime()
        {
            base.EndTime();
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
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult(OperationResult result) : base()
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4, T5 data5)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
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
        public new OperationResult<T1, T2, T3, T4,T5> EndTime()
        {
            base.EndTime();
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
        /// 实例化一个默认的结果对象
        /// </summary>
        public OperationResult(OperationResult result) : base()
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;

        }

        /// <summary>
        /// 使用指定的消息实例化一个默认的结果对象
        /// </summary>
        /// <param name="msg">错误消息</param>
        public OperationResult(OperationResult result, T1 data1, T2 data2, T3 data3, T4 data4, T5 data5, T6 data6)
        {
            var orgin = Assignment(result);
            this.ErrorCode = orgin.ErrorCode;
            this.Exception = orgin.Exception;
            this.InitialTime = orgin.InitialTime;
            this.IsSuccess = orgin.IsSuccess;
            this.Message = orgin.Message;
            foreach (var message in orgin.MessageList)
            {
                if (message != null & message != string.Empty)
                    this.MessageList.Add(message);
            }
            this.Requst = orgin.Requst;
            this.Requst2 = orgin.Requst2;
            this.Response = orgin.Response;
            this.Response2 = orgin.Response2;
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
        public new OperationResult<T1, T2, T3, T4, T5, T6> EndTime()
        {
            base.EndTime();
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

    #endregion

}





