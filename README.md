<p align="center">
  <img src="docs/icon.png" width="120" alt="Polaris">
</p>

<h1 align="center">Polaris 修复插件</h1>

<p align="center">给 PCL2 恢复「下载原版 Minecraft」入口 · 单文件 · 免安装</p>


给 PCL2（Plain Craft Launcher 2）恢复「下载原版 Minecraft」入口的补丁安装器。
单文件 exe，WinForms + dnlib，.NET Framework 4.8。

## 界面

<p align="center">
  <img src="docs/preview.png" width="780" alt="Polaris 界面">
</p>

## 它做什么

PCL2 `2.99.0.0`（界面显示「正式版 2.12.3」）里，下载入口被编译期常量折叠掉了：
`PCL.ModSecret.RestrictMcDownload` 是 `Public Const ... = True`，于是相关代码根本没被编译进 exe。
整套下载机器其实都还在，只有 4 个入口被拦。

Polaris 在副本上改 IL，把这 4 处写回去：

| 补丁点 | 位置 | 改法 |
|---|---|---|
| P1 | `ModSecret::RestrictMcDownload` | 常量 `True` → `False` |
| P2 | `PageDownloadInstall::LoaderInit` | 伪造的 `LoaderTask("关闭下载")` → 真实 `DlClientListLoader` |
| P3 | `PageDownloadModify::LoadInstanceFinished` | `Visibility` `Hidden(2)` → `Visible(0)` |
| P4 | `ModModpack::ModpackInstall()` / `(String,String,String)` | 从参照版本移植 41 / 330 条指令 |

改完复验 4 项全过才落盘，任何一项失败自动回滚成原字节。其余 3 万余方法逐字节不动，不碰强名、版本号、资源与上游接口。

## 功能

- **自动检测 PCL2**：扫描自身目录 / 上次路径 / 桌面（含子目录）/ 下载 / `C:\PCL` 等；
  按程序集内容识别（`Plain Craft Launcher 2` 或存在 `PCL.ModBase`），文件名不必标准；
  多份候选按 **正式版 > 社区版 > 快照版 > 加壳/未知** 排序，同档优先**桌面**的文件，最后比 `VersionCode`
- **版本类型识别**：正式版 / 快照版 / 社区版；没有下载限制的版本（如 2.14 快照）直接判为「无需修复」
- **加壳识别**：CLR 目录为空的原生封装 / VMProtect 之类，明确提示「无法修补」而不是报错或静默跳过
- **安装 / 卸载**：安装前自动备份 `.pclbak`，卸载用备份还原
- **启动 PCL**：工作目录跟随 exe，便携模式不跑偏
- **自动更新**：监视目标目录，PCL 自更新把 exe 换掉后自动重打补丁（可关）
- **PCL2 GitHub 发布页**：一键打开官方发布页
- **加入 QQ 群 / 赞助**：左下角入口，赞助弹窗展示微信赞赏码与支付宝码（扫码即可，不影响任何功能）
- 深色 UI、星光背景、状态灯缓动、按钮扫光；检测与安装全程后台线程

## 构建

```bash
cd src
dotnet build -c Release
# 产物: src/bin/Release/net48/Polaris修复插件.exe
```

需要 .NET SDK 6+（项目目标框架 `net48`，通过 `Microsoft.NETFramework.ReferenceAssemblies` 提供参考程序集）。

### 关于 P4 与 `lib/pcl214.exe`

P4 需要一份**未受限的旧版 PCL2**（2.14 快照版）作为移植来源，仓库**不附带**这个二进制。
把它放到 `src/lib/pcl214.exe` 再编译即可启用 P4；缺失时 P4 补丁会跳过，而复验要求 4 项全过，
因此产物会在安装阶段回滚 —— 这是刻意的「要么全恢复，要么不动」策略。

## 命令号（自动化）

```
Polaris修复插件.exe --cli analyze   "<PCL exe>"
Polaris修复插件.exe --cli install   "<PCL exe>"
Polaris修复插件.exe --cli uninstall "<PCL exe>"
Polaris修复插件.exe --cli detect
Polaris修复插件.exe --cli auto      "<PCL exe>"
```

## 目录

```
src/
  MainForm.cs      界面与交互
  Theme.cs         主题、控件、绘制
  Patcher.cs       IL 补丁引擎（dnlib）
  Finder.cs        目标扫描与识别
  Guard.cs         安装记录、自动更新（FileSystemWatcher）
  Program.cs       入口 + CLI
  assets/          图标与 Logo
  lib/dnlib.dll    dnlib 4.5.0（MIT）
```

## 支持范围

| 目标 | 情况 | 工具行为 |
|---|---|---|
| 正式版 2.99.0.0（2.12.3） | 有下载限制 | 安装 4 处补丁 |
| 快照版 2.14.0.0 | 无限制 | 「无需修复」，4/4 状态灯全绿 |
| 快照版 2.99.0.1（416） | 有下载限制 | 同正式版：P1/P2/P3 打补丁；该版本 P4 未受限，自动跳过 |
| 社区版 | 视构建而定 | 有占位任务/限制文案就补，否则「无需修复」 |
| 加壳 / 原生封装 | 无托管元数据 | 「已加壳 · 无法修补」，并给出内含版本串 |

## 说明

- 只改本机 PCL2 的副本，安装前自动备份，可一键还原。
- 不联网，不写注册表；仅在 `%APPDATA%\Polaris\` 存日志、设置与安装记录。
- 目标版本为 PCL2 `2.99.0.0`；结构不同的版本会提示「版本不支持」而不是乱改。

作者：热带带鱼 · QQ 群：https://qm.qq.com/q/zVmb9qbeJW
