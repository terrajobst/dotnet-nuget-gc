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
            string nugetcache = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\.nuget\\packages\\";
            DirectoryInfo info = new DirectoryInfo(nugetcache);
            long totalDeleted = 0;
            foreach (var folder in info.GetDirectories())
            {
                foreach (var versionFolder in folder.GetDirectories())
                {
                    var folderInfo = versionFolder.GetFiles("*.*", SearchOption.AllDirectories).GroupBy(i => 1).Select(g => new { Size = g.Sum(f => f.Length), LastAccessTime = g.Max(f => f.LastAccessTime) }).First();
                    var lastAccessed = DateTime.Now - folderInfo.LastAccessTime;
                    if (lastAccessed > TimeSpan.FromDays(minDays))
                    {
                        Console.WriteLine($"{versionFolder.FullName} last accessed {Math.Floor(lastAccessed.TotalDays)} days ago");
                        try
                        {
                            versionFolder.MoveTo(Path.Combine(versionFolder.Parent.FullName, "_" + versionFolder.Name)); //Attempt to rename before deleting
                            versionFolder.Delete(true);
                            totalDeleted += folderInfo.Size;
                        }
                        catch { }
                    }
                }
                if (folder.GetDirectories().Length == 0)
                    folder.Delete(true);
            }
            var mbDeleted = (totalDeleted / 1024d / 1024d).ToString("0");
            Console.WriteLine($"Done! Deleted {mbDeleted} Mb");
        }
    }
}