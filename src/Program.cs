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
                if (prune)
                    Console.WriteLine($"{mbDeleted} MB worth of packages are older than {minDays.TotalDays:N0} days or are not the latest version.");
                else
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

            var deleted = new HashSet<DirectoryInfo>();
            
            void CleanPackageDirectory(DirectoryInfo dir)
            {
                DirectoryInfo versionDir;
                FileInfo[] versionDirFiles;

                long DeleteVersion(bool withLockCheck = false)
                {
                    try
                    {
                        var size = versionDirFiles.Sum(f => f.Length);
                        DeleteDir(versionDir, force, withLockCheck);
                        deleted.Add(versionDir);
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
                    return totalDeleted;
                }
                
                var versions = new Dictionary<NuGetVersion, DirectoryInfo>();
                deleted.Clear();
                foreach (var subDir in dir.GetDirectories())
                {
                    if (!NuGetVersion.TryParse(subDir.Name, out var version))
                    {
                        //Delete(versionFolder, force, withLockCheck: false);
                        Console.WriteLine($"Warning: Skipping non-version format directory {subDir.FullName}.");
                        continue;
                    }
                    versions.Add(version, subDir);
                }

                if (prune)
                {
                    // keep newest release and newest prerelease (if newer than newest release)
                    var releases = versions.Keys.Where(v => !v.IsPrerelease).ToArray();
                    var newestRelease = releases.Any() ? releases.Max() : null;
                    var prereleases = versions.Keys.Where(v => v > newestRelease && v.IsPrerelease).ToArray();
                    var newestPrerelease = prereleases.Any() ? prereleases.Max() : null;
                    
                    foreach (var versionedDir in versions)
                    {
                        if (versionedDir.Key == newestRelease) continue;
                        if (versionedDir.Key == newestPrerelease) continue;
                        versionDir = versionedDir.Value;
                        if (deleted.Contains(versionDir)) continue;
                        versionDirFiles = versionDir.GetFiles("*.*", SearchOption.AllDirectories);
                        totalDeleted += DeleteVersion(false);
                    }
                    
                    foreach (var deletedDir in deleted)
                    {
                        var parsedVersion = versions.First(k => k.Value == deletedDir).Key;
                        versions.Remove(parsedVersion);
                    }
                }

                foreach (var versionedDir in versions)
                {
                    versionDir = versionedDir.Value;
                    versionDirFiles = versionDir.GetFiles("*.*", SearchOption.AllDirectories);
                    if (versionDirFiles.Length == 0)
                    {
                        DeleteDir(versionDir, force, withLockCheck: false);
                        continue;
                    }

                    var lastAccessed = DateTime.UtcNow - versionDirFiles.Max(GetLastAccessed);
                    
                    if (lastAccessed <= minDays)
                        continue;
                    
                    Console.WriteLine($"{versionDir.FullName} last accessed {Math.Floor(lastAccessed.TotalDays)} days ago");
                    
                    totalDeleted = DeleteVersion(true);
                }
                if (dir.GetDirectories().Length == 0)
                    DeleteDir(dir, force, withLockCheck: false);
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

        private static void DeleteDir(DirectoryInfo dir, bool force, bool withLockCheck)
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
