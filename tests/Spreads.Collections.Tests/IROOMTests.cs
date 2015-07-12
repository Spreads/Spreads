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
	public class IROOMTests
	{

		private const int _small = 500;
		private const int _big = 100000;
		private Random _rng = new System.Random();
		// most of all, we are interested in SortedMap and SCM, but test other implementations later
		private Dictionary<string,IReadOnlyOrderedMap<DateTime, double>> implemetations =
			new Dictionary<string,IReadOnlyOrderedMap<DateTime, double>>();


		[SetUp]
		public void Init()
		{
		}

		public Dictionary<string, IReadOnlyOrderedMap<DateTime, double>> GetImplementation() {

			var implemetations = new Dictionary<string, IReadOnlyOrderedMap<DateTime, double>>();
			var sm_irregular_small = new SortedMap<DateTime, double>();
			var sm_irregular_big = new SortedMap<DateTime, double>();
			var sm_regular_small = new SortedMap<DateTime, double>();
			var sm_regular_big = new SortedMap<DateTime, double>();

			var scm_irregular_small = new SortedChunkedMap<DateTime, double>();
			var scm_irregular_big = new SortedChunkedMap<DateTime, double>();
			var scm_regular_small = new SortedChunkedMap<DateTime, double>();
			var scm_regular_big = new SortedChunkedMap<DateTime, double>();

			sm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);
			sm_irregular_big.Add(DateTime.Today.AddDays(-2), -2.0);
			scm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);
			scm_irregular_big.Add(DateTime.Today.AddDays(-2), -2.0);

			for (int i = 0; i < _big; i++)
			{
				if (i < _small)
				{
					sm_irregular_small.Add(DateTime.Today.AddDays(i), i);
					sm_regular_small.Add(DateTime.Today.AddDays(i), i);
					scm_irregular_small.Add(DateTime.Today.AddDays(i), i);
					scm_regular_small.Add(DateTime.Today.AddDays(i), i);
				}

				sm_irregular_big.Add(DateTime.Today.AddDays(i), i);
				sm_regular_big.Add(DateTime.Today.AddDays(i), i);
				scm_irregular_big.Add(DateTime.Today.AddDays(i), i);
				scm_regular_big.Add(DateTime.Today.AddDays(i), i);
			}
			// SM regular
			implemetations.Add("sm_irregular_small", sm_irregular_small);
			implemetations.Add("sm_regular_small", sm_regular_small);
			implemetations.Add("scm_irregular_small", scm_irregular_small);
			implemetations.Add("scm_regular_small", scm_regular_small);
			implemetations.Add("sm_irregular_big", sm_irregular_big);
			implemetations.Add("sm_regular_big", sm_regular_big);
			implemetations.Add("scm_irregular_big", scm_irregular_big);
			implemetations.Add("scm_regular_big", scm_regular_big);
			return implemetations;
		}

		[Test]
		public void CouldCreateAllImplementations()
		{
			Assert.IsTrue(GetImplementation().Count > 0);
		}


		[Test]
		public void CouldCompressAndDecompressComplexObject()
		{
		}

	}
}
