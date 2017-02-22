// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Algorithms.Optimization;

namespace Spreads.Core.Tests {

    [TestFixture]
    public class OptimizationTests {


        [Test]
        public async void CouldFindMaximum() {
            var sw = new Stopwatch();
            var startMemory = GC.GetTotalMemory(true);
            sw.Restart();
            var par0 = new Parameter("par1", 0, 100, 1);
            var par1 = new Parameter("par2", 0, 100, 1);
            var par2 = new Parameter("par2", 0, 100, 1);

            var pars = new[] { par0, par1, par2 };

            Func<double[], Task<double>> targetFunc = (args) => {
                //await Task.Delay(0);
                var result = -(Math.Pow(20 - args[0], 2) + Math.Pow(10 - args[1], 2) + Math.Pow(70 - args[2], 2));
                return Task.FromResult(result);
            };

            var maximizer = new GridMaximizer(pars, targetFunc);

            Func<GridMaximizer.EvalResult[], Task<GridMaximizer.EvalResult>> reducer = results => {
                var bestResult = new GridMaximizer.EvalResult { Value = double.MinValue };
                foreach (var result in results) {
                    if (result.Value > bestResult.Value) {
                        bestResult = result;
                    }
                }
                return Task.FromResult(bestResult);
            };

            var optimum = await maximizer.ProcessGrid(pars, reducer);
            var endMemory = GC.GetTotalMemory(false);
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Memory: {endMemory - startMemory}");
            Console.WriteLine($"Optimum: {optimum.Parameters[0].Current} - {optimum.Parameters[1].Current} - {optimum.Parameters[2].Current}");

        }
    }
}