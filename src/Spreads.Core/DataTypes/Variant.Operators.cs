//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//namespace Spreads.DataTypes
//{
//    // TODO replace dynamic for hot paths, at least when types are the same

//    public partial struct Variant
//    {
//        public static Variant operator +(Variant v1, Variant v2)
//        {
//            var o1 = v1.ToObject();
//            var o2 = v2.ToObject();
//            var dyn = (dynamic)o1 + (dynamic)o2;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }

//        public static Variant operator -(Variant v1, Variant v2)
//        {
//            var o1 = v1.ToObject();
//            var o2 = v2.ToObject();
//            var dyn = (dynamic)o1 - (dynamic)o2;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }

//        public static Variant operator -(Variant v1)
//        {
//            var o1 = v1.ToObject();
//            var dyn = -(dynamic)o1;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }

//        public static Variant operator *(Variant v1, Variant v2)
//        {
//            var o1 = v1.ToObject();
//            var o2 = v2.ToObject();
//            var dyn = (dynamic)o1 * (dynamic)o2;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }

//        public static Variant operator /(Variant v1, Variant v2)
//        {
//            var o1 = v1.ToObject();
//            var o2 = v2.ToObject();
//            var dyn = (dynamic)o1 / (dynamic)o2;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }

//        public static Variant operator %(Variant v1, Variant v2)
//        {
//            var o1 = v1.ToObject();
//            var o2 = v2.ToObject();
//            var dyn = (dynamic)o1 % (dynamic)o2;
//            var variant = Variant.FromObject(dyn);
//            return variant;
//        }
//    }
//}