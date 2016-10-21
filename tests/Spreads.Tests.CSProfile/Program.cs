// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Spreads;
using Spreads.Collections;
using Spreads.Extensions.Tests;
using Spreads.Extensions.Tests.Storage;



namespace Spreads.Tests.CSProfile {
    class Program {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            //new DataRepositoryTests().CouldCreateTwoRepositoriesAndSynchronizeSeries();
            //new MoveNextAsyncTests().CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursorManyTimes();
            //var bs = Bootstrap.Bootstrapper.Instance;
            //new StorageTests().CouldCRUDSeriesStorage();
            Console.ReadLine();
            //Console.WriteLine(bs.AppFolder);
        }
    }
}
