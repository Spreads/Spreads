// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Algorithms.Optimization;

namespace Spreads.Core.Tests.Algorithms
{
    [Category("CI")]
    [TestFixture]
    public class OptimizationTests
    {
        public struct EvalAddress
        {
            public long LinearAddress;
            public double Value;

            public static EvalAddress Worst = new EvalAddress() { Value = double.MinValue };
        }

        [Test]
        public async Task CouldFindMaximum()
        {
            var sw = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);
            sw.Restart();
            var par0 = new Parameter("par0", 0, 100, 1);
            var par1 = new Parameter("par1", 0, 100, 1);
            var par2 = new Parameter("par2", 0, 100, 1);

            var pars = new Parameters(new[] { par0, par1, par2 });

            var total = pars.TotalInterations;
            Console.WriteLine($"Total iterations: {total}");

            Func<Parameters, ValueTask<double>> targetFunc = (args) =>
            {
                //return Task.Run(() => {
                //    //await Task.Delay(0);
                //    var result = -(Math.Pow(200 - args[0].Current, 2) + Math.Pow(10 - args[1].Current, 2) +
                //          Math.Pow(70 - args[2].Current, 2));
                //    return result;
                //});

                var result = -(Math.Pow(20 - args[0].Current, 2) + Math.Pow(10 - args[1].Current, 2) +
                               Math.Pow(70 - args[2].Current, 2));
                return new ValueTask<double>(result);
            };

            var maximizer = new GridOptimizer<double>(pars, targetFunc);

            EvalFolderFunc<double, EvalAddress> folder = FolderFunc;

            // NB Func<> cannot have byref params
            //Func<EvalAddress, GridOptimizer.EvalParametersResult, EvalAddress> folder = (state, item) => {
            //    if (item.Value > state.Value) {
            //        return new EvalAddress() { Value = item.Value, LinearAddress = item.Parameters.LinearAddress() };
            //    }
            //    return state;
            //};

            var optimum = await maximizer.FoldGrid(EvalAddress.Worst, folder);

            var optParams = pars.SetPositionsFromLinearAddress(optimum.LinearAddress);

            var endMemory = GC.GetTotalMemory(false);
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Memory: {endMemory - startMemory}");
            Console.WriteLine($"Optimum: {optParams[0].Current} - {optParams[1].Current} - {optParams[2].Current}");
        }

        private EvalAddress FolderFunc(EvalAddress state, ref EvalParametersResult<double> item)
        {
            if (item.Value > state.Value)
            {
                return new EvalAddress() { Value = item.Value, LinearAddress = item.Parameters.LinearAddress() };
            }
            return state;
        }

        /// <summary>
        /// This test should load CPU significantly above one core
        /// </summary>
        [Test, Explicit("long running")]
        public async Task CouldFindMaximumWithSpinWait()
        {
            var par0 = new Parameter("par0", 0, 10, 1);
            var par1 = new Parameter("par1", 0, 10, 1);
            var par2 = new Parameter("par2", 0, 10, 1);

            var pars = new Parameters(new[] { par0, par1, par2 });

            var total = pars.TotalInterations;
            Console.WriteLine($"Total iterations: {total}");

            Func<Parameters, ValueTask<double>> targetFunc = (args) =>
            {
                var task = Task.Run(() =>
                {
                    var sum = 0L;
                    for (var i = 0; i < 10000000; i++)
                    {
                        sum = sum + i;
                    }
                    // avoid any release optimization
                    if (sum == int.MaxValue) throw new ApplicationException();

                    var result = -(Math.Pow(2 - args[0].Current, 2) + Math.Pow(1 - args[1].Current, 2) + Math.Pow(7 - args[2].Current, 2));
                    return result;
                });
                return new ValueTask<double>(task);
            };

            // true for memoize, second run is almost instant
            var maximizer = new GridOptimizer<double>(pars, targetFunc, true);

            EvalFolderFunc<double, EvalAddress> folder = FolderFunc;

            for (int i = 0; i < 2; i++)
            {
                var sw = new Stopwatch();
                var startMemory = GC.GetTotalMemory(true);
                sw.Restart();

                var optimum = await maximizer.FoldGrid(EvalAddress.Worst, folder);
                var optParams = pars.SetPositionsFromLinearAddress(optimum.LinearAddress);

                var endMemory = GC.GetTotalMemory(false);
                sw.Stop();
                Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
                Console.WriteLine($"Memory: {endMemory - startMemory}");
                Console.WriteLine($"Optimum: {optParams[0].Current} - {optParams[1].Current} - {optParams[2].Current}");
            }
        }

        [Test]
        public async Task CouldFindMaximumUsingMaximizeMethod()
        {
            var sw = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);
            sw.Restart();
            var par0 = new Parameter("par0", 0, 100, 1);
            var par1 = new Parameter("par1", 0, 100, 1);
            var par2 = new Parameter("par2", 0, 100, 1);

            var pars = new Parameters(new[] { par0, par1, par2 });

            var total = pars.TotalInterations;
            Console.WriteLine($"Total iterations: {total}");

            Func<Parameters, ValueTask<double>> targetFunc = (args) =>
            {
                var result = -(Math.Pow(200 - args[0].Current, 2) + Math.Pow(10 - args[1].Current, 2) +
                               Math.Pow(70 - args[2].Current, 2));
                return new ValueTask<double>(result);
            };

            var opt = await GridOptimizer.Maximize(pars, targetFunc, false);

            var optParams = opt.Parameters;

            var endMemory = GC.GetTotalMemory(false);
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Memory: {endMemory - startMemory}");
            Console.WriteLine($"Optimum: {optParams[0].Current} - {optParams[1].Current} - {optParams[2].Current}");
        }

        [Test]
        public async Task CouldFindMaximumUsingMaximizeMethodWithBigStep()
        {
            var sw = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);
            sw.Restart();
            var par0 = new Parameter("par0", 0, 100, 1, 10);
            var par1 = new Parameter("par1", 0, 100, 1, 10);
            var par2 = new Parameter("par2", 0, 100, 1, 10);

            var pars = new Parameters(new[] { par0, par1, par2 });

            var total = pars.TotalInterations;
            Console.WriteLine($"Total iterations: {total}");

            Func<Parameters, ValueTask<double>> targetFunc = (args) =>
            {
                var result = -(Math.Pow(200 - args[0].Current, 2) + Math.Pow(10 - args[1].Current, 2) +
                               Math.Pow(70 - args[2].Current, 2));
                return new ValueTask<double>(result);
            };

            var opt = await GridOptimizer.MaximizeWithBigStep(pars, targetFunc, false);

            var optParams = opt.Parameters;

            var endMemory = GC.GetTotalMemory(false);
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Memory: {endMemory - startMemory}");
            Console.WriteLine($"Optimum: {optParams[0].Current} - {optParams[1].Current} - {optParams[2].Current}");
        }
    }
}