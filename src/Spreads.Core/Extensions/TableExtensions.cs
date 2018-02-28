#region summary
//   ------------------------------------------------------------------------------------------------
//   <copyright file="TableExtensions.cs" >
//     Author：MOKEYISH
//     Date：2018/02/28
//     Time：15:44
//   </copyright>
//   ------------------------------------------------------------------------------------------------
#endregion

using System;
using System.Data;
using System.Globalization;
using System.Linq;
using Spreads.DataTypes;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static class TableExtensions
    {
        public static DataTable AsDataTable(this Table table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var dataTable = new DataTable(table.TableName);
            if (table.Columns.Count > 0)
            {
                foreach (var column in table.Columns)
                {
                    dataTable.Columns.Add(column.Name);
                }
            }

            if (table.ColumnsCount <= 0 || table.RowsCount <= 0) return dataTable;
            
            for (var i = 0; i < table.RowsCount; i++)
            {
                dataTable.Rows.Add(table.Rows[i].Values.Select(o => o.ToObject()));
            }
            return dataTable;
        }
    }
}