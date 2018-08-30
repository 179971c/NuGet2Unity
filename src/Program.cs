using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using CommandLine;
using UnityPacker;
using System.Text;

namespace NuGet2Unity
{
	class Program
	{
		private static Options _options;

		private static string[] exclude = { "System.Runtime.Serialization.Primitives" };

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
			ConsoleWriteLine($"Temp directory: {temp}", ConsoleColor.Gray, true);

			bool success = DownloadPackage(opt.Package, opt.Version, temp);
			if(!success)
				return;

			string plugins = Path.Combine(working, "Assets", "Plugins");
			Directory.CreateDirectory(plugins);

			CopyFiles(temp, plugins, opt);

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

		private static bool DownloadPackage(string package, string version, string temp)
		{
			ConsoleWrite("Downloading NuGet package and dependencies...");
			string args = $"install {package} -OutputDirectory {temp} -DependencyVersion Highest";
			if(!string.IsNullOrEmpty(version))
				args += $" -Version {version}";

			ConsoleWriteLine($"\r\nNuGet.exe args: ${args}", ConsoleColor.Gray, true);

			Process p = new Process();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.FileName = "nuget.exe";
			p.StartInfo.Arguments = args;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.CreateNoWindow = true;
			p.Start();
			p.WaitForExit();

			string errorText = p.StandardError.ReadToEnd();
			if (!string.IsNullOrEmpty(errorText))
			{
				ConsoleWriteError(errorText);
				return false;
			}

			ConsoleWriteLine("Complete", ConsoleColor.Green);
			return true;
		}

		private static void CopyFiles(string temp, string working, Options opt)
		{
			ConsoleWrite("Copying files...");

			// delete any existing working files
			foreach(string file in Directory.GetFiles(working))
				File.Delete(file);
			foreach(string dir in Directory.GetDirectories(working))
				Directory.Delete(dir, true);

			string wsa = Path.Combine(working, "WSA");

			if(!opt.SkipWsa)
				Directory.CreateDirectory(wsa);

			string[] dirs = Directory.GetDirectories(temp);
				
			foreach (string dir in dirs)
			{
				foreach(string ex in exclude)
				{
					if(dir.Contains(ex))
						continue;

					if(!opt.SkipJsonFix)
						CreateLinkXml(working);

					string lib = Path.Combine(dir, "lib");
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

						if(!opt.SkipWsa && opt.Net46)
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
			}

			ConsoleWriteLine("Complete", ConsoleColor.Green);
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
			Directory.Delete(dir, true);
			if(!string.IsNullOrEmpty(working))
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
