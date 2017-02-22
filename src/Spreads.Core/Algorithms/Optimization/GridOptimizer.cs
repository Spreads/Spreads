using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Spreads.Algorithms.Optimization {

    public class GridMaximizer {
        public delegate T FolderFunc<T>(T state, ref EvalResult item);
        // result of evaluating a target function with given parameters
        public struct EvalResult {
            public Parameter[] Parameters;
            public double Value;
        }

        // TODO ValueTask
        public struct EvalResultTask {
            public Parameter[] Parameters;
            public ValueTask<double> Value;
        }

        private readonly Parameter[] _parameters;
        private readonly Func<Parameter[], ValueTask<double>> _targetFunc;
        private readonly bool _memoize;
        private readonly Dictionary<long, double> _results = new Dictionary<long, double>();
        private int _tail;
        private readonly int _concurrencyLimit;
        private readonly List<EvalResultTask> _activeTasks;

        public GridMaximizer(Parameter[] parameters, Func<Parameter[], ValueTask<double>> targetFunc, bool memoize = false) {
            _parameters = parameters;
            _targetFunc = targetFunc;
            _memoize = memoize;
            var total = parameters.Select(x => x.Steps).Aggregate(1, (i, st) => checked(i * st));
            Debug.WriteLine($"ProcessGrid total iterations: {total}");
            _concurrencyLimit = Math.Min(Environment.ProcessorCount * 2, total);
            _activeTasks = new List<EvalResultTask>(_concurrencyLimit);
        }

        public async Task<T> ProcessGrid<T>(Parameter[] parameters, T seed, FolderFunc<T> folder) {
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
                            var address = point.LinearAddress();
                            double tmp;
                            var t = (_memoize && _results.TryGetValue(address, out tmp)) ? new ValueTask<double>(tmp) : _targetFunc(point);
                            _activeTasks.Add(new EvalResultTask { Parameters = point, Value = t });
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
                            var address = currentTask.Parameters.LinearAddress();
                            double tmp;
                            if (_memoize) {
                                if (_results.TryGetValue(address, out tmp)) {
                                    Debug.Assert(
                                        Math.Abs((tmp - currentTask.Value.Result) / ((tmp + currentTask.Value.Result))) <
                                        0.00001);
                                } else {
                                    _results[address] = currentTask.Value.Result;
                                }
                            }

                            // NB this is single-threaded application of folder and evalResult.Parameters could be modified
                            // later since they are reused. Folder should copy parameters or store linear address
                            // TODO rebuild position from linear address
                            accumulator = folder(accumulator, ref evalResult);

                            for (int i = 0; i < parameters.Length; i++) {
                                currentTask.Parameters[i] = parameters[i];
                            }
                            address = currentTask.Parameters.LinearAddress();
                            var t = (_memoize && _results.TryGetValue(address, out tmp)) ? new ValueTask<double>(tmp) : _targetFunc(currentTask.Parameters);

                            currentTask.Value = t;
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
                var evalResult = new EvalResult
                {
                    Value = currentTask.Value.Result,
                    Parameters = currentTask.Parameters
                };
                accumulator = folder(accumulator, ref evalResult);
            }

            return accumulator;
        }
    }
}