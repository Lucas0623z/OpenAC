# Windows 系统电池遥测

`WindowsBattery.cs` 是独立编写的 MIT 只读模块，兼容 .NET Framework 4.8 / C# 5。它读取 Windows 的系统电源状态及电池驱动遥测，与 ROG 键鼠耳机的电池设置分开。没有充电上限、充放电设置、注册表修改、驱动安装、提权或定时器。

## 接口与输出

`new WindowsBatteryReader()` 不执行 I/O；`ReadSnapshot()` 才读取。`IWindowsBatteryBackend` 可替换为测试后端，只有 `ReadSystemStatus` 和 `Query` 两个读取入口。

`WindowsBatterySnapshot` 包含 `HasSystemBattery`、`AcOnline`、`Charging`、`Critical`、`BatterySaver` 等可为空布尔值，系统汇总 `ChargePercent`、可用的剩余/满电估计秒数，以及只读的 `Batteries`、`Errors` 集合和读取时间。

每个 `WindowsBatteryRecord` 使用 SHA256 `StableId`，显示名为“系统电池 N”。设计/满充/剩余容量的属性名以 `MilliwattHours` 结尾，电压以 `Millivolts` 结尾，功率以 `Milliwatts` 结尾。另有 `HealthPercent`、该电池计算的 `ChargePercent`、有效循环计数、充放电状态和数据一致性标记。系统汇总百分比不被复制为某一块电池的百分比。

不查询设备序列号、UniqueID、厂商字符串或原始设备路径。`InstanceName` 只在内部精确合并和散列期间使用；输出不包含它。输入行的属性不公开，默认序列化只产生空对象，快照没有原始行。错误只保留固定来源、分类和错误码，不输出可能含身份的异常原文。

## 公开 API 与可缺失的 WMI 遥测

系统汇总通过公开 `GetSystemPowerStatus` 读取。Microsoft 文档定义 AC 状态、电池标志、百分比和估计时间；实现按这些字段的未知值分别处理。[Microsoft：GetSystemPowerStatus](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getsystempowerstatus)、[SYSTEM_POWER_STATUS](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-system_power_status)

