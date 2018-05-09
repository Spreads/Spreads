// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
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
    // TODO seal overriden methods

    public abstract class ConvertSeries<TKey, TValue, TKey2, TValue2, TImpl> : Series<TKey2, TValue2>, IDisposable
        where TImpl : ConvertSeries<TKey, TValue, TKey2, TValue2, TImpl>, new()
    {
        // TODO use ObjectPool
        private static BoundedConcurrentBag<TImpl> Pool;
        private KeyComparer<TKey2> _comparer;

        protected IReadOnlySeries<TKey, TValue> Inner;

        protected ConvertSeries(IReadOnlySeries<TKey, TValue> inner)
        {
            Inner = inner;

            _comparer = KeyComparer<TKey2>.Create(new ConvertComparer(this as TImpl));
        }

        protected ConvertSeries()
        {
        }

        public abstract TKey2 ToKey2(TKey key);

        public abstract TValue2 ToValue2(TValue value);

        public abstract TKey ToKey(TKey2 key2);

        public abstract TValue ToValue(TValue2 value2);

        public override bool IsCompleted => Inner.IsCompleted;
        public override bool IsEmpty => Inner.IsEmpty;

        public override KeyValuePair<TKey2, TValue2> First
            => new KeyValuePair<TKey2, TValue2>(ToKey2(Inner.First.Key), ToValue2(Inner.First.Value));

        public override KeyValuePair<TKey2, TValue2> Last
            => new KeyValuePair<TKey2, TValue2>(ToKey2(Inner.Last.Key), ToValue2(Inner.Last.Value));

        public override IEnumerable<TKey2> Keys => Inner.Keys.Select(ToKey2);
        public override IEnumerable<TValue2> Values => Inner.Values.Select(ToValue2);

        public override bool TryFind(TKey2 key, Lookup direction, out KeyValuePair<TKey2, TValue2> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryFind(ToKey(key), direction, out tmp))
            {
                value = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<TKey2, TValue2>);
            return false;
        }

        public override bool TryGetFirst(out KeyValuePair<TKey2, TValue2> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryGetFirst(out tmp))
            {
                value = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<TKey2, TValue2>);
            return false;
        }

        public override bool TryGetLast(out KeyValuePair<TKey2, TValue2> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryGetLast(out tmp))
            {
                value = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<TKey2, TValue2>);
            return false;
        }

        public override KeyComparer<TKey2> Comparer => _comparer;
        public override bool IsIndexed => Inner.IsIndexed;

        public override ICursor<TKey2, TValue2> GetCursor()
        {
            return new ConvertCursor(Inner.GetCursor(), this as TImpl);
        }

        public override Task<bool> Updated => Inner.Updated;

        public override TValue2 GetAt(int idx)
        {
            return ToValue2(Inner.GetAt(idx));
        }

        public static TImpl Create(IReadOnlySeries<TKey, TValue> innerSeries)
        {
            if (Pool == null || !Pool.TryTake(out TImpl instance))
            {
                instance = new TImpl();
                instance._comparer = KeyComparer<TKey2>.Create(new ConvertComparer(instance));
            }
            instance.Inner = innerSeries;
            return instance;
        }

        public virtual void Dispose(bool disposing)
        {
            var disposable = Inner as IDisposable;
            disposable?.Dispose();
            Inner = null;
            if (disposing)
            {
                // no pooling from finalizers, just don't do that
                if (Pool == null)
                {
                    Pool = new BoundedConcurrentBag<TImpl>(Environment.ProcessorCount * 2);
                }
                if (!Pool.TryAdd(this as TImpl))
                {
                    // not added to the pool, let it die
                    GC.SuppressFinalize(this);
                }
            }
            
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~ConvertSeries()
        {
            // NB need a finalizer because the inner could be a persistent one
            Dispose(false);
        }

        private struct ConvertCursor : ICursor<TKey2, TValue2>
        {
            private readonly ICursor<TKey, TValue> _innerCursor;
            private readonly TImpl _source;

            public ConvertCursor(ICursor<TKey, TValue> innerCursor, TImpl source)
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

            public KeyValuePair<TKey2, TValue2> Current
                => new KeyValuePair<TKey2, TValue2>(CurrentKey, CurrentValue);

            object IEnumerator.Current => Current;

            public bool MoveAt(TKey2 key, Lookup direction)
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
                return _innerCursor.MoveNextBatch(cancellationToken);
            }

            public ICursor<TKey2, TValue2> Clone()
            {
                return new ConvertCursor(_innerCursor.Clone(), _source);
            }

            public bool TryGetValue(TKey2 key, out TValue2 value)
            {
                TValue tmp;
                if (_innerCursor.TryGetValue(_source.ToKey(key), out tmp))
                {
                    value = _source.ToValue2(tmp);
                    return true;
                }
                value = default(TValue2);
                return false;
            }

            public KeyComparer<TKey2> Comparer => _source.Comparer;
            public TKey2 CurrentKey => _source.ToKey2(_innerCursor.CurrentKey);
            public TValue2 CurrentValue => _source.ToValue2(_innerCursor.CurrentValue);

            // TODO object pooling
            public IReadOnlySeries<TKey2, TValue2> CurrentBatch => Create(_innerCursor.CurrentBatch);

            public IReadOnlySeries<TKey2, TValue2> Source => _source; //Create(_innerCursor.Source);
            public bool IsContinuous => _innerCursor.IsContinuous;
        }

        private struct ConvertComparer : IComparer<TKey2>
        {
            private readonly TImpl _source;

            public ConvertComparer(TImpl source)
            {
                _source = source;
            }

            public int Compare(TKey2 x, TKey2 y)
            {
                var comparer = _source.Inner.Comparer;
                var x1 = _source.ToKey(x);
                var y1 = _source.ToKey(y);
                return comparer.Compare(x1, y1);
            }
        }
    }

    public abstract class ConvertMutableSeries<TKey, TValue, TKey2, TValue2, TImpl>
        : ConvertSeries<TKey, TValue, TKey2, TValue2, TImpl>, IPersistentSeries<TKey2, TValue2>
        where TImpl : ConvertMutableSeries<TKey, TValue, TKey2, TValue2, TImpl>, new()
    {
        private static readonly BoundedConcurrentBag<TImpl> Pool = new BoundedConcurrentBag<TImpl>(Environment.ProcessorCount * 2);

        private IMutableSeries<TKey, TValue> MutableInner
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (IMutableSeries<TKey, TValue>)(Inner); }
        }

        protected ConvertMutableSeries()
        {
        }

        protected ConvertMutableSeries(IMutableSeries<TKey, TValue> innerSeries) : base(innerSeries)
        {
        }

        internal static TImpl Create(IMutableSeries<TKey, TValue> innerSeries)
        {
            TImpl inner;
            if (!Pool.TryTake(out inner))
            {
                inner = new TImpl();
            }
            inner.Inner = innerSeries;
            return inner;
        }

        public override void Dispose(bool disposing)
        {
            var disposable = Inner as IDisposable;
            disposable?.Dispose();
            Inner = null;
            var pooled = Pool.TryAdd(this as TImpl);
            // TODO review
            if (disposing && !pooled)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~ConvertMutableSeries()
        {
            Dispose(false);
        }

        public void Add(TKey2 key, TValue2 value)
        {
            MutableInner.Add(ToKey(key), ToValue(value));
        }

        public void AddLast(TKey2 key, TValue2 value)
        {
            MutableInner.AddLast(ToKey(key), ToValue(value));
        }

        public void AddFirst(TKey2 key, TValue2 value)
        {
            MutableInner.AddFirst(ToKey(key), ToValue(value));
        }

        public bool Remove(TKey2 key)
        {
            return MutableInner.Remove(ToKey(key));
        }

        public bool RemoveLast(out KeyValuePair<TKey2, TValue2> kvp)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (MutableInner.RemoveLast(out tmp))
            {
                kvp = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            kvp = default(KeyValuePair<TKey2, TValue2>);
            return false;
        }

        public bool RemoveFirst(out KeyValuePair<TKey2, TValue2> kvp)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (MutableInner.RemoveFirst(out tmp))
            {
                kvp = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            kvp = default(KeyValuePair<TKey2, TValue2>);
            return false;
        }

        public bool RemoveMany(TKey2 key, Lookup direction)
        {
            return MutableInner.RemoveMany(ToKey(key), direction);
        }

        public int Append(IReadOnlySeries<TKey2, TValue2> appendMap, AppendOption option)
        {
            // TODO using ConvertSeries
            throw new NotImplementedException();
        }

        public void Complete()
        {
            MutableInner.Complete();
        }

        public long Count => MutableInner.Count;

        public long Version => MutableInner.Version;

        public override TValue2 this[TKey2 key] => ToValue2(MutableInner[ToKey(key)]);

        TValue2 IMutableSeries<TKey2, TValue2>.this[TKey2 key]
        {
            get { return ToValue2(MutableInner[ToKey(key)]); }
            set { MutableInner[ToKey(key)] = ToValue(value); }
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