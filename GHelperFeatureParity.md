# G-Helper 功能对照：独立实现、保留 MIT

核对日期：2026-10-13。目标版本固定为 [G-Helper e897b278bf809add90c904ba84aa2e451579dd0c](https://github.com/seerge/g-helper/tree/e897b278bf809add90c904ba84aa2e451579dd0c)，不是不断变化的主分支。该版本的 [许可证](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/LICENSE) 是 GPL-3.0；OpenAC 保持 MIT，继续独立实现。下表只记录功能和接口事实，没有复制 G-Helper 的函数、曲线修正、自动切换算法、设备表、UI 或程序库。LGPL 库单独引用时仍需满足其条件；本轮没有引入它们。许可细节见 [OpenSourceInterfaces.md](OpenSourceInterfaces.md)。

这是固定版本的功能类别清单和当前缺口，不是“已经完全兼容”的宣传。表中的读路径存在，不代表本机有该硬件或已验证写入。原 Armoury 页面、官方产品目录、按钮、配置草稿和协议准备均不能代替实际能力检测。

## 状态和实现边界

- **只读准备**：自有 worker 有严格读接口和纯测试；移动硬件尚未实机验证。部分 DSTS 只返回原始状态，不能证明当前配置。
- **局部实现**：自有代码已实现部分型号或不同设备上的相关功能，表内明确范围；不是整个 G-Helper 功能的替代。
- **未实现**：没有完整执行路径；研究过接口也仍算未实现。
- **当前不可读**：合法查询没有提供可信数据，或被权限/接口限制；保持未知。

移动设备的当前精确准备型号只有 RC71L、RC72LA、RC73YA、RC73XA、GA402RJ、GU605MI、FX507ZC4。每次读请求重新核实 ASUS 身份、移动机箱类型、精确型号和已签名 System Control Interface 驱动。未确认型号不发送 ATKACPI IOCTL。详情和 117 项纯测试见 [NotebookWorker-protocol.md](NotebookWorker-protocol.md)。所有 NotebookWorker 写入均在 I/O 前拒绝；GUI 的实验性写入开关不会解除此限制。

下表中的“已有 SCI”指用户机器已安装的 ASUS System Control Interface 内核驱动，并非 OpenAC 自带或新安装驱动。正常 SCI/HID 接口是否允许普通用户访问取决于已有驱动 ACL；不能因为 G-Helper 可调用，就保证任何环境都无需权限。PawnIO 是另外的低层驱动路径，不能混为 SCI。

## 固定源索引

每个索引都固定到同一提交。接口研究的原文件仅保存在私有 `work/reverse/ghelper` 与 `work/reverse/ghelper-parity`，公开包不含 GPL 原代码。

| 索引 | 固定上游源 | 用途 |
|---|---|---|
| A | [AsusACPI.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AsusACPI.cs) | SCI/ACPI 端点、模式、风扇、显示与设备状态 |
| F | [Fans.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Fans.cs)、[FanSensorControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Fan/FanSensorControl.cs) | 风扇、功率与高级设置 |
| M | [ModeControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Mode/ModeControl.cs)、[PowerNative.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Mode/PowerNative.cs) | 自定义模式、Windows 电源配置 |
| G | [GPUModeControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Gpu/GPUModeControl.cs) | GPU 开关、MUX、自动切换、XGM |
| N | [NvidiaGpuControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Gpu/NVidia/NvidiaGpuControl.cs)、[NvmlHelper.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Gpu/NVidia/NvmlHelper.cs) | NVIDIA 驱动接口 |
| D | [AmdGpuControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Gpu/AMD/AmdGpuControl.cs)、[IntelVram.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Gpu/IntelVram.cs) | AMD/Intel 显示设置 |
| S | [ScreenControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Display/ScreenControl.cs)、[ScreenBrightness.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Display/ScreenBrightness.cs)、[ScreenELMB.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Display/ScreenELMB.cs) | 屏幕设置 |
| V | [VisualControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Display/VisualControl.cs) | GameVisual/Splendid、色域、调光 |
| H | [HardwareControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/HardwareControl.cs)、[BatteryControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Battery/BatteryControl.cs) | 传感器、电池、电源状态 |
| L | [Aura.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/USB/Aura.cs)、[AsusLampArray.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/USB/AsusLampArray.cs) | 笔记本背光与灯效 |
| X | [XGM.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/USB/XGM.cs) | XG Mobile 接口 |
| I | [InputDispatcher.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Input/InputDispatcher.cs)、[MKeyControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Input/MKeyControl.cs)、[NumberPad.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Input/NumberPad.cs) | 热键与输入设备 |
| E | [Extra.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Extra.cs)、[ClamshellModeControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Helpers/ClamshellModeControl.cs) | 额外设置与自动策略 |
| AM | [AnimeMatrixDevice.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AnimeMatrix/AnimeMatrixDevice.cs)、[SlashDevice.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AnimeMatrix/SlashDevice.cs) | LED 矩阵、Slash |
| AL | [AllyControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Ally/AllyControl.cs) | 掌机控制器 |
| O | [HardwareOverlay.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Overlay/HardwareOverlay.cs)、[EtwFpsMonitor.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Overlay/EtwFpsMonitor.cs) | 桌面叠层、ETW FPS |
| P | [RyzenSmu.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Pawn/RyzenSmu.cs)、[IntelMsr.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Pawn/IntelMsr.cs) | 特定 CPU 低层通路 |
| U | [UpdatesController.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/UpdatesController.cs)、[AutoUpdateControl.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AutoUpdate/AutoUpdateControl.cs) | 官网目录与应用更新 |
| PM | [AsusMouse.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Peripherals/Mouse/AsusMouse.cs)、[AsusMouseSettings.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AsusMouseSettings.cs) | 鼠标协议与设置 |
| PK | [AsusKeyboard.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Peripherals/Keyboard/AsusKeyboard.cs)、[AsusKeyboardSettings.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AsusKeyboardSettings.cs) | 外接键盘协议与设置 |
| PH | [AsusHeadset.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Peripherals/Headset/AsusHeadset.cs)、[AsusHeadsetSettings.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/AsusHeadsetSettings.cs) | 耳机协议与设置 |

## 模式、风扇、CPU 和 GPU

| 功能 | 固定源接口事实 | OpenAC 当前状态与对应代码 | 依赖 / 尚缺内容 |
|---|---|---|---|
| BIOS 静音/平衡/增强 | A/M：PerformanceMode SCI | 只读准备：NotebookWorker `performanceMode`；原始 queryValue，不保证当前配置 | 已有 SCI，精确机型；模式 setter 未实现 |
| 全速/手动/新增、命名、删除自定义模式 | A/F/M | 未实现；情境配置不等于 BIOS 模式 | 需独立模式存储、验证、恢复行为 |
| CPU/GPU/第三风扇实时 RPM | A/H/F：对应 DSTS | 只读准备：`cpuFan/gpuFan/middleFan` | 七型号门禁；第三风扇不用于掌机；无移动实机 |
| 笔记本三路自定义风扇曲线 | A/F | 未实现；EURUX 曲线属于独立桌面控制器 | SCI/EC 曲线能力、有效节点、原曲线读回和恢复 |
| 风扇滞后/响应时间 | A/F：FanHysteresis | 未实现 | 特定固件，不可套用 EURUX 参数 |
| 每个模式的持续/慢速/快速功率限制 | A/F | 未实现 | 不同代际的 SPL/sPPT/fPPT、Intel PL1/PL2 不同 |
| CPU EDC/TDC、交叉负载、温度目标 | A/F | 未实现 | 型号/代际支持与范围必须单独证实 |
| CPU→GPU 动态功率分配、GPU 功率/温度目标 | A/F | 未实现 | SCI、支持的 NVIDIA/BIOS 型号 |
| Windows 电源模式与 CPU Boost | M | 局部实现：WindowsPower + 工具页，已有方案、AC/DC Boost、手动应用/读回/失败回滚/显式恢复 | 335纯检查+26界面检查；仅实机GET，未实写；电源滑块、自动策略仍未实现；不是BIOS风扇模式 |
| P/E 核开关及数量 | A/E | 未实现 | 特定 Intel SKU、最小/最大核数和重启条件 |
| AMD Curve Optimizer / Undervolt | P/F | 未实现 | CPU 家族 SMU；PawnIO 模块/权限；没有电压写入 |
| 低层 Intel 功耗/能量读写 | P | 未实现 | MSR 驱动/模块；不能用 WMI 标称值代替 |
| GPU Eco/Standard | A/G | 只读准备：`gpuEco` 原始状态；切换未实现 | 已有 SCI；桌面 GPU 不适用该笔记本通路 |
| GPU Ultimate / MUX | A/G | 只读准备：`gpuMux`；切换未实现 | 机型、外接显示、可能重启，不能只改一个布尔值 |
| GPU Optimized、AC/电池自动切换 | G/M | 未实现 | 需要独立状态机、失败回滚、GPU占用判断 |
| dGPU 使用进程列表、结束占用进程、异常恢复 | G/N | 未实现 | 需确认用户动作；不能拿未知 GPU 状态自动结束应用 |
| NVIDIA 核心/显存偏移 | N/F | 未实现 | 已安装 NVIDIA 驱动；不能复制 GPL 调节函数 |
| NVIDIA 最大频率锁定/降压相关设置 | N/F | 未实现 | 一些 G-Helper 调用属于私有驱动接口，公共 SDK 不保证同等功能 |
| NVIDIA 实时电压 | N | 当前不可读 | 公共 Pstates20 的 voltage setting 不是实际 VCore；SMI 已移除 graphics voltage |
| AMD VariBright / OLED 电源优化 | D/V | 未实现 | 已安装 AMD 驱动/ADL、兼容面板 |
| AMD FPS 限制 / GPU 时钟读取 | D | 未实现 | ADL 与特定 GPU；NVIDIA SMI 不覆盖 AMD |
| APU/Intel iGPU 预分配显存 | A/E/D | 未实现 | ASUS SCI 或 Intel 专用路径；可能需重启 |
| XG Mobile 连接、激活、断开 | A/G/X | 未实现；通用发现入口不等于可控制 | 具体 XGM 代际、HID/SCI，安全的断开流程 |

## 屏幕、电池、背光和机身设备

| 功能 | 固定源 | OpenAC 当前状态与对应代码 | 依赖 / 尚缺内容 |
|---|---|---|---|
| 刷新率选择与 AC/电池自动刷新率 | S | 局部实现：WindowsDisplay + 工具页，真实屏幕/当前模式/整数候选、手动应用/读回/恢复 | 87后端+51界面纯检查；实机只读两屏，未修改；精确时序单独报告；分数值选择、内屏自动策略尚缺 |
| Overdrive | A/S | 只读准备：`displayOverdrive` 原始状态 | 精确面板/固件；setter 未实现 |
| MiniLED 单/多分区 | A/S | 未实现 | 两代不同 SCI 端点；普通 RGB/主板 LCD 不等价 |
| 面板亮度 / 亮度快捷键 | S/I | 未实现 | 内屏 WMI/显示驱动；设备 LCD 亮度不等价 |
| Flicker-free dimming | V/S | 未实现 | 色彩处理/面板组合，需要独立视觉与范围验证 |
| ELMB | S/E | 未实现 | 兼容 ASUS 内屏与协议 |
| FHD 模式 / HDR 控制 / Optimal Brightness | A/S | 未实现 | 特定屏幕与固件，不可在桌面发送未知端点 |
| GameVisual/Splendid、色域、白点、护眼 | V | 未实现 | 上游部分使用已安装 ASUS 工具；不能直接分发或执行来宣称自有实现 |
| 电池充电上限 | A/H | 只读准备：`chargeLimit` 返回 nativeStatus | 不是确认的当前上限；setter 未实现 |
| 电源适配器类型 | A/H | 只读准备：`chargerMode` | 已有 SCI；仅已证实状态语义 |
| 电量、健康、设计/满充容量、充放电功率 | H | 局部实现：WindowsBattery + 工具页，Windows状态及可缺失WMI字段、单位/身份/未知值校验 | 351后端+758界面纯检查；本机无系统电池，不代表真实笔记本WMI provider已验；充电上限SCI setter仍缺 |
| 笔记本键盘背光亮度 | A/L | 只读准备：`keyboardBrightness` | 0–3原始级别，仅笔记本门禁；写入未实现 |
| 笔记本 RGB 模式/颜色/速度/动画 | L/A | 未实现；DirectAura 是桌面 USB 控制器 | ROG HID、TUF SCI、LampArray 是不同通路 |
| 背光 AC/电池超时、开机/关机/睡眠策略 | L/E | 未实现 | 机型、Windows事件、状态恢复 |
| AniMe Matrix 图像/GIF、时钟、文字、音频可视化 | AM | 未实现 | 精确矩阵布局、HID、流控；本项目主板 LCD JPEG 不是矩阵功能 |
| Slash 灯效、亮度、动画与音频 | AM | 未实现 | Slash 的独立 HID 格式/模型范围 |
| Fn-Lock、M键、Fn快捷键 | A/I | 局部实现：通用 MacroEngine 热键/宏；ASUS Fn/SCI 事件未实现 | 通用低层键钩不保证收到固件吞掉的 Fn 组合 |
| 触控板开关、NumberPad | A/I | 未实现 | 特定 ACPI/HID 输入设备，实际状态读回 |
| ScreenPad/DUO 第二屏/亮度 | A/S/I | 未实现 | 双屏机型专用端点与实际显示器映射 |
| 摄像头遮挡/LED、麦克风/静音 LED、启动音效 | A/E/I | 局部实现：Windows 音频端点音量/静音；机身 LED/启动音效未实现 | 系统音频静音不等于 ASUS LED 控制 |
| 平板/帐篷/翻转/合盖状态与策略 | A/E | 未实现 | 兼容传感器/移动模式、Windows电源行为 |
| Ally 控制器模式、背键组合与按键绑定 | AL/I | 未实现；四掌机型号只有只读 SCI 准备 | 掌机 HID 命令、完整映射/重置/重连；桌面宏不等价 |
| PCIe ASPM、待机联网、休眠等待策略 | M/E | 未实现 | Windows 电源 API；需要当前配置、权限和恢复测试 |
| ASUS 服务查看/停止、Keystone/额外服务策略 | E | 未实现 | 服务管理权限；OpenAC 没有停服务或修改原安装 |
| 原始 ACPI 测试入口 | A/E | 有意不提供任意命令入口 | 无任意 endpoint/INIT/DEVS；逐个已证实接口独立实现 |

## 监控、叠层、自动化和更新

| 功能 | 固定源 | OpenAC 当前状态与对应代码 | 范围 / 尚缺内容 |
|---|---|---|---|
| CPU 利用率与频率、RAM、磁盘 | H | 局部实现并已接入真实监控：WindowsTelemetry 的 CPU频率/每核信息、RAM有效数据率MT/s、磁盘容量/活动度 | 当前 Windows native/PDH/WMI 源；不是全部 CPU 功耗、温度或实际 BCLK |
| CPU/GPU 温度与风扇 | A/H/N | 局部实现：SMI GPU 温度；NotebookWorker 移动状态准备 | 本机 AIO GET36 全0；CPU/VRM/主板温度、板上RPM仍未知 |
| 多 GPU 时钟/显存/利用率/温度 | N/H | 局部实现并已接入真实监控：WindowsTelemetry 多 NVIDIA GPU | 当前 driver identity/单位；AMD/Intel 尚缺同等遥测 |
| CPU/GPU 功耗 | H/P/N | 未实现完整同等遥测 | Windows 能量计、NVML、CPU SMU/MSR 各有不同门槛 |
| 桌面或游戏监控 OSD | O | 未实现 | 需要自有叠层窗口、生命周期、权限、输入穿透；非UI卡片 |
| 游戏 FPS / Present 监控 | O | 未实现 | 上游使用 ETW，不是复制整个游戏注入器；ETW权限/事件版本需独立验证 |
| AC/电池模式自动选择 | M/G/S/L | 未实现笔记本策略 | ScenarioProfiles 有应用情境关联，但目前不具备该移动状态机 |
| 应用情境与通用热键 | M/I | 局部实现：ScenarioProfiles、MacroEngine 自有实现 | 不可由此宣称 BIOS/GPU/屏幕自动化完整 |
| 官方 BIOS/驱动目录和下载链接 | U | 局部实现：AsusDriverCatalog，官网V2路由/精确型号/时间/版本判断 | 只查目录/链接；未执行驱动/BIOS安装 |
| 外设固件目录 | 上游外设模型与 U | 局部实现：FirmwareCatalog 的官方 manifest | 不自动执行更新器或刷固件；下载目录不代表更新完成 |
| 应用自更新 | U | 局部实现：GitHubUpdateCatalog 的 OpenAC release 链接 | 不复用 G-Helper 发布机制；来源URL校验；下载安装/签名更新仍有缺口 |
| 托盘、开机/恢复、设置持久化与日志 | Program/Settings/M | 局部实现：OpenAC UI/草稿与预览恢复 | 不是完整 G-Helper 托盘/移动热键常驻行为；还需长时间测试 |

## 鼠标、外接键盘、耳机

G-Helper 这个固定版本包含多个各自的协议模型，不能只因为同属 ASUS 就复用同一报文。型号范围应核对 [外设目录树](https://github.com/seerge/g-helper/tree/e897b278bf809add90c904ba84aa2e451579dd0c/app/Peripherals) 及具体模型，而不是拷贝其型号表。OpenAC 官方 SupportedDevicesCatalog 的条目明确 `ControlSupported:false`，它是识别/研究入口，不会自动扩展控制 allowlist。

| 功能 | 固定源 | OpenAC 当前状态与对应代码 | 尚缺内容 |
|---|---|---|---|
| 鼠标 DPI 多档、颜色、X/Y、polling | PM | 局部实现：GearLinkWorker 的 Harpe II Extreme Edition20 精确协议 | 不覆盖 Chakram/Gladius/Keris/Pugio/Spatha/Strix/TUF/MD200 全部模型 |
| Angle snapping/tuning、MotionSync、Lift-off | PM | 局部实现：现有 GearLink 精确已读字段/范围 | 只允许当前 capability；不能跨型号复制范围 |
| 鼠标按键、shift层、自定义组合/启动动作 | PM | 局部实现：自有按键绑定 + MacroEngine；硬件宏/所有组合不完整 | 源布局/键值/命令和写后读回逐型号确认 |
| 鼠标多灯区/动画/Aura同步 | PM | 未实现：当前键鼠没有RGB getter/setter；桌面 Aura 是独立设备 | G-Helper 多模型独有灯区/方向/随机色仍缺 |
| 鼠标 profile、电池、低电报警、休眠 | PM | 局部实现：当前精确设备 getter/setter准备 | 分别确认 RF/有线/接收器能力及实际数据 |
| 鼠标恢复默认、校准、booster、旧代协议 | PM | 未实现全型号流程 | 校准/恢复不能由普通设置报文猜出 |
| 外接键盘 profile、polling、低电/休眠 | PK | 局部实现：Azoth Extreme Edition20 的 GearLink 路径 | 未覆盖全部 Azoth/Claymore/Falchion/Scope/Flare/TUF 型号 |
| 外接键盘 RGB模式/单键灯效/预览 | PK | 局部准备：精确7586/7588静态recipe协议、软件模式getter/接管/交还、严格失败恢复 | 本机静态GET仍FFAA拒绝；手动接管和颜色没有实写验证；单键布局/动画仍缺，不能套用GPL布局表 |
| 外接键盘按键/组合/宏/启动程序 | PK | 局部实现：键映射 + 自有 MacroEngine | 硬件驻留宏、所有型号组合/恢复缺少验证 |
| Azoth OLED 内容、亮度、KPS、lever | PK | 局部实现：GearLinkFeatures 精确现有 getter/setter | 自定义图片/GIF及所有 OLED 型号未实现；当前预设不等于任意上传 |
| 耳机 battery、灯效、EQ与预设 | PH | 局部实现：DELTA II-KJP 电池/EQ等已证实 HID 字段；raw mode0颜色映射不明，effect/color禁用 | Cetra/Clavis/Pelta/StrixGo 等其他模型未完成 |
| 耳机 sidetone、麦克风、噪声抑制、ANC | PH | 局部实现：当前设备 capability允许字段；Windows端点另行支持 | 模型独有 ANC/AdaptiveANC/Dirac 等不能虚构 |
| 耳机 voice prompts、电源、低电警报、恢复 | PH | 局部实现：当前 getter/setter准备字段 | 不代表所有耳机具有这些开关；硬件 reset 尚缺 |
| OMNI 接收器配对/拓扑与固件 | 外设模型/PM/PK | 局部实现：只读接收器发现/拓扑准备 | 配对写入、断开、更新的完整流程尚缺 |

现有外设“写接口存在”与“已在实物改值验证”仍是不同状态。HARPE II EXTREME EDITION20（7546 USB/7548 RF）、AZOTH EXTREME EDITION20（7586 USB/7588 RF）、DELTA II-KJP（7489 RF）均有本机真实在线读取；多数setter只有协议/纯编码及同值no-op验证。此前唯一鼠标备用DPI变更尝试的即时读回未确认，独立读取确认原值恢复，不能当作成功写入。本轮传感器/功能核对没有实际设置、校准、LCD槽位写入或固件操作。当前接口以 [GearLinkWorker-protocol.md](GearLinkWorker-protocol.md) 为准。旧 MouseSDK/KeyboardSDK 崩溃路径不作为适配兜底。

## Armoury 桌面功能不属于移动 ACPI 的延伸

| 功能 | OpenAC 实际范围 | 仍缺内容 |
|---|---|---|
| 主板 USB Aura | DirectAuraWorker 的9个精确 VID/PID 源准备、逻辑通道、4种已证实模式 | 不是笔记本/DRAM/VGA全生态；真实 ARGB 灯数、Gen1/Gen2仍未知 |
| EURUX 风扇/灯效 | SDKWorker type32；4个控制器端口、PWM曲线/有校准范围时fixedRPM、201/202/104协议 | 不是 CPU/机箱/泵主板header；不做101校准；写后实物仍未验证 |
| 主板 LCD51 | 7299/7124/7571源准备；电源/亮度/standby、8预设、JPEG上传/显式槽选择/删除、硬件监控配置 | 本型号 caps 禁自定义GIF；占用/空槽无getter；动画视频不能假接JPEG槽 |
| 主板 Fan Xpert | FanModeWorker 已实现type50 GET0/1/12/13/16/17纯编码/严格解析及preset3草稿/门禁；84纯检查 | 正常调用者/SDK通路尚未合法验证；runtime断开；14仅枚举名未完整证明；不初始化/校准/stress，不声称功能可用 |
| 主板电压/温度/风扇遥测 | 新 TelemetryWorker 隔离GET0/36；原始数据保留且不猜单位/身份 | 本机五项全0；ASUSManagement普通WMI实例AccessDenied；未找到可确认共享内存schema |

## 验收办法：怎样才算补齐和更好

每项新增能力需有精确身份、实际 capability、合法读路径、参数范围、失败/断开状态和可测的结果。无硬件的新增适配应标为源准备并禁用未验证写入；不能用 `fullSupported:true` 把缺口隐藏。模式/风扇/功率等应记录原设置、明确写应答和实际读回，不因应答成功就写“完成”。

可比较的改进指标包括：启动和页切换时不在UI线程执行native调用；单次工作进程硬期限与崩溃隔离；轮询不并发堆积；超时不无限重启；设备断开后不保留旧值；每项数据注明真实来源/单位/采样时间；写失败后的恢复行为可重复验证。现有纯测试覆盖若干项，但没有完成与 G-Helper 的同机性能、功耗或长期稳定性比较，所以目前不宣称“整体比它好”。

下一步可独立实现的优先顺序是：真实Windows/公开GPU遥测 → Windows屏幕与电池读取 → 移动机器只读capability验证 → BIOS模式/充电上限的严格setter与读回 → GPU切换状态机 → 曲线/功率/机型灯效。SMU/MSR/降压、任意ACPI测试、内核模块不是普通桌面监控缺值的替代方案。每步完成后更新对应行和测试证据；保留MIT的独立实现边界不变。
