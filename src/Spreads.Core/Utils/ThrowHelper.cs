// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException()
        {
            throw GetArgumentException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string message)
        {
            throw GetArgumentException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException()
        {
            throw GetArgumentOutOfRangeException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string argument)
        {
            throw GetArgumentOutOfRangeException(argument);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException()
        {
            throw GetInvalidOperationException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidCastException()
        {
            throw GetInvalidCastException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string message)
        {
            throw GetInvalidOperationException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_ForVariantTypeMissmatch()
        {
            throw GetInvalidOperationException_ForVariantTypeMissmatch();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotImplementedException()
        {
            throw GetNotImplementedException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotImplementedException(string message)
        {
            throw GetNotImplementedException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException()
        {
            throw GetNotSupportedException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string message)
        {
            throw GetNotSupportedException(message);
        }

#if SPREADS

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowOutOfOrderKeyException<TKey>(TKey key)
        {
            throw GetOutOfOrderKeyException(key);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowOutOfOrderKeyException<TKey>(TKey key, string message)
        {
            throw GetOutOfOrderKeyException(key, message);
        }

#endif

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowKeyNotFoundException(string message)
        {
            throw GetKeyNotFoundException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string argument)
        {
            throw new ArgumentNullException(argument);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string objectName)
        {
            throw GetObjectDisposedException(objectName);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once InconsistentNaming
        public static void ThrowIOException(string message)
        {
            throw GetIOException(message);
        }

        /// <summary>
        /// Use this instead of Debug.Assert when not in a hot loop.
        /// </summary>
        /// <param name="expectedTrueCondition"></param>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Assert([DoesNotReturnIf(false)]bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                // TODO AssertionFailureException
                throw new Exception("Assertion failure: " + (message ?? string.Empty));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AssertFailFast([DoesNotReturnIf(false)]bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                DoFailFast(message);
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailFast(string? message = null)
        {
            DoFailFast(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoFailFast(string? message = null)
        {
            Environment.FailFast(message, new Exception(message));
        }

        /////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException GetArgumentException()
        {
            return new ArgumentException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException GetArgumentException(string message)
        {
            return new ArgumentException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException()
        {
            return new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string argument)
        {
            return new ArgumentOutOfRangeException(argument);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException()
        {
            return new InvalidOperationException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidCastException GetInvalidCastException()
        {
            return new InvalidCastException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException(string message)
        {
            return new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException_ForVariantTypeMissmatch()
        {
            return new InvalidOperationException("Variant type doesn't match typeof(T)");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotImplementedException GetNotImplementedException()
        {
            return new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotImplementedException GetNotImplementedException(string message)
        {
            return new NotImplementedException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotSupportedException GetNotSupportedException()
        {
            return new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotSupportedException GetNotSupportedException(string message)
        {
            return new NotSupportedException(message);
        }

#if SPREADS

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static OutOfOrderKeyException<TKey> GetOutOfOrderKeyException<TKey>(TKey key)
        {
            return new OutOfOrderKeyException<TKey>(key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static OutOfOrderKeyException<TKey> GetOutOfOrderKeyException<TKey>(TKey key, string message)
        {
            return new OutOfOrderKeyException<TKey>(key, message);
        }

#endif

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static KeyNotFoundException GetKeyNotFoundException(string message)
        {
            return new KeyNotFoundException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ObjectDisposedException GetObjectDisposedException(string objectName)
        {
            return new ObjectDisposedException(objectName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once InconsistentNaming
        private static IOException GetIOException(string message)
        {
            return new IOException(message);
        }
    }
}
