using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Collections.Tests {
    public static class Profiler {
        public static void Main() {
            new ZipNTests().ContinuousZipIsCorrectByRandomCheck();
            //var test = new MoveNextAsyncTests();
            //test.CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();
            Console.ReadLine();
        }
    }
}
