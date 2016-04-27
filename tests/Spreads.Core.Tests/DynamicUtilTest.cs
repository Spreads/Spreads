using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DynamicUtilTest {
        [Test]
        public void CouldGetAnyProperty()
        {
            Assert.AreEqual("RandomPropertyName", DynamicUtil.SR.RandomPropertyName);
        }
    }
}
