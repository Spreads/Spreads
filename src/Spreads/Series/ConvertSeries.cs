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
        private static BoundedConcurrentBag<TImpl> _pool;
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

        public override Opt<KeyValuePair<TKey2, TValue2>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var f = Inner.First;
                return f.IsMissing ? Opt<KeyValuePair<TKey2, TValue2>>.Missing 
                    : new KeyValuePair<TKey2, TValue2>(ToKey2(f.Present.Key), ToValue2(f.Present.Value));
            }
        }
        
        public override Opt<KeyValuePair<TKey2, TValue2>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var last = Inner.Last;
                return last.IsMissing ? Opt<KeyValuePair<TKey2, TValue2>>.Missing 
                    : new KeyValuePair<TKey2, TValue2>(ToKey2(last.Present.Key), ToValue2(last.Present.Value));
            }
        }

        public override IEnumerable<TKey2> Keys => Inner.Keys.Select(ToKey2);
        public override IEnumerable<TValue2> Values => Inner.Values.Select(ToValue2);

        public override bool TryGetValue(TKey2 key, out TValue2 value)
        {
            if (Inner.TryGetValue(ToKey(key), out var tmp))
            {
                value = ToValue2(tmp);
                return true;
            }

            value = default;
            return false;
        }
        
        public override bool TryFindAt(TKey2 key, Lookup direction, out KeyValuePair<TKey2, TValue2> value)
        {
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryFindAt(ToKey(key), direction, out tmp))
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

        public override bool TryGetAt(long idx, out KeyValuePair<TKey2, TValue2> value)
        {            
            KeyValuePair<TKey, TValue> tmp;
            if (Inner.TryGetAt(idx, out tmp))
            {
                value = new KeyValuePair<TKey2, TValue2>(ToKey2(tmp.Key), ToValue2(tmp.Value));
                return true;
            }
            value = default(KeyValuePair<TKey2, TValue2>);
            return false;
            
        }

        public static TImpl Create(IReadOnlySeries<TKey, TValue> innerSeries)
        {
            if (_pool == null || !_pool.TryTake(out TImpl instance))
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
                if (_pool == null)
                {
                    _pool = new BoundedConcurrentBag<TImpl>(Environment.ProcessorCount * 2);
                }
                if (!_pool.TryAdd(this as TImpl))
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

            public Task<bool> MoveNextAsync()
            {
                return MoveNextAsync(default);
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

            public long MoveNext(long stride, bool allowPartial)
            {
                throw new NotImplementedException();
            }

            public bool MovePrevious()
            {
                return _innerCursor.MovePrevious();
            }

            public long MovePrevious(long stride, bool allowPartial)
            {
                throw new NotImplementedException();
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
                if (_innerCursor.TryGetValue(_source.ToKey(key), out var tmp))
                {
                    value = _source.ToValue2(tmp);
                    return true;
                }
                value = default(TValue2);
                return false;
            }

            public CursorState State => _innerCursor.State;
            
            public KeyComparer<TKey2> Comparer => _source.Comparer;
            
            public TKey2 CurrentKey => _source.ToKey2(_innerCursor.CurrentKey);
            
            public TValue2 CurrentValue => _source.ToValue2(_innerCursor.CurrentValue);

            // TODO object pooling
            public IReadOnlySeries<TKey2, TValue2> CurrentBatch => Create(_innerCursor.CurrentBatch);

            public IReadOnlySeries<TKey2, TValue2> Source => _source; //Create(_innerCursor.Source);
            public bool IsContinuous => _innerCursor.IsContinuous;
            public Task DisposeAsync()
            {
                return _innerCursor.DisposeAsync();
            }
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

        public int Append(IReadOnlySeries<TKey2, TValue2> appendMap, AppendOption option)
        {
            // TODO using ConvertSeries
            throw new NotImplementedException();
        }

        public ValueTask<long> TryAppend(IReadOnlySeries<TKey2, TValue2> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
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
        public Task<bool> Set(TKey2 key, TValue2 value)
        {
            return MutableInner.Set(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAdd(TKey2 key, TValue2 value)
        {
            return MutableInner.TryAdd(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> TryAddLast(TKey2 key, TValue2 value)
        {
            return MutableInner.TryAddLast(ToKey(key), ToValue(value));
        }

        public Task<bool> TryAddFirst(TKey2 key, TValue2 value)
        {
            return MutableInner.TryAddFirst(ToKey(key), ToValue(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<TValue2>> TryRemove(TKey2 key)
        {
            var opt = await MutableInner.TryRemove(ToKey(key));
            return opt.IsPresent ? ToValue2(opt.Present) : Opt<TValue2>.Missing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<KeyValuePair<TKey2, TValue2>>> TryRemoveFirst()
        {
            var opt = await MutableInner.TryRemoveFirst();
            return opt.IsPresent
                ? new KeyValuePair<TKey2, TValue2>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value))
                : Opt<KeyValuePair<TKey2, TValue2>>.Missing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<KeyValuePair<TKey2, TValue2>>> TryRemoveLast()
        {
            var opt = await MutableInner.TryRemoveLast();
            return opt.IsPresent
                ? new KeyValuePair<TKey2, TValue2>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value))
                : Opt<KeyValuePair<TKey2, TValue2>>.Missing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<KeyValuePair<TKey2, TValue2>>> TryRemoveMany(TKey2 key, Lookup direction)
        {
            var opt = await MutableInner.TryRemoveMany(ToKey(key), direction);
            return opt.IsPresent
                ? new KeyValuePair<TKey2, TValue2>(ToKey2(opt.Present.Key), ToValue2(opt.Present.Value))
                : Opt<KeyValuePair<TKey2, TValue2>>.Missing;
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