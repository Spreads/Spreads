// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors.Experimental
{
    // NB for union and zip specialization won't work - we must know the type of each cursor
    // changing to BaseCursor also won't work - devirtualization is probably not that smart (but who knows)
    // TCursor is here for the case if later we decide to have base cursor class and to compare virtual vs interfaces.
    // However, for struct cursors that will always require wrapping into base cursor class
    // For now assume that TCursor is SpecializedWrapper over ICursor.
    // This is the point where inlining breaks - but these cursors are specialized themselves
    // The idea is that we could limit non-direct calls to union/zip only, all single-series transformations
    // could be inlined - even if it requires many copy-paste specialized boilerplate.
    // Specialization could work for Panels since each column will have the same cursor type...

    // TODO! this should be a struct

    internal sealed class UnionKeys<TKey, TValue, TCursor> :
        AbstractCursorSeries<TKey, TValue, UnionKeys<TKey, TValue, TCursor>>,
        ISpecializedCursor<TKey, TValue, UnionKeys<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        //private readonly ISeries<TKey, TValue>[] _series;
        private readonly ICursor<TKey, TValue>[] _cursors;

        private readonly KeyComparer<TKey> _comparer;
        private readonly bool[] _movedKeysFlags;
        private readonly SortedDeque<TKey, int> _movedKeys;
        private int _liveCounter;
        private SortedDeque<TKey> _outOfOrderKeys;

        public UnionKeys()
        {
        }

        // TODO ctor should accept cursors and not series, otherwise cursor type is not known
        public UnionKeys(params ISeries<TKey, TValue>[] series)
        {
            for (int i = 0; i < series.Length; i++)
            {
                if (i > 0 && !ReferenceEquals(series[i - 1].Comparer, series[i].Comparer))
                {
                    throw new ArgumentException("UnionKeys: Comparers are not equal");
                }
                _cursors[i] = series[i].GetCursor();
            }

            _comparer = series[0].Comparer;
            _movedKeysFlags = new bool[series.Length];
            _movedKeys = new SortedDeque<TKey, int>(series.Length, new KVPComparer<TKey, int>(_comparer, KeyComparer<int>.Default));
            _liveCounter = series.Length;
            //_outOfOrderKeys = new SortedDeque<TKey>();
        }

        public override KeyComparer<TKey> Comparer => _comparer;

        public override bool IsIndexed => false;

        // TODO this is heaviliy used in MNA, remove LINQ, use livecount, review
        public override bool IsReadOnly => _cursors.All(s => s.Source.IsReadOnly);

        public override Task<bool> Updated => throw new NotImplementedException();

        public TKey CurrentKey { get; private set; }

        public TValue CurrentValue => throw new NotSupportedException("UnionKeys series does not support values.");

        public IReadOnlySeries<TKey, TValue> CurrentBatch => null;

        public bool IsContinuous => false;

        public KeyValuePair<TKey, TValue> Current => throw new NotImplementedException();

        object IEnumerator.Current => throw new NotImplementedException();

        public UnionKeys<TKey, TValue, TCursor> Clone()
        {
            throw new NotImplementedException();
        }

        public override UnionKeys<TKey, TValue, TCursor> Initialize()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool MoveAt(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public bool MoveFirst()
        {
            var moved = false;
            Array.Clear(_movedKeysFlags, 0, _movedKeysFlags.Length);
            _movedKeys.Clear();
            for (int i = 0; i < _cursors.Length; i++)
            {
                var c = _cursors[i];
                var movedX = c.MoveFirst();
                if (movedX)
                {
                    _movedKeysFlags[i] = true;
                    _movedKeys.Add(new KeyValuePair<TKey, int>(c.CurrentKey, i));
                }
                moved = moved || movedX;
            }
            if (moved)
            {
                CurrentKey = _movedKeys.First.Key;
                // keep navigating state unchanged
                if (moved && State == CursorState.Initialized)
                {
                    State = CursorState.Moving;
                }
                State = CursorState.Moving;
                return true;
            }
            return false;
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            //if not this.HasValidState then this.MoveFirst()
            //else
            //  // try to recover cursors that have not moved before
            //  if movedKeys.Count < cursors.Length then
            //    let mutable i = 0
            //    while i < movedKeysFlags.Length do
            //      if not movedKeysFlags.[i] then
            //        let c = cursors.[i]
            //        let moved' = c.MoveAt(this.CurrentKey, Lookup.GT)
            //        if moved' then
            //          movedKeysFlags.[i] <- true
            //          movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
            //      i <- i + 1

            //  // ignore cursors that cannot move ahead of frontier during this move, but do
            //  // not remove them from movedKeys so that we try to move them again on the next move
            //  let mutable ignoreOffset = 0
            //  let mutable leftmostIsAheadOfFrontier = false
            //  // current key is frontier, we could call MN after MP, etc.
            //  while ignoreOffset < movedKeys.Count && not leftmostIsAheadOfFrontier do
            //    //leftmostIsAheadOfFrontier <- not cmp.Compare(movedKeys.First.Key, this.CurrentKey) <= 0
            //    let initialPosition = movedKeys.[ignoreOffset]
            //    let cursor = cursors.[initialPosition.Value]

            //    let mutable shouldMove = cmp.Compare(cursor.CurrentKey, this.CurrentKey) <= 0
            //    let mutable movedAtLeastOnce = false
            //    let mutable passedFrontier = not shouldMove
            //    // try move while could move and not passed the frontier
            //    while shouldMove do
            //      let moved = cursor.MoveNext()
            //      movedAtLeastOnce <- movedAtLeastOnce || moved
            //      passedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) > 0
            //      shouldMove <- moved && not passedFrontier

            //    if movedAtLeastOnce || passedFrontier then
            //      if movedAtLeastOnce then
            //        let newPosition = KVP(cursor.CurrentKey, initialPosition.Value)
            //        // update positions if the current has changed, regardless of the frontier
            //        movedKeys.RemoveAt(ignoreOffset) |> ignore
            //        movedKeys.Add(newPosition)

            //      // here passedFrontier if for cursor that after remove/add is not at ignoreOffset idx
            //      if passedFrontier && cmp.Compare(movedKeys.[ignoreOffset].Key, this.CurrentKey) > 0 then
            //        leftmostIsAheadOfFrontier <- true
            //    else
            //      Trace.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            //      ignoreOffset <- ignoreOffset + 1
            //      ()
            //  // end of outer loop
            //  if leftmostIsAheadOfFrontier then
            //      this.CurrentKey <- movedKeys.[ignoreOffset].Key
            //      true
            //  else
            //      false
            throw new NotImplementedException();
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class ZipN<TKey, TValue, TCursor> :
        AbstractCursorSeries<TKey, TValue[], ZipN<TKey, TValue, TCursor>>,
        ISpecializedCursor<TKey, TValue[], ZipN<TKey, TValue, TCursor>>
        where TCursor : ICursor<TKey, TValue>
    {
        public override KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public override bool IsIndexed => throw new NotImplementedException();

        public override bool IsReadOnly => throw new NotImplementedException();

        public override Task<bool> Updated => throw new NotImplementedException();

        public TKey CurrentKey => throw new NotImplementedException();

        public TValue[] CurrentValue => throw new NotImplementedException();

        public IReadOnlySeries<TKey, TValue[]> CurrentBatch => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public KeyValuePair<TKey, TValue[]> Current => throw new NotImplementedException();

        object IEnumerator.Current => throw new NotImplementedException();

        public ZipN<TKey, TValue, TCursor> Clone()
        {
            throw new NotImplementedException();
        }

        public override ZipN<TKey, TValue, TCursor> Initialize()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool MoveAt(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue[] value)
        {
            throw new NotImplementedException();
        }

        ICursor<TKey, TValue[]> ICursor<TKey, TValue[]>.Clone()
        {
            throw new NotImplementedException();
        }
    }
}