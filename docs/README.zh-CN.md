<h1>Accessible Arena</h1>

<h2>这个 MOD 是什么</h2>

这个 MOD 可让你游玩 Arena——集换式卡牌游戏《万智牌》（Magic: The Gathering）最受欢迎、对新手最友好的数字版本。它为游戏几乎每个方面增加了完整的屏幕阅读器支持与键盘导航。

该 MOD 支持游戏已翻译的所有语言。此外，还部分覆盖了一些游戏本身不支持的语言：在这些语言中，MOD 特有的播报（例如帮助文本和 UI 提示）会被翻译，而卡牌和游戏数据仍使用游戏的默认语言。

<h2>《万智牌》是什么</h2>

《万智牌》是威世智（Wizards of the Coast）注册商标的一款集换式卡牌游戏，你可以扮演法师与其他法师对战，通过卡牌施放法术。《万智牌》有 5 种颜色，代表不同的游戏玩法与风味身份。如果你熟悉炉石传说或游戏王，你会认出许多概念，因为《万智牌》是这些游戏的鼻祖。
如果你想更多了解《万智牌》，官方网站以及许多内容创作者都能帮到你。

<h2>要求</h2>

- Windows 10 或更新版本
- Magic: The Gathering Arena（通过 Wizards 官方安装程序或 Steam 安装）
- 一款屏幕阅读器（仅测试了 NVDA 和 JAWS）
- MelonLoader（安装程序会自动处理）

<h2>安装</h2>

<h3>使用安装程序（推荐）</h3>

1. 从 GitHub 最新发布版下载 [AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe)
2. 如果 MTG Arena 正在运行，请关闭它
3. 运行安装程序。它会检测你的 MTGA 安装，必要时安装 MelonLoader，并部署 MOD
4. 启动 MTG Arena。你应当通过屏幕阅读器听到"Accessible Arena v... launched"

<h3>手动安装</h3>

