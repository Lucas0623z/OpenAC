# 代码来源与许可证选择记录

审查日期：2026-10-14。用户已授权免费开源发布并明确选择继续保留 MIT、独立实现。前面表格记录先前审查阶段；最新开发字节以文末的当前白名单表为准。已发布 0.3 的许可和归档保持原样。

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
| `RogControl.cs` | OWN + WIN | `08599D32CF111BD74D63B61A66B9AEB4A83501CBEF52B5815FE08C4796B4BABF` |
| `ShellStructure.cs` | OWN（界面/应用逻辑） | `D0EF9217D779C76315092F103DECBC959A11249FBC886FF0EFF54FA771F978A8` |
| `StructurePages.cs` | OWN（界面/应用逻辑） | `D2B38664F5B20EF494F16A733A0AA3E0A057BF571CF7E4C2F37AE1657D8BB0C2` |
| `AuraStructure.cs` | OWN（界面/应用逻辑） | `4F66B295827D9D0A75D2B8D7005D9ABADFC7ECF1B447078A2AE940F453A49F05` |
| `SettingsStructure.cs` | OWN（界面/应用逻辑） | `C84AB7EF520C01B8DEE2EED6693955C3F3031CF88642ACD88F41A9ACCFF59F96` |
| `FanStructure.cs` | OWN（界面/应用逻辑） | `AEBBCD71954ECC549AC8BC85712BAC1ADDE0B5EB2705D4180C4D7936D8C5C378` |
| `DeviceStructure.cs` | OWN（界面/应用逻辑） | `F17B92F87595400C2BA1AA20F802EB7BF112BFC0733EDDB2421D2EFEEFD4F5D3` |
| `Monitoring.cs` | OWN + WIN | `A2729EE36732870FAC4CE1304AD502E817F685FA5E6DF80087633BEDE92467B8` |
| `GameLibrary.cs` | OWN（界面/应用逻辑） | `CE0BAD51F65B405B9FF3667E42E0572D8F9DAC26CB545E86A7E5CC08D783F00D` |
| `DeviceGearLink.cs` | OWN（界面/应用逻辑） | `24274EC083AA36D38D7E3B838F92EDE4FEC213B2E9B83D012038D6D7F3678C53` |
| `StorageStructure.cs` | OWN（界面/应用逻辑） | `15D96E142265794D25396A11424622465C865BC964756C4490E60FADAE4D77FF` |
| `NativeSettingsStructure.cs` | OWN（界面/应用逻辑） | `86EC866FA1BD2C3B5C6BC42C0D3828BEA73928337053822562F154428A8E7839` |
| `MacroEngine.cs` | OWN + WIN | `DCB9E28B4EC9CB45CE92B19DD15297D6C3387848000ED3B3F5BE3D7BC686AD7D` |
| `MacroFeatures.cs` | OWN（界面/应用逻辑） | `F49C3433307AC9FCA237B19D6CD55402BE749E239FA089EC006C32283E2FE8E2` |
| `LcdStructure.cs` | OWN（界面/应用逻辑） | `9DBD52400FA38A29BB3A05749CB84545E2D930F1FE249A1B19BF48C7E9EDEFD2` |
| `AudioEndpointStructure.cs` | OWN（界面/应用逻辑） | `04D88338301438F4C62E6C1DD8147AB30D2160D13FEB59F98E46D7F734799C6E` |
| `ReceiverStructure.cs` | OWN（界面/应用逻辑） | `E85720A3B4F535725C8120288C65074712FF4495D2954AB1A6D8929D7E13C741` |
| `FirmwareStructure.cs` | OWN（界面/应用逻辑） | `8310E56A0B816D92DB2C85A0A13BCE12BC9967887FB38DDE3BB5BB1FAC97B16E` |
| `GearExtendedStructure.cs` | OWN（界面/应用逻辑） | `627F1EFDC1F75E102B668494B19FE5EA2EF4D02D102863207BB7CD38F47C65D1` |
| `FanLightingStructure.cs` | OWN（界面/应用逻辑） | `1D90EFB26F514598E58C2C6C2BF2B8E5183363CC3CA0E8ED0D086CBFEB0BE1A4` |
| `ScenarioProfiles.cs` | OWN（界面/应用逻辑） | `E171F233311BA10C44E92A47FA072AE72ACEC56A45F00CB57DBCFCDFB9432A55` |
| `ScenarioStructure.cs` | OWN（界面/应用逻辑） | `CC9FCBC776BF4090A97721203D8FA927B206ECAB0E3F19417BA5F30A94A9123A` |
| `LocalUpdateCatalog.cs` | OWN（界面/应用逻辑） | `DD7DE144611524EA78E564FCB355E8810892B915891EC47D7F5C1E861142E323` |
| `LocalUpdateStructure.cs` | OWN（界面/应用逻辑） | `41CF46015DFF1867BA5D892CE249F3C3F0F24604B03B8C18D805ADACE3C13A70` |
| `BoardArgbStructure.cs` | OWN（界面/应用逻辑） | `4294682EF7551C88FF57A3E8058845E85CEA26769416DDF88FDFAE7E63B73E4B` |
| `DiscoveryStructure.cs` | OWN（界面/应用逻辑） | `114B5AE0ED8CD97D46B31E3139F980037BFA7E192E4A97AA1451ED2B444285AF` |
| `DiscoveryWorker.cs` | OWN + WIN | `11CE88E30E05EE612EBD167448B934FDBAAF77EA5D519A7F23BF64FD5F98712D` |
| `DirectAuraWorker.cs` | MIT + OWN + WIN | `E9B0DBC9949C85C5A9BED9CE7F0B0C50E76E5DFB8DF9CA8290530A9345222885` |
| `SDKWorker.cs` | OWN + ASUS | `24B461D4EC2AB30D575E7A78C7C99EE81778CD413DF3118FABC0FD54CFDD8675` |
| `GearLinkWorker.cs` | OWN + ASUS | `606BEAF0EFBD70EAEF01A7A67115D41F7BA071FAF3AD2D15D5F70E7A41DF1AD2` |
| `GearLinkFeatures.cs` | OWN + ASUS | `7F680E38041ED0992BC65ED09EDB3A60A06B84C71CEF8BF0E7B462DC5D229E52` |
| `FirmwareCatalog.cs` | OWN + ASUS | `8B32033620982A35DB400AF96F11041907182BC8C3ADA37E824F2A9B9E5DCC66` |
| `StorageWorker.cs` | OWN + WIN | `2BAF3E386C7507C69BC66E4AEB5227E969327D18CF37BD6254C103D6003929EF` |
| `AudioEndpointWorker.cs` | OWN + WIN | `BB1187D5A117283647FF7D8F839B94182B7692A54818C1697044D69FF4D3FD45` |
| `ControlPolicy.cs` | OWN（应用/构建配置） | `17B1C70F63D3AC7E70B2439514E6EB0D0366B852770E38363E12D468A94AEECB` |
| `installer/InstallerCore.cs` | OWN + WIN（自写安装器） | `2994F68A25AEDD4433237A0FFF15DAD7F61F6C52C5A76698CA41F9766F3A45EC` |
| `installer/Setup.cs` | OWN + WIN（自写安装器） | `3CCAC443346B2EC844137AC408E9405DEA323A1B9C52E3D2B1E303D696FA0B6D` |
| `MockWorker.cs` | OWN（合成测试/只读测试源码） | `AEE2441A265DAD4B22025662D2444ADABA5436C7C4134B28C972222162B88F18` |
| `SupervisorTests.cs` | OWN（合成测试/只读测试源码） | `8F94B966F5F721BC6ED92A6C95A037ED7E67F2B31EBC8EB59B902B1463DFC729` |
| `ReadOnlyIntegrationTests.cs` | OWN（合成测试/只读测试源码） | `78104E13F77B2B728C69D9C2CDDC88115C5C14BAD264C0D5EF4EC86C4267F694` |
| `SDKWorkerTests.cs` | OWN（合成测试/只读测试源码） | `6E670F1D4F00BC2609867BC99F8B6C8DE927C1B34CA16B98A56B5C95800FE8EB` |
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
| `build.ps1` | OWN（构建/候选准备） | `47F0EB73EA17006B820729FE03966F5CA6153852FACC5F5409843B503511023A` |
| `build-all.ps1` | OWN（构建/候选准备） | `B300E263785C01F80E8D76891BBB0C4B0AA58DA709D90EB65AA539E98C9BAA73` |
| `build-gearlink-worker.ps1` | OWN（构建/候选准备） | `9C0FE4E6C8A7C34882478E5270157D1F511ED9F8E315A88179D71AD1C37098ED` |
| `package-candidate.ps1` | OWN（构建/候选准备） | `BFACC811D63300C6CCEC68DF5FDBEC70FBFD874FDE985684AE98C3DFFCD62470` |
| `.gitignore` | OWN（应用/构建配置） | `55F97C2E76A70F608A59AFFA14D19B06C433D67BF179560C23E938464B927AF2` |
| `installer/build-installer.ps1` | OWN（应用/构建配置） | `A9087B75803A873975B18ADFF25A8346382FEABCC855AF1B37F23544C092F27E` |
| `installer/app.manifest` | OWN（应用/构建配置） | `5F7456487E607BB2E541A0077F62E65A55A0ECA9D7A197D423E38C7044F36584` |
| `installer/RuntimeFiles.json` | OWN（应用/构建配置） | `D5EF95B20D5F3D339AD910DE3828C525D10C61534A83D8A2FFE6419DD71BD30E` |
| `.gitattributes` | OWN（应用/构建配置） | `2DAD7ECA798A0F34EE5870EB20C1D13C0BAE27420B7695C6141A7A4D49D241F3` |

