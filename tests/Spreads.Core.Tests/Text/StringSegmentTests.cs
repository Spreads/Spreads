// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Shouldly;
using Spreads.Text;
using Spreads.Utils;

namespace Spreads.Core.Tests.Text
{
    [TestFixture]
    public class StringSegmentTests
    {
        [Test]
        public void ShouldGetToString()
        {
            var str = "abcdefg";
            var seg = new StringSegment(str, 2, 3);
            seg.ToString().ShouldBe("cde");
            seg.ToString().ShouldNotBeSameAs(str);
            seg.Span.SequenceEqual("cde").ShouldBeTrue();

            char[] chars = str.ToCharArray();
            var seg1 = new StringSegment(chars, 2, 3);
            seg1.ToString().ShouldBe("cde");
            seg1.ToString().ShouldNotBeSameAs(str);
            seg1.Span.SequenceEqual("cde").ShouldBeTrue();

            seg.ShouldBe(seg1);
            seg.GetHashCode().ShouldBe(seg1.GetHashCode());
            (seg == seg1).ShouldBeTrue();
            (seg != seg1).ShouldBeFalse();

            for (int i = 0; i < seg.Length; i++)
            {
                seg[i].ShouldBe(seg1[i]);
            }

        }

        [Test]
        public void ShouldReturnBackingString()
        {
            var str = "abcdefg";
            StringSegment seg = str;
            seg.ToString().ShouldBe(str);
            seg.ToString().ShouldBeSameAs(str);

            char[] chars = str.ToCharArray();
            StringSegment seg1 = chars;
            seg1.ToString().ShouldBe(str);
            seg1.ToString().ShouldNotBeSameAs(str);

            seg.ShouldBe(seg1);
            seg.GetHashCode().ShouldBe(seg1.GetHashCode());
            (seg == seg1).ShouldBeTrue();
            (seg != seg1).ShouldBeFalse();

        }

        [Test]
        public void ShouldCheckBounds()
        {
            var str = "abcdefg";
            char[] chars = str.ToCharArray();

            Should.Throw<ArgumentOutOfRangeException>(() =>
            {
                var _ = new StringSegment(str, 2, 6);
            });
            Should.Throw<ArgumentOutOfRangeException>(() =>
            {
                var _ = new StringSegment(chars, 2, 6);
            });
        }

        [Test]
        public void ShouldTrimSegment()
        {
            var str = "abcdefg";
            char[] chars = str.ToCharArray();

            var seg = new StringSegment(chars, 2, 3).Trim();
            seg.IsTrimmed.ShouldBeTrue();
            seg.ToString().ShouldBe("cde");
            seg.ToString().ShouldNotBeSameAs(str);

            seg.String.ShouldBe("cde");
            seg.Array.ShouldBeNull();
        }

        [Test]
        public void ShouldCompareSegments()
        {
            new StringSegment("xxxa").CompareTo(new StringSegment("xxxb")).ShouldBeNegative();
            new StringSegment("xxxb").CompareTo(new StringSegment("xxxa")).ShouldBePositive();
            new StringSegment("xxxb").CompareTo(new StringSegment("xxxb")).ShouldBe(0);
        }

        [Test]
        public void ShouldReturnNullRefIsEmpty()
        {
            Unsafe.AreSame(ref Unsafe.AsRef(in default(StringSegment).First), ref Unsafe.NullRef<char>());
            default(StringSegment).Span.Length.ShouldBe(0);
        }

        [Test]
        public void ShouldReturnEmptyToStringIsEmpty()
        {
            default(StringSegment).ToString().ShouldBe(string.Empty);
        }

        [Test]
        public void ShouldCheckForNullOrEmpty()
        {
            StringSegment seg = " \n";
            seg.IsEmpty.ShouldBeFalse();
            seg.IsEmptyOrWhiteSpace.ShouldBeTrue();

            seg = "";
            seg.IsEmpty.ShouldBeTrue();
            seg.IsEmptyOrWhiteSpace.ShouldBeTrue();

            seg = new StringSegment("asd", 2, 0);
            seg.IsEmpty.ShouldBeTrue();
            seg.IsEmptyOrWhiteSpace.ShouldBeTrue();
        }

        [Test]
        public void ShouldCheckReferenceEquality()
        {
            var seg = new StringSegment("asd", 1, 1);
            var seg1 = new StringSegment("asd", 1, 1);
            seg.ReferenceEquals(seg1).ShouldBeTrue();
        }

        [Test]
        public void ShouldSlice()
        {
            var seg = new StringSegment("abcdefg");

            seg.Slice(1).ToString().ShouldBe("bcdefg");
            seg.Slice(7).ToString().ShouldBe("");
            Should.Throw<ArgumentOutOfRangeException>(() => seg.Slice(8));
            Should.Throw<ArgumentOutOfRangeException>(() => seg.Slice(-1));

            seg.Slice(1, 2).ToString().ShouldBe("bc");
            seg.Slice(5, 2).ToString().ShouldBe("fg");
            seg.Slice(7, 0).ToString().ShouldBe("");
            Should.Throw<ArgumentOutOfRangeException>(() => seg.Slice(6, 2));
            Should.Throw<ArgumentOutOfRangeException>(() => seg.Slice(-1, 2));
        }

        [Test, Explicit("benchmark")]
        public void SlicePerf()
        {
            var seg = new StringSegment("abcdefghijklmop");
            StringSegment seg1;
            var lenSum = 0;
            var count = 10_000_000;
            for (int _ = 0; _ < 10; _++)
            {
                using (Benchmark.Run("Slice", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        seg1 = seg.Slice((i & 3), 1 + (i & 5));
                        lenSum += seg1.Length;
                    }
                }
            }
            lenSum.ShouldBeGreaterThan(count);
            Benchmark.Dump();
        }
    }
}
