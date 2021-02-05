using System;
using System.Diagnostics.CodeAnalysis;

namespace Lite.Core.Guard
{
    public sealed class Throw
    {
        #region ctor

        private Throw() { }

        #endregion

        #region Public methods

        #region IfNull

        public static void IfNull([NotNull] Object obj)
        {
            IfNull(obj, nameof(obj));
        }

        public static void IfNull(Object obj, string parameterName)
        {
            CheckParameterValue(parameterName);
            string reason = string.Format("{0} cannot be null", parameterName);
            IfNull(obj, parameterName, reason);
        }

        public static void IfNull(Object obj, string parameterName, string reason)
        {
            CheckParameterValue(parameterName);
            CheckParameterValue(reason);
            ThrowImpl((input) => input == null, obj, new ArgumentNullException(parameterName, reason));
        }

        #endregion

        #region IsNullOrWhiteSpace methods

        public static void IfNullOrWhiteSpace(string str)
        {
            IfNullOrWhiteSpace(str, nameof(str));
        }

        public static void IfNullOrWhiteSpace(string str, string parameterName)
        {
            CheckParameterValue(parameterName);
            string reason = string.Format("{0} cannot be null or empty", parameterName);
            IFNullOrWhiteSpace(str, parameterName, reason);
        }

        public static void IFNullOrWhiteSpace(string str, string parameterName, string reason)
        {
            IfNull(str, parameterName, reason);
            ThrowImpl((obj) => string.IsNullOrWhiteSpace(obj.ToString()), str, new ArgumentNullException(parameterName, reason));
        }

        #endregion

        #region IfNegativeOrZero

        public static void IfNegativeOrZero(long number)
        {
            IfNegativeOrZero(number, nameof(number));
        }

        public static void IfNegativeOrZero(long number, string parameterName)
        {
            ThrowImpl((obj) => number <= 0, number, new ArgumentOutOfRangeException(parameterName, number, "Cannot be less or equal zero"));
        }

        public static void IfNegativeOrZero(int number, string parameterName)
        {
            ThrowImpl((obj) => number <= 0, number, new ArgumentOutOfRangeException(parameterName, number, "Cannot be less or equal zero"));
        }

        #endregion

        #region IfZero

        public static void IfZero(object obj, string parameterName)
        {
            if (obj == null)
            {
                return;
            }

            if (obj is long)
            {
                IfZero((long)obj, parameterName);
            }

            if (obj is int)
            {
                IfZero((int)obj, parameterName);
            }
        }

        public static void IfZero(long number, string parameterName)
        {
            ThrowImpl((obj) => number == 0, number, new ArgumentOutOfRangeException(parameterName, number, "Cannot be zero"));
        }

        public static void IfZero(int number, string parameterName)
        {
            ThrowImpl((obj) => number == 0, number, new ArgumentOutOfRangeException(parameterName, number, "Cannot be zero"));
        }

        #endregion

        #region IfFalse methods

        public static void IfFalse(bool value, string parameterName, string reason = null)
        {
            CheckParameterValue(parameterName);
            IfFalse(value, new ArgumentException(reason ?? "Value cannot be false", parameterName));
        }

        public static void IfFalse(bool value, Exception ex)
        {
            ThrowImpl(!value, ex);
        }

        #endregion

        #region IfTrue methods

        public static void IfTrue(bool value, string parameterName, string reason = null)
        {
            CheckParameterValue(parameterName);
            IfTrue(value, new ArgumentException(reason ?? "Value cannot be true", parameterName));
        }

        public static void IfTrue(bool value, Exception ex)
        {
            ThrowImpl(value, ex);
        }

        #endregion

        public static void IfDefault(object value)
        {
            IfTrue(value == default, nameof(value));
        }

        public static void IfEquals<T>(T item, T wrongValue)
            where T : IConvertible, IEquatable<T>
        {
            bool equals = IEquatable<T>.Equals(item, wrongValue);
            IfTrue(equals, nameof(item));
        }

        public static void IfNotEquals<T>(T item, T wrongValue)
            where T : IConvertible, IEquatable<T>
        {
            bool notEquals = !IEquatable<T>.Equals(item, wrongValue);
            ThrowImpl(notEquals, new ArgumentException("Should not be equal"));
        }

        #endregion

        #region Private methods

        private static void CheckParameterValue(string parameter)
        {
            if (parameter == null)
            {
                throw new ThrowException("Input parameter has null value");
            }
        }

        private static void ThrowImpl(Func<object, bool> needToThrow, object obj, Exception ex)
        {
            bool isThrowEx = needToThrow(obj);

            if (!isThrowEx)
            {
                return;
            }

            if (ex == null)
            {
                throw new ThrowException("Exception is not provided");
            }

            throw ex;
        }

        private static void ThrowImpl(bool needToThrow, Exception ex)
        {
            ThrowImpl((obj) => { return bool.Parse(obj.ToString()) == needToThrow; }, needToThrow, ex);
        }

        #endregion
    }
}
