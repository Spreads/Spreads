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
        public KnownTypeAttribute(byte typeCode)
        {
            TypeCode = typeCode;
        }

        public byte TypeCode { get; }

        private static readonly ConcurrentDictionary<byte, Type> KnownTypes = new ConcurrentDictionary<byte, Type>();
        private static readonly ConcurrentDictionary<Type, byte> KnownIds = new ConcurrentDictionary<Type, byte>();

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

                var code = knownTypeAttributes.Single().TypeCode;
                if (!KnownTypes.TryAdd(code, t))
                {
                    Environment.FailFast($"Duplicate type id: {code}");
                }

                return code;
            });
        }
    }
}