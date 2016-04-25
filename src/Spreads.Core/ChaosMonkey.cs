using System;
using System.Diagnostics;
using System.Threading;

namespace Spreads {
    /// <summary>
    /// When CHAOS_MONKEY conditional-compilation directive is set,
    /// calling the methods will raise an error with a given probability
    /// </summary>
    public static class ChaosMonkey {

        [ThreadStatic]
        private static Random _rng;

        [Conditional("CHAOS_MONKEY")]
        public static void OutOfMemory(double probability = 0.1) {
            if (probability == 0.0) return;
            if (_rng == null) _rng = new Random();
            if (_rng.NextDouble() > probability) return;
            throw new OutOfMemoryException();
            //var list = new List<List<long>>();
            //try {
            //    while (true) {
            //        list.Add(new List<long>(int.MaxValue));
            //    }
            //} catch (OutOfMemoryException ooex) {
            //    throw;
            //}
        }

        [Conditional("CHAOS_MONKEY")]
        public static void StackOverFlow(double probability = 0.1) {
            if (probability == 0.0) return;
            if (_rng == null) _rng = new Random();
            if (_rng.NextDouble() > probability) return;
            throw new StackOverflowException();
        }

        [Conditional("CHAOS_MONKEY")]
        public static void ThreadAbort(double probability = 0.1) {
            if (probability == 0.0) return;
            if (_rng == null) _rng = new Random();
            if (_rng.NextDouble() > probability) return;
            Thread.CurrentThread.Abort();
        }

        [Conditional("CHAOS_MONKEY")]
        public static void Slowpoke(double probability = 0.1) {
            if (probability == 0.0) return;
            if (_rng == null) _rng = new Random();
            if (_rng.NextDouble() > probability) return;
            Thread.Sleep(50);
        }

        [Conditional("CHAOS_MONKEY")]
        public static void Chaos(double probability = 0.1) {
            if (probability == 0.0) return;
            if (_rng == null) _rng = new Random();
            var rn = _rng.NextDouble();
            if (rn > probability) return;
            if (rn < probability / 3.0) {
                throw new OutOfMemoryException();
            }
            if (rn < probability * 2.0 / 3.0) {
                throw new StackOverflowException();
            }
            Thread.CurrentThread.Abort();
        }
    }
}
