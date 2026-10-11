using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitGraphLite
{
    // Recently opened folders, newest first, kept in %APPDATA%\GitGraphLite\recent.txt.
    static class Recent
    {
        const int Max = 10;

        static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitGraphLite", "recent.txt"); }
        }

        public static List<string> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<string>();
                return File.ReadAllLines(FilePath).Where(l => l.Length > 0).ToList();
            }
            catch (Exception) { return new List<string>(); }
        }

        public static List<string> Add(string path)
        {
            var list = Load();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, list);
            }
            catch (Exception) { }
            return list;
        }
    }
}
