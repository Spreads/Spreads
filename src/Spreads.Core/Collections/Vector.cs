// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Spreads.Buffers;

namespace Spreads.Collections
{
    // these are not thread-safe if underlying storage is mutating
    // but they point correctly to the right addresses and do not copy
    // use case is for batched and immutable data

    // TODO check order version if storage is mutable?

    [Obsolete("Do not use internally, maybe keep as a public API later")]
    public readonly struct Vector : IVector
    {
        // We keep dangerous readonly methods because they could only crash app or get wrong data
        // to a user who uses these methods incorrectly. But these method will not corrupt underlying
        // data e.g. in mmaped storage.

        internal readonly RetainedVec RetainedVec;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector(RetainedVec retainedVec)
        {
            RetainedVec = retainedVec;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public Vector<T> As<T>()
        //{
        //    return _vectorStorage.GetVector<T>();
        //}

        public object this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (unchecked((uint)index) >= unchecked((uint)RetainedVec.Vec.Length))
                {
                    VecThrowHelper.ThrowIndexOutOfRangeException();
                }
                return RetainedVec.Vec.DangerousGet(index);
            }
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RetainedVec.Vec.Length;
        }

        public Type Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RetainedVec.Vec.ItemType;
        }

        [Obsolete("This is slow if the type T knownly matches the underlying type (the method has type check in addition to bound check). Use typed Vector<T> view over VectorStorage.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetItem<T>(int index)
        {
            return RetainedVec.Vec.Get<T>(index);
        }

        public T DangerousGetItem<T>(int index)
        {
            return RetainedVec.Vec.DangerousGetUnaligned<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetRef<T>(int index)
        {
            return ref RetainedVec.Vec.GetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start, int length)
        {
            throw new NotImplementedException();
        }
    }

    public readonly struct Vector<T> : IVector<T>
    {
        // Mainly used as a strongly-typed view over VectorStorage

        internal readonly RetainedVec RetainedVec;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector(RetainedVec retainedVec)
        {
            var vtidx = VecTypeHelper<T>.RuntimeTypeId;
            if (vtidx != retainedVec.Vec._runtimeTypeId)
            {
                VecThrowHelper.ThrowVecTypeMismatchException();
            }
            RetainedVec = retainedVec;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetItem(index);
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RetainedVec.Vec.Length;
        }

        // From IVec
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public T1 Get<T1>(int index)
        //{
        //    if (typeof(T1) == typeof(T))
        //    {
        //        return (T1)(object)Get(index);
        //    }
        //    VecThrowHelper.ThrowVecTypeMismatchException();
        //    return default;
        //}

        //[EditorBrowsable(EditorBrowsableState.Never)]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public T1 DangerousGet<T1>(int index)
        //{
        //    if (typeof(T1) == typeof(T))
        //    {
        //        return (T1)(object)DangerousGet(index);
        //    }
        //    VecThrowHelper.ThrowVecTypeMismatchException();
        //    return default;
        //}

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetItem(int index)
        {
            // type is checked in ctor, only BC
            if (unchecked((uint)index) >= unchecked((uint)RetainedVec.Vec.Length))
            {
                VecThrowHelper.ThrowIndexOutOfRangeException();
            }
            return RetainedVec.Vec.DangerousGetUnaligned<T>(index);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGetItem(int index)
        {
            return RetainedVec!.Vec.DangerousGetUnaligned<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start, int length)
        {
            throw new NotImplementedException();
        }

        // TODO Explicit and typed Enumerator
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
