using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.Collections.Generic;

namespace Spreads.Experimental.Optimization
{
    public class Parameters : DynamicObject, IReadOnlyList<Parameter>
    {
        private readonly RefList<Parameter> _parameters = new RefList<Parameter>();
        private int _lastLookupIndex = 0;

        public Parameters(params Parameter[] parameters)
        {
            if (parameters.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Length)
            {
                throw new ArgumentException("Parameter codes are not unique");
            }
            _parameters.AddRange(parameters);
        }

        public Parameters(IEnumerable<Parameter> parameters)
        {
            if (parameters.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.Count())
            {
                throw new ArgumentException("Parameter codes are not unique");
            }
            _parameters.AddRange(parameters);
        }

        internal RefList<Parameter> RefList => _parameters;

        public int Count => _parameters.Count;

        public void Add(Parameter parameter)
        {
            if (_parameters.Any(p => p.Code == parameter.Code))
            {
                throw new ArgumentException($"Parameter with the code {parameter.Code} is alreadyin the collection.");
            }
            _parameters.Add(parameter);
        }

        public double this[string code]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // NB simple (almost silly) optimization for linear search:
                // if we lookup the same code or sequentially lookup codes
                // in the same order they were added then we will hit the right parameter
                // instantly or in two comparisons
                var idx = _lastLookupIndex;
                var total = _parameters.Count;
                do // round trip
                {
                    ref var par = ref _parameters[idx];
                    if (par.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastLookupIndex = idx;
                        return par.Current;
                    }
                    idx = (idx + 1) % total;
                } while (idx != _lastLookupIndex);

                var trimmed = code.Trim();
                if (trimmed != code)
                {
                    var result = this[trimmed];
                    Trace.TraceWarning($"Parameter {code} has leading or trailing spaces, check for typos");
                    return result;
                }
                else
                {
                    throw new KeyNotFoundException($"Unknown parameter: {code}");
                }
            }
        }

        public Parameter this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index < 0 || index >= _parameters.Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return _parameters[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (index < 0 || index >= _parameters.Count)
                {
                    throw new IndexOutOfRangeException();
                }
                _parameters[index] = value;
            }
        }

        public int TotalInterations => _parameters.Select(x => x.Steps).Aggregate(1, (i, st) => checked(i * st));

        public Parameters GetRegion(int epsilon)
        {
            var clone = this.Clone();
            for (int i = 0; i < _parameters.Count; i++)
            {
                clone._parameters[i] = _parameters[i].GetRegion(_parameters[i].CurrentPosition, epsilon);
            }
            return clone;
        }

        public Parameters Clone()
        {
            return new Parameters(_parameters.ToArray());
        }

        public Parameters Reset()
        {
            foreach (var p in _parameters)
            {
                p.Reset();
            }
            return this;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var name = binder.Name;
            foreach (var par in _parameters)
            {
                if (par.Code.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    result = par.Current;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public IEnumerator<Parameter> GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }
    }
}
