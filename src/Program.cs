using System;
using System.IO;
using System.Linq;

namespace NugetCacheCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            var flags = args.Where(arg => arg.Length > 0 && arg[0] == '-').ToArray();
            const string forceName = "--force"; const string forceShortName = "-f";
            var force = flags.Any(arg => arg == forceShortName || arg == forceName);

            void Delete(DirectoryInfo dir, bool withLockCheck)
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

            var tail = args.Except(flags);
            var minDays = int.TryParse(tail.FirstOrDefault(), out var n) ? n : 30;
            string nugetcache = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            DirectoryInfo info = new DirectoryInfo(nugetcache);
            long totalDeleted = 0;
            foreach (var folder in info.GetDirectories())
            {
                foreach (var versionFolder in folder.GetDirectories())
                {
                    var files = versionFolder.GetFiles("*.*", SearchOption.AllDirectories);
                    var size = files.Sum(f => f.Length);
                    var lastAccessed = DateTime.Now - files.Max(f => f.LastAccessTime);
                    if (lastAccessed > TimeSpan.FromDays(minDays))
                    {
                        Console.WriteLine($"{versionFolder.FullName} last accessed {Math.Floor(lastAccessed.TotalDays)} days ago");
                        try
                        {
                            Delete(versionFolder, withLockCheck: true);
                            totalDeleted += size;
                        }
                        catch { }
                    }
                }
                if (folder.GetDirectories().Length == 0)
                    Delete(folder, withLockCheck: false);
            }
            var mbDeleted = (totalDeleted / 1024d / 1024d).ToString("0");
            Console.WriteLine(
                force ? $"Done! Deleted {mbDeleted} MB."
                      : $"Will delete {mbDeleted} MB. Re-run with {forceShortName} or {forceName} flag to really delete.");
        }
    }
}