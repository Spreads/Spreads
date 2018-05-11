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
    public class VariantSeries<TKey, TValue> : Series<Variant, Variant>, IReadOnlySeries, IDisposable
    {
        private static ObjectPool<VariantSeries<TKey, TValue>> _pool;
        private readonly KeyComparer<Variant> _comparer;

        internal IReadOnlySeries<TKey, TValue> Inner;

        public VariantSeries(IReadOnlySeries<TKey, TValue> inner)
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
        public sealed override bool IsEmpty => Inner.IsEmpty;

        public sealed override KeyValuePair<Variant, Variant> First
            => new KeyValuePair<Variant, Variant>(ToKey2(Inner.First.Key), ToValue2(Inner.First.Value));

        public sealed override KeyValuePair<Variant, Variant> Last
            => new KeyValuePair<Variant, Variant>(ToKey2(Inner.Last.Key), ToValue2(Inner.Last.Value));

        public sealed override IEnumerable<Variant> Keys => Inner.Keys.Select(ToKey2);
        public sealed override IEnumerable<Variant> Values => Inner.Values.Select(ToValue2);

        public sealed override bool TryFind(Variant key, Lookup direction, out KeyValuePair<Variant, Variant> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryFindAt(ToKey(key), direction, out tmp))
            {
                value = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<Variant, Variant>);
            return false;
        }

        public sealed override bool TryGetFirst(out KeyValuePair<Variant, Variant> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryGetFirst(out tmp))
            {
                value = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<Variant, Variant>);
            return false;
        }

        public sealed override bool TryGetLast(out KeyValuePair<Variant, Variant> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryGetLast(out tmp))
            {
                value = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<Variant, Variant>);
            return false;
        }

        public sealed override KeyComparer<Variant> Comparer => _comparer;
        public sealed override bool IsIndexed => Inner.IsIndexed;

        public sealed override ICursor<Variant, Variant> GetCursor()
        {
            return new VariantCursor(Inner.GetCursor(), this);
        }

        public sealed override Task<bool> Updated => Inner.Updated;

        public sealed override Variant GetAt(int idx)
        {
#pragma warning disable 618
            return ToValue2(Inner.TryGetAt(idx));
#pragma warning restore 618
        }

        public static VariantSeries<TKey, TValue> Create(IReadOnlySeries<TKey, TValue> innerSeries)
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

            public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
            {
                return _innerCursor.MoveNextAsync(cancellationToken);
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

            public bool MoveAt(Variant key, Lookup direction)
            {
                return _innerCursor.MoveAt(_source.ToKey(key), direction);
            }

            public bool MoveFirst()
            {
                return _innerCursor.MoveFirst();
            }

            public bool MoveLast()
            {
                return _innerCursor.MoveLast();
            }

            public bool MovePrevious()
            {
                return _innerCursor.MovePrevious();
            }

            public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
            {
                return _innerCursor.MoveNextSpan(cancellationToken);
            }

            public ICursor<Variant, Variant> Clone()
            {
                return new VariantCursor(_innerCursor.Clone(), _source);
            }

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

            public KeyComparer<Variant> Comparer => _source.Comparer;
            public Variant CurrentKey => _source.ToKey2(_innerCursor.CurrentKey);
            public Variant CurrentValue => _source.ToValue2(_innerCursor.CurrentValue);

            public IReadOnlySeries<Variant, Variant> CurrentBatch => Create(_innerCursor.CurrentSpan);

            public IReadOnlySeries<Variant, Variant> Source => _source; //Create(_innerCursor.Source);
            public bool IsContinuous => _innerCursor.IsContinuous;
        }

        private struct VariantComparer : IComparer<Variant>, IEquatable<VariantComparer>
        {
            private readonly KeyComparer<TKey> _sourceComparer;

            public VariantComparer(IReadOnlySeries<TKey, TValue> inner)
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

        public void Add(Variant key, Variant value)
        {
            MutableInner.Add(ToKey(key), ToValue(value));
        }

        public void AddLast(Variant key, Variant value)
        {
            MutableInner.AddLast(ToKey(key), ToValue(value));
        }

        public void AddFirst(Variant key, Variant value)
        {
            MutableInner.AddFirst(ToKey(key), ToValue(value));
        }

        public bool Remove(Variant key)
        {
            return MutableInner.Remove(ToKey(key));
        }

        public bool RemoveLast(out KeyValuePair<Variant, Variant> kvp)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (MutableInner.RemoveLast(out tmp))
            {
                kvp = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            kvp = default(KeyValuePair<Variant, Variant>);
            return false;
        }

        public bool RemoveFirst(out KeyValuePair<Variant, Variant> kvp)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (MutableInner.RemoveFirst(out tmp))
            {
                kvp = new KeyValuePair<Variant, Variant>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            kvp = default(KeyValuePair<Variant, Variant>);
            return false;
        }

        public bool RemoveMany(Variant key, Lookup direction)
        {
            return MutableInner.RemoveMany(ToKey(key), direction);
        }

        public int Append(IReadOnlySeries<Variant, Variant> appendMap, AppendOption option)
        {
            if (appendMap is VariantSeries<TKey, TValue> vs)
            {
                return MutableInner.Append(vs.Inner, option);
            }
            throw new NotImplementedException();
        }

        public void Complete()
        {
            MutableInner.Complete();
        }

        public long Count => MutableInner.Count;

        public long Version => MutableInner.Version;

        public override Variant this[Variant key] => ToValue2(MutableInner[ToKey(key)]);

        Variant IMutableSeries<Variant, Variant>.this[Variant key]
        {
            get => ToValue2(MutableInner[ToKey(key)]);
            set => MutableInner[ToKey(key)] = ToValue(value);
        }

        public void Flush()
        {
            var p = MutableInner as IPersistentObject;
            p?.Flush();
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