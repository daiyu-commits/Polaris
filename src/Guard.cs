using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Polaris
{
        internal sealed class TargetRecord
    {
        public string Path = "";
        public string Sha = "";
        public string SourceSha = "";
        public string Version = "";
        public bool Patched;
        public string Kind = "";
        public string When = "";
    }

        internal static class GuardStore
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Polaris", "targets.tsv");

        public static TargetRecord Get(string path)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var f = line.Split('\t');
                    if (f.Length < 7) continue;
                    if (!string.Equals(f[0], path, StringComparison.OrdinalIgnoreCase)) continue;
                    return new TargetRecord
                    {
                        Path = f[0], Sha = f[1], SourceSha = f[2], Version = f[3],
                        Patched = f[4] == "1", Kind = f[5], When = f[6]
                    };
                }
            }
            catch { }
            return null;
        }

        public static void Set(TargetRecord rec)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var all = new List<TargetRecord>();
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        var f = line.Split('\t');
                        if (f.Length < 7) continue;
                        all.Add(new TargetRecord
                        {
                            Path = f[0], Sha = f[1], SourceSha = f[2], Version = f[3],
                            Patched = f[4] == "1", Kind = f[5], When = f[6]
                        });
                    }
                all.RemoveAll(r => string.Equals(r.Path, rec.Path, StringComparison.OrdinalIgnoreCase));
                all.Add(rec);
                var sb = new StringBuilder();
                foreach (var r in all)
                    sb.AppendLine(string.Join("\t", new[]
                    {
                        r.Path, r.Sha, r.SourceSha, r.Version, r.Patched ? "1" : "0", r.Kind, r.When
                    }));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

        internal static class Settings
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Polaris", "settings.tsv");

        private static Dictionary<string, string> Load()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return d;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var i = line.IndexOf('\t');
                    if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
            catch { }
            return d;
        }

        private static void Save(Dictionary<string, string> d)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "\t" + kv.Value), Encoding.UTF8);
            }
            catch { }
        }

        public static bool AutoGuard
        {
            get
            {
                var d = Load();
                string v;
                if (d.TryGetValue("AutoGuard", out v)) return v != "0";
                return true;
            }
            set
            {
                var d = Load();
                d["AutoGuard"] = value ? "1" : "0";
                Save(d);
            }
        }
    }

                    internal sealed class ExeWatcher : IDisposable
    {
        private readonly FileSystemWatcher _w;
        private readonly System.Threading.Timer _debounce;
        private readonly object _lock = new object();
        private bool _armed;

        public event Action<string> Settled;

        public string Target { get; private set; }

        public ExeWatcher(string exePath)
        {
            Target = exePath;
            var dir = System.IO.Path.GetDirectoryName(exePath);
            var file = System.IO.Path.GetFileName(exePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            _debounce = new System.Threading.Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);

            _w = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName |
                               NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _w.Changed += OnAny;
            _w.Created += OnAny;
            _w.Renamed += (s, e) => OnAny(s, e);
        }

        private void OnAny(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                _armed = true;
                try { _debounce.Change(2200, Timeout.Infinite); } catch { }
            }
        }

        private void Fire()
        {
            lock (_lock) { if (!_armed) return; _armed = false; }
            var h = Settled;
            if (h != null) { try { h(Target); } catch { } }
        }

        public void Dispose()
        {
            try { if (_w != null) { _w.EnableRaisingEvents = false; _w.Dispose(); } } catch { }
            try { if (_debounce != null) _debounce.Dispose(); } catch { }
        }
    }
}
