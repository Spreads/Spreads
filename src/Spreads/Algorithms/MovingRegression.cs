// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Spreads;

namespace Spreads.Algorithms {
    public static class MovingRegressionExtension {
        public static Series<DateTime, Matrix<double>> MovingRegression(this Series<DateTime, double> y, Series<DateTime, double[]> xs, uint window) {
            var xpx = xs.Zip(y, (dt, xrow, yrow) => {
                // first x row transposed
                var xt = Matrix<double>.Build.DenseOfColumnArrays(new[] { xrow });
                var xpxi = xt.Multiply(xt.Transpose());
                var xpy = xt.Multiply(yrow);
                var tuple =
                    new ValueTuple<Matrix<double>, Matrix<double>>(xpxi, xpy);
                //return xpxi;
                return tuple;
            });

            // then we need to sum xpxi and xpy over a window, make inverse of the first and multiply
            // - the result is betas
            var dim = xs.First.Value.Length;
            var cursorFactory = new Func<ScanLagAllowIncompleteCursor<DateTime, ValueTuple<Matrix<double>, Matrix<double>>, ValueTuple<Matrix<double>, Matrix<double>>>>(() =>
                new ScanLagAllowIncompleteCursor<DateTime, ValueTuple<Matrix<double>, Matrix<double>>, ValueTuple<Matrix<double>, Matrix<double>>>(
                    xpx.GetCursor, window, 1,
                    () => new ValueTuple<Matrix<double>, Matrix<double>>(Matrix<double>.Build.Dense(dim, dim), Matrix<double>.Build.Dense(dim, 1)),
                    (st, add, sub, cnt) => {
                        var cov = st.Value1.Add(add.Value.Value1).Subtract(sub.Value.Value1);
                        var variance = st.Value2.Add(add.Value.Value2).Subtract(sub.Value.Value2);
                        return new ValueTuple<Matrix<double>, Matrix<double>>(cov, variance);
                    }, false));

            Series<DateTime, Matrix<double>> betas;
            betas = new CursorSeries<DateTime, ValueTuple<Matrix<double>, Matrix<double>>>(cursorFactory).Map(
                tpl => {
                    return tpl.Value1.Inverse().Multiply(tpl.Value2);
                });
            return betas;
        }
    }
}
