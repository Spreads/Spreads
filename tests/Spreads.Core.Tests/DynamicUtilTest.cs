using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using static Spreads.DynamicUtil;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DynamicUtilTest {
        [Test]
        public void CouldGetAnyProperty()
        {
            Assert.AreEqual("RandomPropertyName", SR.RandomPropertyName);
            Assert.Throws<Exception>(() =>
            {
                throw new Exception(SR.RandomPropertyName);
            });
        }
    }
}
