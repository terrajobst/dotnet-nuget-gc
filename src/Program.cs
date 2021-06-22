using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;
using NuGet.Versioning;

namespace NugetCacheCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            var force = false;
            var showHelp = false;
            var minDays = TimeSpan.FromDays(90);
            var prune = false;

            var options = new OptionSet
            {
                {
                    "f|force", "Performs the actual clean-up. Default is to do a dry-run and report the clean-up that would be done.",
                    v => force = v is not null
                },
                {
                    "m|min-days=", "Number of days a package must not be used in order to be purged from the cache. Defaults to 90.",
                    v => minDays = ParseDays(v)
                },
                { "p|prune", "Prune older versions of packages regardless of age.", v => prune = v is not null },
                { "?|h|help", "Show this message.", v => showHelp = v != null },
            };

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (FormatException e)
            {
                Console.Error.WriteLine(e.Message);
                return;
            }

            if (showHelp)
            {
                ShowHelp(options);
                return;
            }

            var totalDeleted = CleanCache(force, minDays, prune);
            var mbDeleted = (totalDeleted / 1024d / 1024d).ToString("N0");

            if (force)
            {
                Console.WriteLine($"Done! Deleted {mbDeleted} MB.");
            }
            else
            {
                Console.WriteLine($"{mbDeleted} MB worth of packages are older than {minDays.TotalDays:N0} days.");
                Console.WriteLine("To delete, re-run with -f or --force flag.");
            }
        }

        private static void ShowHelp(OptionSet optionSet)
        {
            Console.WriteLine("usage: dotnet nuget-gc [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
        }

        private static TimeSpan ParseDays(string text)
        {
            if (!int.TryParse(text, out var days))
                throw new FormatException($"'{text}' isn't a valid integer");

            return TimeSpan.FromDays(days);
        }

        private static long CleanCache(bool force, TimeSpan minDays, bool prune)
        {
            var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nugetCachePath = Path.Join(userProfilePath, ".nuget", "packages");
            var nugetCache = new DirectoryInfo(nugetCachePath);
            var totalDeleted = 0L;

            void CleanPackageDirectory(DirectoryInfo dir)
            {
                var versions = new Dictionary<NuGetVersion, DirectoryInfo>();
                foreach (var versionDir in dir.GetDirectories())
                {
                    var files = versionDir.GetFiles("*.*", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {
                        Delete(versionDir, force, withLockCheck: false);
                        continue;
                    }
                    if (!NuGetVersion.TryParse(versionDir.Name, out var version))
                    {
                        //Delete(versionFolder, force, withLockCheck: false);
                        Console.WriteLine($"Warning: Skipping non-version format directory {versionDir.FullName}.");
                        continue;
                    }
                    versions.Add(version, versionDir);
                    var size = files.Sum(f => f.Length);
                    var lastAccessed = DateTime.UtcNow - files.Max(GetLastAccessed);
                    if (lastAccessed > minDays)
                    {
                        Console.WriteLine($"{versionDir.FullName} last accessed {Math.Floor(lastAccessed.TotalDays)} days ago");
                        try
                        {
                            Delete(versionDir, force, withLockCheck: true);
                            totalDeleted += size;
                        }
                        catch (FileNotFoundException)
                        {
                            // ok
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Console.WriteLine($"Warning: Not authorized to delete {versionDir.FullName}.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Deleting {versionDir.FullName} encountered {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
                if (prune)
                {
                    var releases = versions.Keys.Where(v => !v.IsPrerelease).ToArray();
                    var newestRelease = releases.Any() ? releases.Max() : null;
                    var prereleases = versions.Keys.Where(v => v > newestRelease && v.IsPrerelease).ToArray();
                    var newestPrerelease = prereleases.Any() ? prereleases.Max() : null;
                    foreach (var versionedDir in versions)
                    {
                        if (versionedDir.Key == newestRelease) continue;
                        if (versionedDir.Key == newestPrerelease) continue;
                        var versionDir = versionedDir.Value;
                        var files = versionDir.GetFiles("*.*", SearchOption.AllDirectories);
                        var size = files.Sum(f => f.Length);
                        Delete(versionDir, force, withLockCheck: false);
                        totalDeleted += size;
                    }
                }
                if (dir.GetDirectories().Length == 0)
                    Delete(dir, force, withLockCheck: false);
            }

            if (!nugetCache.Exists)
            {
                Console.WriteLine($"Warning: Missing nuget package folder: {nugetCache.FullName}");
            }
            else
            {
                foreach (var dir in nugetCache.GetDirectories())
                {
                    if (dir.Name != ".tools")
                        CleanPackageDirectory(dir);
                    else
                        foreach (var toolDir in dir.GetDirectories())
                            CleanPackageDirectory(toolDir);
                }
            }

            return totalDeleted;
        }

        private static DateTime GetLastAccessed(FileInfo f)
        {
            try
            {
                return DateTime.FromFileTimeUtc(Math.Max(f.LastAccessTimeUtc.ToFileTimeUtc(), f.LastWriteTimeUtc.ToFileTimeUtc()));
            }
            catch
            {
                return f.LastWriteTimeUtc;
            }
        }

        private static void Delete(DirectoryInfo dir, bool force, bool withLockCheck)
        {
            if (!force)
            {
#if DEBUG
                Console.WriteLine($"Would remove {dir.FullName}.");
#endif
                return;
            }
#if DEBUG
            Console.WriteLine($"Removing {dir.FullName}.");
#endif

            if (withLockCheck) // This may only be good enough for Windows
            {
                var parentDir = dir.Parent;
                if (parentDir == null) throw new NotImplementedException("Missing parent directory.");
                var tempPath = Path.Join(parentDir.FullName, "_" + dir.Name);
                dir.MoveTo(tempPath); // Attempt to rename before deleting
                Directory.Delete(tempPath, recursive: true);
            }
            else
            {
                dir.Delete(recursive: true);
            }
        }
    }
}
