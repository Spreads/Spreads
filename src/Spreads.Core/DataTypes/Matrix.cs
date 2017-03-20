// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.DataTypes
{
    public class Matrix<T>
    {
        public Matrix(T[,] data)
        {
            Data = data;
        }

        public T[,] Data { get; }

        public int ColumnsCount => Data.GetLength(1);
        public int RowsCount => Data.GetLength(0);

        public bool CheckIndexes(int row, int column)
        {
            return row >= 0 && column >= 0 && row < RowsCount && column < ColumnsCount;
        }
    }
}