`root\wmi` 的 `BatteryStatus`、`BatteryStaticData`、`BatteryFullChargedCapacity`、`BatteryCycleCount` 作为可选驱动遥测来源。Microsoft 的官方 Scripting Blog 展示了 `BatteryStatus` 的实例名、Active、Tag、容量、电压和状态属性，同时明确说明这种 WMI 类没有作为受支持的公开 WMI 合同记录。实现不能因此承诺所有设备均提供这些类或属性；类缺失、拒绝访问和字段不可用时保持为空。[Microsoft 官方 WMI 示例及限制](https://devblogs.microsoft.com/scripting/using-windows-powershell-to-determine-if-a-laptop-is-on-battery-power/)

字段语义依据公开 WDK 的对应电池 WMI 数据块，不复制其实现。下表区分来源与输出单位。

| 来源 | 读取字段 | 输出 |
| --- | --- | --- |
| BatteryStatus | Active、Tag、PowerOnline、Charging、Discharging、Critical | 当前实例与可用状态；互相矛盾的充/放电状态不给功率 |
| BatteryStatus | RemainingCapacity、Voltage、ChargeRate、DischargeRate | 确认单位后的剩余 mWh、电压 mV、当前方向的 mW |
| BatteryStaticData | Capabilities、DesignedCapacity | 确认容量/功率单位，设计 mWh |
| BatteryFullChargedCapacity | FullChargedCapacity | 满充 mWh |
| BatteryCycleCount | CycleCount | 有效循环计数 |

Microsoft 公布的 WMI 数据块列出这些状态、容量和计数字段。[BATTERY_WMI_STATUS](https://learn.microsoft.com/en-us/windows/win32/api/batclass/ns-batclass-battery_wmi_status)、[BATTERY_WMI_STATIC_DATA](https://learn.microsoft.com/en-us/windows/win32/api/batclass/ns-batclass-battery_wmi_static_data)、[BATTERY_WMI_FULL_CHARGED_CAPACITY](https://learn.microsoft.com/en-us/windows/win32/api/batclass/ns-batclass-battery_wmi_full_charged_capacity)、[BATTERY_WMI_CYCLE_COUNT](https://learn.microsoft.com/en-us/windows/win32/api/batclass/ns-batclass-battery_wmi_cycle_count)

## 单位及未知值

`BatteryFlag=255` 先判为未知，不将它的 128 位误读为无电池；其他标志含 128 时明确无系统电池，不生成电池记录，也不显示百分比。百分比只有 0–100 有效，包括 0；255 或其他超范围值保持未知。AC 和节电状态分别处理其有效值。估计时间的 `0xFFFFFFFF` 不显示，接入 AC 时不显示电池耗尽估计。上述约定来自系统电源状态结构的字段定义。

`BATTERY_CAPACITY_RELATIVE=0x40000000` 表示容量和速率处于未指定的相对单位。实现只有在匹配的静态数据提供可用 Capabilities 且这一位未设置时，才输出 mWh/mW 字段；相对单位或缺 Capabilities 时不把数字标成 mWh/mW，也不通过假设电压转换为 mAh/mA。明确未设置 `BATTERY_SYSTEM_BATTERY` 的实例不被列为电脑系统电池。[Microsoft：BATTERY_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/power/battery-information-str)

电压读取按 mV 保留，不与 mW 相乘混用。当前方向明确时，充电功率为正、放电功率为负；双向标志同时为真或方向缺失时不提供功率。AC 在线与放电并不被自动判为矛盾，因为驱动可能运行电池维护过程。没有原始速率时不推算功率；空闲状态只有两个实际速率都为 0 时才显示 0。[Microsoft：BATTERY_STATUS 单位和未知状态](https://learn.microsoft.com/en-us/windows/win32/power/battery-status-str)

`0xFFFFFFFF` 容量/电压等哨兵、负的无效值、超出 LONG 有效幅度的速率和非整数类型均不给遥测值。Microsoft SDK 定义相应未知容量、时间、电压及速率常量；本模块不把这些常量当作实际电量或功率。[Microsoft SDK：poclass.h](https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/shared/poclass.h)

有效设计容量大于 0 且满充容量可用时，健康百分比计算为 `100 × full / design`，允许实测结果超过 100，不裁成 100。设计值为 0 或字段不可用时健康值为空。剩余容量超过满充容量时保留容量原值，但不制造一个裁切后的电量百分比。循环计数 0 在 WDK 定义中也可表示无计数器，因此保留为未知；百分比及实际遥测速率的 0 不受此规则影响。

## 当前实例与边界

以 `BatteryStatus.Active=true` 的实例为当前读取锚点，已提供 Tag 为 0 的实例不产生当前电池记录。辅助数据按 `StringComparer.Ordinal` 精确的 InstanceName 合并；不同大小写、不同实例或不同电池的资料不互相借用。双方有可用 Tag 时必须相等，否则对应字段保持为空并记录电池更换/数据变化。重复的当前身份或辅助身份被视为有歧义，不能任取第一条。

散列表示 Windows 驱动实例/电池槽位身份，不承诺在更换电池包后识别同一块实体电芯。多个查询也不是原子快照；属性缺失或 Tag 未提供时不能证明所有数据块来自同一次电池状态。

每类最多 128 行，每类等待最多 2 秒，整体读取预算 6 秒。原生 WMI 使用半同步枚举及有限 Timeout，但 Microsoft 明确说明该 Timeout 不约束 Connect。为保证调用方返回有界，查询在单独任务执行并有独立等待期限；超时结果丢弃。[Microsoft：ManagementOptions.Timeout](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementoptions.timeout?view=netframework-4.8.1)

查询门禁是存放在当前 AppDomain 的 System `SemaphoreSlim`，初始化受共同锁保护。真正查询任务结束前才释放；超时调用方不会释放正在查询的门禁。同一 AppDomain 中通过 `Assembly.Load(byte[])` 热更新的不同代码版本共享它，旧版本卡住时新版本也不能叠加查询。另一个 AppDomain 或进程具有独立门禁。不能声称已经强行终止卡住的原生提供程序，但不会在此 AppDomain 不断增加挂起查询。

## 验证

`WindowsBatteryTests.cs` 默认只运行 351 项模拟检查，包括百分比全范围、未知标志、无系统电池、相对单位、缺失字段、充放电冲突、哨兵值、超过 100 的健康值、实例精确合并、重复实例、Tag 更换、错误隐私过滤、多电池、查询边界及跨字节加载程序集共享门禁。没有调用真实 GET、WMI 查询或 setter。

显式 `--read-only` 只调用一次实际 `ReadSnapshot()`。2026-10-13 本机结果为无系统电池、AC 在线、0 个电池记录、百分比未知、0 个来源错误；没有制造笔记本电池或借用 ROG 外设电量。该次路径无需 WMI 查询，所以真实带电池设备上的 WMI 提供程序仍未实机验证。完整结果留在公开源码之外。

本模块没有修改现有 GUI、构建脚本或发布白名单；工具页由主任务接入真实读取结果。
