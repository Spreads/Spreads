// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Data;
using Spreads.DataTypes;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static class DataTableExtensions
    {
        public static Table AsSpreadsTable(this DataTable dataTable)
        {
            if (dataTable == null) throw new ArgumentNullException(nameof(dataTable));

            var table = new Table(dataTable.Rows.Count, dataTable.Columns.Count) {TableName = dataTable.TableName};

            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                table.Columns[i].Name = dataTable.Columns[i].ColumnName;
            }

            for (var i = 0; i < dataTable.Rows.Count; i++)
            {
                for (var j = 0; j < dataTable.Columns.Count; j++)
                {
                    var value = dataTable.Rows[i][dataTable.Columns[j]];
                    table[i, j] = Variant.FromObject(value == DBNull.Value ? null : value);
                }
            }
            return table;
        }
    }
}
