using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    // misery and pain ahead!

    // we need a convenient structure to work from code, not only store as bytes
    // we need it non-generic. 

    public unsafe struct Variant {
        private byte TypeCode;
        private fixed byte FixedData[16];
        private object Object;


        private const sbyte DoubleCode = 14;

        //private void AssertType(sbyte code) {
        //    if (TypeCode != code && TypeCode != default(sbyte)) throw new InvalidCastException("Invalid cast");
        //}


        //public void Test() {
        //    var v = (Variant)123.0;
        //}
        //public static implicit operator Variant(double d) {
        //    return new Variant {
        //        Double = d
        //    };
        //}
        //public static implicit operator double(Variant d) {
        //    //d.AssertType(DoubleCode);
        //    return d.Double;
        //}
        //public static explicit operator Variant(double d) {
        //    return new Variant {
        //        Double = d
        //    };
        //}
        //public static explicit operator double (Variant d) {
        //    d.AssertType((0.0).GetTypeCode());
        //    return d.Double;
        //}
    }
}
