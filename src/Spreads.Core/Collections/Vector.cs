// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Native;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    // these are not thread-safe if underlying storage is mutating
    // but they point correctly to the right addresses and do not copy
    // use case is for batched and immutable data

    public readonly ref struct Vector
    {
        // We keep dangerous readonly methods because they could only crash app or get wrong data
        // to a user who uses these methods incorrectly. But these method will not corrupt underlying
        // data e.g. in mmaped storage.

        internal readonly VectorStorage _vectorStorage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector(VectorStorage vectorStorage)
        {
            _vectorStorage = vectorStorage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<T> As<T>()
        {
            return _vectorStorage.GetVector<T>();
        }

        public object this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vectorStorage[index];
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vectorStorage.Length;
        }

        public Type Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vectorStorage.Type;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object DangerousGet(int index)
        {
            return _vectorStorage.DangerousGet(index);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T DangerousGetRef<T>(int index)
        {
            return ref _vectorStorage.DangerousGetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            return _vectorStorage.Get<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetRef<T>(int index)
        {
            return ref _vectorStorage.GetRef<T>(index);
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

    public readonly ref struct Vector<T>
    {
        internal readonly VectorStorage _vectorStorage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector(VectorStorage vectorStorage)
        {
            var vtidx = VecTypeHelper<T>.RuntimeVecInfo.RuntimeTypeId;
            if (vtidx != vectorStorage._vec._runtimeTypeId)
            {
                VecThrowHelper.ThrowVecTypeMismatchException();
            }
            _vectorStorage = vectorStorage;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // type is checked in ctor, only BC
                if (unchecked((uint)index) >= unchecked((uint)_vectorStorage._length))
                {
                    VecThrowHelper.ThrowIndexOutOfRangeException();
                }
                return _vectorStorage.DangerousGet<T>(index);
            }
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vectorStorage.Length;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGet(int index)
        {
            return _vectorStorage.DangerousGet<T>(index);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T DangerousGetRef(int index)
        {
            return ref _vectorStorage.DangerousGetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetRef(int index)
        {
            return ref _vectorStorage.GetRef<T>(index);
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

    // 
    [Obsolete]
    internal enum SortTracking : byte
    {
        NotTracked,
        Tracked,

        /// <summary>
        /// No mutable operations could change sort order. Any attempt will throw.
        /// </summary>
        Enforced
    }
}