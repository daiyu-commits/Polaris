using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Polaris
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] argv)
        {
                        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var name = new AssemblyName(e.Name).Name;
                if (!string.Equals(name, "dnlib", StringComparison.OrdinalIgnoreCase)) return null;
                using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("dnlib.dll"))
                {
                    if (st == null) return null;
                    var buf = new byte[st.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = st.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return Assembly.Load(buf);
                }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

                        if (argv.Length >= 2 && string.Equals(argv[0], "--cli", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(Cli.Run(argv));
                return;
            }

            Application.Run(new MainForm());
        }
    }

        internal static class Cli
    {
        public static int Run(string[] argv)
        {
            try
            {
                var cmd = argv[1].ToLowerInvariant();
                var target = argv.Length > 2 ? argv[2] : "";

                switch (cmd)
                {
                    case "analyze":
                    {
                        var st = Patcher.Analyze(target);
                        Console.WriteLine($"path      {st.Path}");
                        Console.WriteLine($"display   {st.VersionDisplay} ({st.VersionCode})");
                        Console.WriteLine($"asmver    {st.AsmVersion}   strongname={st.StrongName}");
                        Console.WriteLine($"restrict  {st.HasRestriction}");
                        Console.WriteLine($"patched   P1={st.P1} P2={st.P2} P3={st.P3} P4={st.P4}  => {(st.IsPatched ? "已安装" : "未安装")}");
                        Console.WriteLine($"backup    {(st.HasBackup ? st.BackupPath : "(none)")}");
                        return 0;
                    }
                    case "install":
                    {
                        var before = Patcher.Sha256(target);
                        var refBytes = MainForm.ReadEmbedded("pcl214.exe");
                        var lines = Patcher.Install(target, refBytes, m => Console.WriteLine("          " + m));
                        foreach (var l in lines) Console.WriteLine($"  {l.Level,-4} {l.Id}  {l.Text}");
                        var st = Patcher.Analyze(target);
                        if (st.IsPatched)
                            GuardStore.Set(new TargetRecord
                            {
                                Path = target,
                                Sha = Patcher.Sha256(target),
                                SourceSha = before,
                                Version = st.VersionDisplay,
                                Patched = true,
                                Kind = "pcl2",
                                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        Console.WriteLine($"  result  {(st.IsPatched ? "INSTALLED" : "INCOMPLETE")}");
                        return st.IsPatched ? 0 : 1;
                    }
                    case "uninstall":
                    {
                        Patcher.Uninstall(target, m => Console.WriteLine("          " + m));
                        var st = Patcher.Analyze(target);
                        Console.WriteLine($"  result  patched={st.IsPatched}");
                        return 0;
                    }
                    case "detect":
                    {
                        var list = Finder.Scan();
                        Console.WriteLine($"candidates {list.Count}");
                        foreach (var c in list)
                            Console.WriteLine($"  code={c.Code,-4} {c.Display,-18} {c.Path}");
                        if (list.Count > 0) Console.WriteLine($"best: {list[0].Path}");
                        return list.Count > 0 ? 0 : 1;
                    }
                    case "auto":
                    {
                        var rec = GuardStore.Get(target);
                        Console.WriteLine(rec == null
                            ? "no record"
                            : $"record    {rec.Path}  version={rec.Version}  patched={rec.Patched}  sha={rec.Sha}  when={rec.When}");
                        Console.WriteLine($"autoguard {Settings.AutoGuard}");
                        return 0;
                    }
                    default:
                        Console.Error.WriteLine("usage: --cli analyze|install|uninstall|detect|auto <path>");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FATAL: " + ex.Message);
                return 1;
            }
        }
    }
}
