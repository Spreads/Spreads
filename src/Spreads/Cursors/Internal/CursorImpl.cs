using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Collections.Concurrent;
using Spreads.Collections.Internal;

// ReSharper disable InconsistentNaming

namespace Spreads.Cursors.Internal
{
    internal abstract class CursorLogic
    {
        public abstract int ProcessBatch(in DataBlock input, ref DataBlock output);
    }
    
    /// <summary>
    /// Implements all cursor logic. Non-generic class with generic methods.
    /// Should be wrapped into generic Cursor[K,V] struct.
    /// </summary>
    internal partial class CursorImpl : IDisposable
    { 
        private static readonly ObjectPool<CursorImpl> ObjectPool = new ObjectPool<CursorImpl>(() => new CursorImpl(), perCoreSize: 256);

        protected CursorImpl()
        {
        }

        public static CursorImpl Create()
        {
            var c = ObjectPool.Rent()!;
            
            // TODO
            return c;
        }

        // private CursorState _cursorState;

        /// <summary>
        /// Lifecycle version, incremented every time this object is returned to the pool.
        /// </summary>
        internal int Version; 
        
        private DataBlock? _root;

        private CursorImpl? _innerCursor;

        private DataBlock _currentBlockStorage = DataBlock.Empty;

        internal DataBlock CurrentBlock
        {
#pragma warning disable 618
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentBlockStorage;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                ThrowHelper.Assert(_currentBlockStorage != value, "CursorImpl.set_CurrentBlock: _currentBlockStorage != value");
                    
                if (!_currentBlockStorage.IsEmptySentinel)
                    _currentBlockStorage.Decrement();

                _currentBlockStorage = value;
                CurrentBlockLo = _currentBlockStorage.Lo;
                CurrentBlockHi = _currentBlockStorage.Hi;
                
                if (!value.IsEmptySentinel)
                    value.Increment();
            }
#pragma warning restore 618
        }

        internal int CurrentBlockIndex = -1;
        
        // Cache Hi & Lo, they are volatile and we do not need to update them while in range
        // Need to cache Lo because it's on the same cache line as Hi.
        internal int CurrentBlockLo;
        internal int CurrentBlockHi;
        
        // public CursorState CursorState => _cursorState;

        /// <summary>
        /// Rooted cursor has null <see cref="_innerCursor"/> and has own data source via <see cref="_root"/>. 
        /// </summary>
        public bool IsRooted => _innerCursor == null;
        
        /// <summary>
        /// Composite cursor has non-null <see cref="_innerCursor"/> cursor and transform data from it.
        /// </summary>
        public bool IsComposite => !IsRooted;

        internal object? DataState;

        [Conditional("DEBUG")]
        private void EnsureRootXorInner()
        {
            ThrowHelper.Assert(_root == null ^ _innerCursor == null);
        }


        public bool MoveNextBatch(out DataSegment dataSegment)
        {
            EnsureRootXorInner();
            dataSegment = default;

            
            if (_root != null)
            {
                
            }
            else
            {
                
            }
            
            // TODO delete this comment
            // How to get source block:
            // * Move only forward (could add backward)
            // * 1. Check if CurrentBlock has remaining data. If it does and the block is full - return it.
            // * 2. If we are at the CB.Hi then try cb.NextBlock
            //   2.1. If it present and full, return it. If present and not full, return if available if more than X (TODO X limit)
            //   2.1. If next block is not present and cursor is rooted, return false.
            // * 3. If inner cursor is not null, call MND on it.
            // * 3.1. If MND returns false and this cursor has no batching capability, return false
            // * 3.2. If MND returns false and this cursor could do batching, populate CB by moving inner cursor,
            //        apply batching TODO WRONG! do not even try to get batch from inner cursor if this cursor
            //        doesn't do batching, we should not store output of the inner cursor in a temp block
            //        Call of this method means that caller is able to do batching and callee should 
            //        populate it's CB and return segment. To populate CB, use this.MoveNext(), may need to initialize CB
            //        then overwrite it.
            
            
            return false;
        }

