# OpenAC 官方兼容范围及适配状态

此清单用于确定适配范围和设备名称，不能证明设备可控制。所有 catalog 记录的 `ControlSupported`、`ProtocolVerified`、`HardwareValidated` 固定为 false；实际读写必须由独立设备适配器和功能能力判定。

## 官方范围

- [Armoury Crate 兼容设备 FAQ](https://rog.asus.com/support/faq/1042459/)，页面标示更新时间 2026-06-30，本次核对日期 2026-10-13。
- [官方历史笔记本及台式机清单 PDF](https://dlcdnets.asus.com/pub/ASUS/GamingNB/AppforWin10/ArmouryCrateService/Supported_List.pdf)提供型号示例和变体组；仍以主 FAQ 的发货时间范围为准，不能称为完整当前逐 SKU 登记表。
- [Armoury Crate SE 说明](https://rog.asus.com/support/faq/1050220/)提供 RC71L、RC72LA、RC73YA、RC73XA。SE 页面独立于桌面 Armoury Crate。

兼容 FAQ 对主板按系列及发货时间划定范围，对显卡按产品系列划定范围。ASUS 没有在该 FAQ 提供所有 SKU、USB PID、ACPI 功能码、固件版本或协议。当前清单完整收录该 FAQ 的事实条目和范围规则，不把它扩展成未经验证的具体设备控制列表。

## 记录规则

- 型号名称保留源站写法；官网数处型号缺少分隔符，按明确的 ROG 或 TUF 品牌边界拆开。
- FAQ 同时列出的配置和 Aura 条目合并，分别保存两种资格。仅 Aura 列出的设备不因此具有设备配置资格。
- 同系列 GPU、第三方设备类别、跨年 TUF 笔记本范围保留为规则。规则不会自动匹配或授权设备适配器。
- 官网限定 RGB 版本或排除 GL504GM/GL504GS 的条件保留。`ROG Strix Scar 16 (2024) G614` 按源文保留，未擅自改为另一产品名称。
- 官方 PDF 的斜线后缀组保留为原始变体组，不猜测展开的 SKU。
- 7 条本机安装模块身份单独标记 `LocalInstalledIdentity`。它们是已安装设备模块的名称与 PID 事实，不是新增官方兼容声明。蓝牙和 LE Audio 的 PID 也不代表相应传输路径已实现。
- 图片字段只含已记录的官方来源 URL。没有打包官方图片；缺失的精确型号图片保持 Unknown，不以近似产品图填充。

## 当前数量

| 证据类型 | 记录数 |
| --- | ---: |
| HistoricalPdfModel | 69 |
| LocalInstalledIdentity | 7 |
| OfficialModel | 293 |
| OfficialPartnerCategory | 5 |
| OfficialSEModel | 4 |
| OfficialSeriesRule | 30 |

总记录数 408。不同证据来源可同时出现同名产品，数量不是已完成适配数量。

## 完整条目

| 类别 | 名称 | 平台 | 配置资格 | Aura 资格 | 证据 | 型号或 PID | 条件 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Accessory | ROG Aura Terminal | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Accessory | ROG Raikiri | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Accessory | ROG Raikiri Pro | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Accessory | ROG Spotlight | WindowsPeripheral | — | 是 | OfficialModel |  |  |
| Cooler | ProArt LC 420 | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN 240 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG RYUJIN 360 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG RYUJIN II 240 | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN II 240 ARGB | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN II 360 | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN II 360 ARGB | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN II 360 ARGB EVA EDITION | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN III 240 | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN III 360 | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN III 360 ARGB | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN III 360 ARGB EVA-02 EDITION | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG Ryujin III 360 ARGB Extreme | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN WB BLK | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUJIN WB WHT | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Cooler | ROG RYUO 120 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG RYUO 240 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG RYUO III 240 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG RYUO III 360 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 120 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 120 RGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 240 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 240 RGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 360 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 360 RGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC 360 RGB GUNDAM EDITION | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 120 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 240 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 240 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 280 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 360 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC II 360 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG Strix LC III 240 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG Strix LC III 360 | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG Strix LC III 360 ARGB | WindowsDesktop | 是 | 是 | OfficialModel |  |  |
| Cooler | ROG STRIX LC III 360 ARGB LCD | WindowsDesktop | 是 | — | OfficialModel |  |  |
| Desktop | G10 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G13 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G15 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G15CK | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G15CS | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G15CX | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G16 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G21CN-D (D18) | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G21CT-D (D18) | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G21CX (D18) | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G22 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G35 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | G35CZ | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | G700 | WindowsDesktop | — | 是 | OfficialModel | G700 |  |
| Desktop | GA15 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | GA15DH | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GA15DH-S | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GA35 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | GA35DX | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GL10CS | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GL10DH | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GL12CX | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GL12CX-C | WindowsDesktop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Desktop | GM700 | WindowsDesktop | — | 是 | OfficialModel | GM700 |  |
| Desktop | GT15 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | GT35 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | S502 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | S701 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | T500 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | T501 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | T700 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Desktop | T701 | WindowsDesktop | — | 是 | OfficialModel |  |  |
| FanController | FAN CONTROLER | WindowsPeripheral | — | — | LocalInstalledIdentity | fanController / 1D98 | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| FanController | ROG FAN CONTROLLER PLUS | WindowsPeripheral | — | — | LocalInstalledIdentity | fancard / 1A9D | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| GraphicsCard | ASUS KO All KO series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG ASTRAL RTX5080 | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG ASTRAL RTX5090 | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG ASTRAL RTX5090D | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX GTX 16 series | WindowsDesktop | — | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RTX 20 series | WindowsDesktop | — | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RTX 30 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RTX 40 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RTX 50 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RX 5000 series | WindowsDesktop | — | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RX 6000 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | ROG STRIX RX 7000 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF GTX 16 series | WindowsDesktop | — | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RTX 20 series | WindowsDesktop | — | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RTX 30 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RTX 40 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RTX 50 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RX 6000 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RX 7000 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| GraphicsCard | TUF RX 9000 series | WindowsDesktop | 是 | 是 | OfficialSeriesRule |  | GPU family rule; exact board SKU and firmware are not supplied. |
| Handheld | ROG Ally (2023) | WindowsHandheld | 是 | — | OfficialSEModel | RC71L | Armoury Crate SE; desktop page structure is not the SE layout. |
| Handheld | ROG Ally X (2024) | WindowsHandheld | 是 | — | OfficialSEModel | RC72LA | Armoury Crate SE; desktop page structure is not the SE layout. |
| Handheld | ROG Xbox Ally (2025) | WindowsHandheld | 是 | — | OfficialSEModel | RC73YA | Armoury Crate SE; desktop page structure is not the SE layout. |
| Handheld | ROG Xbox Ally X (2025) | WindowsHandheld | 是 | — | OfficialSEModel | RC73XA | Armoury Crate SE; desktop page structure is not the SE layout. |
| Headset | ASUS TX Gaming TWS | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Cetra | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Cetra II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Cetra RGB | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Cetra True Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Cetra True Wireless Speednova | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Delta | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Delta GUNDAM LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Delta II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG DELTA II-KJP | WindowsPeripheral | — | — | LocalInstalledIdentity | a703 / 1D41,1D42,1D43 | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| Headset | ROG Delta Origin | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Delta S | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Delta S Animate | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Delta S EVA Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Delta S Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Pelta | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Strix Fusion 300 (& PNK LTD) | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Strix Fusion 500 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Strix Fusion 700 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Strix Fusion II 300 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Strix Fusion II 500 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | ROG Strix Fusion Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Strix Go | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Strix Go 2.4 | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Strix Go 2.4 Electro Punk | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Strix Go BT | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | ROG Theta 7.1 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Headset | TUF Gaming H1 Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | TUF Gaming H3 Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | TUF Gaming H5 | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | TUF Gaming H7 | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Headset | TUF Gaming H7 Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| HeadsetStand | ROG Throne | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| HeadsetStand | ROG Throne Qi | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| HeadsetStand | ROG Throne Qi GUNDAM LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ASUS TX 98 Gaming Keyboard | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ASUS TX 98 Gaming Keyboard HATSUNE MIKU EDITION | WindowsPeripheral | — | 是 | OfficialModel |  |  |
| Keyboard | ASUS TX 98 HATSUNE MIKU EDITION | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Keyboard | ASUS TX Gaming Keyboard | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Azoth | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Azoth Extreme | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG AZOTH EXTREME EDITION 20 | WindowsPeripheral | — | — | LocalInstalledIdentity | m709 / 1DA2,1DA4,1DA5 | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| Keyboard | ROG Azoth X | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Claymore | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Claymore II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Falchion | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Falchion Ace | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Falchion Ace HFX | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Falchion NX | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Falchion RX Low Profile | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Flare | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Flare COD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Flare II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Flare II Animate | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Flare PNK LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope II 96 RX Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope II 96 Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope II RX | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope NX | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope NX TKL | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope NX TKL Moonlight White | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope NX Wireless (Deluxe) | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope PBT | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Keyboard | ROG Strix Scope RX | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope RX EVA Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope RX EVA-02 Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope RX TKL Wireless (Deluxe) | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope TKL | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope TKL Electro Punk | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | ROG Strix Scope TKL GUNDAM LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K1 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K3 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K3 Gen II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K3 Gen II HATSUNE MIKU EDITION | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K5 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Keyboard | TUF Gaming K7 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Laptop | ASUS TUF Gaming A14 (2024/2025) (RGB device only) | WindowsLaptop | — | 是 | OfficialSeriesRule |  | RGB-equipped variants only. |
| Laptop | ASUS TUF Gaming A15 | WindowsLaptop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ASUS TUF Gaming A15 (2024/2025) (RGB device only) | WindowsLaptop | — | 是 | OfficialSeriesRule |  | RGB-equipped variants only. |
| Laptop | ASUS TUF Gaming A16 (2024/2025) (RGB device only) | WindowsLaptop | — | 是 | OfficialSeriesRule |  | RGB-equipped variants only. |
| Laptop | ASUS TUF Gaming A17 | WindowsLaptop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ASUS TUF Gaming A18 (2024/2025) (RGB device only) | WindowsLaptop | — | 是 | OfficialSeriesRule |  | RGB-equipped variants only. |
| Laptop | ASUS TUF Gaming F15 | WindowsLaptop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ASUS TUF Gaming F16 (2024/2025) (RGB device only) | WindowsLaptop | — | 是 | OfficialSeriesRule |  | RGB-equipped variants only. |
| Laptop | ASUS TUF Gaming F17 | WindowsLaptop | 是 | — | HistoricalPdfModel |  | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FA506II/IU/IV/IH | WindowsLaptop | 是 | — | HistoricalPdfModel | FA506II | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FA706II/IU/IH | WindowsLaptop | 是 | — | HistoricalPdfModel | FA706II | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX505DH | WindowsLaptop | 是 | — | HistoricalPdfModel | FX505DH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX505DU/DT/DD/DV | WindowsLaptop | 是 | — | HistoricalPdfModel | FX505DU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX505DY | WindowsLaptop | 是 | — | HistoricalPdfModel | FX505DY | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX505GT/GU | WindowsLaptop | 是 | — | HistoricalPdfModel | FX505GT | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX506LH/LI/LU | WindowsLaptop | 是 | — | HistoricalPdfModel | FX506LH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX705DU/DT/DD/DV | WindowsLaptop | 是 | — | HistoricalPdfModel | FX705DU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX705DY | WindowsLaptop | 是 | — | HistoricalPdfModel | FX705DY | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | FX706LI/LH | WindowsLaptop | 是 | — | HistoricalPdfModel | FX706LI | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G511GW/V/U/T | WindowsLaptop | 是 | — | HistoricalPdfModel | G511GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G512LH/LI/LU/LW/LWS/LV | WindowsLaptop | 是 | — | HistoricalPdfModel | G512LH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G531GW/V/U/T/D | WindowsLaptop | 是 | — | HistoricalPdfModel | G531GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G532LU/LW/LV/LWS | WindowsLaptop | 是 | — | HistoricalPdfModel | G532LU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G703GX | WindowsLaptop | 是 | — | HistoricalPdfModel | G703GX | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G703GXR | WindowsLaptop | 是 | — | HistoricalPdfModel | G703GXR | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G711GX/W/V/U/T | WindowsLaptop | 是 | — | HistoricalPdfModel | G711GX | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G712LI/LU/LW/LWS/LV | WindowsLaptop | 是 | — | HistoricalPdfModel | G712LI | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G731GW/V/U/T | WindowsLaptop | 是 | — | HistoricalPdfModel | G731GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | G732LU/LW/LWS/LXS/LV | WindowsLaptop | 是 | — | HistoricalPdfModel | G732LU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GA401IU/IV/II/IH | WindowsLaptop | 是 | — | HistoricalPdfModel | GA401IU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GA401IVC | WindowsLaptop | 是 | — | HistoricalPdfModel | GA401IVC | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GA502DU (GU502DU) | WindowsLaptop | 是 | — | HistoricalPdfModel | GA502DU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GA502IU/II/IV | WindowsLaptop | 是 | — | HistoricalPdfModel | GA502IU | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GL504GW/GV | WindowsLaptop | 是 | — | HistoricalPdfModel | GL504GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GL704GM | WindowsLaptop | 是 | — | HistoricalPdfModel | GL704GM | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GL704GW/GV | WindowsLaptop | 是 | — | HistoricalPdfModel | GL704GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GU502GW/GV/GU | WindowsLaptop | 是 | — | HistoricalPdfModel | GU502GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GU502LWS/LU/LW/LV | WindowsLaptop | 是 | — | HistoricalPdfModel | GU502LWS | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX502GW/GV | WindowsLaptop | 是 | — | HistoricalPdfModel | GX502GW | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX502GWH | WindowsLaptop | 是 | — | HistoricalPdfModel | GX502GWH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX502LXS/LWS | WindowsLaptop | 是 | — | HistoricalPdfModel | GX502LXS | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX531GM | WindowsLaptop | 是 | — | HistoricalPdfModel | GX531GM | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX531GS | WindowsLaptop | 是 | — | HistoricalPdfModel | GX531GS | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX531GX/GW/GV | WindowsLaptop | 是 | — | HistoricalPdfModel | GX531GX | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX531GXH/GWH | WindowsLaptop | 是 | — | HistoricalPdfModel | GX531GXH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX550LWS/LXS | WindowsLaptop | 是 | — | HistoricalPdfModel | GX550LWS | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX701GX/GW/GV | WindowsLaptop | 是 | — | HistoricalPdfModel | GX701GX | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX701GXH | WindowsLaptop | 是 | — | HistoricalPdfModel | GX701GXH | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX701GXR/GWR/GVR | WindowsLaptop | 是 | — | HistoricalPdfModel | GX701GXR | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | GX701LXS/LWS/LV | WindowsLaptop | 是 | — | HistoricalPdfModel | GX701LXS | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Ally (2024) | WindowsLaptop | — | 是 | OfficialModel |  |  |
| Laptop | ROG Flow X13 GV301 | WindowsLaptop | 是 | — | HistoricalPdfModel | GV301 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Flow X16 GV601 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GV601 | RGB-equipped variants only. |
| Laptop | ROG Flow Z13 GZ301 | WindowsLaptop | — | 是 | OfficialModel | GZ301 |  |
| Laptop | ROG G703 | WindowsLaptop | — | 是 | OfficialModel | G703 |  |
| Laptop | ROG Mothership GZ700 | WindowsLaptop | — | 是 | OfficialModel | GZ700 |  |
| Laptop | ROG Strix G15 G512 | WindowsLaptop | — | 是 | OfficialModel | G512 |  |
| Laptop | ROG Strix G15 G513 | WindowsLaptop | 是 | — | HistoricalPdfModel | G513 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Strix G15 G513 | WindowsLaptop | — | 是 | OfficialModel | G513 |  |
| Laptop | ROG Strix G16 (2023) G614 | WindowsLaptop | — | 是 | OfficialModel | G614 |  |
| Laptop | ROG Strix G16 (2025) G615 | WindowsLaptop | — | 是 | OfficialModel | G615 |  |
| Laptop | ROG Strix G17 G712 | WindowsLaptop | — | 是 | OfficialModel | G712 |  |
| Laptop | ROG Strix G17 G713 | WindowsLaptop | 是 | — | HistoricalPdfModel | G713 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Strix G17 G713 | WindowsLaptop | — | 是 | OfficialModel | G713 |  |
| Laptop | ROG Strix G18 (2023) G814 | WindowsLaptop | — | 是 | OfficialModel | G814 |  |
| Laptop | ROG Strix G18 (2024) G814 | WindowsLaptop | — | 是 | OfficialModel | G814 |  |
| Laptop | ROG Strix G18 (2025) G815 | WindowsLaptop | — | 是 | OfficialModel | G815 |  |
| Laptop | ROG STRIX HERO III G531 | WindowsLaptop | — | 是 | OfficialModel | G531 |  |
| Laptop | ROG STRIX HERO III G731 | WindowsLaptop | — | 是 | OfficialModel | G731 |  |
| Laptop | ROG Strix SCAR 15 G532 | WindowsLaptop | — | 是 | OfficialModel | G532 |  |
| Laptop | ROG Strix Scar 15 G533 | WindowsLaptop | 是 | — | HistoricalPdfModel | G533 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Strix Scar 15 G533 | WindowsLaptop | — | 是 | OfficialModel | G533 |  |
| Laptop | ROG Strix Scar 16 (2023) G634 | WindowsLaptop | — | 是 | OfficialModel | G634 |  |
| Laptop | ROG Strix Scar 16 (2024) G614 | WindowsLaptop | — | 是 | OfficialModel | G614 |  |
| Laptop | ROG Strix Scar 16 (2024) G634 | WindowsLaptop | — | 是 | OfficialModel | G634 |  |
| Laptop | ROG Strix SCAR 16 (2025) G635 | WindowsLaptop | — | 是 | OfficialModel | G635 |  |
| Laptop | ROG Strix SCAR 17 (2023) G733 | WindowsLaptop | — | 是 | OfficialModel | G733 |  |
| Laptop | ROG Strix SCAR 17 G732 | WindowsLaptop | — | 是 | OfficialModel | G732 |  |
| Laptop | ROG Strix Scar 17 G733 | WindowsLaptop | 是 | — | HistoricalPdfModel | G733 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Strix Scar 17 G733 | WindowsLaptop | — | 是 | OfficialModel | G733 |  |
| Laptop | ROG Strix Scar 18 (2023) G834 | WindowsLaptop | — | 是 | OfficialModel | G834 |  |
| Laptop | ROG Strix Scar 18 (2024) G834 | WindowsLaptop | — | 是 | OfficialModel | G834 |  |
| Laptop | ROG Strix SCAR 18 (2025) G835 | WindowsLaptop | — | 是 | OfficialModel | G835 |  |
| Laptop | ROG Strix SCAR II (except GL504GM/GL504GS) | WindowsLaptop | — | 是 | OfficialSeriesRule | GL504GM | The FAQ expressly excludes GL504GM and GL504GS from this Aura rule. |
| Laptop | ROG STRIX SCAR III G531 | WindowsLaptop | — | 是 | OfficialModel | G531 |  |
| Laptop | ROG STRIX SCAR III G731 | WindowsLaptop | — | 是 | OfficialModel | G731 |  |
| Laptop | ROG Zephyrus Duo 15 GX550 | WindowsLaptop | — | 是 | OfficialModel | GX550 |  |
| Laptop | ROG Zephyrus Duo 15 SE GX551 | WindowsLaptop | 是 | — | HistoricalPdfModel | GX551 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Zephyrus Duo 15 SE GX551 | WindowsLaptop | — | 是 | OfficialModel | GX551 |  |
| Laptop | ROG Zephyrus duo 16 (2023) GX650 | WindowsLaptop | — | 是 | OfficialModel | GX650 |  |
| Laptop | ROG Zephyrus Duo 16 GX650 | WindowsLaptop | — | 是 | OfficialModel | GX650 |  |
| Laptop | ROG Zephyrus G14 GA401 | WindowsLaptop | 是 | — | HistoricalPdfModel | GA401 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Zephyrus G14 GA402 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GA402 | RGB-equipped variants only. |
| Laptop | ROG Zephyrus G14 GA403 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GA403 | RGB-equipped variants only. |
| Laptop | ROG Zephyrus G15 GA503 | WindowsLaptop | 是 | — | HistoricalPdfModel | GA503 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Zephyrus G15 GA503 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GA503 | RGB-equipped variants only. |
| Laptop | ROG Zephyrus G16 GA605 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GA605 | RGB-equipped variants only. |
| Laptop | ROG Zephyrus G16 GU605 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GU605 | RGB-equipped variants only. |
| Laptop | ROG ZEPHYRUS M GU502 | WindowsLaptop | — | 是 | OfficialModel | GU502 |  |
| Laptop | ROG Zephyrus M15 GU502 | WindowsLaptop | — | 是 | OfficialModel | GU502 |  |
| Laptop | ROG Zephyrus M16 GU603 | WindowsLaptop | 是 | — | HistoricalPdfModel | GU603 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Zephyrus M16 GU603 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GU603 | RGB-equipped variants only. |
| Laptop | ROG Zephyrus M16 GU604 (RGB device only) | WindowsLaptop | — | 是 | OfficialModel | GU604 | RGB-equipped variants only. |
| Laptop | ROG ZEPHYRUS S GX502 | WindowsLaptop | — | 是 | OfficialModel | GX502 |  |
| Laptop | ROG ZEPHYRUS S GX531 | WindowsLaptop | — | 是 | OfficialModel | GX531 |  |
| Laptop | ROG ZEPHYRUS S GX701 | WindowsLaptop | — | 是 | OfficialModel | GX701 |  |
| Laptop | ROG Zephyrus S15 GX502 | WindowsLaptop | — | 是 | OfficialModel | GX502 |  |
| Laptop | ROG Zephyrus S17 GX701 | WindowsLaptop | — | 是 | OfficialModel | GX701 |  |
| Laptop | ROG Zephyrus S17 GX703 | WindowsLaptop | 是 | — | HistoricalPdfModel | GX703 | Historical PDF example or suffix group; current functionality still depends on model capability. |
| Laptop | ROG Zephyrus S17 GX703 | WindowsLaptop | — | 是 | OfficialModel | GX703 |  |
| LaptopDesktop | ROG and TUF laptops and desktops shipped since July 2018 | WindowsPlatform | 是 | — | OfficialSeriesRule |  | Official launch-date boundary: July 2018; linked PDF is historical and is not an exhaustive current SKU registry. |
| LedStrip | XiaoMi Addressable LED Strip | WindowsDesktop | — | 是 | OfficialModel |  |  |
| Microphone | ROG Carnyx | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Monitor | PG248QP | WindowsDesktop | 是 | 是 | OfficialModel | PG248QP |  |
| Monitor | PG259QN | WindowsDesktop | 是 | 是 | OfficialModel | PG259QN |  |
| Monitor | PG259QNR | WindowsDesktop | 是 | 是 | OfficialModel | PG259QNR |  |
| Monitor | PG279QM | WindowsDesktop | 是 | 是 | OfficialModel | PG279QM |  |
| Monitor | PG279QR | WindowsDesktop | 是 | 是 | OfficialModel | PG279QR |  |
| Monitor | PG27AQDP | WindowsDesktop | 是 | 是 | OfficialModel | PG27AQDP |  |
| Monitor | PG27AQN | WindowsDesktop | 是 | 是 | OfficialModel | PG27AQN |  |
| Monitor | PG27UCDM | WindowsDesktop | 是 | 是 | OfficialModel | PG27UCDM |  |
| Monitor | PG27UQ | WindowsDesktop | 是 | 是 | OfficialModel | PG27UQ |  |
| Monitor | PG27UQR | WindowsDesktop | 是 | 是 | OfficialModel | PG27UQR |  |
| Monitor | PG329Q | WindowsDesktop | 是 | 是 | OfficialModel | PG329Q |  |
| Monitor | PG32UCDM | WindowsDesktop | 是 | 是 | OfficialModel | PG32UCDM |  |
| Monitor | PG32UCDP | WindowsDesktop | 是 | 是 | OfficialModel | PG32UCDP |  |
| Monitor | PG32UQ | WindowsDesktop | 是 | 是 | OfficialModel | PG32UQ |  |
| Monitor | PG32UQX | WindowsDesktop | 是 | 是 | OfficialModel | PG32UQX |  |
| Monitor | PG32UQXE | WindowsDesktop | 是 | 是 | OfficialModel | PG32UQXE |  |
| Monitor | PG32UQXR | WindowsDesktop | 是 | 是 | OfficialModel | PG32UQXR |  |
| Monitor | PG349Q | WindowsDesktop | 是 | 是 | OfficialModel | PG349Q |  |
| Monitor | PG34WCDM | WindowsDesktop | 是 | 是 | OfficialModel | PG34WCDM |  |
| Monitor | PG35VQ | WindowsDesktop | 是 | 是 | OfficialModel | PG35VQ |  |
| Monitor | PG39WCDM | WindowsDesktop | 是 | 是 | OfficialModel | PG39WCDM |  |
| Monitor | PG43U | WindowsDesktop | 是 | 是 | OfficialModel | PG43U |  |
| Monitor | PG43UQR | WindowsDesktop | 是 | 是 | OfficialModel | PG43UQR |  |
| Monitor | PG49WCD | WindowsDesktop | 是 | 是 | OfficialModel | PG49WCD |  |
| Monitor | PG65UQ | WindowsDesktop | 是 | 是 | OfficialModel | PG65UQ |  |
| Monitor | XG248Q | WindowsDesktop | 是 | 是 | OfficialModel | XG248Q |  |
| Monitor | XG259QNG | WindowsDesktop | 是 | 是 | OfficialModel | XG259QNG |  |
| Monitor | XG279Q | WindowsDesktop | 是 | 是 | OfficialModel | XG279Q |  |
| Monitor | XG27AC | WindowsDesktop | 是 | 是 | OfficialModel | XG27AC |  |
| Monitor | XG27ACDNG | WindowsDesktop | 是 | 是 | OfficialModel | XG27ACDNG |  |
| Monitor | XG27ACG | WindowsDesktop | 是 | 是 | OfficialModel | XG27ACG |  |
| Monitor | XG27ACMG | WindowsDesktop | 是 | 是 | OfficialModel | XG27ACMG |  |
| Monitor | XG27AQ | WindowsDesktop | 是 | 是 | OfficialModel | XG27AQ |  |
| Monitor | XG27AQDMG | WindowsDesktop | 是 | 是 | OfficialModel | XG27AQDMG |  |
| Monitor | XG27AQM | WindowsDesktop | 是 | 是 | OfficialModel | XG27AQM |  |
| Monitor | XG27UCG | WindowsDesktop | 是 | 是 | OfficialModel | XG27UCG |  |
| Monitor | XG27UQ | WindowsDesktop | 是 | 是 | OfficialModel | XG27UQ |  |
| Monitor | XG27UQR | WindowsDesktop | 是 | 是 | OfficialModel | XG27UQR |  |
| Monitor | XG27WQ | WindowsDesktop | 是 | 是 | OfficialModel | XG27WQ |  |
| Monitor | XG309CM | WindowsDesktop | 是 | 是 | OfficialModel | XG309CM |  |
| Monitor | XG32VC | WindowsDesktop | 是 | 是 | OfficialModel | XG32VC |  |
| Monitor | XG32VQ | WindowsDesktop | 是 | 是 | OfficialModel | XG32VQ |  |
| Monitor | XG32VQR | WindowsDesktop | 是 | 是 | OfficialModel | XG32VQR |  |
| Monitor | XG349C | WindowsDesktop | 是 | 是 | OfficialModel | XG349C |  |
| Monitor | XG35VQ | WindowsDesktop | 是 | 是 | OfficialModel | XG35VQ |  |
| Monitor | XG438Q | WindowsDesktop | 是 | 是 | OfficialModel | XG438Q |  |
| Monitor | XG438QR | WindowsDesktop | 是 | 是 | OfficialModel | XG438QR |  |
| Monitor | XG43UQ | WindowsDesktop | 是 | 是 | OfficialModel | XG43UQ |  |
| Motherboard | ROG CROSSHAIR X870E EXTREME | WindowsDesktop | — | — | LocalInstalledIdentity |  / 1C83 | LCD PID 1C83 belongs to the display function; it does not identify an entire motherboard or authorize mainboard controls. |
| Motherboard | Other ASUS X570 and later motherboards | WindowsPlatform | 是 | — | OfficialSeriesRule |  | Official launch-date boundary: July 2019; rule does not identify every BIOS or USB controller. |
| Motherboard | ROG and ROG Strix Z370 and later motherboards | WindowsPlatform | 是 | — | OfficialSeriesRule |  | Official launch-date boundary: October 2018; rule does not identify every BIOS or USB controller. |
| Mouse | ASUS Mouse MD200 | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ASUS TX Gaming Mouse | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ASUS TX Gaming Mouse Mini | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ASUS TX Gaming Mouse Mini HATSUNE MIKU EDITION | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ProArt Mouse MD300 | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ROG Chakram | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Chakram Core | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Chakram X | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ROG Gladius II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius II Core | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius II Origin | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius II Origin COD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius II Origin PNK LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius II Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius III | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius III Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius III Wireless AimPoint | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Gladius III Wireless AimPoint EVA-02 Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Harpe Ace Aim Lab Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Harpe Ace Extreme | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Harpe Ace Mini | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG HARPE II EXTREME EDITION 20 | WindowsPeripheral | — | — | LocalInstalledIdentity | P725 / 1D7A,1D7C,1D7D | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| Mouse | ROG Keris | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Keris II Ace | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Keris II Origin | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Keris Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Keris Wireless AimPoint | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Keris Wireless EVA Edition | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Pugio | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Pugio II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Sica | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ROG Spatha | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Spatha X | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Carry | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | ROG Strix Evolve | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact II Electro Punk | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact II GUNDAM LTD | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact II Moonlight White | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact II Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact III | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | ROG Strix Impact III Wireless | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | TUF Gaming M3 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | TUF Gaming M3 Gen II | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | TUF Gaming M4 Air | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | TUF Gaming M4 Wireless | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| Mouse | TUF Gaming M5 | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | TUF Gaming Mini Wireless Mouse HATSUNE MIKU EDITION | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| Mouse | TUF Gaming Mouse X KIMETSU NO YAIBA | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| MousePad | ROG Balteus | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| MousePad | ROG Balteus Qi | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| PartnerCategory | Cases | WindowsDesktop | — | 是 | OfficialPartnerCategory |  | Official category only; the FAQ does not enumerate partner SKUs. |
| PartnerCategory | Cooling | WindowsDesktop | — | 是 | OfficialPartnerCategory |  | Official category only; the FAQ does not enumerate partner SKUs. |
| PartnerCategory | LED Strips | WindowsDesktop | — | 是 | OfficialPartnerCategory |  | Official category only; the FAQ does not enumerate partner SKUs. |
| PartnerCategory | Power Supplies | WindowsDesktop | — | 是 | OfficialPartnerCategory |  | Official category only; the FAQ does not enumerate partner SKUs. |
| PartnerCategory | RGB Accessories | WindowsDesktop | — | 是 | OfficialPartnerCategory |  | Official category only; the FAQ does not enumerate partner SKUs. |
| Phone | ROG phone 6 | AndroidPhone | — | 是 | OfficialModel |  |  |
| Phone | ROG phone 6 PRO | AndroidPhone | — | 是 | OfficialModel |  |  |
| Phone | ROG phone 6D | AndroidPhone | — | 是 | OfficialModel |  |  |
| Receiver | ROG OMNI RECEIVER | WindowsPeripheral | — | — | LocalInstalledIdentity | omni / 1ACE | Installed ASUS capability identity only; device PID is transport metadata, not permission to send commands. |
| Storage | ASUS FX | WindowsPeripheral | — | 是 | OfficialModel |  |  |
| Storage | ROG STRIX ARION series | WindowsPeripheral | — | 是 | OfficialSeriesRule |  |  |
| StreamingKit | ROG Carnyx | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| StreamingKit | ROG Clavis | WindowsPeripheral | 是 | 是 | OfficialModel |  |  |
| StreamingKit | ROG Eye S | WindowsPeripheral | 是 | — | OfficialModel |  |  |
| StreamingKit | ROG Strix Magnus | WindowsPeripheral | — | 是 | OfficialModel |  |  |
