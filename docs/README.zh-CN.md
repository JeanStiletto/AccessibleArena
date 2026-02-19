# Accessible Arena

Magic: The Gathering Arena 无障碍模组，让视障玩家能够使用屏幕阅读器进行游戏。支持完整的键盘导航、所有游戏状态的屏幕阅读器播报，以及12种语言的本地化。

**状态：** 公开测试版。核心游戏功能可用。仍有一些边缘情况和小错误。请参阅下方的已知问题。

**注意：** 目前仅支持键盘操作。不支持鼠标或触摸。仅在 Windows 11 配合 NVDA 环境下测试过。其他 Windows 版本和屏幕阅读器（JAWS、讲述人等）可能可用但未经测试。

## 功能

- 所有界面的完整键盘导航（主页、商店、精通、套牌构筑器、对战）
- 通过 Tolk 库集成屏幕阅读器
- 使用方向键读取卡牌信息（名称、法术力费用、类型、力量/防御、规则文本、背景描述、稀有度、画师）
- 完整的对战支持：区域导航、战斗、目标选择、堆叠、浏览器（占卜、刺探、调度）
- 附着和战斗关系播报（被结附、阻挡、被指定为目标）
- 带有购买选项和支付对话框支持的无障碍商店
- 支持机器人对战用于练习
- 随处可用的设置菜单（F2）和帮助菜单（F1）
- 12种语言：英语、德语、法语、西班牙语、意大利语、葡萄牙语（巴西）、日语、韩语、俄语、波兰语、简体中文、繁体中文

## 系统要求

- Windows 10 或更高版本
- Magic: The Gathering Arena（通过官方安装程序或 Epic Games Store 安装）
- 屏幕阅读器（推荐 NVDA：https://www.nvaccess.org/download/）
- MelonLoader（安装程序会自动处理）

## 安装

### 使用安装程序（推荐）

1. 从 GitHub 最新发布版下载 `AccessibleArenaInstaller.exe`：https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. 如果 MTG Arena 正在运行，请先关闭
3. 运行安装程序。它会检测您的 MTGA 安装，必要时安装 MelonLoader，并部署模组
4. 启动 MTG Arena。您应该能通过屏幕阅读器听到"Accessible Arena v... 已启动"

### 手动安装

1. 在您的 MTGA 文件夹中安装 MelonLoader（https://github.com/LavaGang/MelonLoader）
2. 从最新发布版下载 `AccessibleArena.dll`
3. 将 DLL 复制到：`C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. 确保 `Tolk.dll` 和 `nvdaControllerClient64.dll` 在 MTGA 根文件夹中
5. 启动 MTG Arena

## 快速开始

如果您还没有 Wizards 账户，可以在 https://myaccounts.wizards.com/ 创建一个，而无需使用游戏内的注册界面。

安装后，启动 MTG Arena。模组会通过屏幕阅读器播报当前界面。

- 随时按 **F1** 打开可导航的帮助菜单，列出所有键盘快捷键
- 按 **F2** 打开设置菜单（语言、详细程度、教程消息）
- 按 **F3** 听取当前界面名称
- 使用**上/下方向键**或 **Tab/Shift+Tab** 导航菜单
- 按 **Enter** 或**空格**激活元素
- 按 **Backspace** 返回

## 键盘快捷键

### 菜单

- 上/下方向键（或 W/S）：导航项目
- Tab/Shift+Tab：导航项目（与上/下方向键相同）
- 左/右方向键（或 A/D）：轮播和步进控件
- Home/End：跳到第一个/最后一个项目
- Page Up/Page Down：收藏中的上一页/下一页
- Enter/空格：激活
- Backspace：返回

### 对战 - 区域

- C：你的手牌
- G / Shift+G：你的坟墓场 / 对手的坟墓场
- X / Shift+X：你的放逐区 / 对手的放逐区
- S：堆叠
- B / Shift+B：你的生物 / 对手的生物
- A / Shift+A：你的地 / 对手的地
- R / Shift+R：你的非生物 / 对手的非生物

### 对战 - 区域内

- 左/右：导航卡牌
- Home/End：跳到第一张/最后一张卡牌
- 上/下方向键：聚焦卡牌时读取卡牌详情
- I：扩展卡牌信息（关键词描述、其他面）
- Shift+上/下：切换战场行

### 对战 - 信息

- T：当前回合和阶段
- L：生命值总计
- V：玩家信息区域（左/右切换玩家，上/下查看属性）
- D / Shift+D：你的牌库数量 / 对手的牌库数量
- Shift+C：对手手牌数量

### 对战 - 行动

- 空格：确认（让过优先权、确认攻击者/阻挡者、下一阶段）
- Backspace：取消 / 拒绝
- Tab：循环目标或高亮元素
- Ctrl+Tab：仅循环对手目标
- Enter：选择目标

### 对战 - 浏览器（占卜、刺探、调度）

- Tab：导航所有卡牌
- C/D：跳到顶部/底部区域
- 左/右：在区域内导航
- Enter：切换卡牌放置
- 空格：确认选择
- Backspace：取消

### 全局

- F1：帮助菜单
- F2：设置菜单
- F3：播报当前界面
- Ctrl+R：重复上一条播报
- Backspace：通用返回/关闭/取消

## 报告错误

如果您发现了错误，请在 GitHub 上提交 issue：https://github.com/JeanStiletto/AccessibleArena/issues

请包含以下信息：

- 错误发生时您在做什么
- 您期望发生什么
- 实际发生了什么
- 您的屏幕阅读器及版本
- 附上 MelonLoader 日志文件：`C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## 已知问题

