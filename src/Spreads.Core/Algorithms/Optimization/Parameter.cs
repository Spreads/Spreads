// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Algorithms.Optimization
{
    // [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 64)]
    public class Parameter : IEnumerable<double>, IEnumerator<double>
    {
        //[FieldOffset(0)]
        private readonly string _code;

        // [FieldOffset(8)]
        private readonly string _description;

        //[FieldOffset(16)]
        private readonly double _defaultValue;

        //[FieldOffset(24)]
        private readonly double _startValue;

        //[FieldOffset(32)]
        private readonly double _endValue;

        //[FieldOffset(40)]
        private readonly double _stepSize;

        //[FieldOffset(48)]
        private readonly int _steps;

        //[FieldOffset(52)]
        private readonly int _bigStepMultiple;

        //[FieldOffset(56)]
        private int _currentPosition;

        //[FieldOffset(60)]
        private int _offset;

        public Parameter(string code, string description, double defaultValue, double startValue, double endValue, double stepSize = 0, int bigStepMultiple = 1)
        {
            //
            if (endValue < startValue && stepSize > 0) { throw new ArgumentException("endValue <= startValue while step > 0"); }
            if (endValue > startValue && stepSize < 0) { throw new ArgumentException("endValue >= startValue while step < 0"); }
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (stepSize == 0)
            {
                Trace.TraceWarning("Step size is zero, assuming differefe between start and end");
                stepSize = endValue - startValue;
            }
            _code = code.Trim();
            _description = description;
            _defaultValue = defaultValue;
            _startValue = startValue;
            _endValue = endValue;
            _stepSize = stepSize;
            _steps = 1 + (int)Math.Ceiling((_endValue - _startValue) / _stepSize);
            if (bigStepMultiple < 1) { throw new ArgumentOutOfRangeException(nameof(bigStepMultiple)); }
            _bigStepMultiple = bigStepMultiple;
            _currentPosition = -1;
            _offset = 0;
        }

        public Parameter(string code, double startValue, double endValue, double stepSize = 0, int bigStepMultiple = 1)
            : this(code, null, (startValue + endValue) / 2.0, startValue, endValue, stepSize, bigStepMultiple) { }

        public string Code => _code;
        public string Description => _description ?? string.Empty;
        public double DefaultValue => _defaultValue;
        public double StartValue => _startValue;
        public double EndValue => _endValue;
        public double StepSize => _stepSize;

        public int Steps
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _steps; }
        }

        public int BigStepMultiple => _bigStepMultiple;

        public int CurrentPosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _currentPosition; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value < 0 || value >= _steps) throw new ArgumentOutOfRangeException(nameof(value));
                _currentPosition = value;
            }
        }

        public int GridPosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _offset + (_currentPosition == -1 ? 0 : _currentPosition); }
        }

        public double this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index < 0) throw new ArgumentException("Parameter index is negative");
                if (index >= _steps) throw new ArgumentException("Parameter index is greater than number of steps");
                return index == (_steps - 1) ? _endValue : _startValue + index * _stepSize;
            }
        }

        /// <summary>
        /// Epsilon number of step around the position
        /// </summary>
        /// <param name="position"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public Parameter GetRegion(int position, int epsilon)
        {
            var offset = Math.Max(position - epsilon, 0);
            var start = this[offset];
            var end = this[Math.Min(position + epsilon, _steps - 1)];
            var newParameter = new Parameter(_code, start, end, _stepSize, _bigStepMultiple)
            {
                _offset = this._offset + offset
            };
            return newParameter;
        }

        public Parameter WithBigStep()
        {
            var newParameter = new Parameter(_code, _startValue, _endValue, _stepSize * _bigStepMultiple, 1)
            {
                _offset = this._offset
            };
            return newParameter;
        }

        public void Dispose()
        {
        }

        public double Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(_currentPosition < _steps, "Wrong _current position");
                Debug.Assert(_currentPosition >= -1, "Wrong _current position");
                if (_currentPosition == -1)
                {
                    return _defaultValue;
                }
                return _currentPosition == (_steps - 1) ? _endValue : _startValue + _currentPosition * _stepSize;
            }
        }

        object IEnumerator.Current => this.Current;

        public Parameter GetEnumerator()
        {
            var copy = this;
            copy._currentPosition = -1;
            return copy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var nextPosition = _currentPosition + 1;
            Debug.Assert(nextPosition <= _steps);
            if (nextPosition == _steps) { return false; }
            _currentPosition = nextPosition;
            return true;
        }

        public bool BigMoveNext()
        {
            Debug.Assert(_currentPosition + 1 <= _steps);
            if (_currentPosition + 1 == _steps) { return false; }
            if (_currentPosition == -1)
            {
                _currentPosition++;
                return true;
            }
            var nextPosition = _currentPosition + _bigStepMultiple;
            if (nextPosition >= _steps)
            {
                _currentPosition = _steps - 1;
                return true;
            }
            _currentPosition = nextPosition;
            return true;
        }

        public void Reset()
        {
            _currentPosition = -1;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<double> IEnumerable<double>.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public static class ParameterExtensions
    {
        // useful to as a key to memoize target function result at a point, instead of int[]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LinearAddress(this Parameters parameters)
        {
            return parameters.RefList.LinearAddress();
        }

        // useful to as a key to memoize target function result at a point, instead of int[]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LinearAddress(this RefList<Parameter> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            var address = -1L;
            if (parameters.Count == 0) return address;
            address = parameters[0].CurrentPosition;
            if (address == -1) throw new InvalidOperationException("Cannot get address of not started parameter");
            // previous * current dim + current addr
            // TODO test + review
            for (int i = 1; i < parameters.Count; i++)
            {
                if (parameters[i].CurrentPosition == -1) throw new InvalidOperationException("Cannot get address of not started parameter");
                address = address * parameters[i].Steps + parameters[i].CurrentPosition;
            }
            return address;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Parameters SetPositionsFromLinearAddress(this Parameters parameters, long linearAddress)
        {
            var newParameters = SetPositionsFromLinearAddress(parameters.RefList, linearAddress);
            return new Parameters(newParameters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Parameter[] SetPositionsFromLinearAddress(this RefList<Parameter> parameters, long linearAddress)
        {
            var newParameters = parameters.ToArray();
            for (int i = parameters.Count - 1; i >= 1; i--)
            {
                var steps = parameters[i].Steps;
                var tmp = linearAddress / steps;
                var iPos = checked((int)(linearAddress - tmp * steps));
                newParameters[i].CurrentPosition = iPos;
                linearAddress = tmp;
            }
            newParameters[0].CurrentPosition = checked((int)linearAddress);
            return newParameters;
        }
    }
}