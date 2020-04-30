using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads;
using Spreads.Collections.Concurrent;
using Spreads.Collections.Internal;
using Spreads.Native;

// ReSharper disable InconsistentNaming

namespace SpreadsX.Experimental
{
    internal interface ICursorImpl
    {
        CursorState State { get; }
        bool TryMove<K, V>(long stride, bool allowPartial, out long moveCount, ref K key, ref V value);
        //where KVGetter : IBlockIndexCursorKeyValueFactory<K,V>;
    }

    /// <summary>
    /// Implements all cursor logic. Non-generic class with generic methods.
    /// Should be wrapped into generic Cursor[K,V] struct.
    /// </summary>
    internal class CursorImpl
    {
        private static readonly ObjectPool<CursorImpl> ObjectPool = new ObjectPool<CursorImpl>(() => new CursorImpl(), perCoreSize: 256);

        protected CursorImpl()
        {
        }

        private CursorState _state;

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
                if (!_currentBlockStorage.IsEmptySentinel)
                    _currentBlockStorage.Decrement();

                _currentBlockStorage = value;

                if (!value.IsEmptySentinel)
                    value.Increment();
            }
#pragma warning restore 618
        }

        internal int CurrentBlockIndex;

        public CursorState State => _state;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Move<K, V>(long stride, bool allowPartial, ref K key, ref V value)
        {
            // Try to move inside _currentBlock. It could be Empty placeholder,
            // then we will do additional logic. But for MN this attempt should
            // succeed in most cases.

            // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride <= 0). This case is rare.
            // Uninitialized multi-block case goes to rare as well as uninitialized MP
            var newBlockIndex = unchecked((ulong) (CurrentBlockIndex + stride)); // int.Max + long.Max < ulong.Max

            var rowCount = CurrentBlock.RowCount;

            if (newBlockIndex < (ulong) rowCount)
            {
                GetValues(CurrentBlock, (int) newBlockIndex, ref key, ref value);
                CurrentBlockIndex = (int) newBlockIndex;
                return stride;
            }

            // there is some space in the current block, do not lookup the next block
            if (rowCount < CurrentBlock.RowCapacity & stride > 0)
                return 0;

            return MoveBlock(stride, allowPartial, ref key, ref value);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveBlock<K, V>(long stride, bool allowPartial, ref K key, ref V value)
        {
            if(_state == CursorState.Disposed)
                ThrowHelper.ThrowObjectDisposedException("Cursor");

            if (_innerCursor != null)
            {
                if (stride > 0 && _innerCursor.State == CursorState.Batch)
                {
                    
                }
            }
            
            // 
            

            throw new NotImplementedException();
        }

        [Conditional("DEBUG")]
        private void EnsureRootOrInner()
        {
            ThrowHelper.Assert(_root == null ^ _innerCursor == null);
        }
        
        internal virtual bool TryGetNextBatch(ref DataBlock target)
        {
            EnsureRootOrInner();
                
            if (_state == CursorState.Batch)
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

        /// <summary>
        /// We can use this method when we have  
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetValues<K, V>(DataBlock block, int index, ref K key, ref V value)
        {
            if (Getter == null)
            {
                key = block.UnsafeGetRowKey<K>(index);
                value = block.UnsafeGetValue<V>(index);
            }
            else
            {
                GetValuesViaDelegate(block, index, ref key, ref value);
            }
        }

        private void GetValuesViaDelegate<K, V>(DataBlock block, int index, ref K key, ref V value)
        {
            // https://github.com/dotnet/runtime/issues/10048
            // this won't be inlined, so we would need to manually inline and leave only dlg call
            // the top-level method won't be inlined, so it must do all the job
            // but keep it for the final step and compare results.
#if DEBUG
            var dlg = (Getter<K, V>) Getter;
#else
            var dlg = Unsafe.As<Getter<K, V>>(Getter);
#endif
            dlg!.Invoke(block, index, ref key, ref value);
        }

        private object? Getter;

        public static Getter<K, U> Map<K, V, U>(object? getter, Func<K, V, U> selector)
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
                var dlg = Unsafe.As<Getter<K, V>>(getter);
                V middle = default;
                dlg(block, index, ref key, ref middle);
                value = selector(key, middle);
            };
        }

        // TODO FilterMap returns bool
        public static Getter<K, U> Map<K, V, U>(object? getter, Func<V, U> selector)
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
                var dlg = Unsafe.As<Getter<K, V>>(getter);
                V middle = default;
                dlg(block, index, ref key, ref middle);
                value = selector(middle);
            };
        }
    }

    internal delegate void Getter<K, V>(DataBlock block, int index, ref K key, ref V value);

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
        long Move(long steps, bool allowPartial)
        {
            return _impl.Move(steps, allowPartial, ref Key, ref Value);
        }
    }
}