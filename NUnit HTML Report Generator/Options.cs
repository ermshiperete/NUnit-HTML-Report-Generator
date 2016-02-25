// Copyright (c) 2016 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace Jatech.NUnit
{
	public class Options
	{
		private Options()
		{
		}

		[Option('o', "output", HelpText = "Output file (default: NUnitOutput.html)")]
		public string OutputFile { get; set; }

		[Option('f', "force", HelpText = "Overwrite existing output file")]
		public bool OverwriteFile { get; set; }

		[Option('v', "verbose", HelpText = "Output status messages")]
		public bool Verbose { get; set; }

		[Option('h', "help", HelpText = "Display this help")]
		public bool ShowHelp { get; set; }

		[ValueList(typeof(List<string>))]
		public List<string> Inputs { get; set; }

		public string GetUsage()
		{
			var help = new HelpText
			{
				Heading = new HeadingInfo("NUnitHTMLReportGenerator"),
				Copyright = new CopyrightInfo("Jatech Limited", 2014),
				AdditionalNewLineAfterOption = false,
				AddDashesToOption = true
			};
			help.AddOptions(this);
			return help;
		}

		public static Options ParseCommandLineArgs(string[] args)
		{
			var options = new Options();
			if (Parser.Default.ParseArguments(args, options))
			{
				if (!options.ShowHelp && options.Inputs.Count > 0)
				{
					return options;
				}
			}
			// Display the default usage information
			Console.WriteLine(options.GetUsage());
			return null;
		}
	}
}

