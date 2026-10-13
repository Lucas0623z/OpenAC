# 本机更新中心研究：只读缓存与实际缺口

2026-10-13。实现文件 `LocalUpdateCatalog.cs` / `LocalUpdateStructure.cs`，不含 ASUS 原始代码或安装器，不启动服务、不写硬件、不安装更新。

## 可接入的 UI

根窗口 partial `MainWindow` 方法 `UIElement LocalUpdatePanel()`，在原更新中心两组表格下调用 `content.Children.Add(LocalUpdatePanel());`。需要编译纳入两个新源文件，引用已存在的 System.Web.Extensions 和 WPF。所有新增组默认折叠；重新读取仅读取限定本机文件。

`LocalUpdateCatalog.Read()` 返回字典，固定 `sourceKind=local_updater_cache`，`liveServerChecked=false`、`latestKnown=false`、`downloadEnabled=false`、`installationEnabled=false`、`hardwareAccess=false`。

`dependencies` 为 `CachedDeviceDependency[]`，字段 `model/sourcePath/cacheModifiedUtc/devicePageRequirements`；每项为 `CachedUpdateRequirement`：`name/profileId/category/registryKey/minimumVersion/cachedInstalledVersion/minimumStatus`。最低要求只与 `versioninfo.ini` / `deviceinfo.ini` 中精确匹配 regkey 的唯一安装版本缓存比较；多个冲突值或无值则未知。`minimumStatus` 仅 `meets_cached_minimum` / `below_cached_minimum` / `unknown`，不是最新状态，也不证明 DLL 工作正常。

`catalog.items[]` 为当前硬件及核心服务的组，字段 `name/displayName/type/cachedErrorCode/cachedDependencyStatusCode/cachedDownloadedFlag/items`。组内字段 `module/instance/cachedInstalledVersion/cachedServerVersion/cachedDate/latestStatus=unknown/versionsEqualInCache`。下载标志只是原缓存记录，不证明文件存在或通过校验。未知原版状态码保持原值，不解释。

## 本机证据

官方缓存：Program Files/ASUS/ROG Live Service/DeviceUpdateList.xml；ProgramData/ASUS/ROG Live Service/versioninfo.ini、deviceinfo.ini；DeviceDependentVersion 下当前键盘、鼠标、耳机、OMNI 的 JSON。提取当前硬件的组件清单、profileid、category、regkey、minVersion，DTD 与外部实体禁用，文件大小有界。

当前 29 项设备页面依赖均满足其缓存最低要求。举例 MouseSDK 3.01.52 ≥ 3.01.52；KeyboardSDK 3.00.99 ≥ 3.00.97；AudioSDK 3.02.18 ≥ 3.01.37。这不是在线确认。

缓存 server 版本与已安装记录存在逆向差异：AURA 本地 3.11.08 / server 3.10.12；Framework 本地 4.2.5.4 / server 4.2.4.13；MainSDKPlugin 本地 154.0.4258.62 / server 152.0.4191.66。不能将缓存 server 称为最新。

限定安装日志：RLSSetup.log 2026-10-13 08:14:27 MSI exit code 0；Armoury Crate Full Installation Package_01_20260910012104.log 2026-09-10 01:21:05 两条 bRunCmd failure (1,1638)。仅提取时间和数值，不包含命令行、用户路径、序列号、认证凭据、一般服务日志。没有解释 unknown 状态码，也没有把历史成功当修复证据。

## 官方目录与下载校验

静态读取已安装且 Authenticode Valid / ASUSTeK COMPUTER INC. 签名的 ROGLiveService.exe、asupdate.dll、ArmouryCrateInstaller.exe，未执行厂商方法。生产 RLS profile 路径 `https://rog-live-service.asus.com/developer/aura/v2/profiles/`；当前 Framework profileid 39478 的只读未认证请求到 `/39478/` 实际返回 HTTP 401：Authentication credentials were not provided。生产 config root 只读 GET 返回 404，完整路由契约尚未知。未读/复用任何凭据，未绕过受信任进程校验。

asupdate.dll 静态字符串明确存在生产 Omaha endpoint `https://rog-live-service.asus.com/service/update2`、`hash_sha256`、CUP ECDSA、`X-Cup-Server-Proof`、CRX3、`VerifyAsusAuthenticodeSignature`、WinVerifyTrust。RLS 中有 FilesSHA256Match / CheckFileMD5orSha256MatchOrNot。仅看这些名字不能证明具体目录签名、公钥、请求、包映射、更新顺序和安装 rollback 契约完整，因此不会实现猜测的下载/安装或宣称更新修复。

下一步须获得公开且可验证的完整目录与签名契约，建立当前组件 package / profile / dependency 映射，下载时验证官方地址、目录认证、长度与 SHA256，并对解压安装器及内部 DLL 按 ASUS Authenticode policy 校验。固件 GearLink manifest 另属独立已实现的公开只读功能；它提供内容 SHA256 观测值而未提供可信发布签名，不能混称 Armoury 官方组件更新。

验证：UpdateCatalogProbe 六项纯 / 只读检查通过，涵盖缓存不会称最新、DTD 禁止、结构拒绝、数值版本比较及未知版本、禁止下载/安装/硬件访问。证据 `local-update-catalog-evidence.json`。
