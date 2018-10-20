# NuGet2Unity

A .NET Core app to package a NuGet package (and dependencies) as a .UnityPackage for import into a Unity 2018 (or higher) project.
This will build a package that should be compatible with the following Unity player settings
(Player Settings -> Other Settings -> Configuration):

Setting | Value
--------|------
Scripting Runtime Version | .NET 4.x Equivalent
Scripting Backend | Mono or IL2CPP
Api Compatibility Level | .NET Standard 2.0

## Build
It is assumed you have .NET Core 2.1 installed.  If not, you can download from [here](https://www.microsoft.com/net/download/dotnet-core/2.1).  Once installed, from the `src` directory, run the standard `dotnet build` command.

## Usage
`dotnet NuGet2Unity.dll [options]`

Switch | Description
-------|-------------
-n, --nugetpackage  |  Required. NuGet package to repackage
-v, --version       |  Version of NuGet package to use
-p, --unityproject  |  Path to the Unity project to include with this package
-m, --includemeta   |  Include .meta files from the Unity project
-o, --outputpath    |  Required. Path and filename of the .unitypackage
--verbose           |  Maximum verbosity
--netstandard       |  (Default: true) Build for .NET Standard 2.0 compatibility
--skiplinkxml       |  Do not add a link.xml to package
--help              |  Display this help screen.
--version           |  Display version information.
