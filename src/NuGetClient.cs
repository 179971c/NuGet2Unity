//
// Thanks to Martin Björkström and his very helpful post:
// https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries
//

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet2Unity
{
	public class NuGetClient
	{
		public async Task<IEnumerable<string>> DownloadPackageAndDependenciesAsync(string packageId, string version, string nuGetDir)
		{
			List<string> dllsToCopy = new List<string>();

			ILogger logger = NullLogger.Instance;
			NuGetVersion packageVersion = NuGetVersion.Parse(version);
			NuGetFramework nuGetFramework = NuGetFramework.ParseFolder("netstandard20");
			ISettings settings = Settings.LoadDefaultSettings(root: null);
			SourceRepositoryProvider sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());

			using (SourceCacheContext cacheContext = new SourceCacheContext())
			{
				IEnumerable<SourceRepository> repositories = sourceRepositoryProvider.GetRepositories();
				HashSet<SourcePackageDependencyInfo> availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
				await GetPackageDependenciesAync(
					new PackageIdentity(packageId, packageVersion),
					nuGetFramework, cacheContext, logger, repositories, availablePackages);

				PackageResolverContext resolverContext = new PackageResolverContext(
					DependencyBehavior.Lowest,
					new[] { packageId },
					Enumerable.Empty<string>(),
					Enumerable.Empty<PackageReference>(),
					Enumerable.Empty<PackageIdentity>(),
					availablePackages,
					sourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
					logger);

				PackageResolver resolver = new PackageResolver();
				IEnumerable<SourcePackageDependencyInfo> packagesToInstall = resolver.Resolve(resolverContext, CancellationToken.None)
					.Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)));
				PackagePathResolver packagePathResolver = new PackagePathResolver(nuGetDir);
				PackageExtractionContext packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, ClientPolicyContext.GetClientPolicy(settings, logger), logger);

				FrameworkReducer frameworkReducer = new FrameworkReducer();

				foreach (SourcePackageDependencyInfo packageToInstall in packagesToInstall)
				{
					PackageReaderBase packageReader;
					string installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
					if (installedPath == null)
					{
						DownloadResource downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
						DownloadResourceResult downloadResult = await downloadResource.GetDownloadResourceResultAsync(
							packageToInstall,
							new PackageDownloadContext(cacheContext),
							SettingsUtility.GetGlobalPackagesFolder(settings),
							logger, CancellationToken.None);

						await PackageExtractor.ExtractPackageAsync(
							downloadResult.PackageSource,
							downloadResult.PackageStream,
							packagePathResolver,
							packageExtractionContext,
							CancellationToken.None);

						packageReader = downloadResult.PackageReader;
						installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
					}
					else
					{
						packageReader = new PackageFolderReader(installedPath);
					}

					IEnumerable<FrameworkSpecificGroup> libItems = packageReader.GetLibItems();
					NuGetFramework nearest = frameworkReducer.GetNearest(nuGetFramework, libItems.Select(x => x.TargetFramework));
					IEnumerable<string> items = libItems.Where(x => x.TargetFramework.Equals(nearest)).SelectMany(x => x.Items).Where(x => Path.GetExtension(x) == ".dll");
					foreach(var item in items)
						dllsToCopy.Add(Path.Combine(installedPath, item));
				}
			}
			return dllsToCopy;
		}

		private async Task GetPackageDependenciesAync(PackageIdentity package,
			NuGetFramework framework,
			SourceCacheContext cacheContext,
			ILogger logger,
			IEnumerable<SourceRepository> repositories,
			ISet<SourcePackageDependencyInfo> availablePackages)
		{
			if (availablePackages.Contains(package))
				return;

			foreach (SourceRepository sourceRepository in repositories)
			{
				DependencyInfoResource dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
				SourcePackageDependencyInfo dependencyInfo = await dependencyInfoResource.ResolvePackage(
					package, framework, cacheContext, logger, CancellationToken.None);

				if (dependencyInfo == null)
					continue;

				availablePackages.Add(dependencyInfo);
				foreach (PackageDependency dependency in dependencyInfo.Dependencies)
				{
					await GetPackageDependenciesAync(
						new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
						framework, cacheContext, logger, repositories, availablePackages);
				}
			}
		}
	}
}
