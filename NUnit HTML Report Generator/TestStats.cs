// Copyright (c) 2016 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Jatech.NUnit
{
	public class TestStats
	{
		public TestStats()
		{
			Platform = new StringBuilder();
		}

		public string Name { get; set; }
		public int Tests { get; set; }
		public int Errors { get; set; }
		public int Failures { get; set; }
		public int NotRun { get; set; }
		public int Inconclusive { get; set; }
		public int Ignored { get; set; }
		public int Skipped { get; set; }
		public int Invalid { get; set; }
		public StringBuilder Platform { get; private set; }
		public DateTime Date { get; set; }

		public Decimal Percentage
		{
			get
			{
				// Calculate the success rate
				if (Tests > 0)
				{
					int failures = Errors + Failures;
					return decimal.Round(decimal.Divide(failures, Tests) * 100, 1);
				}

				return 0;
			}
		}

		public static TestStats operator +(TestStats a, TestStats b)
		{
			var testStats = new TestStats();
			a = a ?? new TestStats();
			b = b ?? new TestStats();

			testStats.Tests = a.Tests + b.Tests;
			testStats.Errors = a.Errors + b.Errors;
			testStats.Failures = a.Failures + b.Failures;
			testStats.NotRun = a.NotRun + b.NotRun;
			testStats.Inconclusive = a.Inconclusive + b.Inconclusive;
			testStats.Ignored = a.Ignored + b.Ignored;
			testStats.Skipped = a.Skipped + b.Skipped;
			testStats.Invalid = a.Invalid + b.Invalid;

			testStats.Platform.Append(a.Platform);
			foreach (var part in b.Platform.ToString().Split(','))
			{
				if (!testStats.Platform.ToString().Contains(part.Trim()))
				{
					if (testStats.Platform.Length > 0)
						testStats.Platform.Append(", ");
					testStats.Platform.Append(part);
				}
			}
			return testStats;
		}

		public static TestStats Parse(XElement doc)
		{
			var test = new TestStats();
			test.Name = doc.Attribute("name").Value;
			test.Tests = int.Parse(!string.IsNullOrEmpty(doc.Attribute("total").Value) ? doc.Attribute("total").Value : "0");
			test.Errors = int.Parse(!string.IsNullOrEmpty(doc.Attribute("errors").Value) ? doc.Attribute("errors").Value : "0");
			test.Failures = int.Parse(!string.IsNullOrEmpty(doc.Attribute("failures").Value) ? doc.Attribute("failures").Value : "0");
			test.NotRun = int.Parse(!string.IsNullOrEmpty(doc.Attribute("not-run").Value) ? doc.Attribute("not-run").Value : "0");
			test.Inconclusive = int.Parse(!string.IsNullOrEmpty(doc.Attribute("inconclusive").Value) ? doc.Attribute("inconclusive").Value : "0");
			test.Ignored = int.Parse(!string.IsNullOrEmpty(doc.Attribute("ignored").Value) ? doc.Attribute("ignored").Value : "0");
			test.Skipped = int.Parse(!string.IsNullOrEmpty(doc.Attribute("skipped").Value) ? doc.Attribute("skipped").Value : "0");
			test.Invalid = int.Parse(!string.IsNullOrEmpty(doc.Attribute("invalid").Value) ? doc.Attribute("invalid").Value : "0");
			test.Date = DateTime.Parse(string.Format("{0} {1}", doc.Attribute("date").Value, doc.Attribute("time").Value));
			test.Platform.Append(doc.Element("environment").Attribute("platform").Value);
			return test;
		}

		public static TestStats CalculateFromFixture(XElement doc)
		{
			var test = new TestStats();
			test.Name = doc.Attribute("name").Value;
			var testcases = doc.Descendants("test-case");
			test.Tests = testcases.Count(x => x.Attribute("executed").Value.ToLower() == "true");
			test.NotRun = testcases.Count(x => x.Attribute("executed").Value.ToLower() == "false");
			test.Errors = testcases.Count(x => x.Attribute("result").Value.ToLower() == "error");
			test.Failures = testcases.Count(x => x.Attribute("result").Value.ToLower() == "failure");
			test.Inconclusive = testcases.Count(x => x.Attribute("result").Value.ToLower() == "inconclusive");
			test.Ignored = testcases.Count(x => x.Attribute("result").Value.ToLower() == "ignored");
			test.Skipped = testcases.Count(x => x.Attribute("result").Value.ToLower() == "skipped");
			test.Invalid = testcases.Count(x => x.Attribute("result").Value.ToLower() == "notrunnable");
			return test;
		}
	}
}

