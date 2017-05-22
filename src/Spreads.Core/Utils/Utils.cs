// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    internal static class CoreUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, bool> CombinePredicates<TSource>(Func<TSource, bool> predicate1, Func<TSource, bool> predicate2)
        {
            return x => predicate1(x) && predicate2(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, TResult> CombineMaps<TSource, TMiddle, TResult>(Func<TSource, TMiddle> map1, Func<TMiddle, TResult> map2)
        {
            return x => map2(map1(x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TKey, TSource, TResult> CombineMaps<TKey, TSource, TMiddle, TResult>(Func<TKey, TSource, TMiddle> map1, Func<TMiddle, TResult> map2)
        {
            return (k, x) => map2(map1(k, x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TKey, TSource, TResult> CombineMaps<TKey, TSource, TMiddle, TResult>(Func<TKey, TSource, TMiddle> map1, Func<TKey, TMiddle, TResult> map2)
        {
            return (k, x) => map2(k, map1(k, x));
        }

        public static T Identity<T>(T t)
        {
            return t;
        }

        [Obsolete("Use static Identity function")]
        public class IdentityFunction<TElement>
        {
            public static Func<TElement, TElement> Instance => Identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> FilterAndMapToFilterMap<TSource, TResult>(Func<TSource, bool> filter, Func<TSource, TResult> map)
        {
            return x => filter(x) ? new KeyValuePair<bool, TResult>(true, map(x)) : new KeyValuePair<bool, TResult>(false, default(TResult));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TSource>> FilterToFilterMap<TSource>(Func<TSource, bool> filter)
        {
            return FilterAndMapToFilterMap(filter, Identity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> MapToFilterMap<TSource, TResult>(Func<TSource, TResult> map)
        {
            return x => new KeyValuePair<bool, TResult>(true, map(x));
        }

        /// <summary>
        /// Chain two filterMaps together
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMaps<TSource, TMiddle, TResult>(Func<TSource, KeyValuePair<bool, TMiddle>> filterMap1, Func<TMiddle, KeyValuePair<bool, TResult>> filterMap2)
        {
            return x =>
            {
                var middle = filterMap1(x);
                return middle.Key ? filterMap2(middle.Value) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }

        /// <summary>
        /// Apply map to a result of filterMap
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMapWithMap<TSource, TMiddle, TResult>(Func<TSource, KeyValuePair<bool, TMiddle>> filterMap1, Func<TMiddle, TResult> map2)
        {
            return x =>
            {
                var middle = filterMap1(x);
                return middle.Key ? new KeyValuePair<bool, TResult>(true, map2(middle.Value)) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<TSource, KeyValuePair<bool, TResult>> CombineFilterMapWithFilter<TSource, TResult>(Func<TSource, KeyValuePair<bool, TResult>> filterMap1, Func<TResult, bool> filter2)
        {
            return x =>
            {
                var result = filterMap1(x);
                return result.Key && filter2(result.Value) ? new KeyValuePair<bool, TResult>(true, result.Value) : new KeyValuePair<bool, TResult>(false, default(TResult));
            };
        }
    }

    internal static class TypeExtensions
    {
        public static Assembly GetAssembly(this Type type)
        {
            return type.GetTypeInfo().Assembly;
        }

        public static bool GetIsValueType(this Type type)
        {
            return type.GetTypeInfo().IsValueType;
        }

        public static void InvokeMethod(this Assembly assembly, string typeName, string methodName)
        {
            var mi = assembly.GetType(typeName).GetMethod(methodName, (System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            mi.Invoke(null, new object[] { });
        }
    }

#if !NET451
    // TODO Replace usages with SpreadsException
    public class ApplicationException : Exception
    {
        public ApplicationException() : base() { }
        public ApplicationException(string message) : base(message) { }
    }

#endif
}