        public bool MoveAt<K, V, TCursor>(K key, Lookup direction, KeyComparer<K> comparer = default)
        {
            if (typeof(TCursor) == typeof(C<K, V>))
            {
                
            }
            
            return false;
        }
        
        internal virtual bool TryGetNextBatch(ref DataBlock target)
        {
            EnsureRootXorInner();
                
            // if (_cursorState == CursorState.Batch)
            {
                if (_innerCursor == null)
                {
                    var nextBatch = CurrentBlock.NextBlock;
                    if (nextBatch == null || !nextBatch.IsFull)
                        return false;

                    // Batcher(nextBatch, target);

                    throw new NotImplementedException();
                }
            }
            
            return false;
        }
        
        // TODO in Dispose decrement current block
        // Id CursorImpl is protected from double dispose (throws and no side effect on second dispose)
        // then it's safe to borrow block from inner cursors
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetCurrentBlockValues<K, V>(DataBlock block, int index, ref K key, ref V value)
        {
            if (ValueGetter == null)
            {
                key = block.UnsafeGetRowKey<K>(index);
                value = block.UnsafeGetValue<V>(index);
            }
            else
            {
                // TODO this is wrong, current block should store already mapped values
                GetCurrentBlockValuesViaDelegate(block, index, ref key, ref value);
            }
        }

        private void GetCurrentBlockValuesViaDelegate<K, V>(DataBlock block, int index, ref K key, ref V value)
        {
            // https://github.com/dotnet/runtime/issues/10048
            // this won't be inlined, so we would need to manually inline and leave only dlg call
            // the top-level method won't be inlined, so it must do all the job
            // but keep it for the final step and compare results.
#if DEBUG
            // will throw if wrong type
            var dlg = (ValueGetter<K, V>) ValueGetter;
#else
            var dlg = Unsafe.As<ValueGetter<K, V>>(ValueGetter);
#endif
            dlg!.Invoke(block, index, ref key, ref value);
        }

        private object? ValueGetter;

        public static ValueGetter<K, U> Map<K, V, U>(object? getter, Func<K, V, U> selector)
        {
            if (getter == null)
            {
                return (DataBlock block, int index, ref K key, ref U value) =>
                {
                    key = block.UnsafeGetRowKey<K>(index);
                    value = selector(key, block.UnsafeGetValue<V>(index));
                };
            }

            return (DataBlock block, int index, ref K key, ref U value) =>
            {
                var dlg = Unsafe.As<ValueGetter<K, V>>(getter);
                V middle = default;
                dlg(block, index, ref key, ref middle);
                value = selector(key, middle);
            };
        }

        // TODO FilterMap returns bool
        public static ValueGetter<K, U> Map<K, V, U>(object? getter, Func<V, U> selector)
        {
            if (getter == null)
            {
                return (DataBlock block, int index, ref K key, ref U value) =>
                {
                    key = block.UnsafeGetRowKey<K>(index);
                    value = selector(block.UnsafeGetValue<V>(index));
                };
            }

            return (DataBlock block, int index, ref K key, ref U value) =>
            {
                var dlg = Unsafe.As<ValueGetter<K, V>>(getter);
                V middle = default;
                dlg(block, index, ref key, ref middle);
                value = selector(middle);
            };
        }

        public void Dispose()
        {
            // TODO
            unchecked
            {
                Version++;    
            }
            ObjectPool.Return(this);
        }
    }

    internal delegate void ValueGetter<K, V>(DataBlock block, int index, ref K key, ref V value);

    internal delegate void Batcher(DataBlock source, ref DataBlock target);

    internal class ZipImpl : CursorImpl
    {
        private static readonly ObjectPool<ZipImpl> ObjectPool = new ObjectPool<ZipImpl>(() => new ZipImpl(), perCoreSize: 256);

    }

    public struct C<K, V>
    {
        public K Key;
        public V Value;

        private CursorImpl _impl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Move(int steps, bool allowPartial)
        {
            return _impl.Move(steps, allowPartial, ref Key, ref Value);
        }
    }
}