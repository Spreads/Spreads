using Spreads.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    //public struct SMAState
    //{
    //    public double sum;
    //    public int count;
    //}

    internal class SMACursor<K> : BindCursor<K, double, double, double> {
        protected ICursor<K, double> _laggedCursor;
        protected double _sum = 0.0;
        protected int _count = 0;
        private readonly Func<ICursor<K, double>> _cursorFactory;
        protected int _period;
        private readonly bool _allowIncomplete;

        public SMACursor(Func<ICursor<K, double>> cursorFactory, int period, bool allowIncomplete = false)
            : base(cursorFactory, x => x) {
            _cursorFactory = cursorFactory;
            _period = period;
            _allowIncomplete = allowIncomplete;
            
        }

        public override bool TryCreateState(K key, out double state) {
            state = 0.0;

            _sum = 0.0;
            _count = 0;

            if (_laggedCursor == null) {
                _laggedCursor = this.InputCursor.Clone();
            }
            if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
                _sum += _laggedCursor.CurrentValue;
                _count++;

                while (_count < _period && _laggedCursor.MovePrevious()) {
                    _sum += _laggedCursor.CurrentValue;
                    _count++;
                }
                if (_count == _period)
                {
                    state = _sum/_count;
                    return true;
                } else if (_allowIncomplete) {
                    state = _sum / _count;
                    _laggedCursor.MoveFirst(); // it was in reset state because tried to move before the first key
                    return true;
                } else {
                    return false;
                }
            } else {
                return false;
            }
        }


        //public override double EvaluateState(double state) {
        //    return state;
        //}


        public override bool TryGetValue(K key, out double value) {
            using (var tmpcursor = this.InputCursor.Clone()) {
                var c = 0;
                var sum = 0.0;
                sum += tmpcursor.CurrentValue;
                c++;
                while (c < _period && tmpcursor.MovePrevious()) {
                    sum += tmpcursor.CurrentValue;
                    c++;
                }
                if (c == _period || _allowIncomplete) {
                    value = sum / (double)c;
                    return true;
                } else
                {
                    value = 0.0;
                    return false;
                }
            }
        }
        

        public override bool TryUpdateStateNext(KeyValuePair<K, double> next, ref double value) {
            if (_count >= _period) {
                Trace.Assert(_count == _period, "_count should never be above _period");
                _sum += next.Value - _laggedCursor.CurrentValue;
                var laggedMoved = _laggedCursor.MoveNext();
                if (laggedMoved) {
                    value = _sum / (double)_count;
                    return true;
                } else {
                    throw new ApplicationException("Lagged should always move here");
                }
            } else {
                _sum += next.Value;
                _count++;
                // do not move lagged until
                value = _sum / (double)_count;
                return true;
            }
        }

        //public override bool TryUpdateStatePrevious(KeyValuePair<K, double> next, ref double value) {
        //    throw new NotImplementedException("TODO! implement this");
        //}

        public override ICursor<K, double> Clone() {
            var clone = new SimpleMovingAverageCursor<K>(_cursorFactory, _period, _allowIncomplete);
            if (base.HasValidState) {
                clone.MoveAt(base.CurrentKey, Lookup.EQ);
            }
            return clone;
        }

        public override Series<K, V3> Map<V3>(Func<double, V3> f2) {
            throw new NotImplementedException();
        }
    }



    public class SimpleMovingAverageCursor<K> : CursorBind<K, double, double> {
        protected ICursor<K, double> _laggedCursor;
        protected double _sum;
        protected int _count = 0;
        private readonly Func<ICursor<K, double>> _cursorFactory;
        protected int _period;
        private readonly bool _allowIncomplete;

        public SimpleMovingAverageCursor(Func<ICursor<K, double>> cursorFactory, int period, bool allowIncomplete = false)
            : base(cursorFactory) {
            _cursorFactory = cursorFactory;
            _period = period;
            _allowIncomplete = allowIncomplete;
        }

        public override bool TryGetValue(K key, bool isPositioned, out double value) {
            value = 0.0;

            if (isPositioned) // we have moved at the key
            {
                if (_laggedCursor == null) {
                    _laggedCursor = this.InputCursor.Clone();
                }
                _sum = 0.0;
                _count = 0;
                if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
                    _sum += _laggedCursor.CurrentValue;
                    _count++;

                    while (_count < _period && _laggedCursor.MovePrevious()) {
                        _sum += _laggedCursor.CurrentValue;
                        _count++;
                    }
                    if (_count == _period) {
                        value = _sum / (double)_count;
                        return true;
                    } else if (_allowIncomplete) {
                        _laggedCursor.MoveFirst(); // it was in reset state because tried to move before the first key
                        value = _sum / (double)_count;
                        return true;
                    } else {
                        return false;
                    }
                } else {
                    return false;
                }
            } else // we are trying to get value without moving a cursor
              {
                using (var tmpcursor = this.InputCursor.Clone()) {
                    var c = 0;
                    var sum = 0.0;
                    sum += tmpcursor.CurrentValue;
                    c++;
                    while (c < _period && tmpcursor.MovePrevious()) {
                        sum += tmpcursor.CurrentValue;
                        c++;
                    }
                    if (c == _period || _allowIncomplete) {
                        value = sum / (double)c;
                        return true;
                    } else {
                        return false;
                    }
                }
            }
        }

        public override bool TryUpdateNext(KeyValuePair<K, double> next, out double value) {
            if (_count >= _period) {
                Trace.Assert(_count == _period, "_count should never be above _period");
                _sum += next.Value - _laggedCursor.CurrentValue;
                var laggedMoved = _laggedCursor.MoveNext();
                if (laggedMoved) {
                    value = _sum / (double)_count;
                    return true;
                } else {
                    throw new ApplicationException("Lagged should always move here");
                }
            } else {
                _sum += next.Value;
                _count++;
                // do not move lagged until
                value = _sum / (double)_count;
                return true;
            }


        }

        public override ICursor<K, double> Clone() {
            var clone = new SimpleMovingAverageCursor<K>(_cursorFactory, _period, _allowIncomplete);
            if (base.HasValidState) {
                clone.MoveAt(base.CurrentKey, Lookup.EQ);
            }
            return clone;
        }
    }



    public class StandardDeviationCursor<K> : CursorBind<K, double, double> {
        protected ICursor<K, double> _laggedCursor;
        private readonly Func<ICursor<K, double>> _cursorFactory;
        protected int _period;
        protected double _sum;
        protected double _sumSq;

        public StandardDeviationCursor(Func<ICursor<K, double>> cursorFactory, int period)
            : base(cursorFactory) {
            _cursorFactory = cursorFactory;
            _period = period;
        }

        public override bool TryGetValue(K key, bool isPositioned, out double value) {
            value = 0.0;
            if (isPositioned) {
                if (_laggedCursor == null) {
                    _laggedCursor = this.InputCursor.Clone();
                }
                var c = 0;
                _sum = 0.0;
                _sumSq = 0.0;
                if (_laggedCursor.MoveAt(key, Lookup.EQ)) {
                    var curValue = _laggedCursor.CurrentValue;
                    _sum += curValue;
                    _sumSq += curValue * curValue;
                    c++;
                    while (c < _period && _laggedCursor.MovePrevious()) {
                        var curValue2 = _laggedCursor.CurrentValue;
                        _sum += curValue2;
                        _sumSq += curValue2 * curValue2;
                        c++;
                    }
                    if (c == _period) {
                        value = Math.Sqrt(_sumSq / ((double)(c - 1)) - _sum * _sum / ((double)c * (double)(c - 1)));
                        return true;
                    } else {
                        return false;
                    }
                }
                return true;
            } else {
                using (var tmpcursor = this.InputCursor.Clone()) {
                    var c = 0;
                    var sum = 0.0;
                    var sumSq = 0.0;
                    sum += tmpcursor.CurrentValue;
                    sumSq += _laggedCursor.CurrentValue * _laggedCursor.CurrentValue;
                    c++;
                    while (c < _period && tmpcursor.MovePrevious()) {
                        sum += tmpcursor.CurrentValue;
                        sumSq += _laggedCursor.CurrentValue * _laggedCursor.CurrentValue;
                        c++;
                    }
                    if (c == _period) {
                        value = Math.Sqrt(_sumSq / ((double)(c - 1)) - _sum * _sum / ((double)c * (double)(c - 1)));
                        return true;
                    } else {
                        return false;
                    }
                }
            }
        }

        public override bool TryUpdateNext(KeyValuePair<K, double> next, out double value) {
            var cv = _laggedCursor.CurrentValue;
            _sum += next.Value - cv;
            _sumSq += next.Value * next.Value - cv * cv;

            var laggedMoved = _laggedCursor.MoveNext();
            if (laggedMoved) {
                var periodMinusOne = (double)(_period - 1);
                value = Math.Sqrt(_sumSq / periodMinusOne - _sum * _sum / ((double)_period * (periodMinusOne)));
                return true;
            } else {
                throw new ApplicationException("Lagged should always move here");
            }
        }

        public override ICursor<K, double> Clone() {
            var clone = new StandardDeviationCursor<K>(_cursorFactory, _period);
            if (base.HasValidState) {
                clone.MoveAt(base.CurrentKey, Lookup.EQ);
            }
            return clone;
        }
    }

    public static class CursorSeriesExtensions {
        public static Series<K, double> SMA<K>(this ISeries<K, double> source, int period, bool allowIncomplete = false) {
            return new CursorSeries<K, double>(() => new SMACursor<K>(source.GetCursor, period, allowIncomplete));
        }

        /// <summary>
        /// Moving standard deviation
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="source"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public static Series<K, double> StDev<K>(this ISeries<K, double> source, int period) {
            return new CursorSeries<K, double>(() => new StandardDeviationCursor<K>(source.GetCursor, period));
        }

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
        public static IPersistentOrderedMap<K, Vdest> Project<K, Vsrc, Vdest>(this IOrderedMap<K, Vsrc> innerMap,
            Func<Vsrc, Vdest> srcToDest, Func<Vdest, Vsrc> destToSrc) {
            return new ProjectValuesWrapper<K, Vsrc, Vdest>(innerMap, srcToDest, destToSrc);
        }
    }
}
