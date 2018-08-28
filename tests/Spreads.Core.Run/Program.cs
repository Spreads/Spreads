using System;
using System.Threading.Tasks;
using Spreads;
using Spreads.Blosc;

namespace Spreads.Core.Run
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            //var test = new Spreads.Core.Tests.Cursors.AsyncCursorTests();
            //for (int i = 0; i < 1000; i++)
            //{
            //    await test.CouldReadDataStreamWhileWritingFromManyThreads();
            //}

            Console.WriteLine("Blosc processors: " + BloscMethods.ProcessorCount);

            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }
    }
}