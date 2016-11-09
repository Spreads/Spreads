using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads {
    internal static class ThrowHelper {
        public static void ThrowArgumentNullException(ExceptionArgument argument) {
            throw GetArgumentNullException(argument);
        }

        public static void ThrowArgumentException() {
            throw GetArgumentException();
        }

        public static void ThrowArgumentException(ExceptionArgument argument) {
            throw GetArgumentException(argument);
        }

        public static void ThrowArgumentOutOfRangeException() {
            throw GetArgumentOutOfRangeException();
        }

        public static void ThrowArgumentOutOfRangeException(ExceptionArgument argument) {
            throw GetArgumentOutOfRangeException(argument);
        }

        public static void ThrowInvalidOperationException() {
            throw GetInvalidOperationException();
        }

        public static void ThrowInvalidOperationException_ForVariantTypeMissmatch() {
            throw GetInvalidOperationException_ForVariantTypeMissmatch();
        }

        public static void ThrowNotImplementedException() {
            throw GetNotImplementedException();
        }

        /////////////////////////////////////////////////////////////////////////////
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentNullException GetArgumentNullException(ExceptionArgument argument) {
            return new ArgumentNullException(GetArgumentName(argument));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException GetArgumentException() {
            return new ArgumentException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentException GetArgumentException(ExceptionArgument argument) {
            return new ArgumentException(GetArgumentName(argument));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException() {
            return new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument) {
            return new ArgumentOutOfRangeException(GetArgumentName(argument));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException() {
            return new InvalidOperationException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException_ForVariantTypeMissmatch() {
            return new InvalidOperationException("Variant type doesn't match typeof(T)");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotImplementedException GetNotImplementedException() {
            return new NotImplementedException();
        }

        private static string GetArgumentName(ExceptionArgument argument) {
            Debug.Assert(Enum.IsDefined(typeof(ExceptionArgument), argument),
                "The enum value is not defined, please check the ExceptionArgument Enum.");

            return argument.ToString();
        }
    }

    internal enum ExceptionArgument {
        pointer,
        array
    }
}