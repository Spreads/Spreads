using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Spreads;
using Spreads.Collections;
using Spreads.Collections.Tests.Cursors;
using Spreads.Extensions.Tests.Storage;
using Spreads.Extensions.Tests.Storage.SQLite;



namespace Spreads.Tests.CSProfile {
    class Program {
        static void Main(string[] args)
        {
            new MoveNextAsyncTests().CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursorManyTimes();
            //var bs = Bootstrap.Bootstrapper.Instance;
            //new StorageTests().CouldCRUDSeriesStorage();
            Console.ReadLine();
            //Console.WriteLine(bs.AppFolder);
        }
    }
}
