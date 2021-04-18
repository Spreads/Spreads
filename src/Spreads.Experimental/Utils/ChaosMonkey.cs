// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Threading;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Spreads.Utils
{
    public class ChaosMonkeyException : Exception
    {
    }

    /// <summary>
    /// When enabled via <see cref="Settings.EnableChaosMonkey"/>,
    /// calling the methods will raise an error with a given probability
    /// </summary>
    public static class ChaosMonkey
    {
        public static readonly bool Enabled = Settings.EnableChaosMonkey;

        [ThreadStatic]
        private static Random _rng;

        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<string, object> _traceData = new Dictionary<string, object>();

        private static bool _force;
        private static int _scenario;

        /// <summary>
        /// Forces *once* any exception regardless of probability.
        /// </summary>
        public static bool Force
        {
            get { return _force; }
            set { Volatile.Write(ref _force, value); }
        }

        public static int Scenario
        {
            get { return _scenario; }
            set { Volatile.Write(ref _scenario, value); }
        }

        public static Dictionary<string, object> TraceData => _traceData;

        public static void SetTraceData(string key, object value)
        {
            if (Enabled)
            {
                _traceData[key] = value;
            }
        }

        public static void OutOfMemory(double probability = 0.0)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
                throw new OutOfMemoryException();
            }
        }

        public static void StackOverFlow(double probability = 0.0)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
#if NET451
                throw new StackOverflowException();
#else
                throw new ChaosMonkeyException();
#endif
            }
        }

        public static void Exception(double probability = 0.0, int scenario = 0, Exception exception = null)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (scenario > 0 && scenario != Scenario) return;
                if (scenario > 0 && scenario == Scenario) Scenario = 0;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
                throw exception ?? new ChaosMonkeyException();
            }
        }

        public static void FailFast(double probability = 0.0, int scenario = 0)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (scenario > 0 && scenario != Scenario) return;
                if (scenario > 0 && scenario == Scenario) Scenario = 0;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
                Environment.FailFast("Chaos monkey FTW!");
            }
        }

        public static void ThreadAbort(double probability = 0.0)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
#if NET451
            Thread.CurrentThread.Abort();
#else
                throw new ChaosMonkeyException();
#endif
            }
        }

        public static void ThreadSleep(double probability = 0.0, int sleepMilliseconds = 50)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;
                Thread.Sleep(sleepMilliseconds);
            }
        }

        public static void ThreadSpin(double probability = 0.0, int iterations = 1000)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                if (_rng.NextDouble() > probability) return;
                Force = false;

                Thread.SpinWait(iterations);
            }
        }

        public static void Chaos(double probability = 0.0)
        {
            if (Enabled)
            {
                if (Force)
                {
                    probability = 1.0;
                }

                if (probability == 0.0) return;
                if (_rng == null) _rng = new Random();
                var rn = _rng.NextDouble();
                if (rn > probability) return;
                Force = false;
                if (rn < probability / 3.0)
                {
                    throw new OutOfMemoryException();
                }

                if (rn < probability * 2.0 / 3.0)
                {
#if NET451
            throw new StackOverflowException();
#else
                    throw new ChaosMonkeyException();
#endif
                }
#if NET451
            Thread.CurrentThread.Abort();
#else
                throw new ChaosMonkeyException();
#endif
            }
        }
    }
}
