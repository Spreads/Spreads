// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;

// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Spreads
{
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("ReSharper", "RedundantCast")]
        internal static void EnsureOffsetLength(int start, int length, int maxLength)
        {
            if (IntPtr.Size == 8)
            {
                if ((ulong)(uint)start + (ulong)(uint)length > (ulong)(uint)maxLength)
                    ThrowArgumentOutOfRangeException();
            }
            else
            {
                if ((uint)start > (uint)length || (uint)length > (uint)(maxLength - start))
                    ThrowArgumentOutOfRangeException();
            }
        }

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException() => throw new ArgumentException();

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException(string message) => throw new ArgumentException(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException(string argument) => throw GetArgumentOutOfRangeException(argument);

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException() => throw GetInvalidOperationException();

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCastException() => throw new InvalidCastException();

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported() => ThrowInvalidOperationException("Concurrent operations not supported");

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ForVariantTypeMissmatch() => throw new InvalidOperationException("Variant type doesn't match typeof(T)");

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotImplementedException(string message) => throw new NotImplementedException(message);

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException(string message) => throw new NotSupportedException(message);

        [DebuggerStepThrough]
        [DoesNotReturn]
        internal static void ThrowNotSupportedException() => throw new NotSupportedException();

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFoundException(string message) => throw new KeyNotFoundException(message);

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException(string argument) => throw new ArgumentNullException(argument);

        [DebuggerStepThrough]
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException(string objectName) => throw new ObjectDisposedException(objectName);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once InconsistentNaming
        internal static void ThrowIOException(string message) => throw GetIOException(message);

        /// <summary>
        /// </summary>
        /// <param name="expectedTrueCondition"></param>
        /// <param name="message"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Assert([DoesNotReturnIf(parameterValue: false)]
            bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                ThrowAssertionFailure(message);
            }
        }

        /// <summary>
        ///     Throw exception instead of just printing Fail: xxx with Debug.Assert. Zero cost in release.
        /// </summary>
        /// <param name="expectedTrueCondition"></param>
        /// <param name="message"></param>
        [DebuggerStepThrough]
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugAssert([DoesNotReturnIf(parameterValue: false)]
            bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                ThrowAssertionFailure(message);
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowAssertionFailure(string? message = null) => throw new AssertionFailureException("Assertion failure: " + (message ?? string.Empty));

        [Obsolete("Use only when data could be corrupted or for debug")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void AssertFailFast([DoesNotReturnIf(parameterValue: false)]
            bool expectedTrueCondition, string? message = null)
        {
            if (!expectedTrueCondition)
            {
                DoFailFast(message);
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Obsolete("Use only when data could be corrupted or for debug")] // TODO review all usage, we should fail only if data could be corrupted or segfault is expected
        internal static void FailFast(string? message = null) => DoFailFast(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoFailFast(string? message = null) => Environment.FailFast(message, new Exception(message));

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string argument) => new(argument);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException GetInvalidOperationException() => new();

        internal static InvalidOperationException GetInvalidOperationException(ExceptionResource resource) => new(GetResourceString(resource));

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once InconsistentNaming
        private static IOException GetIOException(string message) => new(message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArrayTypeMismatchException() => throw new ArrayTypeMismatchException();

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowVecRuntimeTypeMismatchException() => throw new ArrayTypeMismatchException("Vec runtime type mismatch");

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ArrayNotVector() =>
            throw new InvalidOperationException("Array is not a Vector (one-dimentional array with zero low bound)");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidTypeWithPointersNotSupported(Type targetType) =>
            throw new ArgumentException($"Invalid type with pointers not supported: {targetType}");

        [DoesNotReturn]
        internal static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();

        [DoesNotReturn]
        internal static void ThrowArgumentException_DestinationTooShort() => throw new ArgumentException("SR.Argument_DestinationTooShort");

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRange_IndexException() =>
            throw GetArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_Index);

        [DoesNotReturn]
        internal static void ThrowIndexArgumentOutOfRange_NeedNonNegNumException() =>
            throw GetArgumentOutOfRangeException(ExceptionArgument.index,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        [DoesNotReturn]
        internal static void ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum() =>
            throw GetArgumentOutOfRangeException(ExceptionArgument.length,
                ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

        [DoesNotReturn]
        internal static void ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index() =>
            throw GetArgumentOutOfRangeException(ExceptionArgument.startIndex,
                ExceptionResource.ArgumentOutOfRange_Index);

        [DoesNotReturn]
        internal static void ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count() =>
            throw GetArgumentOutOfRangeException(ExceptionArgument.count,
                ExceptionResource.ArgumentOutOfRange_Count);

        [DoesNotReturn]
        internal static void ThrowWrongKeyTypeArgumentException(object key, Type targetType) => throw GetWrongKeyTypeArgumentException(key, targetType);

        [DoesNotReturn]
        internal static void ThrowWrongValueTypeArgumentException(object value, Type targetType) => throw GetWrongValueTypeArgumentException(value, targetType);

        private static ArgumentException GetAddingDuplicateWithKeyArgumentException(object key) => new($"SR.Format(SR.Argument_AddingDuplicateWithKey, {key})");

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowAddingDuplicateWithKeyArgumentException(object key) => throw GetAddingDuplicateWithKeyArgumentException(key);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFoundException() => throw new KeyNotFoundException();

        [DoesNotReturn]
        internal static void ThrowArgumentException(ExceptionResource resource) => throw GetArgumentException(resource);

        [DoesNotReturn]
        internal static void ThrowArgumentException(ExceptionResource resource, ExceptionArgument argument) => throw GetArgumentException(resource, argument);

        private static ArgumentNullException GetArgumentNullException(ExceptionArgument argument) => new(GetArgumentName(argument));

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException(ExceptionArgument argument) => throw GetArgumentNullException(argument);

        [DoesNotReturn]
        internal static void ThrowArgumentNullException(ExceptionResource resource) => throw new ArgumentNullException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowArgumentNullException(ExceptionArgument argument, ExceptionResource resource) =>
            throw new ArgumentNullException(GetArgumentName(argument), GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument) => throw new ArgumentOutOfRangeException(GetArgumentName(argument));

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource) => throw GetArgumentOutOfRangeException(argument, resource);

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, int paramNumber, ExceptionResource resource) =>
            throw new ArgumentOutOfRangeException(GetArgumentName(argument) + "[" + paramNumber + "]", GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException(ExceptionResource resource) => throw GetInvalidOperationException(resource);

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException(ExceptionResource resource, Exception e) => throw new InvalidOperationException(GetResourceString(resource), e);

        [DoesNotReturn]
        internal static void ThrowSerializationException(ExceptionResource resource) => throw new SerializationException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowSecurityException(ExceptionResource resource) => throw new SecurityException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowRankException(ExceptionResource resource) => throw new RankException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowNotSupportedException(ExceptionResource resource) => throw new NotSupportedException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowUnauthorizedAccessException(ExceptionResource resource) => throw new UnauthorizedAccessException(GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowObjectDisposedException(string objectName, ExceptionResource resource) =>
            throw new ObjectDisposedException(objectName, GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowObjectDisposedException(ExceptionResource resource) => throw new ObjectDisposedException(objectName: null, GetResourceString(resource));

        [DoesNotReturn]
        internal static void ThrowAggregateException(List<Exception> exceptions) => throw new AggregateException(exceptions);

        [DoesNotReturn]
        internal static void ThrowArgumentException_Argument_InvalidArrayType() => throw GetArgumentException(ExceptionResource.Argument_InvalidArrayType);

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumNotStarted() =>
            throw GetInvalidOperationException(ExceptionResource.InvalidOperation_EnumNotStarted);

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumEnded() => throw GetInvalidOperationException(ExceptionResource.InvalidOperation_EnumEnded);

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion() =>
            throw GetInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen() =>
            throw GetInvalidOperationException(ExceptionResource.InvalidOperation_EnumOpCantHappen);

        [DoesNotReturn]
        internal static void ThrowArraySegmentCtorValidationFailedExceptions(Array array, int offset, int count) =>
            throw GetArraySegmentCtorValidationFailedException(array, offset, count);

        private static Exception GetArraySegmentCtorValidationFailedException(Array? array, int offset, int count)
        {
            if (array == null)
                return GetArgumentNullException(ExceptionArgument.array);

            if (offset < 0)
                return GetArgumentOutOfRangeException(ExceptionArgument.offset, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

            if (count < 0)
                return GetArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

            Debug.Assert(array.Length - offset < count);
            return GetArgumentException(ExceptionResource.Argument_InvalidOffLen);
        }

        private static ArgumentException GetArgumentException(ExceptionResource resource) => new(GetResourceString(resource));

        // internal static InvalidOperationException GetInvalidOperationException(ExceptionResource resource) => new(GetResourceString(resource));

        private static ArgumentException GetWrongKeyTypeArgumentException(object key, Type targetType) => new($"SR.Format(SR.Arg_WrongType, {key}, {targetType})", nameof(key));

        private static ArgumentException GetWrongValueTypeArgumentException(object value, Type targetType) =>
            new($"SR.Format(SR.Arg_WrongType, {value}, {targetType})", nameof(value));

        internal static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource) =>
            new(GetArgumentName(argument), GetResourceString(resource));

        private static ArgumentException GetArgumentException(ExceptionResource resource, ExceptionArgument argument) =>
            new(GetResourceString(resource), GetArgumentName(argument));

        // Allow nulls for reference types and Nullable<U>, but not for value types.
        // Aggressively inline so the jit evaluates the if in place and either drops the call altogether
        // Or just leaves null test and call to the Non-returning ThrowHelper.ThrowArgumentNullException
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void IfNullAndNullsAreIllegalThenThrow<T>(object? value, ExceptionArgument argName)
        {
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
            if (default(T) != null && value == null) ThrowArgumentNullException(argName);
        }

        private static string GetArgumentName(ExceptionArgument argument)
        {
            switch (argument)
            {
                case ExceptionArgument.obj:
                    return "obj";
                case ExceptionArgument.dictionary:
                    return "dictionary";
                case ExceptionArgument.array:
                    return "array";
                case ExceptionArgument.info:
                    return "info";
                case ExceptionArgument.key:
                    return "key";
                case ExceptionArgument.text:
                    return "text";
                case ExceptionArgument.value:
                    return "value";
                case ExceptionArgument.startIndex:
                    return "startIndex";
                case ExceptionArgument.list:
                    return "list";
                case ExceptionArgument.index:
                    return "index";
                case ExceptionArgument.capacity:
                    return "capacity";
                case ExceptionArgument.collection:
                    return "collection";
                case ExceptionArgument.item:
                    return "item";
                case ExceptionArgument.converter:
                    return "converter";
                case ExceptionArgument.match:
                    return "match";
                case ExceptionArgument.count:
                    return "count";
                case ExceptionArgument.action:
                    return "action";
                case ExceptionArgument.comparison:
                    return "comparison";
                case ExceptionArgument.exceptions:
                    return "exceptions";
                case ExceptionArgument.exception:
                    return "exception";
                case ExceptionArgument.start:
                    return "start";
                case ExceptionArgument.culture:
                    return "culture";
                case ExceptionArgument.comparer:
                    return "comparer";
                case ExceptionArgument.comparable:
                    return "comparable";
                case ExceptionArgument.length:
                    return "length";
                case ExceptionArgument.manager:
                    return "manager";
                case ExceptionArgument.sourceBytesToCopy:
                    return "sourceBytesToCopy";
                case ExceptionArgument.callBack:
                    return "callBack";
                case ExceptionArgument.creationOptions:
                    return "creationOptions";
                case ExceptionArgument.function:
                    return "function";
                case ExceptionArgument.scheduler:
                    return "scheduler";
                case ExceptionArgument.continuationAction:
                    return "continuationAction";
                case ExceptionArgument.continuationFunction:
                    return "continuationFunction";
                case ExceptionArgument.tasks:
                    return "tasks";
                case ExceptionArgument.asyncResult:
                    return "asyncResult";
                case ExceptionArgument.beginMethod:
                    return "beginMethod";
                case ExceptionArgument.endMethod:
                    return "endMethod";
                case ExceptionArgument.endFunction:
                    return "endFunction";
                case ExceptionArgument.cancellationToken:
                    return "cancellationToken";
                case ExceptionArgument.continuationOptions:
                    return "continuationOptions";
                case ExceptionArgument.delay:
                    return "delay";
                case ExceptionArgument.millisecondsDelay:
                    return "millisecondsDelay";
                case ExceptionArgument.millisecondsTimeout:
                    return "millisecondsTimeout";
                case ExceptionArgument.stateMachine:
                    return "stateMachine";
                case ExceptionArgument.timeout:
                    return "timeout";
                case ExceptionArgument.type:
                    return "type";
                case ExceptionArgument.sourceIndex:
                    return "sourceIndex";
                case ExceptionArgument.sourceArray:
                    return "sourceArray";
                case ExceptionArgument.destinationIndex:
                    return "destinationIndex";
                case ExceptionArgument.destinationArray:
                    return "destinationArray";
                case ExceptionArgument.pHandle:
                    return "pHandle";
                case ExceptionArgument.other:
                    return "other";
                case ExceptionArgument.newSize:
                    return "newSize";
                case ExceptionArgument.lowerBounds:
                    return "lowerBounds";
                case ExceptionArgument.lengths:
                    return "lengths";
                case ExceptionArgument.len:
                    return "len";
                case ExceptionArgument.keys:
                    return "keys";
                case ExceptionArgument.indices:
                    return "indices";
                case ExceptionArgument.index1:
                    return "index1";
                case ExceptionArgument.index2:
                    return "index2";
                case ExceptionArgument.index3:
                    return "index3";
                case ExceptionArgument.length1:
                    return "length1";
                case ExceptionArgument.length2:
                    return "length2";
                case ExceptionArgument.length3:
                    return "length3";
                case ExceptionArgument.endIndex:
                    return "endIndex";
                case ExceptionArgument.elementType:
                    return "elementType";
                case ExceptionArgument.arrayIndex:
                    return "arrayIndex";
                case ExceptionArgument.options:
                    return "options";
                case ExceptionArgument.dictionaryCreationThreshold:
                    return "dictionaryCreationThreshold";
                case ExceptionArgument.queue:
                    return "queue";
                case ExceptionArgument.stack:
                    return "stack";
                case ExceptionArgument.name:
                    return "name";
                case ExceptionArgument.mode:
                    return "mode";
                case ExceptionArgument.view:
                    return "view";
                case ExceptionArgument.offset:
                    return "offset";
                case ExceptionArgument.valueFactory:
                    return "valueFactory";
                case ExceptionArgument.addValueFactory:
                    return "addValueFactory";
                case ExceptionArgument.updateValueFactory:
                    return "updateValueFactory";
                case ExceptionArgument.concurrencyLevel:
                    return "concurrencyLevel";
                case ExceptionArgument.minimumBufferSize:
                    return "minimumBufferSize";
                case ExceptionArgument.elementIndex:
                    return "elementIndex";
                case ExceptionArgument.destination:
                    return "destination";
                case ExceptionArgument.startSegment:
                    return "startSegment";
                case ExceptionArgument.endSegment:
                    return "endSegment";
                case ExceptionArgument.pointer:
                    return "pointer";
                default:
                    Debug.Fail("The enum value is not defined, please check the ExceptionArgument Enum.");
                    return "";
            }
        }

        // This function will convert an ExceptionResource enum value to the resource string.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetResourceString(ExceptionResource resource)
        {
            Debug.Assert(Enum.IsDefined(typeof(ExceptionResource), resource),
                "The enum value is not defined, please check the ExceptionResource Enum.");

            return resource.ToString("G");
        }

        public class AssertionFailureException : Exception
        {
            public AssertionFailureException(string? message = null) : base(message)
            {

            }
        }
    }

    //
    // The convention for this enum is using the argument name as the enum name
    //
    internal enum ExceptionArgument
    {
        obj,
        dictionary,
        dictionaryCreationThreshold,
        array,
        info,
        key,
        collection,
        list,
        match,
        converter,
        queue,
        stack,
        capacity,
        index,
        startIndex,
        value,
        count,
        arrayIndex,
        name,
        mode,
        item,
        options,
        view,
        sourceBytesToCopy,
        action,
        comparison,
        offset,
        newSize,
        elementType,
        length,
        length1,
        length2,
        length3,
        lengths,
        len,
        lowerBounds,
        sourceArray,
        destinationArray,
        sourceIndex,
        destinationIndex,
        indices,
        index1,
        index2,
        index3,
        other,
        comparer,
        endIndex,
        keys,
        creationOptions,
        timeout,
        tasks,
        scheduler,
        continuationFunction,
        millisecondsTimeout,
        millisecondsDelay,
        function,
        exceptions,
        exception,
        cancellationToken,
        delay,
        asyncResult,
        endMethod,
        endFunction,
        beginMethod,
        continuationOptions,
        continuationAction,
        valueFactory,
        addValueFactory,
        updateValueFactory,
        concurrencyLevel,
        text,
        callBack,
        type,
        stateMachine,
        pHandle,
        start,
        minimumBufferSize,
        elementIndex,
        comparable,
        destination,
        startSegment,
        endSegment,
        culture,
        manager,
        pointer
    }

    //
    // The convention for this enum is using the resource name as the enum name
    //
    internal enum ExceptionResource
    {
        Argument_ImplementIComparable,
        Argument_InvalidType,
        Argument_InvalidArgumentForComparison,
        Argument_InvalidRegistryKeyPermissionCheck,
        ArgumentOutOfRange_NeedNonNegNum,

        Arg_ArrayPlusOffTooSmall,
        Arg_NonZeroLowerBound,
        Arg_RankMultiDimNotSupported,
        Arg_RegKeyDelHive,
        Arg_RegKeyStrLenBug,
        Arg_RegSetStrArrNull,
        Arg_RegSetMismatchedKind,
        Arg_RegSubKeyAbsent,
        Arg_RegSubKeyValueAbsent,

        Argument_AddingDuplicate,
        Serialization_InvalidOnDeser,
        Serialization_MissingKeys,
        Serialization_NullKey,
        Argument_InvalidArrayType,
        NotSupported_KeyCollectionSet,
        NotSupported_ValueCollectionSet,
        ArgumentOutOfRange_SmallCapacity,
        ArgumentOutOfRange_Index,
        Argument_InvalidOffLen,
        Argument_ItemNotExist,
        ArgumentOutOfRange_Count,
        ArgumentOutOfRange_InvalidThreshold,
        ArgumentOutOfRange_ListInsert,
        NotSupported_ReadOnlyCollection,
        InvalidOperation_CannotRemoveFromStackOrQueue,
        InvalidOperation_EmptyQueue,
        InvalidOperation_EnumOpCantHappen,
        InvalidOperation_EnumFailedVersion,
        InvalidOperation_EmptyStack,
        ArgumentOutOfRange_BiggerThanCollection,
        InvalidOperation_EnumNotStarted,
        InvalidOperation_EnumEnded,
        NotSupported_SortedListNestedWrite,
        InvalidOperation_NoValue,
        InvalidOperation_RegRemoveSubKey,
        Security_RegistryPermission,
        UnauthorizedAccess_RegistryNoWrite,
        ObjectDisposed_RegKeyClosed,
        NotSupported_InComparableType,
        Argument_InvalidRegistryOptionsCheck,
        Argument_InvalidRegistryViewCheck,
        InvalidOperation_NullArray,
        Arg_MustBeType,
        Arg_NeedAtLeast1Rank,
        ArgumentOutOfRange_HugeArrayNotSupported,
        Arg_RanksAndBounds,
        Arg_RankIndices,
        Arg_Need1DArray,
        Arg_Need2DArray,
        Arg_Need3DArray,
        NotSupported_FixedSizeCollection,
        ArgumentException_OtherNotArrayOfCorrectLength,
        Rank_MultiDimNotSupported,
        InvalidOperation_IComparerFailed,
        ArgumentOutOfRange_EndIndexStartIndex,
        Arg_LowerBoundsMustMatch,
        Arg_BogusIComparer,
        Task_WaitMulti_NullTask,
        Task_ThrowIfDisposed,
        Task_Start_TaskCompleted,
        Task_Start_Promise,
        Task_Start_ContinuationTask,
        Task_Start_AlreadyStarted,
        Task_RunSynchronously_TaskCompleted,
        Task_RunSynchronously_Continuation,
        Task_RunSynchronously_Promise,
        Task_RunSynchronously_AlreadyStarted,
        Task_MultiTaskContinuation_NullTask,
        Task_MultiTaskContinuation_EmptyTaskList,
        Task_Dispose_NotCompleted,
        Task_Delay_InvalidMillisecondsDelay,
        Task_Delay_InvalidDelay,
        Task_ctor_LRandSR,
        Task_ContinueWith_NotOnAnything,
        Task_ContinueWith_ESandLR,
        TaskT_TransitionToFinal_AlreadyCompleted,
        TaskCompletionSourceT_TrySetException_NullException,
        TaskCompletionSourceT_TrySetException_NoExceptions,
        InvalidOperation_WrongAsyncResultOrEndCalledMultiple,
        ConcurrentDictionary_ConcurrencyLevelMustBePositive,
        ConcurrentDictionary_CapacityMustNotBeNegative,
        ConcurrentDictionary_TypeOfValueIncorrect,
        ConcurrentDictionary_TypeOfKeyIncorrect,
        ConcurrentDictionary_SourceContainsDuplicateKeys,
        ConcurrentDictionary_KeyAlreadyExisted,
        ConcurrentDictionary_ItemKeyIsNull,
        ConcurrentDictionary_IndexIsNegative,
        ConcurrentDictionary_ArrayNotLargeEnough,
        ConcurrentDictionary_ArrayIncorrectType,
        ConcurrentCollection_SyncRoot_NotSupported,
        ArgumentOutOfRange_Enum,
        InvalidOperation_HandleIsNotInitialized,
        AsyncMethodBuilder_InstanceNotInitialized,
        ArgumentNull_SafeHandle
    }
}
