using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;

namespace Spreads.DB.Tests {

	[TestFixture]
	public class SCMTests {

		private const int _small = 500;
		private const int _big = 100000;
		private Random _rng = new System.Random();

		[SetUp]
		public void Init() {
		}

		public Dictionary<string, IReadOnlyOrderedMap<DateTime, double>> GetImplementation() {
			var implemetations = new Dictionary<string, IReadOnlyOrderedMap<DateTime, double>>();

			var scm_irregular_small = new SortedChunkedMap<DateTime, double>();
			var scm_irregular_big = new SortedChunkedMap<DateTime, double>();
			var scm_regular_small = new SortedChunkedMap<DateTime, double>();
			var scm_regular_big = new SortedChunkedMap<DateTime, double>();

			scm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);
			scm_irregular_big.Add(DateTime.Today.AddDays(-2), -2.0);

			for (int i = 0; i < _big; i++) {
				if (i < _small) {
					scm_irregular_small.Add(DateTime.Today.AddDays(i), i);
					scm_regular_small.Add(DateTime.Today.AddDays(i), i);
				}

				scm_irregular_big.Add(DateTime.Today.AddDays(i), i);
				scm_regular_big.Add(DateTime.Today.AddDays(i), i);
			}

			implemetations.Add("scm_irregular_small", scm_irregular_small);
			implemetations.Add("scm_regular_small", scm_regular_small);
			implemetations.Add("scm_irregular_big", scm_irregular_big);
			implemetations.Add("scm_regular_big", scm_regular_big);
			return implemetations;
		}

		[Test]
		public void ContentEqualsToExpected() {
			var maps = GetImplementation();
			Assert.AreEqual(maps["scm_irregular_small"][DateTime.Today.AddDays(-2)], -2.0);
			Assert.AreEqual(maps["scm_irregular_big"][DateTime.Today.AddDays(-2)], -2.0);

			for (int i = 0; i < _big; i++) {
				if (i < _small) {
					Assert.AreEqual(maps["scm_irregular_small"][DateTime.Today.AddDays(i)], i);
					Assert.AreEqual(maps["scm_regular_small"][DateTime.Today.AddDays(i)], i);
				}
				Assert.AreEqual(maps["scm_irregular_big"][DateTime.Today.AddDays(i)], i);
				Assert.AreEqual(maps["scm_regular_big"][DateTime.Today.AddDays(i)], i);
			}
			Assert.IsTrue(GetImplementation().Count > 0);
		}

		[Test]
		public void CouldCreateSCM() {

			var scm_irregular_small = new SortedChunkedMap<DateTime, double>();

			scm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);

			for (int i = 0; i < _small; i++) {
				scm_irregular_small.Add(DateTime.Today.AddDays(i), i);
			}
		}

		[Test]
		public void CouldCOmpareDates()
		{
			var dtc = KeyComparer.GetDefault<DateTime>();
			var neg =  dtc.Compare(DateTime.Today.AddDays(-2), DateTime.Today);
			var pos = dtc.Compare(DateTime.Today.AddDays(2), DateTime.Today.AddDays(-2));

			Console.WriteLine(neg);
			Assert.IsTrue(neg < 0);

			Console.WriteLine(pos);
			Assert.IsTrue(pos > 0);
		}

	}
}