- 让过优先权的空格键并不总是可靠（模组会直接点击按钮作为后备方案）
- 套牌构筑器中的套牌列表卡牌仅显示名称和数量，不显示完整卡牌详情
- PlayBlade 队列类型选择（排名、开放对战、Brawl）可能并不总是设置正确的游戏模式

完整列表请参阅 docs/KNOWN_ISSUES.md。

## 故障排除

**启动游戏后没有语音输出**
- 确保在启动 MTG Arena 之前屏幕阅读器已经在运行
- 检查 `Tolk.dll` 和 `nvdaControllerClient64.dll` 是否在 MTGA 根文件夹中（安装程序会自动放置）
- 检查 `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` 中的 MelonLoader 日志是否有错误

**启动时游戏崩溃或模组未加载**
- 确保 MelonLoader 已安装。
- 如果游戏最近更新过，可能需要重新安装 MelonLoader 或模组。再次运行安装程序。
- 检查 `AccessibleArena.dll` 是否在 `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**模组之前正常工作但在游戏更新后停止了**
- MTG Arena 更新可能会覆盖 MelonLoader 文件。再次运行安装程序以重新安装 MelonLoader 和模组。
- 如果游戏大幅更改了内部结构，模组可能需要更新。请在 GitHub 上检查新版本。

**键盘快捷键不起作用**
- 确保游戏窗口处于焦点状态（点击它或使用 Alt+Tab 切换到它）
- 按 F1 检查模组是否活跃。如果您听到帮助菜单，说明模组正在运行。
- 某些快捷键仅在特定上下文中有效（对战快捷键仅在对战期间有效）

**语言错误**
- 按 F2 打开设置菜单，然后使用 Enter 切换语言

## 从源代码构建

要求：.NET SDK（任何支持 net472 目标的版本）

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

构建的 DLL 位于 `src/bin/Debug/net472/AccessibleArena.dll`。

游戏程序集引用应在 `libs/` 文件夹中。从您的 MTGA 安装中复制这些 DLL（`MTGA_Data/Managed/`）：
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader DLL（`MelonLoader.dll`、`0Harmony.dll`）来自您的 MelonLoader 安装。

## 许可证

本项目采用 GNU General Public License v3.0 许可证。详情请参阅 LICENSE 文件。

## 链接

- GitHub：https://github.com/JeanStiletto/AccessibleArena
- NVDA 屏幕阅读器（推荐）：https://www.nvaccess.org/download/
- MelonLoader：https://github.com/LavaGang/MelonLoader
- MTG Arena：https://magic.wizards.com/mtgarena
