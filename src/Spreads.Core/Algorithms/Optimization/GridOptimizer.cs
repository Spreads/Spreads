// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Spreads.Algorithms.Optimization
{
    /// <summary>
    /// Result of evaluating a target function with given parameters.
    /// </summary>
    public struct EvalParametersResult<TEval>
    {
        public Parameters Parameters;
        public TEval Value;
    }

    public struct EvalAddressResult<TEval>
    {
        public long LinearAddress;
        public TEval Value;
    }

    public delegate TState EvalFolderFunc<TEval, TState>(TState state, ref EvalParametersResult<TEval> item);

    public struct GridOptimizer<TEval>
    {
        private struct EvalResultTask
        {
            public Parameters Parameters;
            public ValueTask<TEval> Value;
        }

        private readonly Parameters _parameters;
        private readonly Func<Parameters, ValueTask<TEval>> _targetFunc;
        private readonly bool _memoize;
        private readonly Dictionary<long, TEval> _results;
        private int _tail;
        private readonly int _concurrencyLimit;
        private readonly List<EvalResultTask> _activeTasks;

        public GridOptimizer(Parameters parameters, Func<Parameters, ValueTask<TEval>> targetFunc, bool memoize = false)
        {
            _parameters = parameters;
            _targetFunc = targetFunc;
            _memoize = memoize;
            _results = _memoize ? new Dictionary<long, TEval>() : null;
            var total = parameters.TotalInterations;
            Debug.WriteLine($"FoldGrid total iterations: {total}");
            _concurrencyLimit = Math.Min(Environment.ProcessorCount * 2, total);
            _activeTasks = new List<EvalResultTask>(_concurrencyLimit);
            _tail = 0;
        }

        /// <summary>
        /// Iterate over grid, evaluate target function and fold its results.
        /// </summary>
        public async Task<TState> FoldGrid<TState>(TState seed, EvalFolderFunc<TEval, TState> evalFolder, Parameters parameters = null)
        {
            parameters = parameters ?? _parameters.Clone().Reset();

            var accumulator = seed;

            var depth = 0;
            while (true)
            {
                if (depth < _parameters.Count)
                {
                    if (parameters.RefList[depth].MoveNext())
                    {
                        depth++;
                        // when all moved, the first `if` above is false
                    }
                    else
                    {
                        parameters.RefList[depth].Reset();
                        depth--;
                    }
                }
                else
                {
                    try
                    {
                        var position = _tail % _concurrencyLimit;
                        if (_activeTasks.Count < _concurrencyLimit)
                        {
                            var point = parameters.Clone();
                            var address = point.LinearAddress();
                            TEval tmp;
                            var t = (_memoize && _results.TryGetValue(address, out tmp)) ? new ValueTask<TEval>(tmp) : _targetFunc(point);
                            _activeTasks.Add(new EvalResultTask { Parameters = point, Value = t });
                        }
                        else
                        {
                            // now the active tasks buffer is full
                            // await on the task at the position, process the result and replace the task at the position
                            // if current task is slow and other tasks already completed, we will just replace them very quickly
                            // later, but while we are waiting for the tail task concurrency could drop,
                            // so here is 'amortized' concurrency.

                            var currentTask = _activeTasks[position];
                            await currentTask.Value;

                            var evalResult = new EvalParametersResult<TEval>()
                            {
                                Value = currentTask.Value.Result,
                                Parameters = currentTask.Parameters
                            };
                            var address = currentTask.Parameters.LinearAddress();
                            TEval tmp;
                            if (_memoize)
                            {
                                if (!_results.TryGetValue(address, out tmp))
                                {
                                    _results[address] = currentTask.Value.Result;
                                }
                            }

                            // NB this is single-threaded application of evalFolder and evalResult.Parameters could be modified
                            // later since they are reused. Folder should copy parameters or store linear address
                            accumulator = evalFolder(accumulator, ref evalResult);

                            for (int i = 0; i < parameters.Count; i++)
                            {
                                currentTask.Parameters.RefList[i] = parameters.RefList[i];
                            }
                            address = currentTask.Parameters.LinearAddress();
                            var t = (_memoize && _results.TryGetValue(address, out tmp)) ? new ValueTask<TEval>(tmp) : _targetFunc(currentTask.Parameters);

                            currentTask.Value = t;
                            _activeTasks[position] = currentTask;
                            _tail++;
                        }
                    }
                    finally
                    {
                        depth--;
                    }
                }
                if (depth == -1) break;
            }

            // NB All items in _activeTask are active

            foreach (var currentTask in _activeTasks)
            {
                await currentTask.Value;
                var evalResult = new EvalParametersResult<TEval>
                {
                    Value = currentTask.Value.Result,
                    Parameters = currentTask.Parameters
                };
                accumulator = evalFolder(accumulator, ref evalResult);
            }

            return accumulator;
        }
    }

    public static class GridOptimizer
    {
        private static readonly EvalAddressResult<double> WorstDouble = new EvalAddressResult<double>() { Value = double.MinValue };
        private static readonly EvalFolderFunc<double, EvalAddressResult<double>> DoubleMaxFolder = MaxFolderFunc;

        private static EvalAddressResult<double> MaxFolderFunc(EvalAddressResult<double> state, ref EvalParametersResult<double> item)
        {
            if (item.Value > state.Value)
            {
                return new EvalAddressResult<double> { Value = item.Value, LinearAddress = item.Parameters.LinearAddress() };
            }
            return state;
        }

        /// <summary>
        /// Find parameters that maximize the target function over the parameter grid.
        /// </summary>
        public static async Task<EvalParametersResult<double>> Maximize(Parameters parameters,
            Func<Parameters, ValueTask<double>> targetFunc, bool memoize = false)
        {
            var maximizer = new GridOptimizer<double>(parameters, targetFunc, memoize);
            var optimum = await maximizer.FoldGrid(WorstDouble, DoubleMaxFolder);
            var optParams = parameters.SetPositionsFromLinearAddress(optimum.LinearAddress);
            var result = new EvalParametersResult<double> { Value = optimum.Value, Parameters = optParams };
            return result;
        }

        /// <summary>
        /// Find parameters that maximize the target function over the parameter grid, first searching with big steps.
        /// </summary>
        public static async Task<EvalParametersResult<double>> MaximizeWithBigStep(Parameters parameters,
            Func<Parameters, ValueTask<double>> targetFunc, bool memoize = false)
        {
            var newParameters = new Parameters(parameters.RefList.Select(p => p.WithBigStep()).ToArray());

            var maximizer = new GridOptimizer<double>(newParameters, targetFunc, memoize);
            var optimum = await maximizer.FoldGrid(WorstDouble, DoubleMaxFolder);
            var optParams = newParameters.SetPositionsFromLinearAddress(optimum.LinearAddress);

            // 2nd step, get a region around optimal big step
            for (int i = 0; i < parameters.RefList.Count; i++)
            {
                newParameters.RefList[i] = parameters.RefList[i].GetRegion(optParams.RefList[i].GridPosition * parameters.RefList[i].BigStepMultiple, parameters.RefList[i].BigStepMultiple);
            }

            maximizer = new GridOptimizer<double>(newParameters, targetFunc, memoize);
            optimum = await maximizer.FoldGrid(WorstDouble, DoubleMaxFolder);
            optParams = newParameters.SetPositionsFromLinearAddress(optimum.LinearAddress);
            var ret = parameters.Clone();
            for (int i = 0; i < parameters.RefList.Count; i++)
            {
                ret.RefList[i].CurrentPosition = optParams[i].GridPosition;
            }

            var result = new EvalParametersResult<double> { Value = optimum.Value, Parameters = ret };
            return result;
        }
    }
}