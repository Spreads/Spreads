using System;
using System.Collections.Generic;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Compare string Variants with StringComparison.InvariantCultureIgnoreCase
    /// </summary>
    public class CaseInsensitiveVariantEqualityComparer : IEqualityComparer<Variant>
    {
        public bool Equals(Variant x, Variant y)
        {
            if (x.TypeEnum == TypeEnum.String && y.TypeEnum == TypeEnum.String)
            {
                return x.Get<string>().Equals(y.Get<string>(), StringComparison.OrdinalIgnoreCase);
            }
            return x.Equals(y);
        }

        public int GetHashCode(Variant obj)
        {
            return obj.GetHashCode();
        }
    }
}