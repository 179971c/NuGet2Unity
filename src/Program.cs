using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using CommandLine;
using UnityPacker;
using System.Text;
using NuGet.Packaging;
using NuGet.Frameworks;
using System.Net;
using System.IO.Compression;

namespace NuGet2Unity
{
	class Program
	{
		private static Options _options;
		private static string NuGetUrl = "https://www.nuget.org/api/v2/package/{0}/{1}";

		private static string[] exclude = {
			// system libs
			"NETStandard.Library",
			"Microsoft.NETCore.Platforms",

			// Unity-provided libs (from <Unity>\Editor\Data\NetStandard\compat\2.0.0\shims\netstandard)
			"Microsoft.Win32.Primitives",
			"System.AppContext",
			"System.Collections.Concurrent",
			"System.Collections",
			"System.Collections.NonGeneric",
			"System.Collections.Specialized",
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
			"System.Diagnostics.Tracing",
			"System.Drawing.Primitives",
			"System.Dynamic.Runtime",
			"System.Globalization.Calendars",
			"System.Globalization",
			"System.Globalization.Extensions",
			"System.IO.Compression",
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
			"System.Net.Http",
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

		static void Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args)
				.WithParsed(o => Run(o))
				.WithNotParsed(e => Error(e));
		}

		static void Run(Options opt)
		{
			ConsoleWriteLine($"Packaging {opt.Package} for Unity..." + Environment.NewLine, ConsoleColor.White);

			if(!VerifyOptions(opt))
				return;

			_options = opt;

			string working;
			if(string.IsNullOrEmpty(opt.UnityProject))
				working = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			else
				working = opt.UnityProject;

			ConsoleWriteLine($"Working directory: {working}", ConsoleColor.Gray, true);

			string temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(temp);
			ConsoleWriteLine($"Temp directory: {temp}", ConsoleColor.Gray, true);

			string plugins = Path.Combine(working, "Assets", "Plugins");
			Directory.CreateDirectory(plugins);
			ConsoleWriteLine($"Plugins dir: {plugins}", Console.BackgroundColor, true);

			DownloadPackage(opt.Package, opt.Version, temp);

			bool success = CopyFiles(temp, plugins, opt);

			if(success)
				CreateUnityPackage(opt.Package, working, opt.IncludeMeta, opt.OutputPath);

			Cleanup(temp, string.IsNullOrEmpty(opt.UnityProject) ? working : string.Empty);

			if(Debugger.IsAttached)
				Console.ReadKey();
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

		private static void DownloadPackage(string package, string version, string temp)
		{
			string path = Path.Combine(temp, package);
			string url = string.Format(NuGetUrl, package, version ?? string.Empty);
			if(!Directory.Exists(path))
			{
				ConsoleWrite($"Downloading and extracting {package}...");
				WebClient wc = new WebClient();
				byte[] buff = wc.DownloadData(url);
				MemoryStream ms = new MemoryStream(buff);
				ZipArchive za = new ZipArchive(ms);
				za.ExtractToDirectory(Path.Combine(temp, package));
				ConsoleWriteLine("Complete", ConsoleColor.Green);
			}
		}

		private static bool CopyFiles(string temp, string working, Options opt)
		{
			// delete any existing working files
			foreach(string file in Directory.GetFiles(working))
				File.Delete(file);
			foreach(string dir in Directory.GetDirectories(working))
				Directory.Delete(dir, true);

			string wsa = Path.Combine(working, "WSA");

			//if(!opt.SkipWsa)
			//	Directory.CreateDirectory(wsa);

			string[] dependencies = GetDependencies(temp, _options.Package).ToArray();

			ConsoleWrite("Copying files...");

			foreach (string dependency in dependencies)
			{
				if(!opt.SkipJsonFix)
					CreateLinkXml(working);

				string lib = Path.Combine(temp, dependency, "lib");
				if (Directory.Exists(lib))
				{
					string[] ns = Directory.GetDirectories(lib, "netstandard*");
					if(ns != null && ns.Any())
					{
						ns = ns.OrderByDescending(i => i).ToArray();

						string[] files = Directory.GetFiles(ns[0], "*.dll");
						foreach (string file in files)
						{
							string dest = Path.Combine(working, Path.GetFileName(file));
							ConsoleWrite($"\r\n-> Copying {file} to {dest}", ConsoleColor.Gray, true);
							File.Copy(file, dest, true);
						}
					}
					else
						ConsoleWriteError($"Could not find a .NET Standard DLL for ${Path.GetFileName(dependency)}.");

					//if(!opt.SkipWsa && opt.Net46)
					{
						string[] uap = Directory.GetDirectories(lib, "uap*");
						if(uap != null && uap.Any())
						{
							string[] files = Directory.GetFiles(uap[0], "*.dll");
							foreach (string file in files)
							{
								string dest = Path.Combine(wsa, Path.GetFileName(file));
								ConsoleWrite($"\r\n-> Copying {file} to {dest}", ConsoleColor.Gray, true);
								File.Copy(file, dest, true);
							}
						}
					}
				}
			}

			ConsoleWriteLine("Complete", ConsoleColor.Green);
			return true;
		}

		private static string[] GetDependencies(string dir, string package)
		{
			// if it's in this list, Unity handles it and its dependencies, don't include it
			if(exclude.Contains(package))
				return null;

			// create the list and add the package itself
			List<string> dependencies = new List<string> { package };

			// see if there's a nuspec for this package, bail out if there isn't
			string nuspec = Path.Combine(dir, package, package + ".nuspec");
			if(!File.Exists(nuspec))
				return null;

			// parse the nuspec
			FileStream fs = new FileStream(nuspec, FileMode.Open);
			Manifest manifest = Manifest.ReadFrom(fs, true);
			fs.Close();

			// get the highest versioned .NET Standard dependencies
			var group = (from g in manifest?.Metadata?.DependencyGroups
						where g.TargetFramework.Framework == FrameworkConstants.FrameworkIdentifiers.NetStandard
						orderby g.TargetFramework.Version descending
						select g).FirstOrDefault();

			if(group != null)
			{
				foreach(var p in group.Packages)
				{
					// get this dependency's dependencies recursively and add them to the list
					if(exclude.Contains(p.Id))
						continue;

					DownloadPackage(p.Id, null, dir);
					string[] sub = GetDependencies(dir, p.Id);
					if(sub != null)
						dependencies.AddRange(sub);
				}
			}

			return dependencies.Distinct().ToArray();
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

		private static bool CreateUnityPackage(string package, string working, bool keepMeta, string output)
		{
			ConsoleWrite("Creating Unity package (this may take a few minutes)...");

			string[] includeDirs = { "Assets" };
			Package p = Package.FromDirectory(working, package, keepMeta, includeDirs);
			p.GeneratePackage(output);
			ConsoleWriteLine("Complete", ConsoleColor.Green);
			return true;
		}

		private static void Cleanup(string dir, string working)
		{
			ConsoleWrite("Cleaning up...");

			if(Directory.Exists(dir))
				Directory.Delete(dir, true);

			if(!string.IsNullOrEmpty(working) && Directory.Exists(working))
				Directory.Delete(working, true);

			ConsoleWriteLine("Complete", ConsoleColor.Green);
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

		private static void Error(IEnumerable<Error> errors)
		{
			foreach(Error e in errors)
				Debug.WriteLine(e);
		}
	}
}
