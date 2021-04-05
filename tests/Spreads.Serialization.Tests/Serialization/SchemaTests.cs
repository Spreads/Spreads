// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Serialization.Utf8Json.Resolvers.Internal;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [Category("Serialization")]
    [Category("CI")]
    [TestFixture]
    public class SchemaTests
    {
        [Test]
        public void CouldSerializeContainerSchema()
        {
            var cs = new ContainerSchema()
            {
                ContainerType = 1,
                ContainerFlags = 1,
                ValuesSchema = new TypeSchema()
                {
                    DataTypeHeader = TypeEnumHelper<long>.DataTypeHeader,
                    FixedSize = 8,
                    TypeName = typeof(long).Name,
                    TypeFullName = typeof(long).FullName
                }
            };

            var str = JsonSerializer.ToJsonString(cs, ExcludeNullStandardResolver.Instance);
            Console.WriteLine(str);
        }
    }
}