测试源码只发布文件本身，不发布实机快照、截图、日志、用户配置或测试 EXE。构建表包含当前 candidate preparation 脚本；新写 installer 源码/构建脚本按 OWN 或 OWN+WIN 记录，未引入第三方 installer runtime。本轮来源审查没有执行 build、打包、安装、上传或任何硬件操作。

## 文档和素材处理

README、协议/实现说明、PUBLIC 材料是本项目编写的说明文本；保留其中 primary source URLs。第三方原许可文本仍属于对应第三方 attribution。`public-metadata` 只保留官方出处和占位元信息，不携带官网图片。正式 `LICENSE` 已采用，`PROVENANCE.md` 和 `THIRD-PARTY-NOTICES.md` 可纳入公开材料；草案不公开。公开 manifest/allowlist 由主任务更新。

本轮固定下载的 RIGStats/OpenRGB/Microsoft 对照源保存于工作区 reverse-analysis 目录，完全不进入发布包。来源记录支持当前采用的 MIT；未发现需要先整体换为 GPL 或停止公开准备的实际复制问题。

## 0.3 后续开发分支

本分支新增的实时预览、GitHub 发布更新检查、UI 偏好、只读页面自动查询、
ASUS 官网型号目录与移动设备状态工作进程均为本项目编写。
G-Helper 的 GPL-3.0 应用实现没有复制、链接或打包；仅研究公开接口事实，
各固定来源和方法边界见 `NotebookWorker-protocol.md` 和 `Asus-Public-Catalog.md`。
原厂设备资料和兼容清单用于标记型号事实，不能授权未知设备控制。
官网图片、原应用实现、驱动二进制、实机快照、私有诊断与预览工作目录不进入源码包。

