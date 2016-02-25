#region File Header
// <copyright application="NUnit HTML Report Generator" file="Program.cs" company="Jatech Limited">
// Copyright (c) 2014 Jatech Limited. All rights reserved.
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
// </copyright>
// <author>luke-browning</author>
// <date>26/06/2014</date>
// <summary>
// Console application to convert NUnit XML results file to
// a standalone HTML page based on Bootstrap 3
// </summary>
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace Jatech.NUnit
{
	/// <summary>
	/// The program.
	/// </summary>
	public static class Program
	{
		#region Private Constants

		/// <summary>
		/// Regular expression for acceptable characters in html id.
		/// </summary>
		private static readonly Regex Regex = new Regex("[^a-zA-Z0-9 -]");

		private static int _fileCount = 0;

		private static TestStats TotalTest;
		#endregion

		#region Main

		/// <summary>
		/// Main entry-point for this application.
		/// </summary>
		/// <param name="args">Array of command-line argument strings.</param>
		static void Main(string[] args)
		{
			var html = new StringBuilder();

			var options = Options.ParseCommandLineArgs(args);
			if (options == null)
				return;

			if (options.OutputFile == null)
			{
				if (options.Inputs.Count == 1)
				{
					// Output file with the same name in the same folder
					// with a html extension
					options.OutputFile = Path.ChangeExtension(options.Inputs[0], "html");
				}
				else if (options.Inputs.Count == 2)
				{
					options.OutputFile = options.Inputs[1];
					options.Inputs.RemoveAt(1);
				}
				else
				{
					options.OutputFile = "NUnitOutput.html";
				}
			}

			// Check input file exists and output file doesn't
			if (!CheckInputsAndOutputFile(options.Inputs, options.OutputFile, options.OverwriteFile))
				return;

			// Generate the HTML page
			html.Append(GetHTML5Header("Results"));
			var innerHtml = new StringBuilder();
			foreach (var input in options.Inputs)
				innerHtml.Append(ProcessFile(input, options.Verbose));
			html.Append(GetTotalSummary());
			html.Append(innerHtml);
			html.Append(GetHTML5Footer());

			// Save HTML to the output file
			Console.WriteLine("Writing output to {0}", options.OutputFile);
			File.WriteAllText(options.OutputFile, html.ToString());
		}

		#endregion

		#region Private Methods

		#region File Access

		/// <summary>
		/// Check input and output file existence
		/// Input file should exist, output file should not
		/// </summary>
		/// <param name="inputs">The input file names</param>
		/// <param name="output">The output name</param>
		/// <param name="overwriteOutput">Overwrite the output file</param>
		/// <returns>
		/// true if it succeeds, false if it fails.
		/// </returns>
		private static bool CheckInputsAndOutputFile(List<string> inputs, string output, bool overwriteOutput)
		{
			bool ok = true;

			foreach (var file in inputs)
			{
				if (!File.Exists(file))
				{
					Console.WriteLine("File '{0}' does not exist", file);
					ok = false;
				}
			}
			if (File.Exists(output) && !overwriteOutput)
			{
				Console.WriteLine(string.Format("Output file '{0}' already exists", output));
				ok = false;
			}

			return ok;
		}

		#endregion

		#region Processing

		private static string AddStatisticsCell<T>(string text, T val, string statClass, string valClass)
		{
			return string.Format("<div class=\"col-md-2 col-sm-4 col-xs-6 text-center\"><div class=\"{3}\">{0}</div><div class=\"{2}\">{1}</div></div>",
				text, val, valClass, statClass);
		}

		private static string AddStatisticsCell(string text, int val, string statClass, string valClass, bool colorify)
		{
			return AddStatisticsCell(text, val, statClass, (colorify && val > 0) ? ("text-danger " + valClass) : valClass);
		}

		private static string AddStatistics(TestStats test, string statClass = "stat", string valClass = "val")
		{
			var html = new StringBuilder();
			html.AppendLine(AddStatisticsCell("Tests", test.Tests, statClass, valClass + " ignore-val", false));
			html.AppendLine(AddStatisticsCell("Failures", test.Failures, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Errors", test.Errors, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Not\u00A0Run", test.NotRun, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Inconclusive", test.Inconclusive, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Ignored", test.Ignored, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Skipped", test.Skipped, statClass, valClass, true));
			html.AppendLine(AddStatisticsCell("Invalid", test.Invalid, statClass, valClass, true));
			if (test.Date.Ticks > 0)
			{
				html.AppendLine(AddStatisticsCell("Date", test.Date.ToString("d MMM"), statClass, valClass));
				html.AppendLine(AddStatisticsCell("Time", test.Date.ToShortTimeString(), statClass, valClass));
			}
			if (test.Platform.Length > 0)
				html.AppendLine(AddStatisticsCell("Platform", test.Platform.ToString(), statClass, valClass));
			html.AppendLine(AddStatisticsCell("Success", string.Format("{0}%", 100 - test.Percentage),
				statClass, valClass));
			return html.ToString();
		}

		/// <summary>
		/// Process the results file.
		/// </summary>
		/// <param name="file">The filename of the XML results file to parse.</param>
		/// <param name = "verbose"><c>true</c> to display status message, otherwise <c>false</c></param>
		/// <returns>
		/// HTML page content.
		/// </returns>
		private static string ProcessFile(string file, bool verbose)
		{
			if (verbose)
				Console.WriteLine("Processing {0}", file);
			var html = new StringBuilder();
			XElement doc = XElement.Load(file);

			_fileCount++;

			// Load summary values
			var test = TestStats.Parse(doc);
			TotalTest += test;

			// Summary panel
			html.AppendLine(string.Format("<div class=\"accordion\" id=\"accordion{0}\">", _fileCount));
			html.AppendLine("<div class=\"accordion-heading\">");
			html.AppendLine(string.Format("<a class=\"accordion-toggle\" data-toggle=\"collapse\" data-parent=\"#accordion{0}\" href=\"#collapse{0}\">",
				_fileCount));
			html.AppendLine(
				string.Format("<div class=\"panel-heading\">{0} - Tests: {1} - Failures: <span class=\"{3}\">{2}</span> " +
				"- Errors: <span class=\"{5}\">{4}</span> - Ignored: <span class=\"{7}\">{6}</span> " +
				"- Skipped: <span class=\"{9}\">{8}</span></div>",
					test.Name, test.Tests,
					test.Failures, test.Failures > 0 ? "text-danger" : string.Empty,
					test.Errors, test.Errors > 0 ? "text-danger" : string.Empty,
					test.Ignored, test.Ignored > 0 ? "text-danger" : string.Empty,
					test.Skipped, test.Skipped > 0 ? "text-danger" : string.Empty));
			html.AppendLine("</a></div>");
			html.AppendLine(string.Format("<div id=\"collapse{0}\" class=\"accordion-body collapse\">", _fileCount));
			html.AppendLine("<div class=\"accordion-inner\">");
			html.AppendLine("<div class=\"row\">");
			html.AppendLine("<div class=\"col-md-12\">");
			html.AppendLine("<div class=\"panel panel-default\">");
			html.AppendLine(string.Format("<div class=\"panel-heading\">Summary - <small>{0}</small></div>", test.Name));
			html.AppendLine("<div class=\"panel-body\">");

			html.Append(AddStatistics(test));

			// End summary panel
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");

			// Process test fixtures
			html.Append(ProcessFixtures(doc.Descendants("test-suite").Where(x => x.Attribute("type").Value == "TestFixture")));

			// End container
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");

			return html.ToString();
		}

		/// <summary>
		/// Process the test fixtures.
		/// </summary>
		/// <param name="fixtures">The test-fixture elements.</param>
		/// <returns>
		/// Fixtures as HTML.
		/// </returns>
		private static string ProcessFixtures(IEnumerable<XElement> fixtures)
		{
			StringBuilder html = new StringBuilder();
			int index = 0;
			string fixtureName, fixtureNamespace, fixtureTime, fixtureResult, fixtureReason;

			// Loop through all of the fixtures
			foreach (var fixture in fixtures)
			{
				// Load fixture details
				fixtureName = fixture.Attribute("name").Value;
				fixtureNamespace = GetElementNamespace(fixture);
				fixtureTime = fixture.Attribute("time") != null ? fixture.Attribute("time").Value : string.Empty;
				fixtureResult = fixture.Attribute("result").Value;
				fixtureReason = fixture.Element("reason") != null ? fixture.Element("reason").Element("message").Value : string.Empty;

				html.AppendLine("<div class=\"col-md-3\">");
				html.AppendLine("<div class=\"panel ");

				// Colour code panels
				switch (fixtureResult.ToLower())
				{
					case "success":
						html.Append("panel-success");
						break;
					case "ignored":
						html.Append("panel-info");
						break;
					case "failure":
					case "error":
						html.Append("panel-danger");
						break;
					default:
						html.Append("panel-default");
						break;
				}

				html.Append("\">");
				html.AppendLine("<div class=\"panel-heading\">");
				html.AppendLine(string.Format("{0} - <br><small>{1}</small><small class=\"pull-right\">{2}s</small>", fixtureName, fixtureNamespace, fixtureTime));

				// If the fixture has a reason, display an icon 
				// on the top of the panel with a tooltip containing 
				// the reason
				if (!string.IsNullOrEmpty(fixtureReason))
				{
					html.AppendLine(string.Format("<span class=\"glyphicon glyphicon-info-sign pull-right info hidden-print\" data-toggle=\"tooltip\" title=\"{0}\"></span>", fixtureReason));
				}

				html.AppendLine("</div>");
				html.AppendLine("<div class=\"panel-body\">");

				html.AppendLine("<div class=\"row\">");
				var test = TestStats.CalculateFromFixture(fixture);
				html.Append(AddStatistics(test, "smallstat", ""));
				html.AppendLine("</div><div class=\"row\">");

				// Generate a unique id for the modal dialog
				string modalId = string.Format("modal-{0}-{1}", Regex.Replace(HttpUtility.UrlEncode(fixtureName), string.Empty), index++);

				html.AppendLine("<div class=\"text-center\" style=\"font-size: 1.5em;\">");

				// Add a colour coded link to the modal dialog
				switch (fixtureResult.ToLower())
				{
					case "success":
						html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-success no-underline\">", modalId));
						html.AppendLine("<span class=\"glyphicon glyphicon-ok-sign\"></span>");
						html.AppendLine("<span class=\"test-result\">Success</span>");
						html.AppendLine("</a>");
						break;
					case "ignored":
						html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-info no-underline\">", modalId));
						html.AppendLine("<span class=\"glyphicon glyphicon-info-sign\"></span>");
						html.AppendLine("<span class=\"test-result\">Ignored</span>");
						html.AppendLine("</a>");
						break;
					case "notrunnable":
						html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-default no-underline\">", modalId));
						html.AppendLine("<span class=\"glyphicon glyphicon-remove-sign\"></span>");
						html.AppendLine("<span class=\"test-result\">Not Runnable</span>");
						html.AppendLine("</a>");
						break;
					case "failure":
					case "error":
						html.AppendLine(string.Format("<a href=\"#{0}\" role=\"button\" data-toggle=\"modal\" class=\"text-danger no-underline\">", modalId));
						html.AppendLine("<span class=\"glyphicon glyphicon-exclamation-sign\"></span>");
						html.AppendLine("<span class=\"test-result\">Failed</span>");
						html.AppendLine("</a>");
						break;
					default:
						break;
				}

				html.AppendLine("</div>");

				// Generate a printable view of the fixtures
				html.AppendLine(GeneratePrintableView(fixture, fixtureReason));

				// Generate the modal dialog that will be shown
				// if the user clicks on the test-fixtures
				html.AppendLine(GenerateFixtureModal(fixture, modalId, fixtureName, fixtureReason));

				html.AppendLine("</div>");
				html.AppendLine("</div>");
				html.AppendLine("</div>");
				html.AppendLine("</div>");
			}

			return html.ToString();
		}

		/// <summary>
		/// Gets an elements namespace.
		/// </summary>
		/// <param name="element">The element.</param>
		/// <returns>
		/// The element namespace.
		/// </returns>
		private static string GetElementNamespace(XElement element)
		{
			// Move up the tree to get the parent elements
			var namespaces = element.Ancestors("test-suite").Where(x => x.Attribute("type").Value.ToLower() == "namespace");

			// Get the namespace
			return string.Join(".", namespaces.Reverse().Select(x => x.Attribute("name").Value));
		}

		#endregion

		#region HTML Helpers

		/// <summary>
		/// Generates a printable view of test cases.
		/// </summary>
		/// <param name="fixture">The test fixture.</param>
		/// <param name="warningMessage">Warning message to display.</param>
		/// <returns>
		/// The printable view as HTML.
		/// </returns>
		private static string GeneratePrintableView(XElement fixture, string warningMessage)
		{
			StringBuilder html = new StringBuilder();

			string name, result;
			html.AppendLine("<div class=\"visible-print printed-test-result\">");

			// Display a warning message if set
			if (!string.IsNullOrEmpty(warningMessage))
			{
				html.AppendLine(string.Format("<div class=\"alert alert-warning\"><strong>Warning:</strong> {0}</div>", warningMessage));
			}

			// Loop through test cases in the fixture
			foreach (var testCase in fixture.Descendants("test-case"))
			{
				// Get test case properties
				name = testCase.Attribute("name").Value;
				result = testCase.Attribute("result").Value;

				// Remove namespace if in name
				name = name.Substring(name.LastIndexOf('.') + 1, name.Length - name.LastIndexOf('.') - 1);

				// Create colour coded panel based on result
				html.AppendLine("<div class=\"panel ");

				switch (result.ToLower())
				{
					case "success":
						html.Append("panel-success");
						break;
					case "ignored":
						html.Append("panel-info");
						break;
					case "failure":
					case "error":
						html.Append("panel-danger");
						break;
					default:
						html.Append("panel-default");
						break;
				}

				html.Append("\">");

				html.AppendLine("<div class=\"panel-heading\">");
				html.AppendLine("<h4 class=\"panel-title\">");
				html.AppendLine(name);
				html.AppendLine("</h4>");
				html.AppendLine("</div>");
				html.AppendLine("<div class=\"panel-body\">");
				html.AppendLine(string.Format("<div><strong>Result:</strong> {0}</div>", result));

				// Add failure messages if available
				if (testCase.Elements("failure").Count() == 1)
				{
					html.AppendLine(string.Format("<div><strong>Message:</strong> {0}</div>", testCase.Element("failure").Element("message").Value));
					html.AppendLine(string.Format("<div><strong>Stack Trace:</strong> <pre>{0}</pre></div>", testCase.Element("failure").Element("stack-trace").Value));
				}

				html.AppendLine("</div>");
				html.AppendLine("</div>");
			}

			html.AppendLine("</div>");

			return html.ToString();
		}

		/// <summary>
		/// Generates a modal dialog to display the test-cases in a fixture.
		/// </summary>
		/// <param name="fixture">The fixture element.</param>
		/// <param name="modalId">Identifier for the modal dialog</param>
		/// <param name="title">The dialog title.</param>
		/// <param name="warningMessage">The warning message.</param>
		/// <returns>
		/// The dialogs HTML.
		/// </returns>
		private static string GenerateFixtureModal(XElement fixture, string modalId, string title, string warningMessage)
		{
			var html = new StringBuilder();

			html.AppendLine(string.Format("<div class=\"modal fade\" id=\"{0}\" tabindex=\"-1\" role=\"dialog\" aria-labelledby=\"myModalLabel\" aria-hidden=\"true\">", modalId));
			html.AppendLine("<div class=\"modal-dialog\">");
			html.AppendLine("<div class=\"modal-content\">");
			html.AppendLine("<div class=\"modal-header\">");
			html.AppendLine("<button type=\"button\" class=\"close\" data-dismiss=\"modal\" aria-hidden=\"true\">&times;</button>");
			html.AppendLine(string.Format("<h4 class=\"modal-title\" id=\"myModalLabel\">{0}</h4>", title));
			html.AppendLine("</div>");
			html.AppendLine("<div class=\"modal-body\">");

			int i = 0;
			string name, result;
			html.AppendLine(string.Format("<div class=\"panel-group no-bottom-margin\" id=\"{0}-accordion\">", modalId));

			if (!string.IsNullOrEmpty(warningMessage))
			{
				html.AppendLine(string.Format("<div class=\"alert alert-warning\"><strong>Warning:</strong> {0}</div>", warningMessage));
			}

			// Add each test case to the dialog, colour 
			// coded based on the result
			foreach (var testCase in fixture.Descendants("test-case"))
			{
				// Get properties
				name = testCase.Attribute("name").Value;
				result = testCase.Attribute("result").Value;

				// Remove namespace if included
				name = name.Substring(name.LastIndexOf('.') + 1, name.Length - name.LastIndexOf('.') - 1);

				html.AppendLine("<div class=\"panel ");

				switch (result.ToLower())
				{
					case "success":
						html.Append("panel-success");
						break;
					case "ignored":
						html.Append("panel-info");
						break;
					case "failure":
					case "error":
						html.Append("panel-danger");
						break;
					default:
						html.Append("panel-default");
						break;
				}

				html.Append("\">");

				html.AppendLine("<div class=\"panel-heading\">");
				html.AppendLine("<h4 class=\"panel-title\">");
				html.AppendLine(string.Format("<a data-toggle=\"collapse\" data-parent=\"#{1}\" href=\"#{1}-accordion-{2}\">{0}</a>", name, modalId, i));
				html.AppendLine("</h4>");
				html.AppendLine("</div>");
				html.AppendLine(string.Format("<div id=\"{0}-accordion-{1}\" class=\"panel-collapse collapse\">", modalId, i++));
				html.AppendLine("<div class=\"panel-body\">");
				html.AppendLine(string.Format("<div><strong>Result:</strong> {0}</div>", result));
				if (result.ToLower() == "success")
				{
					var asserts = testCase.Attribute("asserts").Value;
					html.AppendLine(string.Format("<div><strong>Asserts:</strong> {0}</div>", asserts));
				}

				// Add failure messages if available
				if (testCase.Elements("failure").Count() == 1)
				{
					html.AppendLine(string.Format("<div><strong>Message:</strong> {0}</div>", testCase.Element("failure").Element("message").Value));
					html.AppendLine(string.Format("<div><strong>Stack Trace:</strong> <pre>{0}</pre></div>", testCase.Element("failure").Element("stack-trace").Value));
				}

				// Add reason if available
				if (testCase.Elements("reason").Count() == 1)
				{
					html.AppendLine(string.Format("<div><strong>Reason:</strong> {0}</div>", testCase.Element("reason").Element("message").Value));
				}

				html.AppendLine("</div>");
				html.AppendLine("</div>");
				html.AppendLine("</div>");
			}

			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("<div class=\"modal-footer\">");
			html.AppendLine("<button type=\"button\" class=\"btn btn-primary\" data-dismiss=\"modal\">Close</button>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");

			return html.ToString();
		}
		#endregion

		#region HTML5 Template

		/// <summary>
		/// Gets the HTML 5 header.
		/// </summary>
		/// <param name="title">The title for the header.</param>
		/// <returns>
		/// The HTML 5 header.
		/// </returns>
		private static string GetHTML5Header(string title)
		{
			StringBuilder header = new StringBuilder();
			header.AppendLine("<!doctype html>");
			header.AppendLine("<html lang=\"en\">");
			header.AppendLine("  <head>");
			header.AppendLine("    <meta charset=\"utf-8\">");
			header.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=1\" />"); // Set for mobile
			header.AppendLine(string.Format("    <title>{0}</title>", title));

			// Add custom scripts
			header.AppendLine("    <script>");

			// Include jQuery in the page
			header.AppendLine(Properties.Resources.jQuery);
			header.AppendLine("    </script>");
			header.AppendLine("    <script>");

			// Include Bootstrap in the page
			header.AppendLine(Properties.Resources.BootstrapJS);
			header.AppendLine("    </script>");
			header.AppendLine("    <script type=\"text/javascript\">");
			header.AppendLine("    $(document).ready(function() { ");
			header.AppendLine("        $('[data-toggle=\"tooltip\"]').tooltip({'placement': 'bottom'});");
			header.AppendLine("    });");
			header.AppendLine("    </script>");

			// Add custom styles
			header.AppendLine("    <style>");

			// Include Bootstrap CSS in the page
			header.AppendLine(Properties.Resources.BootstrapCSS);
			header.AppendLine("    .page { margin: 15px 0; }");
			header.AppendLine("    .no-bottom-margin { margin-bottom: 0; }");
			header.AppendLine("    .printed-test-result { margin-top: 15px; }");
			header.AppendLine("    .reason-text { margin-top: 15px; }");
			header.AppendLine("    .scroller { overflow: scroll; }");
			header.AppendLine("    @media print { .panel-collapse { display: block !important; } }");
			header.AppendLine("    .val { font-size: 38px; font-weight: bold; margin-top: -10px; }");
			header.AppendLine("    .smallstat { font-size: 7px; }");
			header.AppendLine("    .stat { font-weight: 800; text-transform: uppercase; font-size: 0.85em; color: #6F6F6F; }");
			header.AppendLine("    .test-result { display: block; }");
			header.AppendLine("    .no-underline:hover { text-decoration: none; }");
			header.AppendLine("    .text-default { color: #555; }");
			header.AppendLine("    .text-default:hover { color: #000; }");
			header.AppendLine("    .info { color: #888; }");
			header.AppendLine("    </style>");
			header.AppendLine("  </head>");
			header.AppendLine("  <body>");
			// Container
			header.AppendLine("<div class=\"container-fluid page\">");

			return header.ToString();
		}

		private static string GetTotalSummary()
		{
			// Summary panel
			var html = new StringBuilder();
			html.AppendLine("<div class=\"accordion\" id=\"accordion0\">");
			html.AppendLine("<div class=\"accordion-heading\">");
			html.AppendLine("<a class=\"accordion-toggle\" data-toggle=\"collapse\" data-parent=\"#accordion0\" href=\"#collapse0\">");
			html.AppendLine("<div class=\"row\">");
			html.AppendLine("<div class=\"col-md-12\">");
			html.AppendLine("<div class=\"panel panel-default\">");
			html.AppendLine("<div class=\"panel-heading\">Total Summary</div>");
			html.AppendLine("<div class=\"panel-body\">");

			html.AppendLine(string.Format("<div class=\"col-md-2 col-sm-4 col-xs-6 text-center\"><div class=\"stat\">Test Suites</div><div class=\"val\">{0}</div></div>", _fileCount));
			html.Append(AddStatistics(TotalTest));
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</div>");
			html.AppendLine("</a></div>");
			html.AppendLine("<div id=\"collapse0\" class=\"accordion-body collapse in\">");
			html.AppendLine("<div class=\"accordion-inner\">");
			return html.ToString();
		}

		/// <summary>
		/// Gets the HTML 5 footer.
		/// </summary>
		/// <returns>
		/// A HTML 5 footer.
		/// </returns>
		private static string GetHTML5Footer()
		{
			StringBuilder footer = new StringBuilder();
			footer.AppendLine("</div>");
			footer.AppendLine("</div>");
			footer.AppendLine("</div>");
			footer.AppendLine("  </body>");
			footer.AppendLine("</html>");

			return footer.ToString();
		}

		#endregion

		#endregion
	}
}
