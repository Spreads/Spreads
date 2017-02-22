using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Algorithms.Optimization {



    public class GridMaximizer {

        // result of evaluating a target function with given parameters
        public struct EvalResult {
            public Parameter[] Parameters;
            public double Value;
        }

        public struct EvalResultTask {
            public Parameter[] Parameters;
            public Task<double> Value;
        }

        private readonly Parameter[] _parameters;
        private readonly Func<double[], Task<double>> _targetFunc;
        private ConcurrentDictionary<long, Task<double>> _results = new ConcurrentDictionary<long, Task<double>>();
        private int _tail = 0;
        private readonly int _concurrencyLimit = Environment.ProcessorCount * 2;
        private List<EvalResultTask> _activeTasks;

        public GridMaximizer(Parameter[] parameters, Func<double[], Task<double>> targetFunc) {
            _parameters = parameters;
            _targetFunc = targetFunc;
            _activeTasks = new List<EvalResultTask>(_concurrencyLimit);
        }

        public async Task<EvalResult> ProcessGrid(Parameter[] parameters, Func<EvalResult[], Task<EvalResult>> reducer) {
            var total = parameters.Select(x => x.Steps).Aggregate(1, (i, st) => checked(i * st));
            Debug.WriteLine($"ProcessGrid total iterations: {total}");
            var evalResults = new List<Task<EvalResult>>(total);

            // Idea is that we will have to touch all points in the grid, so instead of 
            // complex parallelism we schedule all the work to the TPL (default thread pool)
            // but limit the concurrency via a semaphore (could be replaced with a custom task scheduler with limited concurrency)
            // Then we accumulate all the tasks and await all of them.
            // Later we apply a folder function that could select optimal parameters, or average/best/worst in the region, etc.

            var depth = 0;
            while (true) {
                if (depth < _parameters.Length) {
                    if (parameters[depth].MoveNext()) {
                        depth++;
                        // when all moved, the first `if` above is false
                    } else {
                        parameters[depth].Reset();
                        depth--;
                    }
                } else {
                    try {
                        // now parameters are at some position
                        var position = parameters.ToArray();
                        evalResults.Add(GetResult(position, CancellationToken.None));
                    } finally {
                        depth--;
                    }
                }
                if (depth == -1) break;
            }

            var all = await Task.WhenAll(evalResults);
            var result = await reducer(all);
            return result;
        }


        private Task<EvalResult> GetResult(Parameter[] parameters, CancellationToken ct) {

            var xxx = new EvalResult() {
                Value = _targetFunc(parameters.Select(x => x.Current).ToArray()).Result,
                Parameters = parameters
            };
            return Task.FromResult(xxx);

            //var id = parameters.LinearAddress();
            //var task = _results.GetOrAdd(id, (addr) => {
            //    return _semaphore.WaitAsync(ct)
            //    .ContinueWith(async (Task t) => {
            //        try {
            //            var parValues = new double[parameters.Length];
            //            for (int j = 0; j < parameters.Length; j++) {
            //                parValues[j] = parameters[j].Current;
            //            }
            //            var result = await _targetFunc(parValues);
            //            return result;
            //        } finally {
            //            _semaphore.Release();
            //        }
            //    }, ct).Unwrap();
            //}).ContinueWith(
            //    t => new EvalResult() { Value = t.Result, Parameters = parameters }, cancellationToken: ct);
            //return task;
        }
    }
}
