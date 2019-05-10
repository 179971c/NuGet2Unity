using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using CommandLine;
using UnityPacker;
using System.Text;
using System.Threading.Tasks;

namespace NuGet2Unity
{
	class Program
	{
		private static Options _options;

		static async Task Main(string[] args)
		{
			await Parser.Default.ParseArguments<Options>(args).MapResult(
				async o => await Run(o),
				async e => await Error(e)
			);
		}

		static async Task Run(Options opt)
		{
			ConsoleWriteLine($"Packaging {opt.Package} for Unity..." + Environment.NewLine, ConsoleColor.White);

			if(!VerifyOptions(opt))
				return;

			_options = opt;

			string workingDir;
			if(string.IsNullOrEmpty(opt.UnityProject))
				workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			else
				workingDir = opt.UnityProject;

			ConsoleWriteLine($"Working directory: {workingDir}", ConsoleColor.Gray, true);

			string nuGetDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			ConsoleWriteLine($"NuGet directory: {nuGetDir}", ConsoleColor.Gray, true);
			
			string pluginsDir = Path.Combine(workingDir, "Assets", "Plugins");
			Directory.CreateDirectory(pluginsDir);
			ConsoleWriteLine($"Plugins dir: {pluginsDir}", Console.BackgroundColor, true);

			ConsoleWrite($"Downloading package and dependencies...", ConsoleColor.Cyan);
			NuGetClient ngc = new NuGetClient();
			IEnumerable<string> files = await ngc.DownloadPackageAndDependenciesAsync(opt.Package, opt.Version, nuGetDir);
			ConsoleWriteLine($"OK", ConsoleColor.Green);

			foreach(string file in files)
			{
				if(!excludePackages.Contains(Path.GetFileNameWithoutExtension(file)))
				{
					ConsoleWriteLine($"Copying {Path.GetFileName(file)} to package", ConsoleColor.Gray, true);
					File.Copy(file, Path.Combine(pluginsDir, Path.GetFileName(file)));
				}
			}
			CreateLinkXml(pluginsDir);
			CreateUnityPackage(opt.Package, workingDir, opt);

			if(Debugger.IsAttached)
				Console.ReadKey();

			Cleanup(nuGetDir, string.IsNullOrEmpty(opt.UnityProject) ? workingDir : string.Empty);
		}

		private static bool VerifyOptions(Options opt)
		{
			if(string.IsNullOrEmpty(opt.OutputPath))
				opt.OutputPath = Directory.GetCurrentDirectory();
			else
				opt.OutputPath = Path.GetFullPath(opt.OutputPath);

			if(!string.IsNullOrEmpty(opt.UnityProject))
				opt.UnityProject = Path.GetFullPath(opt.UnityProject);

			return true;
		}

		private static void CreateLinkXml(string working)
		{
			string linkxml = @"<linker>
	<assembly fullname=""System.Core"">
		<type fullname=""System.Linq.Expressions.Interpreter.LightLambda"" preserve=""all"" />
	</assembly>
{0}</linker>";

			string template = "\t<assembly fullname=\"{0}\" preserve=\"all\" />\r\n";

			StringBuilder sb = new StringBuilder();

			string[] files = Directory.GetFiles(working, "*.dll");
			foreach(string file in files)
			{
				string assembly = Path.GetFileNameWithoutExtension(file);
				sb.AppendFormat(template, assembly);
			}

			string final = string.Format(linkxml, sb.ToString());
			File.WriteAllText(Path.Combine(working, "link.xml"), final);
		}

		private static bool CreateUnityPackage(string package, string working, Options opt)
		{
			ConsoleWrite("Creating Unity package...");

			string[] includeDirs = { "Assets" };
			Package p = Package.FromDirectory(working, package + "-" + opt.Version, opt.IncludeMeta, includeDirs);
			p.GeneratePackage(opt.OutputPath);
			ConsoleWriteLine("OK", ConsoleColor.Green);
			return true;
		}

		private static void Cleanup(string nuGetDir, string working)
		{
			ConsoleWrite("Cleaning up...");

			if(Directory.Exists(nuGetDir))
				Directory.Delete(nuGetDir, true);

			if(!string.IsNullOrEmpty(working) && Directory.Exists(working))
				Directory.Delete(working, true);

			ConsoleWriteLine("OK", ConsoleColor.Green);
		}

		static void ConsoleWrite(string text, ConsoleColor color = ConsoleColor.Cyan, bool verbose = false)
		{
			if(!verbose || (verbose && _options.Verbose))
			{
				ConsoleColor originalColor = Console.ForegroundColor;
				Console.ForegroundColor = color;
				Console.Write(text);
				Console.ForegroundColor = originalColor;
			}
		}

