using System;
using Spreads.Extensions.Tests;

namespace Spreads.Core.Tests {
    class Program {
        static void Main(string[] args)
        {
            //new PersistentMapTests().CouldCRUDDirectDict();
            new DataRepositoryTests().CouldCreateTwoRepositoriesAndSynchronizeSeries();
            Console.ReadLine();
        }
    }
}