本分支已进行编译、合成测试、官网公开目录读取及不显示窗口的布局检查；
此前来源审查中“没有执行 build/上传”的文字描述其原审查阶段，不是本分支的操作记录。
已发布 `v0.3.0-preview` 的归档、哈希与 tag 保持原样；此分支尚未发布新安装包。


新增源/构建文件当前字节哈希：

| 文件 | 来源 | SHA-256 |
|---|---|---|
| `AutoDeviceRead.cs` | OWN（本分支独立实现或测试） | `EA1BF70EC227536E4DA588F9D8367A9F21F058DAFB3EC3CD91558A0C2F851D14` |
| `GitHubUpdateCatalog.cs` | OWN（本分支独立实现或测试） | `CFF6F4CBF721240907D47C15E48BAEEBAB5FD6CC3A5826954F3B088273EF673D` |
| `GitHubUpdateStructure.cs` | OWN（本分支独立实现或测试） | `B07AEF413672DBEE7687D0B076A254DB488B3A4DA96E80B5EE48E4B886877562` |
| `GitHubUpdateCatalogTests.cs` | OWN（本分支独立实现或测试） | `45291F18F4E90800C43999F3AD82C3858D3EBDFB31EED142ADAA1B6FA1DEFA8F` |
| `LivePreviewHost.cs` | OWN（本分支独立实现或测试） | `717AEC3C0BC9F59401DF59687104F901BD19C2B03F8600BA222EF94992FDE4BD` |
| `LivePreviewState.cs` | OWN（本分支独立实现或测试） | `B0EEEF3BCD507BD0CA124CD2999EFAB8CCC8407B29047F0E296B0F50BDD8A002` |
| `UiPreferences.cs` | OWN（本分支独立实现或测试） | `A65DA92C111D364819BCB22DA7B78DFA10C1510279E01E42B65AA851BC98D05D` |
| `SupportedDevicesCatalog.cs` | OWN（本分支独立实现或测试） | `532104E61CD5CF388DA3F23D07D31DC0120AA9A2D3321B98A10B30178051A439` |
| `NotebookWorker.cs` | OWN（本分支独立实现或测试） | `424F48DB66ECE721AABFE2640AC9D363E50FAC7B633B633F32ED240CCA9C1762` |
| `NotebookWorkerTests.cs` | OWN（本分支独立实现或测试） | `5922537BB3387F9DB2B9C88B385F9C7A3B2D7556132E91093D9E8D57F135F0CF` |
| `AsusDriverCatalog.cs` | OWN（本分支独立实现或测试） | `98D33705488F346570E4CD737B8C13BC8121781676867B90BDF15CB022B4A6E2` |
| `AsusDriverCatalogTests.cs` | OWN（本分支独立实现或测试） | `3508E9AB41325C9844A012C782C55E516F612C3CFD956D77835E07EF1CFA31A4` |
| `AsusUpdateStructure.cs` | OWN（本分支独立实现或测试） | `5DD192A0835E659BDA53BCA6C640F52729EC8BB35AA92E4E3013F55C6FCB90DF` |
| `preview.ps1` | OWN（本分支独立实现或测试） | `4A005C4D0828DD38CA676F23EE162D4C0B0463A2D91A678986B9DC6F5B4AA648` |

## 当前界面、监控与 Windows 控制阶段

本次新增设备子页、GameFirst/显卡只读界面结构、DPI阶段保护、移动状态工具页及合成检查均为独立实现。依据实际原版窗口核对普通标签和布局，不复制原版背景、图标、动画、字体或网页实现；G-Helper 仍只作为已固定的接口事实参考，不引入其 GPL 函数或程序集。既有 DirectAura MIT 署名与原厂 SDK 依赖边界保留。

2026-10-14 新增鼠标按键、灯光、校准/Zone 结构及数据草稿，独立绘制按键线稿，修复独立 Y 输入在未失焦时的热更新捕获，并为统一 setter 增加精确读取对象和活跃进程会话校验。校准、FPS 和未知效果没有借用原版默认读数伪装为已实现。

