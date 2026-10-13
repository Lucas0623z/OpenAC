# 宏功能与整合接口

`MacroFeatures.cs` 保留原版“宏”页层级：上方录制/插入与宏文件/撤销/重做/清除，左侧时间和动作列表，右侧参数，右下保存/取消。

- 编辑、插入、删除、排序、撤销/重做；键盘按下/释放，五个鼠标按钮，垂直滚轮，屏幕绝对位置，延时。
- 最多 100 条动作；单次延时最多 60 秒，总延时最多 10 分钟。
- 本地文件采用本版 `.rogmacro.json` 格式，不声称兼容原版 Armoury Crate 宏格式。保存使用临时文件原子替换；导入检查格式、数量与数值，不执行导入内容。
- 只有用户手动按录制按钮后才安装键鼠钩子，F9 或停止按钮结束；达到容量上限自动停止。可选择记录鼠标移动，默认只记键鼠按钮与滚轮。本程序窗口里的输入不录制，注入输入也不录制。
- 只有用户手动按“软件执行”后才启动 3 秒倒计时与临时 F9 停止钩子。执行前要求松开实体按键。取消、错误和正常结束均在 finally 尝试释放本次宏自己按下的键；释放失败明确报错，孤立的释放动作不影响用户按着的键。
- 硬件板载宏写入尚未接通；没有自动播放、开机输入监听或后台常驻录制。

## 主窗口整合

- 宏路由调用 `OriginalMacroPage()`。
- 每次页面切换前调用 `OnMacroNavigation(nextPage)`；离开宏页停止录制并请求终止播放。
- 关闭时如果 `MacroFeaturesActive` 为 true，先设置 Closing 事件 `e.Cancel = true`，再等待 `StopMacroFeaturesAsync()`，随后按原关闭流程最终 `Close()`。等待可保证窗口没有在 finally 释放按键之前退出。
- 将 `MacroFeatures.cs` 和 `MacroEngine.cs` 加入正式源列表。`MacroTests.cs` 与 `MacroRender.cs` 是独立验证入口，不能一起编译进应用。

## 验证边界

127 个合成断言已通过，覆盖编辑历史、数量限制、文件往返/原子替换、非法输入、捕获缓冲、取消和失败后的释放、孤立释放及 Windows INPUT 布局。使用假输入发送器，未启动真实录制/播放。空页及示例页已离屏渲染检查。真实钩子安装、F9 停止、实际软件输入仍需用户手动验证。

Windows 官方 API：

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-input
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
- https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc
- https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc
