#region summary

//   ------------------------------------------------------------------------------------------------
//   <copyright file="TableFieldSet.cs" >
//     Author：MOKEYISH
//     Date：2018/02/28
//     Time：13:55
//   </copyright>
//   ------------------------------------------------------------------------------------------------

#endregion

using System;
using System.Numerics;

namespace Spreads.DataTypes
{
    public abstract class TableFieldSet
    {
        public int Index { get; } = 0;

        public string Name { get; set; }

        public Table Table { get; }

        public abstract Variant[] Values { get; }

        protected TableFieldSet(Table table, int index)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Index = index;
        }
    }

    public class Row : TableFieldSet
    {
        /// <inheritdoc />
        public Row(Table table, int index) : base(table, index)
        {
        }

        /// <inheritdoc />
        public override Variant[] Values => Table.GetRowValues(Index);
    }

    public class Column : TableFieldSet
    {
        /// <inheritdoc />
        public Column(Table table, int index) : base(table, index)
        {
        }

        /// <inheritdoc />
        public override Variant[] Values => Table.GetColumnValues(Index);
    }
}