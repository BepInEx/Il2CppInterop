#addin nuget:?package=Cake.FileHelpers&version=4.0.1
#addin nuget:?package=SharpZipLib&version=1.3.1
#addin nuget:?package=Cake.Compression&version=0.2.6
#addin nuget:?package=Cake.Json&version=6.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.1

var target = Argument("target", "Build");
var buildVersion = Argument("build_version", "");
var buildTag = Argument("build_tag", "");

string RunGit(string command, string separator = "")
{
    using(var process = StartAndReturnProcess("git", new ProcessSettings { Arguments = command, RedirectStandardOutput = true }))
    {
        process.WaitForExit();
        return string.Join(separator, process.GetStandardOutput());
    }
}

Task("Build")
    .Does(() =>
{
    var msBuildSettings = new DotNetCoreMSBuildSettings();
    if (!string.IsNullOrEmpty(buildVersion))
        msBuildSettings.Properties["VersionPrefix"] = new string[]{buildVersion};
    if (!string.IsNullOrEmpty(buildTag))
        msBuildSettings.Properties["VersionSuffix"] = new string[]{buildTag};
    var buildSettings = new DotNetCoreBuildSettings {
        Configuration = "Release",
		MSBuildSettings = msBuildSettings
    };

    DotNetCoreBuild(".", buildSettings);
});

Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
{
    var distDir = Directory("./bin/zip");
    CreateDirectory(distDir);

    var versionString = string.IsNullOrEmpty(buildVersion) ? "" : $".{buildVersion}";
    if (!string.IsNullOrEmpty(buildVersion) && !string.IsNullOrEmpty(buildTag))
        versionString += $"-{buildTag}";
    var pathsToIgnore = new HashSet<string> { "NuGet", "zip" };
    foreach (var dir in GetDirectories("./bin/*", new GlobberSettings { Predicate = f => !pathsToIgnore.Contains(f.Path.GetDirectoryName()) })) {
        ZipCompress(dir, distDir + File($"{dir.GetDirectoryName()}{versionString}.zip"));
    }
});

RunTarget(target);
