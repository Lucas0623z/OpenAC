# Windows 电源方案与 CPU Boost

`WindowsPower.cs` 是按 Microsoft 公开接口独立编写的 MIT 实现，兼容 .NET Framework 4.8 / C# 5。它读写 Windows 电源方案里的处理器 Boost 策略，不代表 BIOS、EC 或主板风扇模式，不声明已经完成 G-Helper 的全部功能。

## 已实现的接口

| 接口 | 行为 |
| --- | --- |
| `WindowsPowerService.ReadSnapshot()` | 返回当前方案、实际方案列表、当前方案的 AC/DC Boost 原始索引，以及读取期间方案是否一致 |
| `ReadCpuBoost(Guid scheme)` | 读取当前枚举列表中的指定方案，分别保留 AC/DC 数值或读取错误，不切换该方案 |
| `ChangeActiveScheme(Guid target, Guid expectedActive)` | 手动切换已枚举方案；检查预期当前方案、刷新目标列表、执行及读回，失败时尝试恢复 |
| `ChangeCpuBoost(Guid expectedActive, uint? ac, uint? dc)` | 手动修改当前方案的 AC、DC 或两者，索引只接受 0–6；未指定的通道不写入 |
| `Restore(WindowsPowerRestorePoint point)` | 手动恢复同一服务实例签发的原值；再次检查方案和本模块留下的值，发生外部更改时拒绝覆盖 |
| `IWindowsPowerBackend` | 可注入的读写边界，测试替换为内存模拟实现 |

所有 getter、构造函数和测试默认入口都不会写入 Windows 电源策略。没有定时应用、启动应用、关机恢复、方案创建/删除、隐藏属性注册表修改或提权功能。GUI 后续应由明确的用户操作调用 setter，并使用新的读取结果作为 `expectedActive`；本模块没有接入现有 GUI 或构建清单。

## 原生接口依据

方案列表使用 `PowerEnumerate` 的 `ACCESS_SCHEME=16`；处理器设置子组为 `54533251-82be-4824-96c1-47b60b740d00`。枚举只接收 16 字节 GUID，拒绝重复、空 GUID 和超范围结果。[Microsoft：PowerEnumerate](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerenumerate)

当前方案读取通过 `PowerGetActiveScheme`。该接口返回 Windows 分配的 GUID 指针，本实现无论 GUID 转换是否成功都在 `finally` 中调用 `LocalFree`。手动切换通过 `PowerSetActiveScheme`。两个接口及写值接口当前文档归于 `powersetting.h`，运行库均为 `PowrProf.dll`。[Microsoft：读取当前方案](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme)、[设置当前方案](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme)、[LocalFree](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-localfree)

显示名使用 `PowerReadFriendlyName` 获取的 Unicode 字符串；缓冲区按 API 返回的字节数分配并限制上限，名称读取发生尺寸变化时最多重试三次。[Microsoft：PowerReadFriendlyName](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadfriendlyname)

AC/DC 索引分别使用对应的读值接口，失败不会替换为默认值；例如 AC 不可读时仍可以返回有效 DC 值。原始索引超过当前支持范围时保持其原值，setter 不会把它当作可以恢复的 0–6 值。[Microsoft：AC 读取](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex)、[DC 读取](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreaddcvalueindex)

写入当前方案的设置后，需要再次调用 `PowerSetActiveScheme` 才会应用。本实现仅在手动 Boost 修改及其必要回滚内执行这一操作；方案仍须等于预检的方案。[Microsoft：AC 写入](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex)、[DC 写入](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwritedcvalueindex)

Boost 设置 GUID 为 `be337238-0d82-4146-a960-4f3749d470c7`。Microsoft 的设置页给出 0–4，另一个官方 `ProcessorPerformanceBoostMode` 枚举也给出 5/6。实现使用这两个来源确定的索引及枚举名；具体行为取决于处理器、CPPC/PEP 和 Windows，不从保存索引推断实际 CPU 频率或固件能力。[Microsoft：PERFBOOSTMODE](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/options-for-perf-state-engine-perfboostmode)、[官方 Boost 枚举](https://learn.microsoft.com/en-us/dotnet/api/microsoft.windows.eventtracing.power.processorperformanceboostmode?view=trace-processor-dotnet-1.0)

| 索引 | `WindowsProcessorBoostMode` |
| --- | --- |
| 0 | Disabled |
| 1 | Enabled |
| 2 | Aggressive |
| 3 | EfficientEnabled |
| 4 | EfficientAggressive |
| 5 | AggressiveAtGuaranteed |
| 6 | EfficientAggressiveAtGuaranteed |

## 手动更改与恢复

`WindowsPowerChangeResult` 区分 `Succeeded`、`WriteAttempted`、`Changed`、`RollbackAttempted` 和 `RollbackSucceeded`。`Before` 保留修改前方案及数值，`After` 是可取得的后续读取结果；`Error` 与 `NativeErrorCode` 保留主要失败，`RollbackError` 单独记录恢复失败。成功返回仅说明 Windows 存储索引及活动方案已读回，不保证 CPU 的瞬时运行频率。

Boost 修改在任何写入之前读取原值。如果请求中的任一通道不可读取，或原值不在可恢复的范围内，整个请求会在写入前失败。修改每个通道前及重新应用方案前，都重新检查活动方案和各请求通道的预期值。数值已等于请求时不写入，也不重新应用方案。

失败回滚逐项检查当前索引。只有仍等于本次请求值的项才写回原值；已经是原值的项不再写入，变成第三个值的项被视为外部修改，保留并报告回滚未完成。若原方案已经变为非活动方案，可恢复本次修改的显式方案索引，但不会将它重新激活并覆盖其他程序的方案选择。恢复写值失败或重新应用失败都会单独报告。

`RestorePoint` 只包含本模块仍留下的更改。恢复操作会再次检查记录中的预期方案和索引，包括进入恢复事务后的原值读取；使用其他服务实例的记录、已删除的方案或已被外部修改的索引都会拒绝。记录是同一服务实例内存中的手动恢复能力，不会被自动持久化或执行。失败结果中的 `Before` 仍保留原值，即使不能签发安全恢复记录。

Microsoft 这些接口没有提供比较并交换事务。进程内操作通过锁串行化，但系统设置、厂商软件或另一个进程仍可能在两个调用之间更改方案或数值，甚至发生相同值的往返。预检、读回和回滚是可观察的尽力保护，不能承诺跨进程原子性。回滚错误不被包装为成功。

## 验证

`WindowsPowerTests.cs` 默认仅使用 `Fake` 后端，335 项检查通过，包括 49 组合法 AC/DC 组合、单通道修改、原值捕获和手动恢复、非法输入、未知原值、方案删除、访问失败、读回不一致、部分写入、写入后报告失败、方案/索引竞态、回滚失败及外部状态保留。没有调用真实 setter。

测试的显式 `--read-only` 模式只调用一次 `ReadSnapshot()` 工作流，可把结果写到调用者指定的本地文件；不接受写入模式。2026-10-13 完成一次实际读取：枚举 4 个方案，读取期间活动方案一致，AC/DC Boost 均为 2。实机写入与实机恢复未验证，完整结果留在公开源码之外。

已接入工具页及公共构建。未知 Boost 索引保留在 RawAcBoost/RawDcBoost 诊断字段，对应可操作值保持未知。GUI 的 26 项纯检查覆盖草稿、默认关闭和失败恢复点保留；恢复点等待保留或恢复决定期间，热预览暂缓重建该服务实例。
