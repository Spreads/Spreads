/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads {

    public static class CoreUtils {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, bool> CombinePredicates<TSource>(Func<TSource, bool> predicate1, Func<TSource, bool> predicate2) {
            return x => predicate1(x) && predicate2(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, TResult> CombineMaps<TSource, TMiddle, TResult>(Func<TSource, TMiddle> map1, Func<TMiddle, TResult> map2) {
            return x => map2(map1(x));
        }

        public class IdentityFunction<TElement> {
            public static Func<TElement, TElement> Instance
            {
                get { return x => x; }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> FilterAndMapToFilterMap<TSource, TResult>(Func<TSource, bool> filter, Func<TSource, TResult> map) {
            return x => filter(x) ? new KeyValuePair<bool, TResult>(true, map(x)) : new KeyValuePair<bool, TResult>(false, default(TResult));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TSource>> FilterToFilterMap<TSource>(Func<TSource, bool> filter) {
            return FilterAndMapToFilterMap(filter, IdentityFunction<TSource>.Instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> MapToFilterMap<TSource, TResult>(Func<TSource, TResult> map) {
            return x => new KeyValuePair<bool, TResult>(true, map(x));
        }

        /// <summary>
        /// Chain two filterMaps together
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMaps<TSource, TMiddle, TResult>(Func<TSource, KeyValuePair<bool, TMiddle>> filterMap1, Func<TMiddle, KeyValuePair<bool, TResult>> filterMap2) {
            return x => {
                var middle = filterMap1(x);
                return middle.Key ? filterMap2(middle.Value) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }

        /// <summary>
        /// Apply map to a result of filterMap
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMapWithMap<TSource, TMiddle, TResult>(Func<TSource, KeyValuePair<bool, TMiddle>> filterMap1, Func<TMiddle, TResult> map2) {
            return x => {
                var middle = filterMap1(x);
                return middle.Key ? new KeyValuePair<bool, TResult>(true, map2(middle.Value)) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMapWithFilter<TSource, TResult>(Func<TSource, KeyValuePair<bool, TResult>> filterMap1, Func<TResult, bool> filter2) {
            return x => {
                var result = filterMap1(x);
                return result.Key && filter2(result.Value) ? new KeyValuePair<bool, TResult>(true, result.Value) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }

        // Doesn't work http://stackoverflow.com/questions/32864239/get-unsafe-pointer-to-array-of-keyvaluepairdatetime-decimal-in-c-sharp
        //public static T UnsafeCast<T>(Object o) where T : class
        //{
        //    var type = typeof (System.Runtime.CompilerServices.CallSite).Assembly.GetType("System.Runtime.CompilerServices.JitHelpers");
        //    var method = type.GetMethod("UnsafeCast", BindingFlags.NonPublic | BindingFlags.Static);
        //    var generic = method.MakeGenericMethod(typeof(T));
        //    var ret = (T)generic.Invoke(o, null);
        //    return ret;
        //}

    }




    public static class TypeExtensions {
        public static Assembly GetAssembly(this Type type) {
            return type.GetTypeInfo().Assembly;
        }

        public static bool GetIsValueType(this Type type) {
            return type.GetTypeInfo().IsValueType;
        }

        public static void InvokeMethod(this Assembly assembly, string typeName, string methodName) {
            var mi = assembly.GetType(typeName).GetMethod(methodName, (System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            mi.Invoke(null, new object[] { });
        }
    }

#if !NET451
    public class ApplicationException : Exception {

        public ApplicationException(string message) : base(message) { }
    }

#endif

}
