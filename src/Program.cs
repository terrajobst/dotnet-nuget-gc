using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Options;

namespace NugetCacheCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            var force = false;
            var showHelp = false;
            var minDays = TimeSpan.FromDays(90);

            var options = new OptionSet {
                {"f|force", "Performs the actual clean-up. Default is to do a dry-run and report the clean-up that would be done.", v => force = v != null},
                {"m|min-days=", "Number of days a package must not be used in order to be purged from the cache. Defaults to 90.", v => minDays = ParseDays(v)},
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

            var totalDeleted = CleanCache(force, minDays);
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
            optionSet.WriteOptionDescriptions (Console.Out);
        }

        private static TimeSpan ParseDays(string text)
        {
            if (!int.TryParse(text, out var days))
                throw new FormatException($"'{text}' isn't a valid integer");

            return TimeSpan.FromDays(days);
        }

        private static long CleanCache(bool force, TimeSpan minDays)
        {
            var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nugetCachePath = Path.Join(userProfilePath, ".nuget", "packages");
            var nugetCache = new DirectoryInfo(nugetCachePath);
            var totalDeleted = 0L;
            if (!nugetCache.Exists)
            {
                Console.WriteLine($"Warning: Missing nuget package folder: {nugetCache.FullName}");
            }
            else
            {
                foreach (var folder in nugetCache.GetDirectories())
                {
                    foreach (var versionFolder in folder.GetDirectories())
                    {
                        var files = versionFolder.GetFiles("*.*", SearchOption.AllDirectories);
                        if (files.Length == 0)
                        {
                            Delete(versionFolder, force, withLockCheck: false);
                            continue;
                        }
                        var size = files.Sum(f => f.Length);
                        var lastAccessed = DateTime.UtcNow - files.Max(GetLastAccessed);
                        if (lastAccessed > minDays)
                        {
                            Console.WriteLine($"{versionFolder.FullName} last accessed {Math.Floor(lastAccessed.TotalDays)} days ago");
                            try
                            {
                                Delete(versionFolder, force, withLockCheck: true);
                                totalDeleted += size;
                            }
                            catch (FileNotFoundException)
                            {
                                // ok
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Console.WriteLine($"Warning: Not authorized to delete {versionFolder.FullName}.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Deleting {versionFolder.FullName} encountered {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                    if (folder.GetDirectories().Length == 0)
                        Delete(folder, force, withLockCheck: false);
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
                return;

            if (withLockCheck) // This may only be good enough for Windows
            {
                var tempPath = Path.Join(dir.Parent.FullName, "_" + dir.Name);
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