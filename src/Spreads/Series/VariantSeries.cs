// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.DataTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public class VariantSeries<TKey, TValue> : Series<Variant, Variant>, ISeries, IDisposable
    {
        private static ObjectPool<VariantSeries<TKey, TValue>> _pool;
        private readonly KeyComparer<Variant> _comparer;

        internal ISeries<TKey, TValue> Inner;

        public VariantSeries(ISeries<TKey, TValue> inner)
        {
            Inner = inner;
            _comparer = KeyComparer<Variant>.Create(new VariantComparer(Inner));
        }

        protected VariantSeries()
        {
        }

        /// <inheritdoc />
        public TypeEnum KeyType { get; } = VariantHelper<TKey>.TypeEnum;

        /// <inheritdoc />
        public TypeEnum ValueType { get; } = VariantHelper<TValue>.TypeEnum;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Variant ToKey2(TKey key)
        {
            return Variant.Create(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Variant ToValue2(TValue value)
        {
            return Variant.Create(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TKey ToKey(Variant key)
        {
            return key.Get<TKey>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TValue ToValue(Variant value)
        {
            return value.Get<TValue>();
        }

        public sealed override bool IsCompleted => Inner.IsCompleted;

        public sealed override Opt<KeyValuePair<Variant, Variant>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var opt = Inner.First;
                return opt.IsPresent
                    ? Opt.Present(new KeyValuePair<Variant, Variant>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value)))
                    : Opt<KeyValuePair<Variant, Variant>>.Missing;
            }
        }

        public sealed override Opt<KeyValuePair<Variant, Variant>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var opt = Inner.Last;
                return opt.IsPresent
                    ? Opt.Present(new KeyValuePair<Variant, Variant>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value)))
                    : Opt<KeyValuePair<Variant, Variant>>.Missing;            }
        }

        public sealed override IEnumerable<Variant> Keys => Inner.Keys.Select(ToKey2);
        public sealed override IEnumerable<Variant> Values => Inner.Values.Select(ToValue2);

        public sealed override bool TryFindAt(Variant key, Lookup direction, out KeyValuePair<Variant, Variant> value)
        {
            if (Inner.TryFindAt(ToKey(key), direction, out var tmp))
            {
                value = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default;
            return false;
        }

        public sealed override KeyComparer<Variant> Comparer => _comparer;
        public sealed override bool IsIndexed => Inner.IsIndexed;

        public sealed override ICursor<Variant, Variant> GetCursor()
        {
            return new VariantCursor(Inner.GetCursor(), this);
        }

        public sealed override ValueTask Updated => Inner.Updated;

        public override bool TryGetValue(Variant key, out Variant value)
        {
            if (Inner.TryGetValue(ToKey(key), out var tmp))
            {
                value = ToValue2(tmp);
                return true;
            }
            value = default;
            return false;
        }

        public sealed override bool TryGetAt(long idx, out KeyValuePair<Variant, Variant> value)
        {
            if (Inner.TryGetAt(idx, out var kvp))
            {
                value = new KeyValuePair<Variant, Variant>(ToKey2(kvp.Key), ToValue2(kvp.Value));
                return true;
            }

            value = default;
            return false;

        }

        public static VariantSeries<TKey, TValue> Create(ISeries<TKey, TValue> innerSeries)
        {
            VariantSeries<TKey, TValue> instance;
            if (_pool == null || ReferenceEquals(instance = _pool.Allocate(), null))
            {
                instance = new VariantSeries<TKey, TValue>(innerSeries);
            }
            else
            {
                instance.Inner = innerSeries;
            }
            return instance;
        }

        protected virtual void Dispose(bool disposing)
        {
            var disposable = Inner as IDisposable;
            disposable?.Dispose();
            Inner = null;
            if (disposing)
            {
                // no pooling from finalizers, just don't do that
                if (_pool == null)
                {
                    _pool = new ObjectPool<VariantSeries<TKey, TValue>>(() => null, Environment.ProcessorCount * 2);
                }
                _pool.Free(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private struct VariantCursor : ICursor<Variant, Variant>
        {
            private readonly ICursor<TKey, TValue> _innerCursor;
            private readonly VariantSeries<TKey, TValue> _source;

            public VariantCursor(ICursor<TKey, TValue> innerCursor, VariantSeries<TKey, TValue> source)
            {
                _innerCursor = innerCursor;
                _source = source;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Task<bool> MoveNextAsync()
            {
                return _innerCursor.MoveNextAsync();
            }
           

            public void Dispose()
            {
                _innerCursor.Dispose();
            }

            public bool MoveNext()
            {
                return _innerCursor.MoveNext();
            }

            public void Reset()
            {
                _innerCursor.Reset();
            }

            public KeyValuePair<Variant, Variant> Current
                => new KeyValuePair<Variant, Variant>(CurrentKey, CurrentValue);

            object IEnumerator.Current => Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveAt(Variant key, Lookup direction)
            {
                return _innerCursor.MoveAt(_source.ToKey(key), direction);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveFirst()
            {
                return _innerCursor.MoveFirst();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveLast()
            {
                return _innerCursor.MoveLast();
            }

            public long MoveNext(long stride, bool allowPartial)
            {
                throw new NotImplementedException();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MovePrevious()
            {
                return _innerCursor.MovePrevious();
            }

            public long MovePrevious(long stride, bool allowPartial)
            {
                throw new NotImplementedException();
            }

            public Task<bool> MoveNextBatch()
            {
                return _innerCursor.MoveNextBatch();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ICursor<Variant, Variant> Clone()
            {
                return new VariantCursor(_innerCursor.Clone(), _source);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetValue(Variant key, out Variant value)
            {
                TValue tmp;
                if (_innerCursor.TryGetValue(_source.ToKey(key), out tmp))
                {
                    value = _source.ToValue2(tmp);
                    return true;
                }
                value = default(Variant);
                return false;
            }

            public CursorState State => _innerCursor.State;
            public KeyComparer<Variant> Comparer => _source.Comparer;
            public Variant CurrentKey
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _source.ToKey2(_innerCursor.CurrentKey); }
            }

            public Variant CurrentValue
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _source.ToValue2(_innerCursor.CurrentValue); }
            }

            public ISeries<Variant, Variant> CurrentBatch => Create(_innerCursor.CurrentBatch);

            public ISeries<Variant, Variant> Source => _source; //Create(_innerCursor.Source);
            public bool IsContinuous => _innerCursor.IsContinuous;
            
            public Task DisposeAsync()
            {
                return _innerCursor.DisposeAsync();
            }
        }

        private struct VariantComparer : IComparer<Variant>, IEquatable<VariantComparer>
        {
            private readonly KeyComparer<TKey> _sourceComparer;

            public VariantComparer(ISeries<TKey, TValue> inner)
            {
                _sourceComparer = inner.Comparer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(Variant x, Variant y)
            {
                var x1 = x.Get<TKey>();
                var y1 = y.Get<TKey>();
                return _sourceComparer.Compare(x1, y1);
            }

            public bool Equals(VariantComparer other)
            {
                return Equals(_sourceComparer, other._sourceComparer);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is VariantComparer && Equals((VariantComparer)obj);
            }

            public override int GetHashCode()
            {
                return _sourceComparer.GetHashCode();
            }
        }
    }

    public sealed class MutableVariantSeries<TKey, TValue>
        : VariantSeries<TKey, TValue>, IPersistentSeries<Variant, Variant>
    {
        private static ObjectPool<MutableVariantSeries<TKey, TValue>> _pool;

        private IMutableSeries<TKey, TValue> MutableInner
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (IMutableSeries<TKey, TValue>)(Inner); }
        }

        public MutableVariantSeries(IMutableSeries<TKey, TValue> innerSeries) : base(innerSeries)
        {
        }

        internal static MutableVariantSeries<TKey, TValue> Create(IMutableSeries<TKey, TValue> innerSeries)
        {
            MutableVariantSeries<TKey, TValue> instance;
            if (_pool == null || ReferenceEquals(instance = _pool.Allocate(), null))
            {
                instance = new MutableVariantSeries<TKey, TValue>(innerSeries);
            }
            else
            {
                instance.Inner = innerSeries;
            }
            return instance;
        }

        protected override void Dispose(bool disposing)
        {
            var disposable = Inner as IDisposable;
            disposable?.Dispose();
            Inner = null;
            if (disposing)
            {
                // no pooling from finalizers, just don't do that
                if (_pool == null)
                {
                    _pool = new ObjectPool<MutableVariantSeries<TKey, TValue>>(() => null, Environment.ProcessorCount * 2);
                }
                _pool.Free(this);
            }
        }

        public Task<bool> TryRemoveMany(Variant key, Variant updatedAtKey, Lookup direction)
        {
            throw new NotSupportedException();
        }

        public ValueTask<long> TryAppend(ISeries<Variant, Variant> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
        {
            throw new NotImplementedException();
        }

        public Task Complete()
        {
            return MutableInner.Complete();
        }

        public long Count => MutableInner.Count;

        public long Version => MutableInner.Version;
        
        public bool IsAppendOnly => MutableInner.IsAppendOnly;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> Set(Variant key, Variant value)
        {
            return MutableInner.TryAdd(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAdd(Variant key, Variant value)
        {
            return MutableInner.TryAdd(ToKey(key), ToValue(value));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAddLast(Variant key, Variant value)
        {
            return MutableInner.TryAddLast(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAddFirst(Variant key, Variant value)
        {
            return MutableInner.TryAddFirst(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<Variant>> TryRemove(Variant key)
        {
            var opt = await MutableInner.TryRemove(ToKey(key));
            return opt.IsPresent
                ? Opt.Present(ToValue2(opt.Present))
                : Opt<Variant>.Missing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<KeyValuePair<Variant, Variant>>> TryRemoveFirst()
        {
            var opt = await MutableInner.TryRemoveFirst();
            return opt.IsPresent
                ? Opt.Present(new KeyValuePair<Variant, Variant>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value)))
                : Opt<KeyValuePair<Variant, Variant>>.Missing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<KeyValuePair<Variant, Variant>>> TryRemoveLast()
        {
            var opt = await MutableInner.TryRemoveLast();
            return opt.IsPresent
                ? Opt.Present(new KeyValuePair<Variant, Variant>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value)))
                : Opt<KeyValuePair<Variant, Variant>>.Missing;
        }

        public async ValueTask<Opt<KeyValuePair<Variant, Variant>>> TryRemoveMany(Variant key, Lookup direction)
        {
            var opt = await MutableInner.TryRemoveMany(ToKey(key), direction);
            return opt.IsPresent
                ? Opt.Present(new KeyValuePair<Variant, Variant>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value)))
                : Opt<KeyValuePair<Variant, Variant>>.Missing;
        }

        public Task Flush()
        {
            return MutableInner is IPersistentObject p ? p.Flush() : Task.CompletedTask;
        }

        public string Id
        {
            get
            {
                var p = MutableInner as IPersistentObject;
                return p?.Id;
            }
        }
    }
}