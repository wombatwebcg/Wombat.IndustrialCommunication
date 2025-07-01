using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Security.Permissions;


namespace Wombat.IndustrialCommunication
{
    /// <summary>
    ///     Represents slave errors that occur during communication.
    /// </summary>
    [Serializable]
    public class DevcieResponseException : Exception
    {
        private readonly ExceptionResponse _exceptionResponse;


        /// <summary>
        ///     Initializes a new instance of the <see cref="DevcieResponseException" /> class.
        /// </summary>
        public DevcieResponseException()
        {
        }

        public DevcieResponseException(string message)
            : base(message)
        {
        }

        public DevcieResponseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal DevcieResponseException(ExceptionResponse exceptionResponse)
        {
            _exceptionResponse = exceptionResponse;
        }

        internal DevcieResponseException(string message, ExceptionResponse exceptionResponse)
            : base(message)
        {
            _exceptionResponse = exceptionResponse;
        }

        //protected DevcieResponseException(SerializationInfo info, StreamingContext context)
        //    : base(info, context)
        //{
        //    if (info != null)
        //    {
        //        _exceptionResponse = new exceptionResponse(info.GetByte(SlaveAddressPropertyName),
        //            info.GetByte(FunctionCodePropertyName), info.GetByte(SlaveExceptionCodePropertyName));
        //    }
        //}

        public override string Message
        {
            get
            {
                return String.Concat(base.Message,
                    _exceptionResponse != null
                        ? String.Concat(Environment.NewLine, _exceptionResponse)
                        : String.Empty);
            }
        }


    }
}