1. 将 [MelonLoader](https://github.com/LavaGang/MelonLoader) 安装到 MTGA 文件夹
2. 从最新发布版下载 `AccessibleArena.dll`
3. 将 DLL 复制到 MTGA 的 Mods 文件夹：
   - WotC 安装：`C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Steam 安装：`C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. 确保 `Tolk.dll` 和 `nvdaControllerClient64.dll` 位于 MTGA 根文件夹
5. 启动 MTG Arena

<h2>卸载</h2>

再次运行安装程序。如果 MOD 已安装，它会提供卸载选项。你也可以选择一并移除 MelonLoader。如需手动卸载，请从 `Mods\` 文件夹删除 `AccessibleArena.dll`，并从 MTGA 根文件夹移除 `Tolk.dll` 和 `nvdaControllerClient64.dll`。

<h2>如果你是从炉石传说过来的</h2>

如果你玩过 Hearthstone Access，你会有充分的理由认出许多东西，因为不仅游戏原理相近，我也遵循了许多设计原则。但仍然有一些东西不一样。

首先，你要导航的区域更多，因为《万智牌》有坟场、放逐区和一些额外区域。你的战场大小没有限制，并且有额外的排序行，以便更好地管理可能出现的众多元素。

你的法术力不会自动增长，而是来自不同颜色的地牌，你必须主动将它们打出。据此，法术力费用包含无色部分和有色部分，二者相加构成必须满足的卡牌完整费用需求。

你不能直接攻击生物，只有对手和一些非常特定的卡（鹏洛客和战役）才能被攻击者作为攻击目标。作为防御者，你必须决定是否要阻挡一次攻击以让生物互相战斗。如果不阻挡，伤害会击中你的玩家头像，但你的生物可以保持完好。进一步地，伤害不会在生物上累积，而是在每个回合结束时被治疗，也就是在你和对手的回合结束时都会被治疗。要与不愿意与你战斗的对手生物互动，你必须打出特定的牌，或者强力施压对手的生命总量，使其别无选择只能牺牲宝贵的生物以求生。

游戏有非常清晰区分的战斗阶段，允许进行特定的动作，比如抓牌、施放法术或战斗。据此，《万智牌》允许并鼓励你在对手回合做事。别再傻坐等着事情发生，打造一副互动性的牌组，当场粉碎敌人的计划。

<h2>起步</h2>

游戏首先会要求你提供一些个人数据并注册一个角色。通过游戏内机制应当能完成，但如果不能，你也可以改用游戏的网站来完成，它完全可访问。

游戏以教程开始，你将在其中学习《万智牌》的基础知识。MOD 在标准教程之外为屏幕阅读器用户添加了自定义教程提示。完成教程后，你会获得 5 副入门套牌作为奖励，每种颜色一副。

从这里开始，你有几种方式可以解锁更多卡牌并学习游戏：

- **颜色挑战：** 游玩五种《万智牌》颜色各自的颜色挑战。每个挑战让你与 4 个 NPC 对手作战，最后与一位真实玩家对战。
- **入门套牌活动：** 与拥有相同套牌选项的真实玩家一同，使用 10 副双色牌组中的一副进行游玩。
- **Jump In：** 选择两个不同颜色和主题的 20 张卡包，将它们合并为一副牌组，与有类似选择的真实玩家对战。你会在此活动中获得免费代币，并保留所选的卡牌。
- **火花天梯（Spark Ladder）：** 在某个时刻，Spark Ladder 会解锁，你将与真实对手进行你的第一次排位赛。

查看社交菜单下的邮件，里面有很多奖励和卡牌包。

游戏会根据你玩了什么以及玩了多少来逐步解锁各种模式。它会在进度和目标菜单中给你提示和任务，并在游玩菜单中突出显示你相关的模式。一旦你完成了足够多的新手玩家内容，所有不同的模式和活动都会完全开放。

在多元宇宙典籍中，你可以了解各种游戏模式和机制。它会随着 NPE 体验的进展而扩展。

在设置账号下，你可以跳过所有教程体验并强制解锁一切，从一开始就拥有完全的自由。然而，玩新手玩家活动能给你带来大量卡牌，建议新手玩家进行。只有在已经知道自己在做什么时才提前解锁一切。否则初学者内容能提供丰富的乐趣和学习体验，并会很好地引导你。

<h2>键盘快捷键</h2>

导航在各处遵循标准约定：方向键移动，Home/End 跳到第一个/最后一个，Enter 选择，Space 确认，Backspace 返回或取消。Tab/Shift+Tab 同样可用于导航。Page Up/Page Down 翻页。

<h3>全局</h3>

- F1：帮助菜单（列出当前屏幕的所有快捷键）
- Ctrl+F1：播报当前屏幕的快捷键
- F2：MOD 设置
- F3：播报当前屏幕
- F4：好友面板（在菜单中）/ 对战聊天（在对战中）
- F5：检查 / 开始更新
- Ctrl+R：重复最后一次播报

<h3>对战 - 区域</h3>

你的区域：C（手牌）、G（坟场）、X（放逐）、S（堆叠）、W（指挥区）
对手区域：Shift+G、Shift+X、Shift+W
战场：B / Shift+B（生物）、A / Shift+A（地）、R / Shift+R（非生物）
区域内：左右键导航，上下键读取卡牌详情，I 查看扩展信息
Shift+上/下：切换战场行

<h3>对战 - 信息</h3>

- T：回合/阶段
- L：生命总值
- V：玩家信息区域
- D / Shift+D：牌库张数
- Shift+C：对手手牌张数
- M / Shift+M：你 / 对手的地汇总
- K：焦点卡牌的指示物信息
- O：游戏日志（最近的对战播报）
- E / Shift+E：你 / 对手的计时器

<h3>对战 - 选定目标与行动</h3>

- Tab / Ctrl+Tab：循环切换目标（全部 / 仅对手）
- Enter：选择目标
- Space：让渡优先权，确认攻击/阻挡，推进阶段

<h3>对战 - Full control 与阶段停顿</h3>

- P：切换 full control（临时，换阶段时重置）
- Shift+P：切换锁定的 full control（永久）
- Shift+Backspace：切换直到对手动作前结束（软跳过）
- Ctrl+Backspace：切换跳过回合（强制跳过整个回合）
- 1-0：切换阶段停顿（1=维持，2=抓牌，3=第一主要，4=战斗开始，5=宣告攻击者，6=宣告阻挡者，7=战斗伤害，8=战斗结束，9=第二主要，0=结束步骤）

<h3>对战 - 浏览器（占卜、监视、调度）</h3>

- Tab：浏览所有卡牌
- C/D：在上/下区域之间跳转
- Enter：切换卡牌放置

<h2>故障排查</h2>

<h3>启动游戏后没有语音</h3>

- 确保在启动 MTG Arena 之前屏幕阅读器已在运行
- 检查 `Tolk.dll` 和 `nvdaControllerClient64.dll` 是否位于 MTGA 根文件夹（安装程序会自动放置）
- 检查 MTGA 文件夹中 MelonLoader 日志（`MelonLoader\Latest.log`）是否有错误

<h3>游戏启动时崩溃或 MOD 未加载</h3>

- 确保 MelonLoader 已安装。
- 如果游戏最近进行了更新，MelonLoader 或 MOD 可能需要重新安装。再次运行安装程序。
- 检查 `AccessibleArena.dll` 是否位于你 MTGA 安装中的 `Mods\` 文件夹内

<h3>MOD 原本可用，但游戏更新后不工作了</h3>

- MTG Arena 更新可能会覆盖 MelonLoader 文件。再次运行安装程序以重新安装 MelonLoader 和 MOD。
- 如果游戏的内部结构发生了重大变更，MOD 可能需要更新。在 GitHub 上查看新版本。

<h3>键盘快捷键不工作</h3>

- 确保游戏窗口处于焦点（点击它或 Alt+Tab 到它）
- 按 F1 检查 MOD 是否处于激活状态。如果听到帮助菜单，则 MOD 正在运行。
- 某些快捷键只在特定上下文中有效（对战快捷键只在对战期间有效）

<h3>语言错误</h3>

- 按 F2 打开设置菜单，然后用 Enter 循环切换语言

<h3>Windows 警告安装程序或 DLL 不安全</h3>

安装程序和 MOD 的 DLL 未进行代码签名。代码签名证书每年需要几百欧元，对一个免费的无障碍项目来说不现实。因此，Windows SmartScreen 和某些杀毒工具会在你第一次运行安装程序时发出警告，或把 DLL 标记为"未知发布者"。

为验证你下载的文件与 GitHub 上发布的文件一致，每次发布都会列出 `AccessibleArenaInstaller.exe` 和 `AccessibleArena.dll` 的 SHA256 校验值。你可以计算你下载文件的哈希并比对：

- PowerShell：`Get-FileHash <文件名> -Algorithm SHA256`
- 命令提示符：`certutil -hashfile <文件名> SHA256`

如果哈希与发布说明中的一致，则文件是真实的。若要在 SmartScreen 警告下运行安装程序，请选择"更多信息"，然后选择"仍要运行"。

<h2>报告错误</h2>

如果你发现错误，可以在你找到该 MOD 发布之处发帖，或[在 GitHub 上开一个 issue](https://github.com/JeanStiletto/AccessibleArena/issues)。

请包含以下信息：

- 错误发生时你在做什么
- 你期望发生什么
- 实际发生了什么
- 如果想附上游戏日志，请关闭游戏，并分享 MTGA 文件夹中的 MelonLoader 日志文件：
  - WotC：`C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam：`C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>已知问题</h2>
游戏应当涵盖几乎每个屏幕，但可能存在一些尚未完全可用的边缘情况。PayPal 使用非音频的非法验证码阻挡盲人用户，因此如果你想在游戏中花费真钱，你必须借助有视力者的帮助或使用其他支付方式。
一些特定活动可能未完全可用。与真实玩家的抽牌赛（draft）有一个尚未支持的大厅屏幕，但在 quickdraft 中你在面对人类对手之前会与机器人一起挑选卡牌，这一模式可用，并推荐给喜欢这种体验的所有人。立方体（Cube）模式尚未触及。我甚至不太清楚这是关于什么的，而且它需要大量游戏内资源。因此我会在有时间或收到请求时处理。
游戏的装饰性系统，包括表情、宠物、卡牌样式与称号，目前仅部分受支持。
MOD 仅在 Windows 上使用 NVDA 和 JAWS 进行了测试，仍依赖未修改的 Tolk 库。我无法在此测试 Mac 或 Linux 的兼容性，而像 Prism 这样的跨平台库当前并未完全支持游戏依赖的旧版 .NET。因此只有当有人能帮助测试其他平台或未修改 Tolk 未完全支持的亚洲屏幕阅读器时，我才会切换到更广泛的库。所以如果你想让我在此方面工作，请别犹豫联系我。

当前的已知问题列表见 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)。

<h2>免责声明</h2>
<h3>其他类型的可访问性</h3>

这个 MOD 叫 Accessible Arena 主要是因为听起来好听。但目前它只是一个屏幕阅读器可访问性 MOD。我对用此 MOD 覆盖更多残障、视觉障碍、运动障碍等绝对感兴趣。但我只有屏幕阅读器可访问性方面的经验。作为完全失明的人，例如颜色和字体方面的问题对我完全抽象。因此如果你希望实现此类内容，请在能清楚描述你的需求并愿意帮助我测试结果的情况下别犹豫联系我。
届时我将高兴地让这个 MOD 的名字更名副其实。

<h3>与公司的联系</h3>

遗憾的是，我无法获得关于 Arena 团队或非正式开发者联系人的可靠信息。因此我暂时决定跳过他们的官方沟通渠道。在 3 个月的开发与游玩中我从未遇到任何机器人保护系统，因此我认为他们无法检测到我们作为 MOD 用户。但作为单人，我不想冒险使用官方渠道进行沟通。因此请广泛传播这个 MOD，让我们建立一个庞大且有价值的社区。然后如果我们决定直接联系他们，我们将处于更好的位置。请不要在没有先与我沟通的情况下试图给他们写信。特别是不要向他们发送关于原生可访问性或将我的 MOD 整合进他们代码库的请求。两者都不会发生。

<h3>游戏内购买</h3>

Arena 有一些真钱相关机制，你可以购买游戏内货币。这些支付方式大多可访问，PayPal 除外，因为他们在登录中加入了验证码保护。你可以尝试卸载 MOD 来进行支付方式注册并请求有视力者的帮助，但由于他们的验证码是一场可访问性噩梦，且威世智进一步糟糕且不良地实现了它，即便如此也不可靠。
但其他支付方式工作稳定。我和其他人测试过在游戏中购买物品，使用该系统应当是安全的。但完全可能会发生错误，甚至 MOD 会误导你。可能点错东西、显示错误或不完整的信息、由于 Arena 的内部变化做错事。我可以测试，但我无法 100% 保证你不会用真钱买错东西。我不会对此承担责任，而且由于这不是官方 Arena 产品，游戏公司也不会。请在此情况下甚至不要尝试申请退款，他们不会退。

<h3>AI 使用</h3>

该 MOD 的代码 100% 是在 Anthropic 的 Claude 代理帮助下创建的，使用 Opus 模型：从 4.5 开始，大部分开发在 4.6 上进行，而发布前的最后步骤在 4.7 上完成。并且感谢我最大的贡献者，也使用了一点 Codex。我知道使用 AI 所带来的问题。但在每个人都使用这些软件去做许多更阴暗之事、而游戏行业无法在质量或数量上给我们想要的可访问性的时代，我仍然决定使用这些工具。

<h2>如何贡献</h2>

我很高兴接受贡献，并且有 [blindndangerous](https://github.com/blindndangerous)，已经有另一个人的大量有价值的工作成为了这个 MOD 的一部分。我特别感兴趣的是我无法测试的改进与修复，比如不同系统配置、修复我不会的语言等。但我也接受功能请求。在你开始做某事前，请查看已知问题。

- 一般的贡献指南见 [CONTRIBUTING.md](../CONTRIBUTING.md)
- 翻译帮助见 [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>致谢</h2>

现在我要感谢许许多多的人，因为幸运的是，这不仅仅是我和 AI 在一个黑箱中所做的工作，而是我周围整整一个网络在帮助、赋能、仅仅是以社交与友善的方式陪伴我。
如果我忘记了你或你希望以不同名字出现或不被提及，请给我发私信。

首先，这个工作在很大程度上建立在其他人的工作上，他们已经完成了开创性的事情，我只是为 Accessible Arena 重做一遍。
在设计方面，从 Hearthstone Access 那里我能继承很多东西，不仅因为所有玩过那款游戏的人都熟悉它，而且因为它确实是很好的 UI 设计。
在模组开发方面，我要感谢 Zax 的模组开发 Discord 的成员。你们不仅已经把我只需安装和使用的所有东西、所有工具和流程都搞清楚了。你们还直接地或者通过公开讨论或帮助其他新手，教给我关于 AI 模组开发所需要知道的一切。此外，你们还给了我以及我的项目一个可以存在的平台和社区。

至于大量的代码贡献，我要感谢 [blindndangerous](https://github.com/blindndangerous)，他也在这个项目上做了大量工作。在项目的整个生命周期中，我想我收到了他约 50 个以上的 PR，涵盖所有类型的问题，从需要处理的小烦恼到更大的 UI 建议以及游戏整个屏幕的可访问性。
进一步感谢 Ahix，他创建了[用于大型 AI 编码项目的重构提示词](https://github.com/ahicks92/llm-mod-refactoring-prompts)，我在自己重构之上运行它们以确保代码质量和可维护性。

关于代码贡献，我要感谢：
- [blindndangerous](https://github.com/blindndangerous)
- [LordLuceus](https://github.com/LordLuceus)

关于测试测试版、反馈和建议，我要感谢：
- Alfi
- Plüschyoda
- Firefly92
- Berenion
- [blindndangerous](https://github.com/blindndangerous)
- Toni Barth
- Chaosbringer216
- ABlindFellow
- SightlessKombat
- hamada
- Zack
- glaroc
- zersiax
- kairos4901
- [patricus3](https://github.com/patricus3)
- [LordLuceus](https://github.com/LordLuceus)

至于为理解视觉流程并确认某些事情而进行的明眼人测试，我要感谢：
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>所用工具</h3>

- 所有包含模型的 Claude
- MelonLoader
- 用于 IL 补丁的 Harmony
- 用于与屏幕阅读器沟通的 Tolk
- 用于反编译游戏代码的 ILSpy

<h2>支持你的 MOD 开发者</h2>

制作这个 MOD 对我来说不仅有很多乐趣和赋能，而且也花费了我大量时间和真金白银用于 Claude 订阅。我会保留这些订阅，用于未来几年的进一步改进和项目维护。
因此如果你愿意且能够承担一次性或每月的捐赠，可以看看这里。
我会非常感谢对我工作的这种认可，它为我继续在 Arena 上工作以及希望将来在其他大型项目上工作提供了稳定的基础。

[Ko-fi：ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>许可证</h2>

本项目采用 GNU General Public License v3.0 许可。详见 LICENSE 文件。

<h2>链接</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>其他语言</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [繁體中文](README.zh-TW.md)
