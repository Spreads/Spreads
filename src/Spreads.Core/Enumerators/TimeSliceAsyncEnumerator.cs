using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Enumerators
{
    // NB it is incorrect to aggregate values based on end of period:
    // first, we lose points at the beginning or have a weird point, e.g. 10:00:00 that has only values exactly at that moment.
    // second, all periods technically start at zero and end one tick before the next period, e.g. midnight point belongs to the following day,
    //    which ends at 23:59:59.9999999...
    // third, we could easily get 'correct' aggregates with end-of-period (without missing values) by just applying `lag(1)`

    // TODO use PeriodTick

    internal struct TimeSliceAsyncEnumerable<TValue, TAggr> : IAsyncEnumerable<KeyValuePair<DateTime, TAggr>>
    {
        private readonly IEnumerable<KeyValuePair<DateTime, TValue>> _series;
        private readonly Func<TValue, TAggr> _initState;
        private readonly Func<TAggr, TValue, TAggr> _aggregator;
        private readonly UnitPeriod _unitPeriod;
        private readonly int _periodLength;
        private readonly int _offset;

        public TimeSliceAsyncEnumerable(IEnumerable<KeyValuePair<DateTime, TValue>> series, Func<TValue, TAggr> initState,
            Func<TAggr, TValue, TAggr> aggregator, UnitPeriod unitPeriod, int periodLength = 1, int offset = 0)
        {
            if (periodLength <= 0) throw new ArgumentOutOfRangeException(nameof(periodLength));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset >= periodLength) throw new ArgumentException("Offset must be smaller that period length");
            _series = series;
            _initState = initState;
            _aggregator = aggregator;
            _unitPeriod = unitPeriod;
            _periodLength = periodLength;
            _offset = offset;
        }

        public TimeSliceAsyncEnumerator GetEnumerator()
        {
            return new TimeSliceAsyncEnumerator(this);
        }

        IAsyncEnumerator<KeyValuePair<DateTime, TAggr>> IAsyncEnumerable<KeyValuePair<DateTime, TAggr>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<KeyValuePair<DateTime, TAggr>> IEnumerable<KeyValuePair<DateTime, TAggr>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal enum TimeSlicePosition : byte
        {
            NotStarted = 0,
            Aggregating = 1,
            PassedToNext = 2,
            FinishedSync = 3,
            FinishedAsync = 4
        }

        internal struct TimeSliceAsyncEnumerator : IAsyncEnumerator<KeyValuePair<DateTime, TAggr>>
        {
            private readonly TimeSliceAsyncEnumerable<TValue, TAggr> _source;
            private readonly IEnumerator<KeyValuePair<DateTime, TValue>> _enumerator;
            private KeyValuePair<DateTime, TAggr> _current;
            private TimeSlicePosition _position;

            public TimeSliceAsyncEnumerator(TimeSliceAsyncEnumerable<TValue, TAggr> source)
            {
                _source = source;
                _enumerator = _source._series.GetEnumerator();
                _current = default(KeyValuePair<DateTime, TAggr>);
                _position = TimeSlicePosition.NotStarted;
            }

            private DateTime StartOfSlice(DateTime tick)
            {
                long ticksPerPeriod;
                switch (_source._unitPeriod)
                {
                    case UnitPeriod.Tick:
                    return tick;

                    case UnitPeriod.Millisecond:
                    ticksPerPeriod = TimeSpan.TicksPerMillisecond;
                    break;

                    case UnitPeriod.Second:
                    ticksPerPeriod = TimeSpan.TicksPerSecond;
                    break;

                    case UnitPeriod.Minute:
                    ticksPerPeriod = TimeSpan.TicksPerMinute;
                    break;

                    case UnitPeriod.Hour:
                    ticksPerPeriod = TimeSpan.TicksPerHour;
                    break;

                    case UnitPeriod.Day:
                    ticksPerPeriod = TimeSpan.TicksPerDay;
                    break;

                    case UnitPeriod.Month:
                    throw new NotImplementedException();
                    case UnitPeriod.Eternity:
                    throw new NotImplementedException();
                    default:
                    throw new ArgumentOutOfRangeException();
                }
                var ticksPerSlice = ticksPerPeriod * _source._periodLength;
                var ticks = ((tick.Ticks / (ticksPerSlice)) + _source._offset) * ticksPerSlice;
                return new DateTime(ticks, tick.Kind);
            }

            public void Dispose()
            {
                _enumerator.Dispose();
            }

            public bool MoveNext()
            {
                switch (_position)
                {
                    case TimeSlicePosition.NotStarted:
                    if (_enumerator.MoveNext())
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        _current = new KeyValuePair<DateTime, TAggr>(slice, _source._initState(_enumerator.Current.Value));
                        _position = TimeSlicePosition.Aggregating;
                        goto case TimeSlicePosition.Aggregating;
                    }
                    _position = TimeSlicePosition.FinishedSync;
                    return false;

                    case TimeSlicePosition.Aggregating:
                    var prev = _current;
                    while (_enumerator.MoveNext())
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        if (slice == prev.Key)
                        {
                            _current = new KeyValuePair<DateTime, TAggr>(slice, _source._aggregator(prev.Value, _enumerator.Current.Value));
                            prev = _current;
                        }
                        else
                        {
                            _position = TimeSlicePosition.PassedToNext;
                            return true;
                        }
                    }
                    _position = TimeSlicePosition.FinishedSync;
                    // at least one move was OK, need to return true
                    return true;

                    case TimeSlicePosition.PassedToNext:
                    // here we have one unused value at the current position
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        _current = new KeyValuePair<DateTime, TAggr>(slice, _source._initState(_enumerator.Current.Value));
                        _position = TimeSlicePosition.Aggregating;
                        goto case TimeSlicePosition.Aggregating;
                    }

                    case TimeSlicePosition.FinishedSync:
                    return false;

                    default:
                    throw new ArgumentOutOfRangeException();
                }
            }

            public void Reset()
            {
                _enumerator.Reset();
            }

            object IEnumerator.Current => Current;

            public KeyValuePair<DateTime, TAggr> Current => _current;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                var e = _enumerator as IAsyncEnumerator<KeyValuePair<DateTime, TValue>>;
                if (e == null) return false;
                switch (_position)
                {
                    case TimeSlicePosition.NotStarted:
                    if (await e.MoveNext(cancellationToken))
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        _current = new KeyValuePair<DateTime, TAggr>(slice, _source._initState(_enumerator.Current.Value));
                        _position = TimeSlicePosition.Aggregating;
                        goto case TimeSlicePosition.Aggregating;
                    }
                    return false;

                    case TimeSlicePosition.FinishedSync:
                    case TimeSlicePosition.Aggregating:
                    var prev = _current;
                    while (await e.MoveNext(cancellationToken))
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        if (slice == prev.Key)
                        {
                            _current = new KeyValuePair<DateTime, TAggr>(slice, _source._aggregator(prev.Value, _enumerator.Current.Value));
                            prev = _current;
                        }
                        else
                        {
                            _position = TimeSlicePosition.PassedToNext;
                            return true;
                        }
                    }
                    _position = TimeSlicePosition.FinishedAsync;
                    return true;

                    case TimeSlicePosition.PassedToNext:
                    // here we have one unused value at the current position
                    {
                        var slice = StartOfSlice(_enumerator.Current.Key);
                        _current = new KeyValuePair<DateTime, TAggr>(slice, _source._initState(_enumerator.Current.Value));
                        _position = TimeSlicePosition.Aggregating;
                        goto case TimeSlicePosition.Aggregating;
                    }

                    case TimeSlicePosition.FinishedAsync:
                    return false;

                    default:
                    throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}