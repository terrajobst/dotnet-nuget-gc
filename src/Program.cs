using System;
using System.IO;
using System.Linq;

namespace NugetCacheCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            int minDays = 30;
            if (args.Length > 0 && int.TryParse(args[0], out minDays)) { }
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
                            versionFolder.MoveTo(Path.Combine(versionFolder.Parent.FullName, "_" + versionFolder.Name)); //Attempt to rename before deleting
                            versionFolder.Delete(true);
                            totalDeleted += size;
                        }
                        catch { }
                    }
                }
                if (folder.GetDirectories().Length == 0)
                    folder.Delete(true);
            }
            var mbDeleted = (totalDeleted / 1024d / 1024d).ToString("0");
            Console.WriteLine($"Done! Deleted {mbDeleted} MB.");
        }
    }
}