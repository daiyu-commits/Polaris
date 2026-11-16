using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Polaris
{
        internal sealed class PatchLine
    {
        public string Id;
        public string Level;
        public string Text;
        public override string ToString() => $"{Id} {Level} {Text}";
    }

        internal sealed class PclState
    {
        public string Path;
        public string FileVersion;
        public string AsmVersion;
        public string VersionDisplay;
        public string VersionCode;
        public string StrongName;

        public bool P1, P2, P3, P4;
        public bool HasRestriction;
        public bool HasBackup;
        public string BackupPath;

        public bool IsPatched => P1 && P2 && P3 && P4;
        public bool IsSupported => HasRestriction;
    }

                                                        internal static class Patcher
    {
        public const string BackupSuffix = ".pclbak";
        private const string FakeLoaderTitle = "关闭下载";
        private const string BlockedText = "暂时不再提供 MC 下载";

                public static bool LooksLikePcl(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                if (new FileInfo(path).Length < 1024 * 1024) return false;
                using (var mod = ModuleDefMD.Load(File.ReadAllBytes(path)))
                {
                    var name = mod.Assembly?.Name?.ToString() ?? "";
                    if (name.IndexOf("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                                        return mod.GetTypes().Any(t => t.FullName == "PCL.ModBase");
                }
            }
            catch { return false; }
        }

                public static void PeekVersion(string path, out int code, out string display)
        {
            code = 0;
            display = "";
            try
            {
                using (var mod = ModuleDefMD.Load(File.ReadAllBytes(path)))
                {
                    var t = mod.GetTypes().FirstOrDefault(x => x.FullName == "PCL.ModBase");
                    if (t == null) return;
                    var fc = t.Fields.FirstOrDefault(x => x.Name == "VersionCode");
                    if (fc?.Constant != null && fc.Constant.Value != null)
                        int.TryParse(Convert.ToString(fc.Constant.Value), out code);
                    var fd = t.Fields.FirstOrDefault(x => x.Name == "VersionDisplay");
                    if (fd?.Constant != null)
                        display = Convert.ToString(fd.Constant.Value) ?? "";
                }
            }
            catch
            {
                                try { int.TryParse(System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion?.Split('.')[0], out code); } catch { }
            }
        }

        public static PclState Analyze(string path)
        {
            var st = new PclState { Path = path };
            st.BackupPath = path + BackupSuffix;
            st.HasBackup = File.Exists(st.BackupPath);

            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            st.FileVersion = fvi.FileVersion ?? "";

            ModuleDefMD mod;
            try { mod = ModuleDefMD.Load(File.ReadAllBytes(path)); }
            catch (Exception ex)
            {
                throw new InvalidOperationException("不是有效的 .NET 程序: " + ex.Message);
            }
            using (mod)
            {
                st.AsmVersion = mod.Assembly?.Version?.ToString() ?? "";
                st.StrongName = mod.Assembly?.PublicKey?.Token?.ToString() ?? "(none)";
                st.VersionDisplay = ConstStr(mod, "PCL.ModBase", "VersionDisplay");
                st.VersionCode = ConstStr(mod, "PCL.ModBase", "VersionCode");

                var secret = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.ModSecret");
                var rf = secret?.Fields.FirstOrDefault(f => f.Name == "RestrictMcDownload");
                st.P1 = rf != null && rf.HasConstant && rf.Constant?.Value is bool b && !b;

                var pdi = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.PageDownloadInstall");
                var li = pdi?.Methods.FirstOrDefault(m => m.Name == "LoaderInit" && m.HasBody);
                bool real = li != null && li.Body.Instructions.Any(i => i.Operand is FieldDef f && f.Name == "DlClientListLoader");
                bool fake = li != null && li.Body.Instructions.Any(i => i.Operand is string s && s.Contains(FakeLoaderTitle));
                st.P2 = real && !fake;

                var pdm = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.PageDownloadModify");
                var lif = pdm?.Methods.FirstOrDefault(m => m.Name == "LoadInstanceFinished" && m.HasBody);
                st.P3 = CardVisible(lif);

                var mm = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.ModModpack");
                bool p4 = true, hasP4 = false;
                if (mm != null)
                {
                    foreach (var m in mm.Methods.Where(x => x.Name == "ModpackInstall" && x.HasBody))
                    {
                        hasP4 = true;
                        if (m.Body.Instructions.Any(i => i.Operand is string s && s.Contains(BlockedText))) p4 = false;
                    }
                }
                st.P4 = hasP4 && p4;

                                st.HasRestriction =
                    (li != null && fake) ||
                    (mm != null && mm.Methods.Where(x => x.Name == "ModpackInstall" && x.HasBody)
                        .Any(m => m.Body.Instructions.Any(i => i.Operand is string s && s.Contains(BlockedText)))) ||
                    rf != null;
            }
            return st;
        }

        private static string ConstStr(ModuleDefMD mod, string type, string field)
        {
            var t = mod.GetTypes().FirstOrDefault(x => x.FullName == type);
            var f = t?.Fields.FirstOrDefault(x => x.Name == field);
            var c = f?.Constant;
            return c == null ? "" : Convert.ToString(c.Value);
        }

                private static bool CardVisible(MethodDef lif)
        {
            if (lif == null || !lif.HasBody) return false;
            var ins = lif.Body.Instructions;
            for (int i = 0; i < ins.Count; i++)
            {
                if (!(ins[i].Operand?.ToString() ?? "").Contains("get_CardMinecraft")) continue;
                for (int j = i + 1; j < Math.Min(i + 4, ins.Count); j++)
                {
                    if (ins[j].OpCode == OpCodes.Ldc_I4_0) return true;
                    if (ins[j].OpCode == OpCodes.Ldc_I4_1 || ins[j].OpCode == OpCodes.Ldc_I4_2 || ins[j].OpCode == OpCodes.Ldc_I4_3)
                        return false;
                }
            }
            return false;
        }

                                        public static List<PatchLine> Install(string path, byte[] referenceExe, Action<string> log)
        {
            var lines = new List<PatchLine>();
            log?.Invoke("INFO 正在检查文件占用…");
            AssertNotLocked(path);

                        var backup = path + BackupSuffix;
            if (!File.Exists(backup))
            {
                File.Copy(path, backup, true);
                log?.Invoke("INFO 已备份原文件 → " + System.IO.Path.GetFileName(backup));
            }
            else
            {
                log?.Invoke("INFO 备份已存在，沿用: " + System.IO.Path.GetFileName(backup));
            }
            log?.Invoke("INFO 原文件 SHA-256 " + Sha256(backup));

                        var mod = ModuleDefMD.Load(File.ReadAllBytes(path));
            bool changed = false;
            using (mod)
            {
                lines.Add(Patch1(mod));
                lines.Add(Patch2(mod));
                lines.Add(Patch3(mod));
                lines.AddRange(Patch4(mod, referenceExe));
                foreach (var l in lines) { if (l.Level == "OK") changed = true; }

                if (!changed)
                {
                    log?.Invoke("SKIP 目标已是恢复状态，无需改动");
                    return lines;
                }

                var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "pclplugin-" + Guid.NewGuid().ToString("N") + ".exe");
                mod.Write(tmp);

                                var attrs = File.GetAttributes(path);
                try
                {
                    File.Copy(tmp, path, true);
                }
                finally
                {
                    TryDelete(tmp);
                    File.SetAttributes(path, attrs);
                }
            }

            var after = Analyze(path);
            if (!after.IsPatched)
            {
                                File.Copy(backup, path, true);
                throw new InvalidOperationException(
                    $"复验未通过（P1={after.P1} P2={after.P2} P3={after.P3} P4={after.P4}），已自动还原原文件。");
            }

            log?.Invoke("INFO 复验通过：4/4 补丁点已落地");
            log?.Invoke("INFO 新文件 SHA-256 " + Sha256(path));
            return lines;
        }

        private static void AssertNotLocked(string path)
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                string n;
                try { n = p.MainModule?.FileName; } catch { continue; }
                if (string.IsNullOrEmpty(n)) continue;
                if (string.Equals(n, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("PCL 正在运行，请先完全退出后再安装。");
            }
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
            catch (IOException)
            {
                throw new InvalidOperationException("文件被占用（PCL 可能仍在运行），请退出后重试。");
            }
        }

        private static PatchLine Patch1(ModuleDefMD mod)
        {
            var l = new PatchLine { Id = "P1" };
            var secret = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.ModSecret");
            var rf = secret?.Fields.FirstOrDefault(f => f.Name == "RestrictMcDownload");
            if (rf == null) { l.Level = "SKIP"; l.Text = "限制开关字段不存在"; return l; }
            if (!rf.HasConstant) { l.Level = "SKIP"; l.Text = "限制开关不是常量"; return l; }
            if (rf.Constant?.Value is bool b && !b) { l.Level = "SKIP"; l.Text = "限制开关已是 False"; return l; }
            rf.Constant.Value = false;
            l.Level = "OK"; l.Text = "限制开关 RestrictMcDownload: True → False";
            return l;
        }

        private static PatchLine Patch2(ModuleDefMD mod)
        {
            var l = new PatchLine { Id = "P2" };
            var pdi = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.PageDownloadInstall");
            var li = pdi?.Methods.FirstOrDefault(m => m.Name == "LoaderInit" && m.HasBody);
            if (li == null) { l.Level = "SKIP"; l.Text = "下载页入口 LoaderInit 不存在"; return l; }

            var ins = li.Body.Instructions;
            if (ins.Any(i => i.Operand is FieldDef f && f.Name == "DlClientListLoader"))
            { l.Level = "SKIP"; l.Text = "已在使用真实下载加载器"; return l; }

            var mdType = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.ModDownload");
            var field = mdType?.Fields.FirstOrDefault(f => f.Name == "DlClientListLoader" && f.IsStatic);
            if (field == null) { l.Level = "FAIL"; l.Text = "找不到 DlClientListLoader 字段"; return l; }

            int endIdx = -1;
            for (int i = 0; i < ins.Count; i++)
            {
                if (ins[i].OpCode != OpCodes.Newobj) continue;
                var mr = ins[i].Operand as MemberRef;
                if (mr == null || mr.Name != ".ctor") continue;
                if (!(mr.Class?.Name.ToString() ?? "").StartsWith("LoaderTask")) continue;
                endIdx = i; break;
            }
            if (endIdx < 0) { l.Level = "FAIL"; l.Text = "未找到「关闭下载」占位任务的构造"; return l; }

            int startIdx = -1;
            for (int i = endIdx - 1; i >= 0; i--)
            {
                if (ins[i].OpCode == OpCodes.Ldstr) { startIdx = i; break; }
                if (ins[i].OpCode == OpCodes.Ret) break;
            }
            if (startIdx < 0) { l.Level = "FAIL"; l.Text = "未找到占位段起点"; return l; }

            bool hasAction = false;
            for (int i = startIdx; i <= endIdx; i++)
                if (ins[i].OpCode == OpCodes.Newobj && (ins[i].Operand as MemberRef)?.Name == ".ctor"
                    && ((ins[i].Operand as MemberRef)?.Class?.Name.ToString() ?? "").StartsWith("Action")) hasAction = true;
            if (!hasAction) { l.Level = "FAIL"; l.Text = "占位段特征不匹配"; return l; }

            int removed = endIdx - startIdx + 1;
            for (int i = endIdx; i >= startIdx; i--) ins.RemoveAt(i);
            ins.Insert(startIdx, Instruction.Create(OpCodes.Ldsfld, field));

            l.Level = "OK"; l.Text = $"下载页入口改为真实加载器（替换 {removed} 条占位指令）";
            return l;
        }

        private static PatchLine Patch3(ModuleDefMD mod)
        {
            var l = new PatchLine { Id = "P3" };
            var pdm = mod.GetTypes().FirstOrDefault(t => t.FullName == "PCL.PageDownloadModify");
            var lif = pdm?.Methods.FirstOrDefault(m => m.Name == "LoadInstanceFinished" && m.HasBody);
            if (lif == null) { l.Level = "SKIP"; l.Text = "LoadInstanceFinished 不存在"; return l; }

            var ins = lif.Body.Instructions;
            for (int i = 0; i < ins.Count; i++)
            {
                if (!(ins[i].Operand?.ToString() ?? "").Contains("get_CardMinecraft")) continue;
                for (int j = i + 1; j < Math.Min(i + 4, ins.Count); j++)
                {
                    if (ins[j].OpCode == OpCodes.Ldc_I4_2)
                    {
                        ins[j].OpCode = OpCodes.Ldc_I4_0;
                        l.Level = "OK"; l.Text = "原版卡片可见性 Hidden → Visible";
                        return l;
                    }
                    if (ins[j].OpCode == OpCodes.Ldc_I4_0)
                    { l.Level = "SKIP"; l.Text = "原版卡片已可见"; return l; }
                    break;
                }
            }
            l.Level = "SKIP"; l.Text = "未找到可见性赋值";
            return l;
        }

        private static List<PatchLine> Patch4(ModuleDefMD mod, byte[] referenceExe)
        {
            var res = new List<PatchLine>();
            if (referenceExe == null || referenceExe.Length == 0)
            {
                res.Add(new PatchLine { Id = "P4", Level = "SKIP", Text = "缺少参照版本，跳过整合包修复" });
                return res;
            }

            ModuleDefMD reference;
            try { reference = ModuleDefMD.Load(referenceExe); }
            catch (Exception ex)
            {
                res.Add(new PatchLine { Id = "P4", Level = "FAIL", Text = "参照版本无法解析: " + ex.Message });
                return res;
            }

            using (reference)
            {
                var imp = new Importer(mod);
                var t = mod.GetTypes().FirstOrDefault(x => x.FullName == "PCL.ModModpack");
                var rt = reference.GetTypes().FirstOrDefault(x => x.FullName == "PCL.ModModpack");
                if (t == null || rt == null)
                {
                    res.Add(new PatchLine { Id = "P4", Level = "SKIP", Text = "整合包模块不存在" });
                    return res;
                }

                foreach (var m in t.Methods.Where(x => x.Name == "ModpackInstall" && x.HasBody).ToList())
                {
                    var tag = $"P4({m.Parameters.Count} 参数)";
                    var rm = rt.Methods.FirstOrDefault(x => x.Name == m.Name && x.Parameters.Count == m.Parameters.Count && x.HasBody);
                    if (rm == null) { res.Add(new PatchLine { Id = tag, Level = "SKIP", Text = "参照版本无对应方法" }); continue; }

                    bool blocked = m.Body.Instructions.Any(i => i.Operand is string s && s.Contains(BlockedText));
                    if (!blocked) { res.Add(new PatchLine { Id = tag, Level = "SKIP", Text = "未被拦截" }); continue; }

                    var bIns = m.Body.Instructions.ToList();
                    var bVars = m.Body.Variables.ToList();
                    var bEh = m.Body.ExceptionHandlers.ToList();

                    if (!Transplant(mod, imp, rm, m, out string why))
                    { res.Add(new PatchLine { Id = tag, Level = "FAIL", Text = "移植失败: " + why }); continue; }

                    if (!Validate(mod, m, out string bad))
                    {
                        m.Body.Instructions.Clear(); foreach (var x in bIns) m.Body.Instructions.Add(x);
                        m.Body.Variables.Clear(); foreach (var x in bVars) m.Body.Variables.Add(x);
                        m.Body.ExceptionHandlers.Clear(); foreach (var x in bEh) m.Body.ExceptionHandlers.Add(x);
                        res.Add(new PatchLine { Id = tag, Level = "FAIL", Text = "引用校验失败，已回滚: " + bad });
                        continue;
                    }

                    res.Add(new PatchLine { Id = tag, Level = "OK", Text = $"整合包分发已还原（{m.Body.Instructions.Count} 条指令）" });
                }
            }

            if (res.Count == 0)
                res.Add(new PatchLine { Id = "P4", Level = "SKIP", Text = "无可处理的入口" });
            return res;
        }

        private static bool Transplant(ModuleDefMD target, Importer imp, MethodDef src, MethodDef dst, out string why)
        {
            why = "";
            try
            {
                var body = dst.Body;
                body.Instructions.Clear();
                body.ExceptionHandlers.Clear();
                body.Variables.Clear();
                foreach (var v in src.Body.Variables) body.Variables.Add(new Local(imp.Import(v.Type)));

                var map = new Dictionary<Instruction, Instruction>();
                foreach (var i in src.Body.Instructions)
                {
                    object operand = i.Operand;
                    if (operand is IMethod mm) operand = imp.Import(mm);
                    else if (operand is IField ff) operand = imp.Import(ff);
                    else if (operand is IType tt) operand = imp.Import(tt);
                    var ni = new Instruction(i.OpCode, operand);
                    map[i] = ni;
                    body.Instructions.Add(ni);
                }
                foreach (var kv in map)
                {
                    var ni = kv.Value;
                    if (ni.Operand is Instruction ti && map.TryGetValue(ti, out var repl)) ni.Operand = repl;
                    else if (ni.Operand is Instruction[] arr)
                        ni.Operand = arr.Select(x => map.TryGetValue(x, out var y) ? y : x).ToArray();
                }
                foreach (var eh in src.Body.ExceptionHandlers)
                {
                    body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
                    {
                        CatchType = eh.CatchType == null ? null : imp.Import(eh.CatchType),
                        TryStart = MapIns(map, eh.TryStart),
                        TryEnd = MapIns(map, eh.TryEnd),
                        FilterStart = MapIns(map, eh.FilterStart),
                        HandlerStart = MapIns(map, eh.HandlerStart),
                        HandlerEnd = MapIns(map, eh.HandlerEnd),
                    });
                }
                return true;
            }
            catch (Exception ex) { why = ex.Message; return false; }
        }

        private static Instruction MapIns(Dictionary<Instruction, Instruction> map, Instruction ins) =>
            ins != null && map.TryGetValue(ins, out var n) ? n : null;

                private static bool Validate(ModuleDefMD mod, MethodDef m, out string bad)
        {
            bad = "";
            var types = new Dictionary<string, TypeDef>();
            foreach (var t in mod.GetTypes()) types[t.FullName.ToString()] = t;

            foreach (var i in m.Body.Instructions)
            {
                MemberRef mr = i.Operand as MemberRef;
                if (mr == null && i.Operand is MethodSpec ms) mr = ms.Method as MemberRef;
                if (mr == null) continue;

                var ownerName = mr.DeclaringType?.FullName.ToString() ?? "";
                if (!ownerName.StartsWith("PCL.")) continue;
                if (!types.TryGetValue(ownerName, out var td))
                { bad = $"{mr.FullName}（类型 {ownerName} 不存在）"; return false; }

                if (mr.IsMethodRef)
                {
                    int argc = mr.MethodSig?.Params.Count ?? 0;
                    bool found = td.Methods.Any(x =>
                    {
                        if (x.Name.ToString() != mr.Name.ToString()) return false;
                        int expected = argc + (x.IsStatic ? 0 : 1);
                        return x.Parameters.Count == expected || (x.IsStatic && x.Parameters.Count == argc);
                    });
                    if (!found) { bad = $"{mr.FullName}（方法不存在）"; return false; }
                }
                else
                {
                    if (!td.Fields.Any(x => x.Name.ToString() == mr.Name.ToString()))
                    { bad = $"{mr.FullName}（字段不存在）"; return false; }
                }
            }
            return true;
        }

        public static string Uninstall(string path, Action<string> log)
        {
            var backup = path + BackupSuffix;
            if (!File.Exists(backup))
                throw new InvalidOperationException("没有找到备份文件，无法卸载：" + backup);
            AssertNotLocked(path);
            var before = Sha256(path);
            File.Copy(backup, path, true);
            var after = Sha256(path);
            log?.Invoke("INFO 卸载前 SHA-256 " + before);
            log?.Invoke("INFO 还原后 SHA-256 " + after);
            if (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(after, Sha256(backup), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("还原后哈希与备份不一致，请手动用 .pclbak 覆盖。");
            return after;
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        private static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }
}
