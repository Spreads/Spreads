using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Spreads.Algorithms.Hash;

namespace Spreads.Algorithms {
    public static class Algo {
        public static MathProvider Math = MathProvider.Instance;
        public class MathProvider {
            internal static MathProvider Instance = new MathProvider();
            private MathProvider() { }
        }


        public static HashProvider Hash = HashProvider.Instance;
        public class HashProvider {
            internal static HashProvider Instance = new HashProvider();
            private HashProvider() { }
        }
    }


    // we could use extension methods to extend Algo.Math, it is quite convenient at first glance
    // extension methods are normal static methods and could be inlined by JIT
    // 
    internal static class SimpleMath {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddTwoInts(this Algo.MathProvider provider, int first, int second) {
            return first + second;
        }

        public static void TestMe() {
            System.Math.Abs(-1);
            Algo.Math.AddTwoInts(42, 3);

        }
    }
}
