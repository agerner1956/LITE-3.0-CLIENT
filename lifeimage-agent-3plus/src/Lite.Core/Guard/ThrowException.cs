using System;

namespace Lite.Core.Guard
{
    /// <summary>
    /// This class is used to indicates that something is wrong with <see cref="Throw"/> during execution.
    /// </summary>
    [Serializable]
    public class ThrowException : System.Exception
    {
        #region ctor

        public ThrowException(string reason)
            : base(reason)
        {
        }

        public ThrowException()
        {
        }

        public ThrowException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ThrowException(System.Runtime.Serialization.SerializationInfo serializationInfo,
            System.Runtime.Serialization.StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        #endregion
    }
}
