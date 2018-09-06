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
            var test = new Spreads.Core.Tests.Serialization.TimeStampSerializationTests();
            
            test.TestTimestampBranchlessRead();

            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }
    }
}