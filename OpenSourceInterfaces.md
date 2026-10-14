# OpenAC 可参考的开源硬件接口

OpenAC状态核对日期：2026-10-14；上游许可及接口仍按下表固定版本研究。此文针对G-Helper「Libraries and projects used」列出的七个项目，区分接口事实、可链接的库、系统驱动和尚未完成的适配。名单来源固定为 [G-Helper e897b278bf809add90c904ba84aa2e451579dd0c](https://github.com/seerge/g-helper/tree/e897b278bf809add90c904ba84aa2e451579dd0c)。这张名单是G-Helper的来源说明，不能作为OpenAC已支持所有设备的证据。

本接口研究没有引入这七个项目的二进制、安装驱动、加载SMU模块或执行电压/功率设置。OpenAC现有主板HID适配仍保留RIGStats MIT出处。NotebookWorker的独立DSTS状态路径现扩至10个精确型号，并新增6项固定DEVS手动设置准备；依赖用户已安装的签名SCI及当前能力，不表示移动实机已验证，见 [NotebookWorker-protocol.md](NotebookWorker-protocol.md)。没有复制G-Helper、Linux或OpenRGB的实现函数。

## 固定来源与实施边界

| 项目 | 固定版本/提交 | 核实许可 | 可用于 OpenAC 的方式 | 环境与未完成部分 |
|---|---|---|---|---|
| [Linux kernel ASUS WMI](https://github.com/torvalds/linux/blob/24a19b953e2a3d0ea5a8c4951a0ab8f32720c907/include/linux/platform_data/x86/asus-wmi.h) | `24a19b953e2a3d0ea5a8c4951a0ab8f32720c907` | kernel 为 GPL-2.0-only + syscall exception；该内部头标 GPL-2.0 | 核对 DSTS/DEVS、端点编号、presence 位等事实，独立实现 Windows 适配 | Linux 驱动不能链接为 Windows C# 库；Windows 仍需 ASUS SCI 驱动及具体型号验证 |
| [NvAPIWrapper](https://github.com/falahati/NvAPIWrapper/tree/2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7) | `0.8.1.101` / `2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7` | LGPL-3.0 许可正文 | 可考虑合规地引用独立 managed DLL，保留署名、许可、对应源码和替换/重链接途径 | `net45` 可作为 .NET Framework 4.8 候选；依赖已安装 NVIDIA 驱动；本轮未集成或查询 NVAPI |
| [Starlight](https://github.com/vddCore/Starlight/tree/ab183e9dac9a93bb3029fe25ea8093ca33e6515c) | `ab183e9dac9a93bb3029fe25ea8093ca33e6515c` | LGPL-3.0 许可正文 | 可条件合规地引用独立库；当前优先核对 AniMe Matrix/HID 协议事实 | 固定源目标为 .NET 7，不能直接引用到现有 Framework 4.8；不是通用主板 LCD/OLED 适配 |
| [UXTU](https://github.com/JamesCJ60/Universal-x86-Tuning-Utility/tree/eeb43042883bc9a80847999b2957b6ded86938a9) | `eeb43042883bc9a80847999b2957b6ded86938a9` | GPL-3.0 许可正文 | 独立研究 CPU family / SMU 接口事实；不复制其 GPL 实现到 MIT 源 | Manifest 要求管理员；CPU 曲线/功率/降压路径未集成、未执行 |
| [PawnIO](https://github.com/namazso/PawnIO/tree/9d52965895588b0ffa3703b72eec75ba19f4ccc0) | source `9d52965895588b0ffa3703b72eec75ba19f4ccc0`；官方 setup `2.2.0` | 源 driver GPL-2.0-or-later + 明确例外；PawnIOLib LGPL-2.1-or-later；官方 binary 另有 proprietary 条款 | 独立客户端通过限定 IOCTL 的路线有明确例外；若以后启用，应让用户自行取得官方版本，并分别核对模块许可 | 真实 Windows 内核驱动；默认 SYSTEM/管理员访问，官方模块有签名校验；本轮未安装或加载 |
| [ASUSCtl 当前仓库](https://github.com/opengamingcollective/asusctl/tree/b8cf3c14d10bddf54fa8fc6f09f5f6606f58240f) | `6.5.0` / `b8cf3c14d10bddf54fa8fc6f09f5f6606f58240f` | MPL-2.0 | 可核对型号差异与协议；若采用源文件，保留 MPL 并公开受覆盖文件及修改 | Linux Rust daemon / D-Bus / kernel 接口，不是可直接链接的 Windows DLL；未移植 |
| [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB/-/tree/54f82e70042932a9520d676aab995f170863e7de) | `54f82e70042932a9520d676aab995f170863e7de` | 项目 GPL v2；已核 ASUS detector 标 GPL-2.0-or-later | 独立协议实现，或以后开发独立 SDK TCP 客户端与外部 server 通信；不把 controller 函数编进 MIT 源 | USB/HID 与 SMBus 是不同后端；Windows SMBus 使用 PawnIO 且要求管理员，不能宣称全部 RGB 免驱动已支持 |

表中的 LGPL/GPL 版本按实际许可正文与文件声明记录；没有仅依据 GitHub 自动分类推断所有文件都允许「or later」。具体采用哪个文件、库版本及发行方式时，必须保留其原有许可，不能用 OpenAC 的 MIT 文件覆盖第三方条件。

## Linux kernel：接口事实有价值，Windows 需要自己的受限读取层

[asus-wmi.h 固定源](https://github.com/torvalds/linux/blob/24a19b953e2a3d0ea5a8c4951a0ab8f32720c907/include/linux/platform_data/x86/asus-wmi.h)可核对 ASUS 的方法标识、风扇/充电/显示端点和状态位。它是内核内部头，不能把 Linux 的 syscall 例外当作复制其全部实现的通用许可；[COPYING](https://github.com/torvalds/linux/blob/24a19b953e2a3d0ea5a8c4951a0ab8f32720c907/COPYING)和[例外正文](https://github.com/torvalds/linux/blob/24a19b953e2a3d0ea5a8c4951a0ab8f32720c907/LICENSES/exceptions/Linux-syscall-note)分别说明范围。

OpenAC已将少量端点事实写入独立NotebookWorker：固定DSTS、精确ASUS移动机型与SCI驱动校验、限定状态端点和进程期限。新DEVS仅接性能模式、充电上限、键盘亮度、Overdrive、GPU Eco和MUX六项独立类型/范围/依赖校验，不接受任意endpoint。普通状态读取不授权写入；新控制捕获和当前进程会话在发送边界再验。除键盘亮度已有当前值decoder外，不把DSTS capability/nativeStatus当已设置值或恢复凭据。没有移动实机读写验证，没有导入Linux函数或头文件；桌面Fan Xpert、EURUX控制器和主板LCD不能套用这些编号。

## NvAPIWrapper：有可行的独立程序集路线

上游的 [project](https://github.com/falahati/NvAPIWrapper/blob/2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7/NvAPIWrapper/NvAPIWrapper.csproj)明确 `netstandard2.0;net45`、版本 `0.8.1.101`；G-Helper 固定 [project](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/GHelper.csproj)也引用该 NuGet 版本。不是必须把 G-Helper 的 GPL NVIDIA 控制器一起搬过来。

OpenAC 可先实现一个隔离的只读 GPU worker，引用独立 NvAPIWrapper DLL，并仅允许枚举 GPU、名称、驱动版本、温度、当前时钟和利用率等 getter。上游 [PhysicalGPU](https://github.com/falahati/NvAPIWrapper/blob/2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7/NvAPIWrapper/GPU/PhysicalGPU.cs)与 [DelegateFactory](https://github.com/falahati/NvAPIWrapper/blob/2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7/NvAPIWrapper/Native/Helpers/DelegateFactory.cs)提供对应 managed API，并最终调用 native `nvapi64`。必须逐 API 处理不支持、无权限、驱动缺失和超时，不能将库列出的所有功能变成每台 GPU 的支持承诺。当前监控仍使用自写 Windows 读取及 `nvidia-smi`，没有 NvAPIWrapper 适配完成标记。

[LGPL-3.0 第 4 节](https://github.com/falahati/NvAPIWrapper/blob/2a41eb4d6b1bceff5dbb33a3b5ea70bd582214a7/LICENSE)允许满足条件的 combined work：随分发提供准确库署名和 GPL/LGPL 文本、对应库源码/修改，并保留用户调试修改及替换/重链接该库的途径。OpenAC 自有文件可以继续 MIT；第三方 DLL 与其修改不能改标 MIT。强名称签名不等于 native DLL 的 Authenticode 签名，发布设计应避免用不可替换的签名/固定哈希封死 LGPL 要求的修改途径。应使用上游源码/包作来源，不能从 G-Helper 可执行文件中取出不明修改的 DLL 当作已审核版本。

另一个候选是依据 [NVIDIA 官方公共 NVAPI SDK](https://github.com/NVIDIA/nvapi/tree/70d337db9186e968eab622f7e786de7e437faf3d)独立写最小绑定。固定源的 `nvapi.h` 标 MIT，[License.txt](https://github.com/NVIDIA/nvapi/blob/70d337db9186e968eab622f7e786de7e437faf3d/License.txt)覆盖公共 `.lib`；[README](https://github.com/NVIDIA/nvapi/blob/70d337db9186e968eab622f7e786de7e437faf3d/README.md)区分这些公开 SDK 文件与显示驱动中的实际 NVAPI 实现。公共 SDK 的 MIT 不赋予重新分发 NVIDIA 显示驱动任意 DLL 的许可。本轮不加载这些 DLL；后续需要确认已安装 native 库来源、架构、签名与实际 GPU 响应。也不默认开放驱动重启、风扇设置、超频、功率、电压或降压。

## Starlight：AniMe Matrix 专用，不能冒充全部屏幕适配

[README](https://github.com/vddCore/Starlight/blob/ab183e9dac9a93bb3029fe25ea8093ca33e6515c/README.md)的明确目标是 Zephyrus G14 2022 GA402 的 AniMe Matrix 和 Aura。[基本库 project](https://github.com/vddCore/Starlight/blob/ab183e9dac9a93bb3029fe25ea8093ca33e6515c/Starlight/Starlight.csproj)是 `.NET 7`；[Windows transport](https://github.com/vddCore/Starlight/blob/ab183e9dac9a93bb3029fe25ea8093ca33e6515c/Hideous/Platform/WindowsUsbProvider.cs)用 HidSharp 对实际匹配设备执行 HID feature/read/write，并没有自己的 Windows 内核驱动。

可依据 [LGPL-3.0](https://github.com/vddCore/Starlight/blob/ab183e9dac9a93bb3029fe25ea8093ca33e6515c/LICENSE.md)条件使用独立库，但当前 Framework 4.8 应用不能直接加载它的 .NET 7 程序集。可评估另行隔离 helper，或者保持现有自写有界 HID worker、仅采用已核协议事实。其 [AniMe 型号描述](https://github.com/vddCore/Starlight/blob/ab183e9dac9a93bb3029fe25ea8093ca33e6515c/Starlight/Asus/AnimeMatrix/AnimeMatrixDevice.cs)针对特定 VID/PID 和像素布局；不得把它当作 AZOTH OLED、主板 5 英寸 LCD 或 Slash 的同一协议。未移植其像素映射、未发送唤醒/初始化/帧数据，HidSharpCore 依赖及任何新增 helper 运行时也需要单独审核。

## UXTU 与 PawnIO：CPU 调校和驱动应分层处理

UXTU 的 [GPL-3.0](https://github.com/JamesCJ60/Universal-x86-Tuning-Utility/blob/eeb43042883bc9a80847999b2957b6ded86938a9/LICENSE)覆盖其实现。固定源 `RyzenSmu.cs` / `AMDPawnIO.cs` 是按 CPU family 选择的 SMU/mailbox、曲线和功率路径；[app.manifest](https://github.com/JamesCJ60/Universal-x86-Tuning-Utility/blob/eeb43042883bc9a80847999b2957b6ded86938a9/Universal%20x86%20Tuning%20Utility/app.manifest)要求管理员。它不是 ASUS LCD、风扇集线器或外设通用驱动。当前 MIT 源不链接或翻译这些 GPL 函数；CPU 调校仍未实现。

PawnIO 必须区分三层。Driver 的 [源码许可与 IOCTL 例外](https://github.com/namazso/PawnIO/blob/9d52965895588b0ffa3703b72eec75ba19f4ccc0/README.md)明确允许独立程序只经设备 IO control 通信的组合，但不把载入 Pawn 接口的模块一起豁免。该项目的 [PawnIOLib](https://github.com/namazso/PawnIO/blob/9d52965895588b0ffa3703b72eec75ba19f4ccc0/PawnIOLib/PawnIOLib.cpp)标 LGPL-2.1-or-later；官方模块 [RyzenSMU.p](https://github.com/namazso/PawnIO.Modules/blob/24425a5299e84410751da882c121f322fb030b82/RyzenSMU.p)也须保留其 LGPL 声明。官方[二进制使用说明](https://github.com/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules)将签名/不受限 binary editions 列为 proprietary，并允许转发 installer；推荐引导用户从 [pawnio.eu](https://pawnio.eu/) 自行取得再本地组合。不能把官方安装包标成 GPL 源码构建物。

[固定 INF](https://github.com/namazso/PawnIO/blob/9d52965895588b0ffa3703b72eec75ba19f4ccc0/PawnIO/PawnIO.inf.in)显示真实 kernel service，默认安全描述只授予 SYSTEM/管理员；[模块加载代码](https://github.com/namazso/PawnIO/blob/9d52965895588b0ffa3703b72eec75ba19f4ccc0/PawnIO/src/vm.cpp)验证签名模块。官方 [Setup 2.2.0](https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0)允许配置非管理员暴露，不等于默认没有权限要求。OpenAC 不修改这些 ACL、不采用测试签名绕过方式、不安装服务、不调用任何 SMU/电压/功率命令。将来若考虑读取 CPU 遥测，需要分别确定 CPU family、签名模块、权限和只读命令，而不能直接启用 G-Helper 的整套初始化流程。

## ASUSCtl：MPL 文件可以单独保留，Linux 后端仍需要移植

G-Helper 链接的 [旧 GitLab 固定 README](https://gitlab.com/asus-linux/asusctl/-/blob/321b1114a0ac3782d67aa39201682effb749f4d9/README.md)宣布迁移到 OpenGamingCollective；旧 workspace 版本为 6.3.8，当前固定 [Cargo.toml](https://github.com/opengamingcollective/asusctl/blob/b8cf3c14d10bddf54fa8fc6f09f5f6606f58240f/Cargo.toml)为 6.5.0。[MPL-2.0 第 3 节](https://github.com/opengamingcollective/asusctl/blob/b8cf3c14d10bddf54fa8fc6f09f5f6606f58240f/LICENSE)要求受覆盖源文件及修改保留 MPL、提供对应源和 notice，并允许满足条件的 larger work 使用自己的其他许可；它不是要求所有无关 OpenAC 文件都改成 MPL，也不是允许把 copied 文件统一改成 MIT。

其 [平台说明](https://github.com/opengamingcollective/asusctl/blob/b8cf3c14d10bddf54fa8fc6f09f5f6606f58240f/README.md)描述 Linux `asusd`、D-Bus、kernel 平台接口和按型号变化的灯光映射。这些组件不能直接替换 Windows 的 ASUS SCI/USB worker。可用来核对 AniMe/Slash/Aura 格式、型号分歧和平台能力，再独立设计 Windows 后端；本轮没有移植或执行 daemon、没有借它的支持表宣称全部型号已验证。

## OpenRGB：协议或外部 SDK 值得参考，GPIO/SMBus 不等于 USB

[项目许可](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/54f82e70042932a9520d676aab995f170863e7de/LICENSE)为 GPL v2；[本轮 ASUS detector](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/54f82e70042932a9520d676aab995f170863e7de/Controllers/AsusAuraUSBController/AsusAuraUSBControllerDetect.cpp)明确 GPL-2.0-or-later。MIT 源可以保持独立研究已证 VID/PID、report 字段和效果编号，但不复制其 controller 函数、检测算法或完整型号/布局表，再用重命名当成自有实现。

另一条路线是依据[官方 OpenRGB SDK 网络协议](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/54f82e70042932a9520d676aab995f170863e7de/Documentation/OpenRGBSDK.md)写独立客户端，由用户已安装的外部 OpenRGB server 负责硬件。官方明确提供这种 third-party 客户端用途；这是新后端设计，不是把 GPL 核心链接进 OpenAC。也不能声称任何「单独进程」安排都自动豁免复制代码的许可。当前没有 SDK 客户端、没有启动 OpenRGB、没有下载它的二进制。

Windows [USB 文档](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/54f82e70042932a9520d676aab995f170863e7de/Documentation/USBAccess.md)与 [SMBus 文档](https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/54f82e70042932a9520d676aab995f170863e7de/Documentation/SMBusAccess.md)的要求不同：SMBus 的 Windows 路径使用 PawnIO 并要求管理员；USB 路径不等于必须装同一内核驱动。OpenAC 现有 DirectAuraWorker 只覆盖来源已准备的 USB 协议族，不涵盖内存 SMBus、所有显卡 LED、主板物理灯珠数量或 Gen 模式。

## 可执行的下一阶段与当前状态

1. 优先评估独立 NvAPI 只读 worker。两种可选来源为可替换 LGPL NvAPIWrapper DLL，或 NVIDIA 当前 MIT 公共 SDK 的最小绑定；最终发行需固定文件哈希、notice、构建及依赖来源。还没有实施，GPU 设置仍不能点击应用。
2. 对已写NotebookWorker的10精确型号/6项手动SCI路径做移动实机读取及明确用户控制验收。风扇曲线、功率、GPU自动迁移/恢复仍缺；不能把source准备或提交应答当完成适配。
3. AniMe Matrix/Slash、OpenRGB SDK、ASUSCtl 的 Windows 移植分别作为新后端，先合成测试，再对应实机读取验证；不以一个 VID 或项目名称宣称所有显示器/键鼠/主板支持。
4. UXTU/PawnIO 的内核与 SMU 路线保留为研究候选，没有自动安装、管理员提权或默认电压设置。

私有参考文本及 SHA-256 清单保存在 `work/reverse/library-audit`，不会随公开源码分发原厂/GPL参考实现。此文仅记录目前查明的采用方案；如果以后实际加入第三方文件、程序集或服务，必须同步更新 `THIRD-PARTY-NOTICES.md`、`PROVENANCE.md` 和精确发行清单。

## 本轮独立实现的边界

WindowsBrightness依据Microsoft的WmiMonitorBrightness/WmiSetBrightness接口新写，使用独立worker监督期限、当前支持级别与写后读取。provider异常或无有效方法结果时，本次AppDomain禁再次写入，手动读取不能清掉限制；不引用G-Helper亮度实现。见 [WindowsBrightness-protocol.md](WindowsBrightness-protocol.md)。

FanNativeWorker仅准备固定已安装ASUS服务的正常COM getter。普通读取的进程映像前检被拒绝，另一次管理员扩展读在曲线ABI校验失败；没有可用的完整公共runtime快照或fan控制。不修改ACL、不改变caller、不复用原版认证；原始Duty/Curve数字不叫百分比/°C/RPM。见 [FanNative-protocol.md](FanNative-protocol.md)。

外接键鼠新增9家族18个PID的静态RGB/软件归属协议为独立MIT实现，没有移入GPL型号表或函数；多区先验证整组计划，失败/未知读回停止，不借未知拓扑自动动画。对应新增设备尚未实机验证，见 [GearLinkLightingFamilies-protocol.md](GearLinkLightingFamilies-protocol.md)。共享相位Aura仅能提交整区统一颜色；此接口研究不意味着完整OpenRGB或G-Helper功能已经实现。

PID7489新增耳机最小lighting scope同样为独立C#实现，复用现有已核legacy GET/LED开关/Static1报文字段事实，未复制ASUS/GPL函数。单HID句柄内先完整读取，构造全部步骤，显式启用后的完整保留字段读回和最终recipe读回分开；16秒总预算及进程监督，partial/未知结果停止，没有自动退出Aura或恢复。源码与609项合成检查见 [GearLinkHeadsetLighting-protocol.md](GearLinkHeadsetLighting-protocol.md)，没有此新灯光路径的实机SET验证。
