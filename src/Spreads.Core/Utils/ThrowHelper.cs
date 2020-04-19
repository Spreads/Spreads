// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#nullable enable

using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    internal static class ThrowHelper
    {
        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException()
        {
            throw GetArgumentException();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string message)
        {
            throw GetArgumentException(message);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException()
        {
            throw GetArgumentOutOfRangeException();
        }

        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string argument)
        {
            throw GetArgumentOutOfRangeException(argument);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException()
        {
            throw GetInvalidOperationException();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidCastException()
        {
            throw GetInvalidCastException();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string message)
        {
            throw GetInvalidOperationException(message);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_ForVariantTypeMissmatch()
        {
            throw GetInvalidOperationException_ForVariantTypeMissmatch();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotImplementedException()
        {
            throw GetNotImplementedException();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotImplementedException(string message)
        {
            throw GetNotImplementedException(message);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException()
        {
            throw GetNotSupportedException();
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string message)
        {
            throw GetNotSupportedException(message);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowKeyNotFoundException(string message)
        {
            throw GetKeyNotFoundException(message);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string argument)
        {
            throw new ArgumentNullException(argument);
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string objectName)
        {
            throw GetObjectDisposedException(objectName);
        }

        [DoesNotReturn]
        [ContractAnnotation("=> halt")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once InconsistentNaming
        public static void ThrowIOException(string message)
        {
            throw GetIOException(message);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="expectedTrueCondition"></param>
        /// <param name="message"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("halt <= expectedTrueCondition:false")]
        public static void Assert([DoesNotReturnIf(false)]bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                ThrowAssertionFailure(message);
            }
        }

        /// <summary>
        /// Throw exception instead of just printing Fail: xxx with Debug.Assert. Zero cost in release.
        /// </summary>
        /// <param name="expectedTrueCondition"></param>
        /// <param name="message"></param>
        [DebuggerStepThrough]
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("halt <= expectedTrueCondition:false")]
        public static void DebugAssert([DoesNotReturnIf(false)]bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                ThrowAssertionFailure(message);
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowAssertionFailure(string? message = null)
        {
            throw new AssertionFailureException("Assertion failure: " + (message ?? string.Empty));
        }

        [Obsolete("Use only when data could be corrupted or for debug")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [ContractAnnotation("halt <= expectedTrueCondition:false")]
        public static void AssertFailFast([DoesNotReturnIf(false)]bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                DoFailFast(message);
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [ContractAnnotation("=> halt")]
        [Obsolete("Use only when data could be corrupted or for debug")] // TODO review all usage, we should fail only if data could be corrupted or segfault is expected
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

        public class AssertionFailureException : Exception
        {
            public AssertionFailureException(string? message = null) : base(message)
            {
                
            }
        }
    }
}
