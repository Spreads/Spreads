// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using Spreads.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Generation
{
    public class SortedMapGenerator<V>
    {
        private readonly long _periodTicks;
        private readonly Func<int, V, V> _valueGenerator;
        private Stopwatch sw = new Stopwatch();
        private CancellationTokenSource Cts;
        private Task runner;

        /// <summary>
        /// Generate T every number of ticks with counter
        /// </summary>
        /// <param name="periodTicks"></param>
        /// <param name="valueGenerator"></param>
        public SortedMapGenerator(long periodTicks, Func<int, V, V> valueGenerator)
        {
            _periodTicks = periodTicks;
            _valueGenerator = valueGenerator;
        }

        /// <summary>
        /// Returns a sorted map that is being filled by new values
        /// </summary>
        /// <param name="durationMsecs"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        public SortedMap<DateTime, V> Generate(int durationMsecs = 0, SortedMap<DateTime, V> existing = null, CancellationTokenSource cts = null)
        {
            var spin = new SpinWait();
            var sm = existing ?? new SortedMap<DateTime, V>();
            Cts = cts ?? new CancellationTokenSource();
            var c = 0;
            var previous = sm.Last.Present.Value; // if missing then default OK
            if (durationMsecs > 0)
            {
                Cts.CancelAfter(durationMsecs);
            }
            var startDt = DateTime.UtcNow;

            sw.Start();

            runner = Task.Run(() =>
            {
                while (!Cts.IsCancellationRequested)
                {
                    while (sw.Elapsed.Ticks < _periodTicks + 1)
                    {
                        // spin
                        //Console.WriteLine("Spin");
                    }
                    // do work
                    var next = _valueGenerator(c, previous);
                    sm.TryAdd(startDt.AddTicks(c * _periodTicks + sw.ElapsedTicks), next);
                    previous = next;
                    c++;
                    sw.Restart();
                }
            }, Cts.Token);

            return sm;
        }

        public void Cancel()
        {
            Cts.Cancel();
        }
    }
}