		static void ConsoleWriteLine(string text, ConsoleColor color = ConsoleColor.Cyan, bool verbose = false)
		{
			if(!verbose || (verbose && _options.Verbose))
			{
				ConsoleColor originalColor = Console.ForegroundColor;
				Console.ForegroundColor = color;
				Console.WriteLine(text);
				Console.ForegroundColor = originalColor;
			}
		}

		static void ConsoleWriteError(string text)
		{
			ConsoleColor originalColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(text);
			Console.ForegroundColor = originalColor;
		}

		private static Task Error(IEnumerable<Error> errors)
		{
			foreach(Error e in errors)
				ConsoleWriteError(e.ToString());
			return Task.FromResult(false);
		}

		private static string[] excludePackages = {
			// system libs
			"NETStandard.Library",
			"Microsoft.NETCore.Platforms",
			"Microsoft.CSharp",

			// Unity-provided libs/shims
			"Microsoft.Win32.Primitives",
			"netstandard",
			"System.AppContext",
			"System.Collections.Concurrent",
			"System.Collections",
			"System.Collections.NonGeneric",
			"System.Collections.Specialized",
			"System.ComponentModel.Annotations",
			"System.ComponentModel",
			"System.ComponentModel.EventBasedAsync",
			"System.ComponentModel.Primitives",
			"System.ComponentModel.TypeConverter",
			"System.Console",
			"System.Data.Common",
			"System.Diagnostics.Contracts",
			"System.Diagnostics.Debug",
			"System.Diagnostics.FileVersionInfo",
			"System.Diagnostics.Process",
			"System.Diagnostics.StackTrace",
			"System.Diagnostics.TextWriterTraceListener",
			"System.Diagnostics.Tools",
			"System.Diagnostics.TraceSource",
			"System.Drawing.Primitives",
			"System.Dynamic.Runtime",
			"System.Globalization.Calendars",
			"System.Globalization",
			"System.Globalization.Extensions",
			"System.IO.Compression.ZipFile",
			"System.IO",
			"System.IO.FileSystem",
			"System.IO.FileSystem.DriveInfo",
			"System.IO.FileSystem.Primitives",
			"System.IO.FileSystem.Watcher",
			"System.IO.IsolatedStorage",
			"System.IO.MemoryMappedFiles",
			"System.IO.Pipes",
			"System.IO.UnmanagedMemoryStream",
			"System.Linq",
			"System.Linq.Expressions",
			"System.Linq.Parallel",
			"System.Linq.Queryable",
			"System.Net.Http.Rtc",
			"System.Net.NameResolution",
			"System.Net.NetworkInformation",
			"System.Net.Ping",
			"System.Net.Primitives",
			"System.Net.Requests",
			"System.Net.Security",
			"System.Net.Sockets",
			"System.Net.WebHeaderCollection",
			"System.Net.WebSockets.Client",
			"System.Net.WebSockets",
			"System.ObjectModel",
			"System.Reflection",
			"System.Reflection.Emit",
			"System.Reflection.Emit.ILGeneration",
			"System.Reflection.Emit.Lightweight",
			"System.Reflection.Extensions",
			"System.Reflection.Primitives",
			"System.Resources.Reader",
			"System.Resources.ResourceManager",
			"System.Resources.Writer",
			"System.Runtime.CompilerServices.VisualC",
			"System.Runtime",
			"System.Runtime.Extensions",
			"System.Runtime.Handles",
			"System.Runtime.InteropServices",
			"System.Runtime.InteropServices.RuntimeInformation",
			"System.Runtime.InteropServices.WindowsRuntime",
			"System.Runtime.Numerics",
			"System.Runtime.Serialization.Formatters",
			"System.Runtime.Serialization.Json",
			"System.Runtime.Serialization.Primitives",
			"System.Runtime.Serialization.Xml",
			"System.Security.Claims",
			"System.Security.Cryptography.Algorithms",
			"System.Security.Cryptography.Csp",
			"System.Security.Cryptography.Encoding",
			"System.Security.Cryptography.Primitives",
			"System.Security.Cryptography.X509Certificates",
			"System.Security.Principal",
			"System.Security.SecureString",
			"System.ServiceModel.Duplex",
			"System.ServiceModel.Http",
			"System.ServiceModel.NetTcp",
			"System.ServiceModel.Primitives",
			"System.ServiceModel.Security",
			"System.Text.Encoding",
			"System.Text.Encoding.Extensions",
			"System.Text.RegularExpressions",
			"System.Threading",
			"System.Threading.Overlapped",
			"System.Threading.Tasks",
			"System.Threading.Tasks.Parallel",
			"System.Threading.Thread",
			"System.Threading.ThreadPool",
			"System.Threading.Timer",
			"System.ValueTuple",
			"System.Xml.ReaderWriter",
			"System.Xml.XDocument",
			"System.Xml.XmlDocument",
			"System.Xml.XmlSerializer",
			"System.Xml.XPath",
			"System.Xml.XPath.XDocument",
		};
	}
}
