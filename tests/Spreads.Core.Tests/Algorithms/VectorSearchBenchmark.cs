// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Algorithms;

namespace Spreads.Core.Tests.Algorithms
{
    [TestFixture, Explicit]
    public class VectorSearchBenchmark
    {
        private double[] _array;

        private double[] _powers =
        {
            0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 0.975, 0.99, 1, 1.05, 1.1, 1.25, 1.5, 1.75, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100
        };

        private (TimeSpan IS, TimeSpan BSO, TimeSpan BSC)[] _results;

        [TestCase(1_000)]
        [TestCase(10_000)]
        [TestCase(100_000)]
        [TestCase(1000_000)]
        public void Run(int arraySize)
        {
            _array = new double[arraySize];
            _results = new (TimeSpan IS, TimeSpan BS, TimeSpan BSC)[_powers.Length];

            // rounds, the last results will be used
            for (int r = 0; r < 3; r++)
            {
                for (var i = 0; i < _powers.Length; i++)
                {
                    Prepare(_powers[i]);
                    _results[i].IS = InterpolationSearch();
                    _results[i].BSO = BinarySearchOptimized();
                    _results[i].BSC = BinarySearchClassic();
                }
            }

            Console.WriteLine("Power;IS;BS_Opt;BS_Clas;Ratio IS/BS_Opt");

            for (int i = 0; i < _results.Length; i++)
            {
                var power = _powers[i];
                var (IS, BSO, BSC) = _results[i];
                Console.WriteLine($"{power:N2};" +
                                  $"{(IS.Ticks * 100 / _array.Length):N0};" +
                                  $"{(BSO.Ticks * 100 / _array.Length):N0};" +
                                  $"{(BSC.Ticks * 100 / _array.Length):N0};" +
                                  $"{IS.TotalMilliseconds / BSO.TotalMilliseconds:N2}");
            }
        }

        private void Prepare(double power)
        {
            for (int i = 0; i < _array.Length; i++)
            {
                double t = i / (double)(_array.Length);
                _array[i] = _array.Length * Math.Pow(t, power);
            }

            for (int i = 1; i < _array.Length; i++)
            {
                if (_array[i - 1] > _array[i])
                {
                    // double t = i / (double)(_array.Length);
                    // _array[i] = _array.Length * Math.Pow(t, power);
                    Assert.Fail($"{i}: {_array[i - 1]} >= {_array[i]}");
                }
            }
        }

        private int Rounds => Math.Min(10, Math.Max(3, 500_000 / _array.Length));
        private readonly TimeSpan[] _tempResults = new TimeSpan[10];

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TimeSpan InterpolationSearch()
        {
            for (int r = 0; r < Rounds; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < _array.Length; i++)
                {
                    VectorSearch.SortedSearch(ref _array[0], _array.Length, i);
                }

                _tempResults[r] = sw.Elapsed;
            }

            return _tempResults.Take(Rounds).Min();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TimeSpan BinarySearchOptimized()
        {
            for (int r = 0; r < Rounds; r++)
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < _array.Length; i++)
                {
                    VectorSearch.BinarySearch(ref _array[0], _array.Length, i);
                }

                _tempResults[r] = sw.Elapsed;
            }

            return _tempResults.Take(Rounds).Min();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TimeSpan BinarySearchClassic()
        {
            for (int r = 0; r < Rounds; r++)
            {
                var sw = Stopwatch.StartNew();
                Span<double> asSpan = _array.AsSpan();
                for (int i = 0; i < asSpan.Length; i++)
                {
                    VectorSearch.BinarySearchClassic(ref _array[0], _array.Length, i);
                }

                _tempResults[r] = sw.Elapsed;
            }

            return _tempResults.Take(Rounds).Min();
        }
    }
}
