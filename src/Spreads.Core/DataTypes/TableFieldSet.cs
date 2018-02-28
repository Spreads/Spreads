// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Diagnostics;

namespace Spreads.DataTypes
{
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public abstract class TableFieldSet
    {
        private string _name;
        public int Index { get; }

        public string Name
        {
            get => _name ?? Index.ToString();
            set => _name = value;
        }

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
