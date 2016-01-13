using Spreads.Collections.Tests.Cursors;
using System;

namespace Spreads.Collections.Tests {
    /// <summary>
    /// Change the project type to a console app and run a profiler with Alt+F2 in VS
    /// </summary>
    public static class Profiler {
        public static void Main() {
            //new ZipNTests().ContinuousZipIsCorrectByRandomCheck();
            //var test = new MoveNextAsyncTests();
            //test.CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();


            (new BatchMapValuesCursorTests()).CouldAddWithSimdMathProvider();
            ////////////////////////////////////////
            Console.WriteLine("Finished profiling");
            Console.ReadLine();
        }
    }
}
