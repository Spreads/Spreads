// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;

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
