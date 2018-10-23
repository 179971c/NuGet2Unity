using CommandLine;

namespace NuGet2Unity
{
	class Options
	{
		[Option('n', "nugetpackage", Required = true, HelpText = "NuGet package to repackage")]
		public string Package { get; set; }

		[Option('v', "version", HelpText = "Version of NuGet package to use")]
		public string Version { get; set; }

		[Option('p', "unityproject", HelpText = "Path to the Unity project to include with this package")]
		public string UnityProject { get; set; }

		[Option('m', "includemeta", HelpText = "Include .meta files from the Unity project")]
		public bool IncludeMeta { get; set; }

		[Option('o', "outputpath", Default = ".", HelpText = "Directory to save the .unitypackage")]
		public string OutputPath { get; set; }

		[Option("verbose", HelpText = "Maximum verbosity")]
		public bool Verbose { get; set; }

		[Option("skiplinkxml", HelpText = "Do not add a link.xml to package")]
		public bool SkipLinkXml { get; set; }
	}
}
