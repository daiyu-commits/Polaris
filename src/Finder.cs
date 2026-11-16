using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Polaris
{
        internal sealed class Candidate
    {
        public string Path;
        public int Code;
        public string Display;
        public string FileName;
        public string Variant = "";
        public bool OnDesktop;
    }

                        internal static class Finder
    {
        private static readonly HashSet<string> SkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "node_modules", ".git", ".vs", "obj", "bin", "$RECYCLE.BIN", "System Volume Information",
          "AppData", "Windows", "Program Files", "Program Files (x86)" };

        public static List<Candidate> Scan()
        {
            var roots = new List<string>();

            var self = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) ?? "";
            if (!string.IsNullOrEmpty(self)) roots.Add(self);

            var last = LastPath();
            if (!string.IsNullOrEmpty(last))
            {
                var d = Path.GetDirectoryName(last);
                if (!string.IsNullOrEmpty(d)) roots.Add(d);
            }

            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive",
                                   "Desktop"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            roots.Add(@"C:\PCL");
            roots.Add(@"D:\PCL");
            roots.Add(@"E:\PCL");

            var files = new List<string>();
            foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct(StringComparer.OrdinalIgnoreCase))
                foreach (var dir in Walk(root, 2))
                {
                    string[] exes;
                    try { exes = Directory.GetFiles(dir, "*.exe"); } catch { continue; }
                    foreach (var f in exes)
                        if (IsSuspectName(Path.GetFileName(f))) files.Add(f);
                }

            var result = new List<Candidate>();
            foreach (var f in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Patcher.LooksLikePcl(f)) continue;
                int code;
                string display;
                Patcher.PeekVersion(f, out code, out display);
                result.Add(new Candidate
                {
                    Path = f,
                    Code = code,
                    Display = display,
                    FileName = Path.GetFileName(f),
                    Variant = VariantOf(display),
                    OnDesktop = IsDesktopPath(f)
                });
            }

            return result
                .OrderByDescending(c => VariantWeight(c.Variant))
                .ThenByDescending(c => c.OnDesktop)
                .ThenByDescending(c => c.Code)
                .ThenBy(c => c.Path)
                .ToList();
        }

                private static int VariantWeight(string variant)
        {
            switch (variant)
            {
                case "正式版": return 3;
                case "社区版": return 2;
                case "快照版": return 1;
                default: return 0;
            }
        }

        private static string VariantOf(string display)
        {
            if (string.IsNullOrEmpty(display)) return "";
            foreach (var v in new[] { "正式版", "社区版", "快照版", "测试版", "开发版" })
                if (display.StartsWith(v, StringComparison.Ordinal)) return v;
            return "其他";
        }

        private static bool IsDesktopPath(string path)
        {
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            })
            {
                if (string.IsNullOrEmpty(root)) continue;
                var prefix = root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsSuspectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("._cache", StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith(".", StringComparison.Ordinal)) return false;

            var n = name.ToLowerInvariant();
            if (n.Contains("launcher")) return true;
            if (n.Contains("craft")) return true;
            if (n.StartsWith("pcl")) return true;
            return false;
        }

        private static IEnumerable<string> Walk(string root, int depth)
        {
            var queue = new Queue<Tuple<string, int>>();
            queue.Enqueue(Tuple.Create(root, 0));
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                yield return cur.Item1;
                if (cur.Item2 >= depth) continue;
                string[] subs;
                try { subs = Directory.GetDirectories(cur.Item1); } catch { continue; }
                foreach (var s in subs)
                    if (!SkipDirs.Contains(Path.GetFileName(s))) queue.Enqueue(Tuple.Create(s, cur.Item2 + 1));
            }
        }

        private static string LastPath()
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     "Polaris", "last.txt");
                return File.Exists(p) ? File.ReadAllText(p).Trim() : "";
            }
            catch { return ""; }
        }
    }
}