WorkerTextProtocol 和测试为独立实现的输入流封装；客户端固定发送 UTF-8，接收端仅过滤流头已知前导字节，随后严格解码，不改变全局控制台编码或反射修改 Framework 私有字段。Framework 的 Process/StreamWriter 行为通过纯自子进程探针与 [Microsoft Process](https://github.com/microsoft/referencesource/blob/main/System/services/monitoring/system/diagnosticts/Process.cs)、[Microsoft StreamWriter](https://github.com/microsoft/referencesource/blob/main/mscorlib/system/io/streamwriter.cs) 核对，仅使用行为事实，没有复制其实现函数。完整构建通过 53 组、17613 项纯检查，包含 131 项流封装检查；本轮没有真实硬件设置写入。

原版实窗覆盖与不可用页面见 OriginalUiInventory.md；主板 COM 只读探针的结果及限制见 FanXpert-Interface-Research.md。私有截图、探针、实机快照和用户数据不属于公开清单。本阶段没有真实硬件设置写入或真实安装/卸载；当前代码不宣称全型号适配、完整 G-Helper 功能、完整 Fan Xpert/Aura 控制或像素完全一致。

下表覆盖当前显式白名单中的源码、测试和构建配置，替代上方历史哈希用于此开发提交。来源细节与接口事实见对应协议文档。

| 文件 | 来源 | 当前 SHA-256 |
|---|---|---|
| `AssemblyInfo.cs` | OWN + 接口事实（细节见对应协议） | `1AF1432E6C8D4152D08AA8D595DCA9662849ADD98F78560B14D3D285DC18C2FA` |
| `RogControl.cs` | OWN + 接口事实（细节见对应协议） | `5BFA1FC5C73A447A6A584996D4FD8D0FA143DB252B7C72ECBDB2EF5B6E15452F` |
| `ShellStructure.cs` | OWN + 接口事实（细节见对应协议） | `7B999ACDD063A8AA0D71967EBE56A402F9BFD4509BC095658D83339856E3BFE5` |
| `StructurePages.cs` | OWN + 接口事实（细节见对应协议） | `CD922251340C81E6C93D42BEDACCC8429E36E61C84F653A21EDD0C77685D4199` |
| `AuraStructure.cs` | OWN + 接口事实（细节见对应协议） | `E589EF175A77380D4E50FDE1B0016B8423C72EE7695A2B00268F0623788C38E1` |
| `SettingsStructure.cs` | OWN + 接口事实（细节见对应协议） | `C84AB7EF520C01B8DEE2EED6693955C3F3031CF88642ACD88F41A9ACCFF59F96` |
| `FanStructure.cs` | OWN + 接口事实（细节见对应协议） | `984A36C51FFA4FD2DFCD25A5EE90B5A9C91FF0EC43B9A0D7A3220D8A3F56F74C` |
| `DeviceStructure.cs` | OWN + 接口事实（细节见对应协议） | `71871520158394051C96B330FEC066D19D059403C59CAE68E5DFE4EF99B23833` |
| `Monitoring.cs` | OWN + 接口事实（细节见对应协议） | `9FC31C84E860F40F17C9B430946D06C6017D32ADA5527E9676F5D2C096FB177B` |
| `GameLibrary.cs` | OWN + 接口事实（细节见对应协议） | `E6700AA3859880B27A5C6149CC2F90761EE2496E0F4C663AD54BB7EFB2D6F46B` |
| `DeviceGearLink.cs` | OWN + 接口事实（细节见对应协议） | `08A9D188C8C0B784D24DC39CDF8FDA396AEAAF32544D43955BE668A4B2AD02D3` |
| `StorageStructure.cs` | OWN + 接口事实（细节见对应协议） | `C7BDA80AC32B77D3F79EEC8AA1BB28604B1C0EACA0B9DCA95060FD5AF25BDF98` |
| `NativeSettingsStructure.cs` | OWN + 接口事实（细节见对应协议） | `C7D903EABC19E987642F4B5F276ADD124295E654AD65B3AF86B7C1D0070A251E` |
| `MacroEngine.cs` | OWN + 接口事实（细节见对应协议） | `DCB9E28B4EC9CB45CE92B19DD15297D6C3387848000ED3B3F5BE3D7BC686AD7D` |
| `MacroFeatures.cs` | OWN + 接口事实（细节见对应协议） | `E14369774C4D436216216620FCE51D6F2A1943755153060073DF1662A4FE4980` |
| `LcdStructure.cs` | OWN + 接口事实（细节见对应协议） | `B6DE12FA410B74CF6712173000B68896225403D6E4A2C00239CD0CFCA5FE77BC` |
| `AudioEndpointStructure.cs` | OWN + 接口事实（细节见对应协议） | `04D88338301438F4C62E6C1DD8147AB30D2160D13FEB59F98E46D7F734799C6E` |
| `ReceiverStructure.cs` | OWN + 接口事实（细节见对应协议） | `6EDECD5CDDB974A038BE54B32B216974CAF1510704C6C7616ED9B741D7213533` |
| `FirmwareStructure.cs` | OWN + 接口事实（细节见对应协议） | `5AF65E06E784D49897DE9D79259D868A1792B9CEF839CCB87F304E932A715F82` |
| `GearExtendedStructure.cs` | OWN + 接口事实（细节见对应协议） | `627F1EFDC1F75E102B668494B19FE5EA2EF4D02D102863207BB7CD38F47C65D1` |
| `FanLightingStructure.cs` | OWN + 接口事实（细节见对应协议） | `1D90EFB26F514598E58C2C6C2BF2B8E5183363CC3CA0E8ED0D086CBFEB0BE1A4` |
| `ScenarioProfiles.cs` | OWN + 接口事实（细节见对应协议） | `E171F233311BA10C44E92A47FA072AE72ACEC56A45F00CB57DBCFCDFB9432A55` |
| `ScenarioStructure.cs` | OWN + 接口事实（细节见对应协议） | `ADEB3E2B15EC9BDDECC5063639E16D6473D5A752D85449874ABA10EAC56BFD03` |
| `LocalUpdateCatalog.cs` | OWN + 接口事实（细节见对应协议） | `DD7DE144611524EA78E564FCB355E8810892B915891EC47D7F5C1E861142E323` |
| `LocalUpdateStructure.cs` | OWN + 接口事实（细节见对应协议） | `41CF46015DFF1867BA5D892CE249F3C3F0F24604B03B8C18D805ADACE3C13A70` |
| `BoardArgbStructure.cs` | OWN + 接口事实（细节见对应协议） | `FA535F673DB7BC623F26A112483AE77F98B364E09A5029C163D2C78E5C77A7D4` |
| `DiscoveryStructure.cs` | OWN + 接口事实（细节见对应协议） | `9362E7C65C45AEB05332E4B44554BBF35236DF52158B6F0F7D31503FB92D9216` |
| `DiscoveryWorker.cs` | OWN + 接口事实（细节见对应协议） | `73B068940CA91772C02C1C3B381CCA536F0A66AA600D2BE1A10854E1D60B353B` |
| `DirectAuraWorker.cs` | MIT + OWN + 接口事实（原 notice 保留） | `F5F4D96FB7DFF91AABF6F9C2D3C98E7DCE73152C49592E1BA1E90C0A2E17AE79` |
| `SDKWorker.cs` | OWN + 已安装 ASUS ABI 事实（不分发 SDK） | `6C61734959C6498670CEDD8AF1ECE1AE558D854F632A905D70E2703F62DA54A8` |
| `GearLinkWorker.cs` | OWN + 接口事实（细节见对应协议） | `69809523B9B7E5D0264C25343F192F66A4E256036BEEB8023C41A29F237B9A49` |
| `GearLinkFeatures.cs` | OWN + 接口事实（细节见对应协议） | `76E98280D46BEFBE6D5B7CDCAE267787EBE131A4EE6411941F75E26F1CD7E2A1` |
| `FirmwareCatalog.cs` | OWN + 接口事实（细节见对应协议） | `8B32033620982A35DB400AF96F11041907182BC8C3ADA37E824F2A9B9E5DCC66` |
| `StorageWorker.cs` | OWN + 接口事实（细节见对应协议） | `C246A001ED3CFCE6EFF70C1DCEC0DDC505046D65D661AC0ED2CEF8C41E01F9BC` |
| `AudioEndpointWorker.cs` | OWN + 接口事实（细节见对应协议） | `63B1AA26D5D9132CDE331DCF4F77D58595F86FEB1E141F88180564A2B28B7280` |
| `ControlPolicy.cs` | OWN + 接口事实（细节见对应协议） | `F9749E0AC69B9969511ECF906F03E9239AEC46824FB41735B840E5AA40B627AC` |
| `installer/InstallerCore.cs` | OWN + 接口事实（细节见对应协议） | `7F9005542D048D794D068B017FB9610672563B6EDEF495ABCCF75522230FBBE1` |
| `installer/Setup.cs` | OWN + 接口事实（细节见对应协议） | `3CCAC443346B2EC844137AC408E9405DEA323A1B9C52E3D2B1E303D696FA0B6D` |
| `AutoDeviceRead.cs` | OWN + 接口事实（细节见对应协议） | `2461C37B2178BFD80B181255DF676CCEB8C91A4836B35771822E901BC5BD12EC` |
| `GitHubUpdateCatalog.cs` | OWN + 接口事实（细节见对应协议） | `CFF6F4CBF721240907D47C15E48BAEEBAB5FD6CC3A5826954F3B088273EF673D` |
| `GitHubUpdateStructure.cs` | OWN + 接口事实（细节见对应协议） | `B07AEF413672DBEE7687D0B076A254DB488B3A4DA96E80B5EE48E4B886877562` |
| `LivePreviewHost.cs` | OWN + 接口事实（细节见对应协议） | `6A40DFB7E6B31E450421F87D7FD2EA16342C0DEAA53C62BB2CAEE2ECAE213EF7` |
| `LivePreviewState.cs` | OWN + 接口事实（细节见对应协议） | `F8A7B45D27330D036C9DE59DA38C4D24E48305F7568F4C43D38099FDA1D3C1D2` |
| `UiPreferences.cs` | OWN + 接口事实（细节见对应协议） | `A52B4E71FA0315399F4302F6C9F2DC942A1D3AD8E65CBFC93C143374B3B93C96` |
| `SupportedDevicesCatalog.cs` | OWN + 接口事实（细节见对应协议） | `532104E61CD5CF388DA3F23D07D31DC0120AA9A2D3321B98A10B30178051A439` |
| `NotebookWorker.cs` | OWN + 接口事实（细节见对应协议） | `ADD45B1F430AE68FF3DCEF81899FD1F0CB152A3F7BFCD4A4A36E1A903FB77E28` |
| `AsusDriverCatalog.cs` | OWN + 接口事实（细节见对应协议） | `98D33705488F346570E4CD737B8C13BC8121781676867B90BDF15CB022B4A6E2` |
| `AsusUpdateStructure.cs` | OWN + 接口事实（细节见对应协议） | `FF7F93DFD0E7DE89DE0D2210D755F78CE476809F4A735533C190E8FE959A259F` |
| `AuraSyncCoordinator.cs` | OWN + 接口事实（细节见对应协议） | `3851A209D41B21FB102A5D7B43AFDD258971B44FF4A3413A49D2162F9A170984` |
| `FanModeWorker.cs` | OWN + 接口事实（细节见对应协议） | `7C958CD33BA39A9CD24CAE8DBAE557BF3BE7E8A5AD83699CB0079E336519A132` |
| `MonitoringModels.cs` | OWN + 接口事实（细节见对应协议） | `EBCE22199F48368CF38ED5A86A5722AFB3EEB66869D32836B6EC4AE5F79DAD16` |
| `MonitoringLayout.cs` | OWN + 接口事实（细节见对应协议） | `3C32ED406E6110687203BAAD3B2909C78E01043D55205D13B16638FA3C35AC8B` |
| `PreviewAssets.cs` | OWN + 接口事实（细节见对应协议） | `6C36D4E78221C5B421AF46060447BFDE8D77F4910B3E41176EABD1911DE07A1B` |
| `TelemetryWorker.cs` | OWN + 接口事实（细节见对应协议） | `C3B40DBFFAEFF3B3A5A223241A547C1C47C63DB9CA18B484ABF8D7833CE6EE89` |
| `WindowsTelemetry.cs` | OWN + 接口事实（细节见对应协议） | `B248E0379DEB9480E2EFFC9F81AB001B5FED2A3BCF10135F1FB075D49647A7C5` |
| `WindowsPower.cs` | OWN + 接口事实（细节见对应协议） | `A284AD1FC4FCAF858B6E999FCE754091CC29D9C8D3FBEB87EDA0A7056E6EB501` |
| `WindowsToolsStructure.cs` | OWN + 接口事实（细节见对应协议） | `4DFDFA7A59DD241B21B619AC601C3E3CF04320C6390EF7096D4DB210F346D982` |
| `WindowsDisplay.cs` | OWN + 接口事实（细节见对应协议） | `749E566E53C1B55CE2EE6C814CED72E1F6AAEF4E8901534DF5E5AD5049BB8B24` |
| `WindowsDisplayStructure.cs` | OWN + 接口事实（细节见对应协议） | `FCCE8608EB9DE388F0B525F7A239F81FF38BCD646CC51DDE456ECF3560962343` |
| `WindowsBattery.cs` | OWN + 接口事实（细节见对应协议） | `6751D9A7A21A07BF5EEE189E8B3578EF8919ECA7A062D06BA9C9AAEFD0058019` |
| `WindowsBatteryStructure.cs` | OWN + 接口事实（细节见对应协议） | `B5F9AA2FD652EE3D0288A41C4E02A441A8377A7C38AD85FE26C5F4791D4B316E` |
| `NotebookStructure.cs` | OWN + 接口事实（细节见对应协议） | `CE92471989B1237F4AB3418889D116E6C414F849FCEA9F4A105AD59ECD54B48F` |
| `MousePerformanceStructure.cs` | OWN + 接口事实（细节见对应协议） | `FA2B86006E1BBAD6BB6F7B0B341804931F09813763B625DA49AB8C07D56B9B81` |
| `OriginalPixelLayout.cs` | OWN + 接口事实（细节见对应协议） | `A571C909AEC2CC514E71F23C67B9FDF4E20C7BA8926E51C61EBAB82DE9987ED6` |
| `KeyboardKeysStructure.cs` | OWN + 接口事实（细节见对应协议） | `150DCD78923406A4A3769E4D1C1CA47FA960950B9864B6652F8825E76A14D79A` |
| `KeyboardViewsStructure.cs` | OWN + 接口事实（细节见对应协议） | `3272C951235996C3C2E44F57E03C34249E2826A982EAAA22F40E3CF8BADAC7D8` |
| `GearPowerStructure.cs` | OWN + 接口事实（细节见对应协议） | `F8C10F307E91D4C47C220143FD5BD37C0B12D3D927BF8FEF3B5DAAFD12AFAF9E` |
| `KeyboardLightingStructure.cs` | OWN + 接口事实（细节见对应协议） | `EA8567702958A5AEAB3B5ADEEBE1A2FA92B12D7BBB08484DE40B1DB66CFF24EE` |
| `HeadsetAudioStructure.cs` | OWN + 接口事实（细节见对应协议） | `F3169959866A0490B54F317138E823E240A4C11433C0A241D226CCB800991877` |
| `GameFirstStructure.cs` | OWN + 接口事实（细节见对应协议） | `7FB6A4171516573CD6514B3719AAA329E924C682F9061F74A62726A7918C7008` |
| `GpuLightingStructure.cs` | OWN + 接口事实（细节见对应协议） | `37EF2D5F6F887BBA3A934BF79CF205021EBEC355FBA1DF417DE9B8F6027FE7DD` |
| `MouseButtonsStructure.cs` | OWN + 接口事实（细节见对应协议） | `2CD9401E11D176936CF61013226A30FA129BA3FB0753F11F4E29D695F54D05FC` |
| `MouseLightingStructure.cs` | OWN + 接口事实（细节见对应协议） | `ED989C2B238B8AC05F351A3A8F096F042E9286CF5D3BBCCEFA9D9A930C706A5E` |
| `MouseCalibrationZoneStructure.cs` | OWN + 接口事实（细节见对应协议） | `22BBE4E4C3D78EF9A54C30D1EE7850F3A898375BBC2679E0E6F72FAAD992AAF0` |
| `WorkerTextProtocol.cs` | OWN + 接口事实（细节见对应协议） | `EF759B050FC7F5E7CB579E4E4A6C2F1C3C69FE9967C9EF471B0886A5239A2453` |
| `MockWorker.cs` | OWN + 接口事实（细节见对应协议） | `36A887F6F0BE4A6D4145E698E42993DE778839AB31046A66C4D2CFA50BF3E494` |
| `SupervisorTests.cs` | OWN + 接口事实（细节见对应协议） | `A31F64A779455709DCF40E924CB84A4334597072A88DCFFEDD26F696E87F8FE8` |
| `ReadOnlyIntegrationTests.cs` | OWN + 接口事实（细节见对应协议） | `78104E13F77B2B728C69D9C2CDDC88115C5C14BAD264C0D5EF4EC86C4267F694` |
| `SDKWorkerTests.cs` | OWN + 接口事实（细节见对应协议） | `02214CEE807DCFB709B9C1F14EDC5A8A8119BF53E3FEB190EF745D74A2BBE80F` |
| `MacroTests.cs` | OWN + 接口事实（细节见对应协议） | `555C983C3379CD6A47137F951CD95F65C7FA2BAEB9AC8302DE5912439972CACC` |
| `GameLibraryTests.cs` | OWN + 接口事实（细节见对应协议） | `C475B4817C1ADBF1CD05E7376447F72BD7942580178A9AE2BC2EF3AAA29E1D68` |
| `ScenarioTests.cs` | OWN + 接口事实（细节见对应协议） | `65EAF60AF3AC677941EA271B444C070CA0131DCA38CAA98624E94C3D10D94F13` |
| `PeripheralReadOnlyTests.cs` | OWN + 接口事实（细节见对应协议） | `E04C166F124482AF492377659029A76D070B4F92AEDA50F022691B29DE3B103E` |
| `NativeDraftTests.cs` | OWN + 接口事实（细节见对应协议） | `FD3F765D44CD4B0F0B6AD0FAADCA0FD6C0E6D232F2F3052FFE35F54D116E6AC8` |
| `LocalUpdateCatalogTests.cs` | OWN + 接口事实（细节见对应协议） | `384E10AD47A09F37219373FAD3C0AFDACFB0AD6DDA12DB536A2BCEA6C08507ED` |
| `DiscoveryGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `7710849523D88758A2397E2E752535AFB7E36641F4E71B5D9B5774B204895C71` |
| `ControlPolicyTests.cs` | OWN + 接口事实（细节见对应协议） | `FC5681A2419A0E4A410FF0BD446E1DC5454BF89AB2BB15FA454F8D158BE5F564` |
| `installer/InstallerTests.cs` | OWN + 接口事实（细节见对应协议） | `C7F0630CE69729ABDA031679882F80F864D3057FF81CF4D3702C19B21DBF2DA5` |
| `GitHubUpdateCatalogTests.cs` | OWN + 接口事实（细节见对应协议） | `45291F18F4E90800C43999F3AD82C3858D3EBDFB31EED142ADAA1B6FA1DEFA8F` |
| `NotebookWorkerTests.cs` | OWN + 接口事实（细节见对应协议） | `5922537BB3387F9DB2B9C88B385F9C7A3B2D7556132E91093D9E8D57F135F0CF` |
| `AsusDriverCatalogTests.cs` | OWN + 接口事实（细节见对应协议） | `3508E9AB41325C9844A012C782C55E516F612C3CFD956D77835E07EF1CFA31A4` |
| `AuraSyncCoordinatorTests.cs` | OWN + 接口事实（细节见对应协议） | `C06F9A08A1DA22C3C85DA14172C66EA269E283E16FCC48C6B575D8D481B025EE` |
| `FanModeWorkerTests.cs` | OWN + 接口事实（细节见对应协议） | `67C6C5FF573E63315572B5F8B34C0CF70E7222D5901C447E4D2EB35D02B98F73` |
| `MonitoringTests.cs` | OWN + 接口事实（细节见对应协议） | `4CF0B18BAE1A49F6554515C6C3F4160DC7BFF399870242F03D8C4BAB3CC22ABF` |
| `PeripheralLightingTests.cs` | OWN + 接口事实（细节见对应协议） | `67712CC5317C3989FDCCA9B5D6B31F3A38F96C4E9681B32AA9742292C4879B85` |
| `TelemetryWorkerTests.cs` | OWN + 接口事实（细节见对应协议） | `7EC5C65B70639D23961533E4F5A12A4A751971DB191ECEEEEFD849CA27CC3192` |
| `WindowsTelemetryTests.cs` | OWN + 接口事实（细节见对应协议） | `97C65152870431226B37E3E4974763578BB802EB9AFF90B00F652F8AD1046D07` |
| `WindowsPowerTests.cs` | OWN + 接口事实（细节见对应协议） | `A6ABD09864FAE872FD5515FB5458E06B666B3BFAB3B28F27D58AFAD09101E0A1` |
| `WindowsToolsTests.cs` | OWN + 接口事实（细节见对应协议） | `9DB9CE3C591013F53CD156B01A437A0285376F3062FE0C1AC591973534C58212` |
| `WindowsDisplayTests.cs` | OWN + 接口事实（细节见对应协议） | `C877280D4566D20514C23BC08CC71F0303197F68C05DB5F4C2FC3735FE48F7AF` |
| `WindowsDisplayGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `2D1733BE1084926BF8BEDA983BEF8AC9439BE42486D6E86E9CB1A3DF0AAFB481` |
| `WindowsBatteryTests.cs` | OWN + 接口事实（细节见对应协议） | `88EFE7F5E594B80534861E28E28AF0619B711F02AC980E994236114964A4A2E8` |
| `WindowsBatteryGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `78C40954E67DB6F2C21EDDEDA0B9A1CDFA92C49A2B2754B53E17DC7FD58D92A7` |
| `NotebookGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `91CAEA5372C48A688BFDB2FC6BF2EEE71397FFC8CA0948CA98C24FAB68A264C3` |
| `MousePerformanceGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `635A3CA62E117802177ECDD1976BE65892A5E0A30A0D9213E6E0C076F0663C70` |
| `OriginalPixelLayoutTests.cs` | OWN + 接口事实（细节见对应协议） | `EE54EEC7441BA742E16970402BBF48FE1C427D53C4C408B6E65805556354BA01` |
| `KeyboardViewsGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `0E5A1EFE330C538A836C307E611F0A66ED935FE1CAB75ADDC514D567C92ABB09` |
| `GearPowerGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `7E17B05F87C511F9E673E382A6110A3DA34D90577C0B3EE1E921F4FC64E364A4` |
| `KeyboardLightingGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `D4FDD750952057DA60E0004627F515D0ED88EFEC2B8171719518BD451EE848FD` |
| `HeadsetAudioGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `77F8E86805EBBECE5689F403F5AF60FCE8E407675C0C434B804FF40B2DF83C1A` |
| `MotherboardGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `CE7AA2F2F2DB3E735B16F337B507E7A4353D124472E92E40B6918804F67DE833` |
| `LcdSettingsGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `7650544C17AC83A6CE69311B936940E0A11964D8BB0E92DE89718614933A00A7` |
| `LcdMonitorGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `82BCE7E74A5E1A7838A71B24C8B90B3656E9C86E351E5CE6C90241C0997E4B75` |
| `GameLibraryGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `0678F5C66BCBC3D4CCCA3FF287788DDDC8D745C2D8474C53E23FDA883C92D7DD` |
| `ReceiverGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `8A8191AA520B4EC042B8E2C21813A281C602BF66E8AF8CCC04AD51D07B41D356` |
| `ScenarioGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `180B4AC97531EE6FBD0FF4F94AC1E63A60363831C911F4EBE323A1683CE730F0` |
| `GameFirstGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `25247726FA73DD459DE4B7E760FDE7063EB0E347EBCE454F02217CDC84C38C16` |
| `MacroGuiTests.cs` | OWN + 接口事实（细节见对应协议） | `467FD9ACAEC1044C15E0AEA96BB0303718BC318CA1793B8124A97E7018C10622` |
| `checks/MouseButtonsChecks.cs` | OWN + 接口事实（细节见对应协议） | `D9145CA2EDC4C040C3B6A8902F6E837A3A73224C7FD477A48A25724C4160E9DA` |
| `checks/MouseLightingChecks.cs` | OWN + 接口事实（细节见对应协议） | `50173F1C955675C8B0804F299731B073037056DCA3BB4D872D623D8EB885949B` |
| `checks/MouseCalibrationZoneChecks.cs` | OWN + 接口事实（细节见对应协议） | `569D88C047A11FAAA893A40FB80184A8B6D4091FA938134EED79D811F84ECC7B` |
| `checks/MousePageLifecycleChecks.cs` | OWN + 接口事实（细节见对应协议） | `130BCFDE74495106AAA250AA5D4287916BCE75BD0DFB7F98950D62977964A7C0` |
| `WorkerTextProtocolTests.cs` | OWN + 接口事实（细节见对应协议） | `D3A9AF78E0257294970ED304D519BC2E4713AF85ED180132B2CAD63CEFF5CCDD` |
| `app.manifest` | OWN + 接口事实（细节见对应协议） | `AC2CA247C2A6AF54787C26A2A7CF66D9292A622A5A902C051DC37471A6FF55C7` |
| `OpenAC.exe.config` | OWN + 接口事实（细节见对应协议） | `601E8AA2F94CB6F551B2A78B971130E48AA27C6616554467CD51BF20F97C94AA` |
| `build.ps1` | OWN + 接口事实（细节见对应协议） | `6EA3B8B26D29FF95F2265A48D89CAA95FFA001AB0DCFC24F165CB47D3E8F2EEB` |
| `build-all.ps1` | OWN + 接口事实（细节见对应协议） | `D6F7EE1F99BCCF86C343F121B609479AD765BC71D408CD295ECD19B27C85B22D` |
| `build-gearlink-worker.ps1` | OWN + 接口事实（细节见对应协议） | `AC671F55D7934708EF6BDC695CB9AD47119E5AF52D592B55E6EA62194B58C174` |
| `package-candidate.ps1` | OWN + 接口事实（细节见对应协议） | `BFACC811D63300C6CCEC68DF5FDBEC70FBFD874FDE985684AE98C3DFFCD62470` |
| `.gitignore` | OWN + 接口事实（细节见对应协议） | `55F97C2E76A70F608A59AFFA14D19B06C433D67BF179560C23E938464B927AF2` |
| `installer/build-installer.ps1` | OWN + 接口事实（细节见对应协议） | `6CC39552B066D06760D65CB5344B6846C06B8C860CF94A949FE39135FA5293CE` |
| `installer/app.manifest` | OWN + 接口事实（细节见对应协议） | `5F7456487E607BB2E541A0077F62E65A55A0ECA9D7A197D423E38C7044F36584` |
| `installer/RuntimeFiles.json` | OWN + 接口事实（细节见对应协议） | `C48B8807B0D193F8826A852519A515A9E06AD4D72BFEF97273FA8FE4D5A6B435` |
| `.gitattributes` | OWN + 接口事实（细节见对应协议） | `2DAD7ECA798A0F34EE5870EB20C1D13C0BAE27420B7695C6141A7A4D49D241F3` |
| `preview.ps1` | OWN + 接口事实（细节见对应协议） | `D79DA2183A4324BEB6E91E524C1BDAD99F17C34499FC117F914499A9CD8AE7AB` |
