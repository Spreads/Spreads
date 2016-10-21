using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using System.Runtime.InteropServices;


namespace Spreads.Core.Tests {

    public class IdClass<T>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public virtual T GetNoInline(T t)
        {
            return t;
        }
    }

    [TestFixture]
    public class FunctionCombinationTests {




        [Test]
        [Ignore]
        public void IdentityFunctionTests() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 2000000000; i++) {
                sum += i;
            }
            sw.Stop();
            Console.WriteLine($"Elapsed direct {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {2000000.0 / sw.ElapsedMilliseconds * 1.0}");

            var idClass = new IdClass<int>();
            sw = new Stopwatch();
            sw.Start();
            sum = 0;
            for (var i = 0; i < 2000000000; i++) {
                sum += idClass.GetNoInline(i);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed Class Virt {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {2000000.0 / sw.ElapsedMilliseconds * 1.0}");

            sw = new Stopwatch();
            sw.Start();
            sum = 0;
            Func<int, int> id = x => x;
            for (var i = 0; i < 2000000000; i++) {
                sum += id(i);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed ID {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {2000000.0/sw.ElapsedMilliseconds*1.0}");

            sw = new Stopwatch();
            sw.Start();
            sum = 0;
            var f = CoreUtils.IdentityFunction<int>.Instance;
            for (var i = 0; i < 2000000000; i++) {
                sum += f(i);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed IdentityFunction {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {2000000.0 / sw.ElapsedMilliseconds * 1.0}");

            


        }

        

    }
}
