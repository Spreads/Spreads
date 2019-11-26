//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.


//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Threading;
//using System.Threading.Tasks.Sources;

//namespace Spreads
//{
//    public class BlockingCursor<TKey, TValue, TCursor> :
//        ICursor<TKey, TValue>,
//        ICursor<TKey, TValue, TCursor>,
//        IAsyncCompletable
//        where TCursor : ICursor<TKey, TValue, TCursor>
//    {
        
//        private TCursor _cursor;
//        private SemaphoreSlim _semaphore = new SemaphoreSlim(0, Int32.MaxValue);
//        private IDisposable _subscription;

//        public BlockingCursor(TCursor cursor)
//        {
//            _cursor = cursor;
//            _subscription = _cursor.AsyncCompleter.Subscribe(this);
//        }

//        public CursorState State => _cursor.State;

//        public KeyComparer<TKey> Comparer => _cursor.Comparer;

//        public bool MoveFirst()
//        {
//            return _cursor.MoveFirst();
//        }

//        public bool MoveLast()
//        {
//            // TODO blocking
//            return _cursor.MoveLast();
//        }

//        public bool MoveNext()
//        {
//            return _cursor.MoveNext();
//        }

//        public long MoveNext(long stride, bool allowPartial)
//        {
//            // TODO blocking
//            return _cursor.MoveNext(stride, allowPartial);
//        }

//        public bool MovePrevious()
//        {
//            return _cursor.MovePrevious();
//        }

//        public long MovePrevious(long stride, bool allowPartial)
//        {
//            return _cursor.MovePrevious(stride, allowPartial);
//        }

//        public bool MoveAt(TKey key, Lookup direction)
//        {
//            return _cursor.MoveAt(key, direction);
//        }

//        public TKey CurrentKey => _cursor.CurrentKey;

//        public TValue CurrentValue => _cursor.CurrentValue;
//        Series<TKey, TValue, TCursor> ICursor<TKey, TValue, TCursor>.Source => _cursor.Source;

//        public IAsyncCompleter AsyncCompleter => _cursor.AsyncCompleter;

//        public ISeries<TKey, TValue> Source => _cursor.Source;

//        public bool IsContinuous => _cursor.IsContinuous;

//        public TCursor Initialize()
//        {
//            return _cursor.Initialize();
//        }

//        TCursor ICursor<TKey, TValue, TCursor>.Clone()
//        {
//            return _cursor.Clone();
//        }

//        public bool IsIndexed => _cursor.IsIndexed;

//        public bool IsCompleted => _cursor.IsCompleted;

//        public ICursor<TKey, TValue> Clone()
//        {
//            return _cursor.Clone();
//        }

//        public bool TryGetValue(TKey key, out TValue value)
//        {
//            return _cursor.TryGetValue(key, out value);
//        }

//        bool IEnumerator.MoveNext()
//        {
//            return MoveNext();
//        }

//        public void Reset()
//        {
//            _cursor.Reset();
//        }

//        public KeyValuePair<TKey, TValue> Current => _cursor.Current;

//        object? IEnumerator.Current => Current;

//        public void Dispose()
//        {
//            _subscription.Dispose();
//            _semaphore.Dispose();
//            _cursor.Dispose();
//        }

//        public void TryComplete(bool cancel)
//        {
//            try
//            {
//                _semaphore.Release();
//            }
//            catch (SemaphoreFullException)
//            { }
//        }
//    }
//}