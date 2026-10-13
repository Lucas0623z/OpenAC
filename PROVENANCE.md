# 代码来源与许可证选择记录

审查日期：2026-10-13。最终公开清单含 38 个运行/安装源码、13 个测试源码与 11 个构建配置，共 62 个代码/配置文件；下方 SHA256 对应最终公开字节。用户已授权免费开源发布，项目已采用标准 MIT。

## 已采用的项目许可证

项目已采用标准 **MIT**，署名 `Copyright (c) 2026 OpenAC contributors`。源码包、程序安装目录和安装包许可材料应保留正式 `LICENSE`、`THIRD-PARTY-NOTICES.md` 与 `DirectAuraWorker-LICENSE.txt`。已识别的第三方实现改写来自 MIT 的 RIGStats；其余实现者已说明是新写 C#/WPF，使用设备协议和 Windows ABI 事实。当前逐文件检查及 OpenRGB 对照未找到必须把现有项目改为 GPL 的直接代码复制证据。

免费发布与 MIT 许可可以同时成立。MIT 允许后续使用者商业使用、修改和再分发，因此不应在 MIT 文本中追加“禁止商业使用”限制。[OSI 标准 MIT 文本](https://opensource.org/license/mit)

项目负责人已审阅 `LICENSE-MIT-DRAFT.txt`，仅将其中标准正文落地为正式 `LICENSE`；草案仅保留在本地工作材料，不进入公开允许清单。第三方 Daniel Valfridsson 的原始署名不被项目署名覆盖。

如后来找到具体 GPL 函数、具有原创表达的表或实现的直接复制，应定位文件与来源，选择移除/重新实现该部分，或者按实际派生关系调整许可。不能把“无法完全证明历史上从未复制”无限保留为发布门槛；同样不能用一条源码注释代替有具体疑点时的复核。本轮对照结论为“未发现实际 GPL 代码复制”，不是对全部历史行为的绝对断言。

若未来因新代码的真实来源调整为 GPL-2.0-or-later，其 source distribution 和 binary obligations 需按实际组合检查。特别是 `SDKWorker` 对本机 ASUS 非自由 DLL 的必要调用关系需单独评估；“动态加载”“没有捆绑 DLL”或“在另一个进程中”不能自动解决所有组合许可问题。当前采用 MIT 依据的是已核对的代码来源。[对照项目的 GPL 许可证](https://github.com/CalcProgrammer1/OpenRGB/blob/54f82e70042932a9520d676aab995f170863e7de/LICENSE)

## 分类依据

- **OWN**：本轮实现者说明为新写 C#/WPF/PowerShell，当前文件检查未发现第三方应用实现的直接复制。界面按原版页面组织、实机观察和功能标题重新实现；不包含原厂图片、字体或前端代码。OWN 表示本项目的实现来源，不主张 ASUS 品牌或原 UI 素材的权利。
- **MIT**：实际从 RIGStats MIT 实现参考改写，必须保留其署名与许可。
- **WIN**：按 Microsoft 官方接口定义与文档写出的 C# ABI/系统调用，包括 GUID、结构布局、常量、COM 方法顺序。原始 SDK headers 与生成的 interop assemblies 不随包分发。
- **ASUS**：按官方/本机安装模块取得的命令号、PID、字节/XML 布局、数值枚举、按键 code、范围与 getter/setter 行为重新实现。不是将原厂函数或整个模块改名后发布。

来源声明来自本线程实际实现者 `root`、`interfaces`、`plugin_analysis` 和 `aura_worker`，并用当前源码、构建引用与下方固定参考交叉核对。它们是工程溯源记录，不是每个历史步骤的外部审计认证。

主任务随后新增的 `ControlPolicy.cs` 和 `ControlPolicyTests.cs` 也为本项目新写：实现当前会话的实验性设备写入开关，默认关闭；没有引入第三方许可代码。其最终代码和相关 GUI/build 已纳入下方 hash 快照。

安装器任务改为独立 C# WinForms 实现，使用本机 Windows 自带 .NET Framework 编译器；Windows 快捷方式仅按 WSH/CreateShortcut 接口事实以自写反射代码调用。没有采纳第三方 installer 应用函数，也不分发 compiler/runtime。曾获取的 NSIS 下载内容未执行，不属于发布依赖或载荷，不能误列成已使用的第三方代码。

## RIGStats 对应范围

| 本地 DirectAuraWorker 实现 | 固定 MIT 参考中的对应实现 | 本地变化 |
|---|---|---|
| Report | AuraUsb.Report | .NET Framework 数组构造 |
| ParseFirmware / ParseConfig | ParseFirmware / ParseConfigTable | 异常处理、空/长度校验 |
| ParseZones | Zones 的 Motherboard 分支 | 只接已证明主板家族，额外 header 数量限制 |
| Mode / EffectPackets | Mode / SetEffect 的 Motherboard 分支 | 字符串输入、先验证全部报文、超过 16 LED 拒绝 |
| Apply 中颜色亮度计算 | Scale / ParseColor | 参数类型、颜色和亮度严格验证 |
| KnownMotherboard 的五个 PID | Families 的五个 Motherboard 项 | 不纳入四个 Addressable 家族 PID |
| NativeHid.EnumerateAura 的接口枚举/描述及 P/Invoke | HidDevice.Enumerate / DevicePath / Describe / Windows ABI | 精确 VID/PID/usage/报告长度过滤 |

上游固定文件与逐字节 SHA256：

| 参考 | 固定版本 | SHA256 |
|---|---|---|
| [AuraUsb.cs](https://github.com/dvalfrid/rigstats/blob/2e9c9caa7512360b858dc22696b1f0aea1d653b9/sensor-sidecar/Control/Lighting/AuraUsb.cs) | 2e9c9caa7512360b858dc22696b1f0aea1d653b9 | `44371F54E2ECA9E2E8814C66706E5056C1F19350B062613CBAFD807084FDFD83` |
| [HidDevice.cs](https://github.com/dvalfrid/rigstats/blob/0d2033d6b99ecc83db842a70278de650d4d91b72/sensor-sidecar/Control/Lighting/HidDevice.cs) | 0d2033d6b99ecc83db842a70278de650d4d91b72 | `C956AF2099B79E0892A59D019467597A8E2D04E2EFF92BE7BAC0196A2CFE2439` |
| [LICENSE](https://github.com/dvalfrid/rigstats/blob/34981a0e0d676e2122c5e24b38af7a3260e2f4f1/LICENSE) | 34981a0e0d676e2122c5e24b38af7a3260e2f4f1 | `F68DC7AD78DA2088AA3EE490CDAFA25973E7CC2A22A617B9BCC4D44EC7EA70A3` |

本地 `DirectAuraWorker-LICENSE.txt` 的 SHA256 为 `DD726FFC1A165DA64A6A47991B9E09A72D954811C2B5D890DB7AB5923B1FAE14`。截取其 MIT notice 并仅归一空白后，内容与固定上游 LICENSE 一致。

`AuraBoundedIo`、`IAuraIoApi`、native OVERLAPPED/CancelIoEx 期限与未完成 I/O 保留、互斥 ownership、JSON 监督和对应 fake tests 是本项目新写。随后 GearLink 的 `GearBoundedIo` 复用该本项目实现，改为 900ms transaction budget 并整合自己的状态；这不新增外部第三方代码来源。

## OpenRGB 实际对照

本轮使用固定 revision `54f82e70042932a9520d676aab995f170863e7de`。这是一份可复查的当前对照版本，不宣称它就是最初编写时获取的唯一版本。

| 对照文件 | SHA256 |
|---|---|
| [AsusAuraUSBController.cpp](https://github.com/CalcProgrammer1/OpenRGB/blob/54f82e70042932a9520d676aab995f170863e7de/Controllers/AsusAuraUSBController/AsusAuraUSBController/AsusAuraUSBController.cpp) | `C2A69B1195FAF7093B59B3AF59E18748ABFD9F2D70E960E0A1FEE497BC1C2805` |
| [AsusAuraMainboardController.cpp](https://github.com/CalcProgrammer1/OpenRGB/blob/54f82e70042932a9520d676aab995f170863e7de/Controllers/AsusAuraUSBController/AsusAuraUSBController/AsusAuraMainboardController.cpp) | `A5A8A5663AA458E912FD9B09778CE47C1E8317523811C0AA87ACD3EB57FE19EA` |
| [AsusAuraUSBControllerDetect.cpp](https://github.com/CalcProgrammer1/OpenRGB/blob/54f82e70042932a9520d676aab995f170863e7de/Controllers/AsusAuraUSBController/AsusAuraUSBControllerDetect.cpp) | `A72307848971BFDE043FA404E30A7061A7D9921F3AC5EFA46E326A9123710221` |
| [LICENSE](https://github.com/CalcProgrammer1/OpenRGB/blob/54f82e70042932a9520d676aab995f170863e7de/LICENSE) | `7687B2F71C25336B0273F5C175286EBBC12D89ADD925B2F6C333E51FA3B1782D` |

对应参考事实为 EC report ID、82/B0/35/36 command、配置偏移、mode 值、16-bit mask 和五个 motherboard PID。本地没有移入 OpenRGB 的 SetGen1、SendDirect、完整型号检测表、C++ controller hierarchy 或库链接。相似的报文生成 C# 结构可以对照上方 MIT 源找到明确来源；五个 PID 是识别协议值，没有移入 OpenRGB 数十项的整张设备表。构建脚本没有 OpenRGB 源文件、库或 binary dependency。

## Windows 接口参考

Macro 使用 SendInput、INPUT/KEYBDINPUT/MOUSEINPUT 及低层键鼠 hook；Monitoring 使用 WMI、GetSystemTimes、GlobalMemoryStatusEx 和已安装 nvidia-smi 命令；Storage 使用 Windows NVMe Get Log Page 查询结构；Audio 使用 MMDevice/IAudioEndpointVolume；Discovery 使用 SetupAPI、CM parent 和设备属性。

相关 primary documentation 已在 `Macro-Implementation.md`、`AudioEndpoint-Implementation.md`、`Discovery-Implementation.md` 列出。额外固定 ABI 对照为 microsoft/win32metadata revision `76c04c2021ef4a831a6f1e06d9566002d746139b`：

| 仅用于接口对照的 header | SHA256 |
|---|---|
| [mmdeviceapi.h](https://github.com/microsoft/win32metadata/blob/76c04c2021ef4a831a6f1e06d9566002d746139b/generation/WinSDK/RecompiledIdlHeaders/um/mmdeviceapi.h) | `DCACABDF69F267A7892AB61F22F3293FC3173C2D962306CE89A0145DEF7B7B1A` |
| [endpointvolume.h](https://github.com/microsoft/win32metadata/blob/76c04c2021ef4a831a6f1e06d9566002d746139b/generation/WinSDK/RecompiledIdlHeaders/um/endpointvolume.h) | `E9A35C9EDF8C9DD931DA5FE1EA84344F0E20595D037EC6E97FA189D3D155CCD0` |
| [devpkey.h](https://github.com/microsoft/win32metadata/blob/76c04c2021ef4a831a6f1e06d9566002d746139b/generation/WinSDK/RecompiledIdlHeaders/shared/devpkey.h) | `6D59670E0157B4DBE3B279785491D74058FEA7C548016CFC90696542F7094A80` |

此仓库 [LICENSE](https://github.com/microsoft/win32metadata/blob/76c04c2021ef4a831a6f1e06d9566002d746139b/LICENSE) 明确保留原 SDK header 许可；不能把 repository MIT 当作原 headers 已重新许可。公开文件是手写 C# ABI 与自己的控制逻辑，原 headers 和下载参考都排除。Windows/.NET framework system assemblies 由系统提供，不打包原系统 DLL。

## ASUS 参考事实与外部依赖

GearLinkWorker/Features/FirmwareCatalog 的命令号、报告格式、偏移、按键 code 对应、枚举和 manifest URLs 来自 ASUS 官方 GearLink 前端。实现者确认未复制其 JS/TS/C++ 函数或模块；C# validation、JSON pipeline、transport 和 UI 是重新实现。原始解析材料不进入公开包。

官方 manifest 的本轮已保存版本：

| URL | 版本 | 本轮已保存文件 SHA256 |
|---|---|---|
| [7489 manifest](https://gearlink.asus.com/view/7489/manifest.json) | 1.00.35 | `A1595F4C166C26B59CF60C5D02B017B31A2700AAB3A34740CD651BD50177E9BF` |
| [7548 manifest](https://gearlink.asus.com/view/7548/manifest.json) | 1.00.15 | `58F5D6B5DC0EF34CBFDDF568251351158070E63ECA30FF1DE487469CB11980A0` |
| [7588 manifest](https://gearlink.asus.com/view/7588/manifest.json) | 1.00.09 | `6AAE54DE921B4D2BE9A1611B60EC50F93E4992404835B5E9631BE65C77D8B5C2` |
| [6862 manifest](https://gearlink.asus.com/view/6862/manifest.json) | 1.00.24 | `D7D37B543CB92022DE4598AA616E3FDE80D6920DED3C5B027E6AC38ACDA147A3` |
| [6864 manifest](https://gearlink.asus.com/view/6864/manifest.json) | 1.00.12 | `E3506FC3F0C54FD2616BAFEB20C8F4FAE2501A8D53BEB2883AC58228EBEA9BF2` |

官方 URLs 内容可能更新；上述 hash 对应分析时保存的内容，不保证未来 URL 返回相同内容，也不授权其素材再分发。

SDKWorker 初始 x86 加载与 allowlist 由 plugin_analysis 实现；fan/LCD 的 source-based setters、范围、readback/ACK 区分、JPEG normalization、hardware monitor、fan lighting 由 aura_worker 扩充。使用本机模块接口事实，不复制 native DLL 实现。少量 effect ID/颜色数/速度方向能力表是已确认的型号协议映射，公开表应继续只含必需的数值能力事实，不加入厂商原文档或资源表。

固定本机参考标识（仅记 hash 和相对于安装根的通用路径，不分发原文件）：

| ArmouryDevice 安装根内参考 | SHA256 |
|---|---|
| modules/FAN_AND_CONTROLLER/index.js | `B3262ADFD96AB04B137F211AC3EB27E0DAC8D3DCB63DC14EB8F12EE5BDEB3E16` |
| modules/aio/index.js | `44B6FFB889C85988D5B176A3F73D5FB43E0EB2F34657C83F088176DD244D80CD` |
| modules/motherboard/index.js | `91754CD5572516F356DAE1B58CD1E19D35C8C20E9C4FA904545C23092C7C5816` |
| View/7576/resources/src/_ref/caps.json | `DFC66FC1E4C7D27AA4759F17E179069D378823AF12F1D398554ACC3BEE312BE7` |

本机 ArmouryAIOSDK.dll / OmapboxSDK.dll 是可选运行路径依赖，未修改或分发；本项目 MIT 不覆盖它们，也不授予安装/使用原厂 SDK 的额外权利。遗留 COM probes、TypeLib exporters、原 ASUS COM/type library、解包来源、图片/图标/字体/GIF/LCD preset media 均不在本轮公开允许清单。

## 逐文件判定与源码快照

SHA256 用于精确标识本轮审查内容；不是签名、版权登记或许可证证明。其他任务后续修改源文件时，安装包的最终 manifest 必须重新计算 hash；若仅新增自写逻辑，不需要把来源结论退回无限历史核查。若新增外部源码或资产，需给该具体部分补来源。

| 文件 | 来源分类 | 本轮 SHA256 |
|---|---|---|
| `AssemblyInfo.cs` | OWN（界面/应用逻辑） | `1AF1432E6C8D4152D08AA8D595DCA9662849ADD98F78560B14D3D285DC18C2FA` |
| `RogControl.cs` | OWN + WIN | `BFDD6E8D394FD16E7E71FEC21EC5C10C30538DAEF5EB2B38092491E016ADC728` |
| `ShellStructure.cs` | OWN（界面/应用逻辑） | `55C655C5B95AE8713F4FAC4A672C1232EB4F307BBA7725519BC953F827EC35E1` |
| `StructurePages.cs` | OWN（界面/应用逻辑） | `73C4A905C57D01C4B277EFDEF1567D925E030201E022315FDD65E76C4B5E7330` |
| `AuraStructure.cs` | OWN（界面/应用逻辑） | `48811E1CA885B1C3EE73A0E62EEF3C98D4678E6A2D90758755D6E1899DF0B32D` |
| `SettingsStructure.cs` | OWN（界面/应用逻辑） | `69C9C4061DF1A897F558D667A8D6EA296D4FE74F5175C1AEC3577A358C6FEC04` |
| `FanStructure.cs` | OWN（界面/应用逻辑） | `8D54779CFF96AD0251E4A2B5CF8264C6749574C152E2D0875C668BDF15FF9C92` |
| `DeviceStructure.cs` | OWN（界面/应用逻辑） | `17F11E6018F9C9038CC7CA27C0E839D18F80728E8AE0BA16209BFE4EBE6A32D9` |
| `Monitoring.cs` | OWN + WIN | `A2729EE36732870FAC4CE1304AD502E817F685FA5E6DF80087633BEDE92467B8` |
| `GameLibrary.cs` | OWN（界面/应用逻辑） | `CE0BAD51F65B405B9FF3667E42E0572D8F9DAC26CB545E86A7E5CC08D783F00D` |
| `DeviceGearLink.cs` | OWN（界面/应用逻辑） | `9AFF02D560333D733F4B0E673F4380331BB65DAFB7A297D8AEEC35FCA16D7370` |
| `StorageStructure.cs` | OWN（界面/应用逻辑） | `15D96E142265794D25396A11424622465C865BC964756C4490E60FADAE4D77FF` |
| `NativeSettingsStructure.cs` | OWN（界面/应用逻辑） | `86EC866FA1BD2C3B5C6BC42C0D3828BEA73928337053822562F154428A8E7839` |
| `MacroEngine.cs` | OWN + WIN | `DCB9E28B4EC9CB45CE92B19DD15297D6C3387848000ED3B3F5BE3D7BC686AD7D` |
| `MacroFeatures.cs` | OWN（界面/应用逻辑） | `F49C3433307AC9FCA237B19D6CD55402BE749E239FA089EC006C32283E2FE8E2` |
| `LcdStructure.cs` | OWN（界面/应用逻辑） | `9DBD52400FA38A29BB3A05749CB84545E2D930F1FE249A1B19BF48C7E9EDEFD2` |
| `AudioEndpointStructure.cs` | OWN（界面/应用逻辑） | `04D88338301438F4C62E6C1DD8147AB30D2160D13FEB59F98E46D7F734799C6E` |
| `ReceiverStructure.cs` | OWN（界面/应用逻辑） | `E85720A3B4F535725C8120288C65074712FF4495D2954AB1A6D8929D7E13C741` |
| `FirmwareStructure.cs` | OWN（界面/应用逻辑） | `8310E56A0B816D92DB2C85A0A13BCE12BC9967887FB38DDE3BB5BB1FAC97B16E` |
| `GearExtendedStructure.cs` | OWN（界面/应用逻辑） | `EEA8D7F60058125CE29B7E4DC61FEB1881DE65DB0809CD6F46B693946B69C45A` |
| `FanLightingStructure.cs` | OWN（界面/应用逻辑） | `1D90EFB26F514598E58C2C6C2BF2B8E5183363CC3CA0E8ED0D086CBFEB0BE1A4` |
| `ScenarioProfiles.cs` | OWN（界面/应用逻辑） | `E171F233311BA10C44E92A47FA072AE72ACEC56A45F00CB57DBCFCDFB9432A55` |
| `ScenarioStructure.cs` | OWN（界面/应用逻辑） | `CC9FCBC776BF4090A97721203D8FA927B206ECAB0E3F19417BA5F30A94A9123A` |
| `LocalUpdateCatalog.cs` | OWN（界面/应用逻辑） | `DD7DE144611524EA78E564FCB355E8810892B915891EC47D7F5C1E861142E323` |
| `LocalUpdateStructure.cs` | OWN（界面/应用逻辑） | `41CF46015DFF1867BA5D892CE249F3C3F0F24604B03B8C18D805ADACE3C13A70` |
| `BoardArgbStructure.cs` | OWN（界面/应用逻辑） | `4294682EF7551C88FF57A3E8058845E85CEA26769416DDF88FDFAE7E63B73E4B` |
| `DiscoveryStructure.cs` | OWN（界面/应用逻辑） | `114B5AE0ED8CD97D46B31E3139F980037BFA7E192E4A97AA1451ED2B444285AF` |
| `DiscoveryWorker.cs` | OWN + WIN | `11CE88E30E05EE612EBD167448B934FDBAAF77EA5D519A7F23BF64FD5F98712D` |
| `DirectAuraWorker.cs` | MIT + OWN + WIN | `9AE693F3F34E47607EE1991F273BC26928A61E70D7958F445F364F2314D7FBEE` |
| `SDKWorker.cs` | OWN + ASUS | `C3F266383256503FCDD730FF173452E2084805C83C1463B4E96E528E80BCC1E3` |
| `GearLinkWorker.cs` | OWN + ASUS | `37EA23026740F5536F8F0814F008711F82B4A99F4AEDDD8E1E989BBDD6901E12` |
| `GearLinkFeatures.cs` | OWN + ASUS | `FC091A14F969D7030D1FC15DFDBD04656E502207EB66CA0DB37A1988FB5920DA` |
| `FirmwareCatalog.cs` | OWN + ASUS | `8B32033620982A35DB400AF96F11041907182BC8C3ADA37E824F2A9B9E5DCC66` |
| `StorageWorker.cs` | OWN + WIN | `2BAF3E386C7507C69BC66E4AEB5227E969327D18CF37BD6254C103D6003929EF` |
| `AudioEndpointWorker.cs` | OWN + WIN | `BB1187D5A117283647FF7D8F839B94182B7692A54818C1697044D69FF4D3FD45` |
| `ControlPolicy.cs` | OWN（应用/构建配置） | `C6C3DF8D8A59B649F719E45ECDE083161E01FB3E1000E822D2947E975C3C33F6` |
| `installer/InstallerCore.cs` | OWN + WIN（自写安装器） | `2994F68A25AEDD4433237A0FFF15DAD7F61F6C52C5A76698CA41F9766F3A45EC` |
| `installer/Setup.cs` | OWN + WIN（自写安装器） | `3CCAC443346B2EC844137AC408E9405DEA323A1B9C52E3D2B1E303D696FA0B6D` |
| `MockWorker.cs` | OWN（合成测试/只读测试源码） | `AEE2441A265DAD4B22025662D2444ADABA5436C7C4134B28C972222162B88F18` |
| `SupervisorTests.cs` | OWN（合成测试/只读测试源码） | `8F94B966F5F721BC6ED92A6C95A037ED7E67F2B31EBC8EB59B902B1463DFC729` |
| `ReadOnlyIntegrationTests.cs` | OWN（合成测试/只读测试源码） | `78104E13F77B2B728C69D9C2CDDC88115C5C14BAD264C0D5EF4EC86C4267F694` |
| `SDKWorkerTests.cs` | OWN（合成测试/只读测试源码） | `947F86D479C14401FAFBB92849C5DEA01C79F9E2AB1EEF089B9477A9421B4CEE` |
| `MacroTests.cs` | OWN（合成测试/只读测试源码） | `555C983C3379CD6A47137F951CD95F65C7FA2BAEB9AC8302DE5912439972CACC` |
| `GameLibraryTests.cs` | OWN（合成测试/只读测试源码） | `C475B4817C1ADBF1CD05E7376447F72BD7942580178A9AE2BC2EF3AAA29E1D68` |
| `ScenarioTests.cs` | OWN（合成测试/只读测试源码） | `65EAF60AF3AC677941EA271B444C070CA0131DCA38CAA98624E94C3D10D94F13` |
| `PeripheralReadOnlyTests.cs` | OWN（合成测试/只读测试源码） | `E04C166F124482AF492377659029A76D070B4F92AEDA50F022691B29DE3B103E` |
| `NativeDraftTests.cs` | OWN（合成测试/只读测试源码） | `B5F41659AA59EEF66B92BCD19464FC119BBDB6363B6C13D07915B51DE7A08197` |
| `LocalUpdateCatalogTests.cs` | OWN（合成测试/只读测试源码） | `384E10AD47A09F37219373FAD3C0AFDACFB0AD6DDA12DB536A2BCEA6C08507ED` |
| `DiscoveryGuiTests.cs` | OWN（合成测试/只读测试源码） | `23E9707D6C20FFDDDE6855A317A3FB31078FCC5F130806ADF9022A80D68318C0` |
| `ControlPolicyTests.cs` | OWN（合成测试源码） | `0008D97C86CE1B35AC5AC3314DE299430BB9D8290871B4B04FEDE870C31BE6EA` |
| `installer/InstallerTests.cs` | OWN（合成测试源码） | `C22A152CB5C2A27F799DCB49634ECF2CDE3636C9C91D5FB7409FBF912D51D3A3` |
| `app.manifest` | OWN（构建/候选准备） | `AC2CA247C2A6AF54787C26A2A7CF66D9292A622A5A902C051DC37471A6FF55C7` |
| `OpenAC.exe.config` | OWN（构建/候选准备） | `601E8AA2F94CB6F551B2A78B971130E48AA27C6616554467CD51BF20F97C94AA` |
| `build.ps1` | OWN（构建/候选准备） | `75676CB3BA21B79D9C12B311673E183F638F062B7595A7018486DEEBCA1DECA7` |
| `build-all.ps1` | OWN（构建/候选准备） | `BD070805E6919EFB943A70E34CB5A71929B1727FDE4735797E0C6D2729152291` |
| `build-gearlink-worker.ps1` | OWN（构建/候选准备） | `9C0FE4E6C8A7C34882478E5270157D1F511ED9F8E315A88179D71AD1C37098ED` |
| `package-candidate.ps1` | OWN（构建/候选准备） | `BFACC811D63300C6CCEC68DF5FDBEC70FBFD874FDE985684AE98C3DFFCD62470` |
| `.gitignore` | OWN（应用/构建配置） | `52AA1D835D8604EBFECD2410DA17D71EABD1DA443B686CB321D811D2382E570E` |
| `installer/build-installer.ps1` | OWN（应用/构建配置） | `A9087B75803A873975B18ADFF25A8346382FEABCC855AF1B37F23544C092F27E` |
| `installer/app.manifest` | OWN（应用/构建配置） | `5F7456487E607BB2E541A0077F62E65A55A0ECA9D7A197D423E38C7044F36584` |
| `installer/RuntimeFiles.json` | OWN（应用/构建配置） | `D5EF95B20D5F3D339AD910DE3828C525D10C61534A83D8A2FFE6419DD71BD30E` |
| `.gitattributes` | OWN（应用/构建配置） | `2DAD7ECA798A0F34EE5870EB20C1D13C0BAE27420B7695C6141A7A4D49D241F3` |

测试源码只发布文件本身，不发布实机快照、截图、日志、用户配置或测试 EXE。构建表包含当前 candidate preparation 脚本；新写 installer 源码/构建脚本按 OWN 或 OWN+WIN 记录，未引入第三方 installer runtime。本轮来源审查没有执行 build、打包、安装、上传或任何硬件操作。

## 文档和素材处理

README、协议/实现说明、PUBLIC 材料是本项目编写的说明文本；保留其中 primary source URLs。第三方原许可文本仍属于对应第三方 attribution。`public-metadata` 只保留官方出处和占位元信息，不携带官网图片。正式 `LICENSE` 已采用，`PROVENANCE.md` 和 `THIRD-PARTY-NOTICES.md` 可纳入公开材料；草案不公开。公开 manifest/allowlist 由主任务更新。

本轮固定下载的 RIGStats/OpenRGB/Microsoft 对照源保存于工作区 reverse-analysis 目录，完全不进入发布包。来源记录支持当前采用的 MIT；未发现需要先整体换为 GPL 或停止公开准备的实际复制问题。

