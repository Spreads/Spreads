// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Spreads.DataTypes
{
    [Obsolete("TODO needs rework or delete it")]
    public class KnownTypeAttribute : Attribute
    {
#if NET451

        static KnownTypeAttribute() {
            var assemblied = AppDomain.CurrentDomain
                .GetAssemblies().Where(a => !a.CodeBase.Contains("mscorlib.dll"));
            var types = assemblied
                    .SelectMany(s => {
                        try {
                            return s.GetTypes();
                        } catch {
                            return new Type[] { };
                        }
                    })
                    .Where(p => {
                        try {
                            return p.GetTypeInfo().GetCustomAttribute<KnownTypeAttribute>() != null
                                   && p.GetTypeInfo().IsClass && !p.GetTypeInfo().IsAbstract;
                        } catch {
                            return false;
                        }
                    }).ToList();
            foreach (var t in types) {
                var attr = t.GetTypeInfo().GetCustomAttribute<KnownTypeAttribute>();
                if (!KnownTypes.TryAdd(attr.TypeCode, t)) {
                    KnownTypes[attr.TypeCode] = t;
                    //Environment.FailFast($"Duplicate type id: {attr.TypeCode}");
                }
            }
        }

#endif

        public KnownTypeAttribute(byte typeCode)
        {
            TypeCode = typeCode;
        }

        public byte TypeCode { get; }

        private static readonly ConcurrentDictionary<byte, Type> KnownTypes = new ConcurrentDictionary<byte, Type>();
        private static readonly ConcurrentDictionary<Type, byte> KnownIds = new ConcurrentDictionary<Type, byte>();

        public static void RegisterType<T>(byte typeId)
        {
            if (!KnownTypes.TryAdd(typeId, typeof(T)))
            {
                Environment.FailFast($"Duplicate type id: {typeId}");
            }
        }

        public static Type GetType(byte typeId)
        {
            return KnownTypes[typeId];
        }

        public static byte GetTypeId(Type t)
        {
            return KnownIds.GetOrAdd(t, type =>
            {
                var attrs = type.GetTypeInfo().GetCustomAttributes<KnownTypeAttribute>();
                var knownTypeAttributes = attrs as KnownTypeAttribute[] ?? attrs.ToArray();
                if (knownTypeAttributes.Count() != 1)
                {
                    Environment.FailFast($"Type {type.Name} is not known");
                }
                return knownTypeAttributes.Single().TypeCode;
            });
        }
    }
}