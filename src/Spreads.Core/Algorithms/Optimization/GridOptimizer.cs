using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
        // TODO ValueTask
        public struct EvalResultTask {
            public Parameter[] Parameters;
            public Task<double> Value;
        }

        private readonly Parameter[] _parameters;
        private readonly Func<Parameter[], Task<double>> _targetFunc;
        private Dictionary<long, double> _results = new Dictionary<long, double>();
        private int _tail = 0;
        private readonly int _concurrencyLimit;
        private readonly List<EvalResultTask> _activeTasks;

        public GridMaximizer(Parameter[] parameters, Func<Parameter[], Task<double>> targetFunc) {
            _parameters = parameters;
            _targetFunc = targetFunc;
            var total = parameters.Select(x => x.Steps).Aggregate(1, (i, st) => checked(i * st));
            Debug.WriteLine($"ProcessGrid total iterations: {total}");
            _concurrencyLimit = Math.Min(Environment.ProcessorCount * 2, total);
            _activeTasks = new List<EvalResultTask>(_concurrencyLimit);
        }

        public async Task<T> ProcessGrid<T>(Parameter[] parameters, T seed, Func<T, EvalResult, T> folder) {

            //var evalResults = new List<Task<EvalResult>>(total);

            // TODO remove this commnet about initial BAD idea
            // Idea is that we will have to touch all points in the grid, so instead of 
            // complex parallelism we schedule all the work to the TPL (default thread pool)
            // but limit the concurrency via a semaphore (could be replaced with a custom task scheduler with limited concurrency)
            // Then we accumulate all the tasks and await all of them.
            // Later we apply a folder function that could select optimal parameters, or average/best/worst in the region, etc.

            var accumulator = seed;

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
                        var position = _tail % _concurrencyLimit;
                        if (_activeTasks.Count < _concurrencyLimit) {
                            var point = parameters.ToArray();

                            // TODO ValueTask and memoize results by parameters linear address
                            _activeTasks.Add(new EvalResultTask {Parameters = point, Value = _targetFunc(point)});

                        } else {
                            // now the active tasks buffer is full
                            // await on the task at the position, process the result and replace the task at the position
                            // if current task is slow and other tasks already completed, we will just replace them very quickly
                            // later, but while we are waiting for the tail task concurrency could drop,
                            // so here is 'amortized' concurrency.

                            var currentTask = _activeTasks[position];
                            await currentTask.Value;
                            var evalResult = new EvalResult() {
                                Value = currentTask.Value.Result,
                                Parameters = currentTask.Parameters
                            };
                            // NB this is single-threaded application of folder and evalResult.Parameters could be modified
                            // later since they are reused. Folder should copy parameters or store linear address
                            // TODO rebuild position from linear address
                            accumulator = folder(accumulator, evalResult);

                            for (int i = 0; i < parameters.Length; i++) {
                                currentTask.Parameters[i] = parameters[i];

                            }
                            currentTask.Value = _targetFunc(currentTask.Parameters);
                            _activeTasks[position] = currentTask;
                            _tail++;
                        }

                    } finally {
                        depth--;
                    }
                }
                if (depth == -1) break;
            }

            // NB All items in _activeTask are active

            foreach (var currentTask in _activeTasks) {
                await currentTask.Value;
                var evalResult = new EvalResult() {
                    Value = currentTask.Value.Result,
                    Parameters = currentTask.Parameters
                };
                accumulator = folder(accumulator, evalResult);
            }

            return accumulator;
        }


        private Task<EvalResult> GetResult(Parameter[] parameters, CancellationToken ct) {

            var xxx = new EvalResult() {
                Value = _targetFunc(parameters).Result,
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
