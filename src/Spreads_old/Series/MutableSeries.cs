﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace Spreads
{
    // TODO review if this could be ever needed, if not then delete the file

    //[Obsolete("Use interface")]
    //public class MutableSeries<TK, TV> : Series<TK, TV>, IMutableSeries<TK, TV>
    //{
    //    private readonly IMutableSeries<TK, TV> _innerSeries;

    //    public MutableSeries(IMutableSeries<TK, TV> innerSeries)
    //    {
    //        _innerSeries = innerSeries;
    //    }

    //    public long Count => _innerSeries.Count;

    //    public long Version => _innerSeries.Version;

    //    public override bool IsCompleted => _innerSeries.IsCompleted;

    //    public override KeyComparer<TK> Comparer => _innerSeries.Comparer;

    //    public override bool IsIndexed => _innerSeries.IsIndexed;

    //    public override bool IsEmpty => _innerSeries.IsEmpty;

    //    public override KeyValuePair<TK, TV> First => _innerSeries.First;

    //    public override KeyValuePair<TK, TV> Last => _innerSeries.Last;

    //    TV IMutableSeries<TK, TV>.this[TK key]
    //    {
    //        get { return _innerSeries[key]; }
    //        set { _innerSeries[key] = value; }
    //    }

    //    public override IEnumerable<TK> Keys => _innerSeries.Keys;

    //    public override IEnumerable<TV> Values => _innerSeries.Values;

    //    public override bool TryFindAt(TK key, Lookup direction, out KeyValuePair<TK, TV> value)
    //    {
    //        return _innerSeries.TryFindAt(key, direction, out value);
    //    }

    //    public override bool TryGetFirst(out KeyValuePair<TK, TV> value)
    //    {
    //        return _innerSeries.TryGetFirst(out value);
    //    }

    //    public override bool TryGetLast(out KeyValuePair<TK, TV> value)
    //    {
    //        return _innerSeries.TryGetLast(out value);
    //    }

    //    public void Add(TK key, TV value)
    //    {
    //        _innerSeries.Add(key, value);
    //    }

    //    public void AddLast(TK key, TV value)
    //    {
    //        _innerSeries.AddLast(key, value);
    //    }

    //    public void AddFirst(TK key, TV value)
    //    {
    //        _innerSeries.AddFirst(key, value);
    //    }

    //    public bool Remove(TK key)
    //    {
    //        return _innerSeries.Remove(key);
    //    }

    //    public bool RemoveLast(out KeyValuePair<TK, TV> kvp)
    //    {
    //        return _innerSeries.RemoveLast(out kvp);
    //    }

    //    public bool RemoveFirst(out KeyValuePair<TK, TV> kvp)
    //    {
    //        return _innerSeries.RemoveFirst(out kvp);
    //    }

    //    public bool RemoveMany(TK key, Lookup direction)
    //    {
    //        return _innerSeries.RemoveMany(key, direction);
    //    }

    //    public int Append(ISeries<TK, TV> appendMap, AppendOption option)
    //    {
    //        return _innerSeries.Append(appendMap, option);
    //    }

    //    public void Complete()
    //    {
    //        _innerSeries.Complete();
    //    }

    //    public override ICursor<TK, TV> GetCursor()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}