using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Spreads;
using Spreads.Collections;
using Spreads.Extensions.Tests.Storage.SQLite;


public class MyInc {
}


namespace Spreads.Tests.CSProfile {
    class Program {
        static void Main(string[] args) {
            var bs = Bootstrap.Bootstrapper.Instance;
            new SqlitePerformanceTest().InsertSpeed();
            Console.ReadLine();
            Console.WriteLine(bs.AppFolder);
        }
    }
}
