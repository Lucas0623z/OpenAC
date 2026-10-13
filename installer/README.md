# OpenAC 安装器

这是本项目新写的 C# / Windows Forms 安装器，使用 Windows 本机 .NET Framework 编译器构建。它没有采用 NSIS、Inno Setup 或 ASUS 安装器代码；不分发编译器、运行时、原厂驱动或 SDK。

## 安装范围

- 仅当前用户，固定目录为 `%LOCALAPPDATA%\Programs\OpenAC`，不可改到其他目录。
- 只安装 `RuntimeFiles.json` 的 16 个明确文件；主程序为 `OpenAC.exe`。
- 可选择当前用户的开始菜单及桌面快捷方式，卸载条目仅写 HKCU。
- 显示 MIT 许可证，需勾选后点击安装；不会自动启动主程序。
- 安装不修改 ASUS 服务、驱动、固件或硬件设置。OpenAC 的实验性设备写入仍默认关闭。
- 清单不包含官网图片、用户配置、原厂二进制、源码包、测试程序或 reverse-analysis 下载。

此预览安装器未签名。源码与发行 hash 可供核对；这些 hash 不是代码签名或 ASUS 授权。

## 构建

需要 Windows x64、.NET Framework 4.8 及其 C# 编译器。先由主项目完成干净的公开运行载荷，确保所有需要的 notices 与最新源码一致。

```powershell
.\installer\build-installer.ps1 -PayloadDirectory .\outputs\OpenAC-0.3-PublicPreview -RunChecks
```

也可指定新输出目录和数字版本：

```powershell
.\installer\build-installer.ps1 -PayloadDirectory C:\path\to\reviewed-runtime -OutputDirectory .\installer\artifacts\reviewed-build-1 -Version 0.3.0 -RunChecks
```

脚本要求新的输出目录，不覆盖或删除既有构建。它按精确白名单读取文件、拒绝 reparse points，生成内嵌 ZIP 与逐文件 SHA256 清单，编译 x64 WinForms EXE，然后运行只验证模式。构建期允许生成文件，但不会执行安装、卸载、注册快捷方式或运行程序载荷。

产物包括：

- `OpenAC-0.3.0-preview-Setup.exe`
- `VERIFY.json`、`BUILD-INFO.json`、`SHA256SUMS.txt`
- 供构建审查的 `InstallerPayload.json` / ZIP；它们不是额外安装载荷。
- 使用 `-RunChecks` 时生成纯测试 EXE；该测试 EXE不加入安装 ZIP。

## 只验证模式

```powershell
.\OpenAC-0.3.0-preview-Setup.exe --verify
```

`--verify` 校验内嵌 metadata、ZIP 路径、数量、长度、SHA256 和固定目标规则。返回 JSON 与 exit code：成功 0，失败 1。它不创建安装目录，不读取安装记录/注册表，不创建快捷方式，不运行 OpenAC 或任何设备 worker。对 GUI EXE，若宿主没有显示输出，请按构建脚本方式重定向 stdout 并检查 exit code。

## 卸载和文件所有权

安装时保存 `INSTALLER-OWNERSHIP.json`，并将同一安装器复制为 `Uninstall.exe`。卸载时不会把可编辑 state 当成可信 hash 来源；版本、全部 16 个 runtime 文件和卸载程序的路径/长度/hash，都必须逐项匹配卸载程序自身内嵌原始载荷及自身 EXE hash。

卸载只针对固定 OpenAC 安装目录中的白名单文件。内容已修改的发行文件、未知文件和全部用户配置保留。快捷方式只有目标和参数都对应 OpenAC 时才删除；注册表条目只有产品标识与固定安装位置匹配时才删除。仅删除已知空目录，不使用递归删除。

`Local\OpenAC-Installer` mutex 只保护实际安装/卸载 mutation，重复进程立即返回忙碌错误。安装界面不长期占有 mutex。路径、安装记录和文件校验在删除前完成；可见的 reparse points、junctions 或不一致记录会让操作失败。

由于 Windows 不允许删除正在运行的 EXE，卸载入口在用户点击后把自己复制到 `%TEMP%\OpenAC-Uninstall-<GUID>\Uninstall.exe`，等待原窗口退出，再执行受保护的卸载流程。临时副本保留，关闭卸载窗口后可手动删除该明确临时目录，不启动脚本自删或设置开机删除。

此版不覆盖升级：已有安装必须先通过其原卸载入口卸载；若有修改过的文件被保留，需自行备份/迁出后再安装。旧配置目录 `%LOCALAPPDATA%\ROG Control` 为兼容早期草稿继续保留，本安装器不迁移或删除它。

## 纯测试和当前验证边界

`InstallerTests.cs` 使用内存合成 metadata/ZIP/self bytes，验证精确目标、路径穿越/DOS 设备名、字段类型、数量/重复/成员、长度/hash 和 state 与可信原载荷的绑定。它不调用生产 Install/UninstallFiles、不接触注册表或快捷方式、不运行载荷或硬件接口。

本轮纯测试为 130 项通过。安装 UI 已编写，构建和 `--verify` 可独立复核。实机安装、卸载、快捷方式和注册表 mutation 尚未实际执行，因此不能把纯测试声称为完整安装/卸载实机验证。

## 来源

`InstallerCore.cs`、`Setup.cs`、测试与构建脚本为本项目新写。Windows Script Host/CreateShortcut 是 Microsoft Windows 接口事实，以 C# 反射调用，没有复制第三方 installer 实现。原下载失败的 `toolchain` 文件不是依赖，不进入公开源或安装载荷。项目正式 MIT license 与第三方 notices 位于上级目录；它们也随运行载荷安装。

