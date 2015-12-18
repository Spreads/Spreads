/*
    Copyright(c) 2014-2015 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using Spreads.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {


    //internal class SMACursor<K> : SimpleBindCursor<K, double, double> {
    //    protected ICursor<K, double> _laggedCursor;
    //    protected double _sum = 0.0;
    //    protected int _count = 0;
    //    private readonly Func<ICursor<K, double>> _cursorFactory;
    //    protected int _period;
    //    private readonly bool _allowIncomplete;

    //    public override bool IsContinuous {
    //        get {
    //            throw new NotImplementedException();
    //        }
    //    }

    //    public SMACursor(Func<ICursor<K, double>> cursorFactory, int period, bool allowIncomplete = false)
    //        : base(cursorFactory) {
    //        _cursorFactory = cursorFactory;
    //        _period = period;
    //        _allowIncomplete = allowIncomplete;

    //    }

    //    //private SMACursor(Func<ICursor<K, double>> cursorFactory, int period, bool allowIncomplete, Func<double,double> mapper)
    //    //    : base(cursorFactory, mapper) {
    //    //    _cursorFactory = cursorFactory;
    //    //    _period = period;
    //    //    _allowIncomplete = allowIncomplete;
    //    //}

    //    public override bool TryGetValue(K key, bool isMove, out double value) {
    //        if (isMove) {
    //            value = 0.0;

    //            _sum = 0.0;
    //            _count = 0;

    //            if (_laggedCursor == null) {
    //                _laggedCursor = this.InputCursor.Clone();
    //            }
    //            if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
    //                _sum += _laggedCursor.CurrentValue;
    //                _count++;

    //                while (_count < _period && _laggedCursor.MovePrevious()) {
    //                    _sum += _laggedCursor.CurrentValue;
    //                    _count++;
    //                }
    //                if (_count == _period) {
    //                    value = _sum / _count;
    //                    return true;
    //                } else if (_allowIncomplete) {
    //                    value = _sum / _count;
    //                    _laggedCursor.MoveFirst(); // it was in reset value because tried to move before the first key
    //                    return true;
    //                } else {
    //                    return false;
    //                }
    //            } else {
    //                return false;
    //            }
    //        } else {
    //            Trace.TraceWarning("TODO SMACursor: this is inefficient, do not clone on every call");
    //            using (var tmpcursor = this.InputCursor.Clone()) {
    //                var c = 0;
    //                var sum = 0.0;
    //                sum += tmpcursor.CurrentValue;
    //                c++;
    //                while (c < _period && tmpcursor.MovePrevious()) {
    //                    sum += tmpcursor.CurrentValue;
    //                    c++;
    //                }
    //                if (c == _period || _allowIncomplete) {
    //                    value = sum / (double)c;
    //                    return true;
    //                } else {
    //                    value = 0.0;
    //                    return false;
    //                }
    //            }
    //        }
    //    }


    //    public override bool TryUpdateNext(KeyValuePair<K, double> next, out double value) {
    //        if (_count >= _period) {
    //            Trace.Assert(_count == _period, "_count should never be above _period");
    //            _sum += next.Value - _laggedCursor.CurrentValue;
    //            var laggedMoved = _laggedCursor.MoveNext();
    //            if (laggedMoved) {
    //                value = _sum / (double)_count;
    //                return true;
    //            } else {
    //                throw new ApplicationException("Lagged should always move here");
    //            }
    //        } else {
    //            _sum += next.Value;
    //            _count++;
    //            // do not move lagged until
    //            value = _sum / (double)_count;
    //            return true;
    //        }
    //    }

    //    //public override bool TryUpdateStatePrevious(KeyValuePair<K, double> next, ref double value) {
    //    //    throw new NotImplementedException("TODO! implement this");
    //    //}

    //    public override ICursor<K, double> Clone() {
    //        var clone = new SimpleMovingAverageCursor<K>(_cursorFactory, _period, _allowIncomplete);
    //        if (base.HasValidState) {
    //            clone.MoveAt(base.CurrentKey, Lookup.EQ);
    //        }
    //        return clone;
    //    }


    //    //public override Series<K, V3> Map<V3>(Func<double, V3> f2)
    //    //{
    //    //    new SMACursor<K>(_cursorFactory, _period, _allowIncomplete, f2);
    //    //}
    //}



    //public class SimpleMovingAverageCursor<K> : SimpleBindCursor<K, double, double> {
    //    protected ICursor<K, double> _laggedCursor;
    //    protected double _sum;
    //    protected int _count = 0;
    //    private readonly Func<ICursor<K, double>> _cursorFactory;
    //    protected int _period;
    //    private readonly bool _allowIncomplete;

    //    public override bool IsContinuous => false;

    //    public SimpleMovingAverageCursor(Func<ICursor<K, double>> cursorFactory, int period, bool allowIncomplete = false)
    //        : base(cursorFactory) {
    //        _cursorFactory = cursorFactory;
    //        _period = period;
    //        _allowIncomplete = allowIncomplete;
    //    }

    //    public override bool TryGetValue(K key, bool isMove, out double value) {
    //        value = 0.0;

    //        if (isMove) // we have moved at the key
    //        {
    //            if (_laggedCursor == null) {
    //                _laggedCursor = this.InputCursor.Clone();
    //            }
    //            _sum = 0.0;
    //            _count = 0;
    //            if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
    //                _sum += _laggedCursor.CurrentValue;
    //                _count++;

    //                while (_count < _period && _laggedCursor.MovePrevious()) {
    //                    _sum += _laggedCursor.CurrentValue;
    //                    _count++;
    //                }
    //                if (_count == _period) {
    //                    value = _sum / (double)_count;
    //                    return true;
    //                } else if (_allowIncomplete) {
    //                    _laggedCursor.MoveFirst(); // it was in reset value because tried to move before the first key
    //                    value = _sum / (double)_count;
    //                    return true;
    //                } else {
    //                    return false;
    //                }
    //            } else {
    //                return false;
    //            }
    //        } else // we are trying to get value without moving a cursor
    //          {
    //            using (var tmpcursor = this.InputCursor.Clone()) {
    //                var c = 0;
    //                var sum = 0.0;
    //                sum += tmpcursor.CurrentValue;
    //                c++;
    //                while (c < _period && tmpcursor.MovePrevious()) {
    //                    sum += tmpcursor.CurrentValue;
    //                    c++;
    //                }
    //                if (c == _period || _allowIncomplete) {
    //                    value = sum / (double)c;
    //                    return true;
    //                } else {
    //                    return false;
    //                }
    //            }
    //        }
    //    }

    //    public override bool TryUpdateNext(KeyValuePair<K, double> next, out double value) {
    //        if (_count >= _period) {
    //            Trace.Assert(_count == _period, "_count should never be above _period");
    //            _sum += next.Value - _laggedCursor.CurrentValue;
    //            var laggedMoved = _laggedCursor.MoveNext();
    //            if (laggedMoved) {
    //                value = _sum / (double)_count;
    //                return true;
    //            } else {
    //                throw new ApplicationException("Lagged should always move here");
    //            }
    //        } else {
    //            _sum += next.Value;
    //            _count++;
    //            // do not move lagged until
    //            value = _sum / (double)_count;
    //            return true;
    //        }


    //    }

    //    public override ICursor<K, double> Clone() {
    //        var clone = new SimpleMovingAverageCursor<K>(_cursorFactory, _period, _allowIncomplete);
    //        if (base.HasValidState) {
    //            clone.MoveAt(base.CurrentKey, Lookup.EQ);
    //        }
    //        return clone;
    //    }
    //}



    //public class StandardDeviationCursor<K> : SimpleBindCursor<K, double, double> {
    //    protected ICursor<K, double> _laggedCursor;
    //    private readonly Func<ICursor<K, double>> _cursorFactory;
    //    protected int _period;
    //    protected double _sum;
    //    protected double _sumSq;

    //    public StandardDeviationCursor(Func<ICursor<K, double>> cursorFactory, int period)
    //        : base(cursorFactory) {
    //        _cursorFactory = cursorFactory;
    //        _period = period;
    //    }

    //    public override bool IsContinuous => false;

    //    public override bool TryGetValue(K key, bool isPositioned, out double value) {
    //        value = 0.0;
    //        if (isPositioned) {
    //            if (_laggedCursor == null) {
    //                _laggedCursor = this.InputCursor.Clone();
    //            }
    //            var c = 0;
    //            _sum = 0.0;
    //            _sumSq = 0.0;
    //            if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
    //                var curValue = _laggedCursor.CurrentValue;
    //                _sum += curValue;
    //                _sumSq += curValue * curValue;
    //                c++;
    //                while (c < _period && _laggedCursor.MovePrevious()) {
    //                    var curValue2 = _laggedCursor.CurrentValue;
    //                    _sum += curValue2;
    //                    _sumSq += curValue2 * curValue2;
    //                    c++;
    //                }
    //                if (c == _period) {
    //                    value = Math.Sqrt(_sumSq / ((double)(c - 1)) - _sum * _sum / ((double)c * (double)(c - 1)));
    //                    return true;
    //                } else {
    //                    return false;
    //                }
    //            }
    //            return true;
    //        } else {
    //            using (var tmpcursor = this.InputCursor.Clone()) {
    //                var c = 0;
    //                var sum = 0.0;
    //                var sumSq = 0.0;
    //                sum += tmpcursor.CurrentValue;
    //                sumSq += _laggedCursor.CurrentValue * _laggedCursor.CurrentValue;
    //                c++;
    //                while (c < _period && tmpcursor.MovePrevious()) {
    //                    sum += tmpcursor.CurrentValue;
    //                    sumSq += _laggedCursor.CurrentValue * _laggedCursor.CurrentValue;
    //                    c++;
    //                }
    //                if (c == _period) {
    //                    value = Math.Sqrt(_sumSq / ((double)(c - 1)) - _sum * _sum / ((double)c * (double)(c - 1)));
    //                    return true;
    //                } else {
    //                    return false;
    //                }
    //            }
    //        }
    //    }

    //    public override bool TryUpdateNext(KeyValuePair<K, double> next, out double value) {
    //        var cv = _laggedCursor.CurrentValue;
    //        _sum += next.Value - cv;
    //        _sumSq += next.Value * next.Value - cv * cv;

    //        var laggedMoved = _laggedCursor.MoveNext();
    //        if (laggedMoved) {
    //            var periodMinusOne = (double)(_period - 1);
    //            value = Math.Sqrt(_sumSq / periodMinusOne - _sum * _sum / ((double)_period * (periodMinusOne)));
    //            return true;
    //        } else {
    //            throw new ApplicationException("Lagged should always move here");
    //        }
    //    }

    //    public override ICursor<K, double> Clone() {
    //        var clone = new StandardDeviationCursor<K>(_cursorFactory, _period);
    //        if (base.HasValidState) {
    //            clone.MoveAt(base.CurrentKey, Lookup.EQ);
    //        }
    //        return clone;
    //    }
    //}


    internal class SmaState {
        public double Count;
        public double Sum;
    }

    internal class StDevState {
        public double Count;
        public double Sum;
        public double SumSq;
    }


    public static class CursorSeriesExtensions {

        // TODO Rewrite via BindCursor

        public static Series<K, double> SMA<K>(this ISeries<K, double> source, int period, bool allowIncomplete = false) {
            Func<ICursor<K, SmaState>> factory = () => new ScanLagAllowIncompleteCursor<K, double, SmaState>(source.GetCursor, (uint)period, 1,
               () => new SmaState(),
               (st, add, sub, cnt) => {
                   st.Count = cnt;
                   st.Sum = st.Sum + add.Value - sub.Value;
                   return st;
               }, allowIncomplete);
            return (new CursorSeries<K, SmaState>(factory).Map(st => st.Sum / st.Count));
        }


        public static Series<K, double> StDev<K>(this ISeries<K, double> source, int period, bool allowIncomplete = false) {
            Func<ICursor<K, StDevState>> factory = () => new ScanLagAllowIncompleteCursor<K, double, StDevState>(source.GetCursor, (uint)period, 1,
                () => new StDevState(),
                (st, add, sub, cnt) => {
                    st.Count = cnt;
                    st.Sum = st.Sum + add.Value - sub.Value;
                    st.SumSq = st.SumSq + (add.Value * add.Value) - (sub.Value * sub.Value);
                    return st;
                }, allowIncomplete);
            // Filter (k, st) => st.Count > 1,
            return (new CursorSeries<K, StDevState>(factory))
                .FilterMap((k, st) => st.Count > 1, st => {
                    var periodMinusOne = (double)(st.Count - 1.0);
                    var value = Math.Sqrt((st.SumSq / periodMinusOne) - (st.Sum * st.Sum) / ((double)(st.Count) * (periodMinusOne)));
                    return value;
                }); //.Filter(x => x > 0.0);
        }



        //internal static Series<K, double> SMAOld<K>(this ISeries<K, double> source, int period, bool allowIncomplete = false) {
        //    return new CursorSeries<K, double>(() => new SMACursor<K>(source.GetCursor, period, allowIncomplete));
        //}

        ///// <summary>
        ///// Moving standard deviation
        ///// </summary>
        ///// <typeparam name="K"></typeparam>
        ///// <param name="source"></param>
        ///// <param name="period"></param>
        ///// <returns></returns>
        //public static Series<K, double> StDevOld<K>(this ISeries<K, double> source, int period) {
        //    return new CursorSeries<K, double>(() => new StandardDeviationCursor<K>(source.GetCursor, period));
        //}

        /// <summary>
        /// Eager grouping using LINQ group by on Series IEnumerable<KVP<,>> interface
        /// </summary>
        public static Series<K, V> GroupBy<K, V>(this ISeries<K, V> source, Func<K, K> keySelector, Func<IEnumerable<KeyValuePair<K, V>>, V> valueSelector) {
            var sm = new SortedMap<K, V>();
            foreach (var gr in (source as IEnumerable<KeyValuePair<K, V>>).GroupBy(kvp => keySelector(kvp.Key))) {
                sm.Add(gr.Key, valueSelector(gr));
            }
            return sm;
        }

        /// <summary>
        /// Projects values from source to destination and back
        /// </summary>
        public static IPersistentOrderedMap<K, VDest> BiMap<K, VSrc, VDest>(this IOrderedMap<K, VSrc> innerMap,
            Func<VSrc, VDest> srcToDest, Func<VDest, VSrc> destToSrc) {
            return new ProjectValuesWrapper<K, VSrc, VDest>(innerMap, srcToDest, destToSrc);
        }
    }
}
