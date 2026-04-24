<h1>Accessible Arena</h1>

<h2>這個 MOD 是什麼</h2>

這個 MOD 可讓你遊玩 Arena——集換式卡牌遊戲《魔法風雲會》（Magic: The Gathering）最受歡迎、對新手最友善的數位版本。它為遊戲幾乎每個方面新增了完整的螢幕閱讀器支援與鍵盤導覽。

該 MOD 支援遊戲已翻譯的所有語言。此外，還部分涵蓋了一些遊戲本身不支援的語言：在這些語言中，MOD 特有的播報（例如說明文字和 UI 提示）會被翻譯，而卡牌與遊戲資料仍使用遊戲的預設語言。

<h2>《魔法風雲會》是什麼</h2>

《魔法風雲會》是威世智（Wizards of the Coast）註冊商標的一款集換式卡牌遊戲，你可以扮演法師與其他法師對戰，透過卡牌施放法術。《魔法風雲會》有 5 種顏色，代表不同的遊戲玩法與風味身分。如果你熟悉爐石戰記或遊戲王，你會認出許多概念，因為《魔法風雲會》是這些遊戲的鼻祖。
如果你想更多了解《魔法風雲會》，官方網站以及許多內容創作者都能幫到你。

<h2>需求</h2>

- Windows 10 或更新版本
- Magic: The Gathering Arena（透過 Wizards 官方安裝程式或 Steam 安裝）
- 一款螢幕閱讀器（僅測試了 NVDA 和 JAWS）
- MelonLoader（安裝程式會自動處理）

<h2>安裝</h2>

<h3>使用安裝程式（建議）</h3>

1. 從 GitHub 最新發行版下載 [AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe)
2. 如果 MTG Arena 正在執行，請關閉它
3. 執行安裝程式。它會偵測你的 MTGA 安裝，必要時安裝 MelonLoader，並佈署 MOD
4. 啟動 MTG Arena。你應當透過螢幕閱讀器聽到「Accessible Arena v... launched」

<h3>手動安裝</h3>

1. 將 [MelonLoader](https://github.com/LavaGang/MelonLoader) 安裝到 MTGA 資料夾
2. 從最新發行版下載 `AccessibleArena.dll`
3. 將 DLL 複製到 MTGA 的 Mods 資料夾：
   - WotC 安裝：`C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Steam 安裝：`C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. 確保 `Tolk.dll` 和 `nvdaControllerClient64.dll` 位於 MTGA 根資料夾
5. 啟動 MTG Arena

<h2>解除安裝</h2>

再次執行安裝程式。如果 MOD 已安裝，它會提供解除安裝選項。你也可以選擇一併移除 MelonLoader。如需手動解除安裝，請從 `Mods\` 資料夾刪除 `AccessibleArena.dll`，並從 MTGA 根資料夾移除 `Tolk.dll` 和 `nvdaControllerClient64.dll`。

<h2>如果你是從爐石戰記過來的</h2>

如果你玩過 Hearthstone Access，你會有充分的理由認出許多東西，因為不僅遊戲原理相近，我也遵循了許多設計原則。不過仍然有一些東西不一樣。

首先，你要導覽的區域更多，因為《魔法風雲會》有墳場、放逐區和一些額外區域。你的戰場大小沒有限制，並且有額外的排序列，以便更好地管理可能出現的眾多元素。

你的魔法力不會自動增長，而是來自不同顏色的地牌，你必須主動將它們打出。據此，魔法力費用包含無色部分和有色部分，兩者相加構成必須滿足的卡牌完整費用需求。

你不能直接攻擊生物，只有對手和一些非常特定的卡（鵬洛客和戰役）才能被攻擊者作為攻擊目標。作為防禦者，你必須決定是否要阻擋一次攻擊以讓生物互相戰鬥。如果不阻擋，傷害會擊中你的玩家頭像，但你的生物可以保持完好。進一步地，傷害不會在生物上累積，而是在每個回合結束時被治療，也就是在你和對手的回合結束時都會被治療。要與不願意與你戰鬥的對手生物互動，你必須打出特定的牌，或者強力施壓對手的生命總量，使其別無選擇只能犧牲寶貴的生物以求生。

遊戲有非常清晰區分的戰鬥階段，允許進行特定的動作，比如抽牌、施放法術或戰鬥。據此，《魔法風雲會》允許並鼓勵你在對手回合做事。別再傻坐等著事情發生，打造一副互動性的牌組，當場粉碎敵人的計畫。

<h2>起步</h2>

遊戲首先會要求你提供一些個人資料並註冊一個角色。透過遊戲內機制應當能完成，但如果不能，你也可以改用遊戲的網站來完成，它完全可存取。

遊戲以教學開始，你將在其中學習《魔法風雲會》的基礎知識。MOD 在標準教學之外為螢幕閱讀器使用者新增了自訂教學提示。完成教學後，你會獲得 5 副入門套牌作為獎勵，每種顏色一副。

從這裡開始，你有幾種方式可以解鎖更多卡牌並學習遊戲：

- **顏色挑戰：** 遊玩五種《魔法風雲會》顏色各自的顏色挑戰。每個挑戰讓你與 4 個 NPC 對手作戰，最後與一位真實玩家對戰。
- **入門套牌活動：** 與擁有相同套牌選項的真實玩家一起，使用 10 副雙色牌組中的一副進行遊玩。
- **Jump In：** 選擇兩個不同顏色和主題的 20 張卡包，將它們合併為一副牌組，與有類似選擇的真實玩家對戰。你會在此活動中獲得免費代幣，並保留所選的卡牌。
- **火花天梯（Spark Ladder）：** 在某個時刻，Spark Ladder 會解鎖，你將與真實對手進行你的第一次排位賽。

查看社交選單下的郵件，裡面有許多獎勵和卡牌包。

遊戲會根據你玩了什麼以及玩了多少來逐步解鎖各種模式。它會在進度和目標選單中給你提示和任務，並在遊玩選單中突顯你相關的模式。一旦你完成了足夠多的新手玩家內容，所有不同的模式和活動都會完全開放。

在多重宇宙典籍中，你可以了解各種遊戲模式和機制。它會隨著 NPE 體驗的進展而擴展。

在設定帳號下，你可以跳過所有教學體驗並強制解鎖一切，從一開始就擁有完全的自由。然而，玩新手玩家活動能給你帶來大量卡牌，建議新手玩家進行。只有在已經知道自己在做什麼時才提前解鎖一切。否則初學者內容能提供豐富的樂趣和學習體驗，並會很好地引導你。

<h2>鍵盤快速鍵</h2>

導覽在各處遵循標準慣例：方向鍵移動，Home/End 跳到第一個/最後一個，Enter 選取，Space 確認，Backspace 返回或取消。Tab/Shift+Tab 同樣可用於導覽。Page Up/Page Down 翻頁。

<h3>全域</h3>

- F1：說明選單（列出目前畫面的所有快速鍵）
- Ctrl+F1：播報目前畫面的快速鍵
- F2：MOD 設定
- F3：播報目前畫面
- F4：好友面板（在選單中）/ 對戰聊天（在對戰中）
- F5：檢查 / 開始更新
- Ctrl+R：重複最後一次播報

<h3>對戰 - 區域</h3>

你的區域：C（手牌）、G（墳場）、X（放逐）、S（堆疊）、W（指揮區）
對手區域：Shift+G、Shift+X、Shift+W
戰場：B / Shift+B（生物）、A / Shift+A（地）、R / Shift+R（非生物）
區域內：左右鍵導覽，上下鍵讀取卡牌詳情，I 檢視擴充資訊
Shift+上/下：切換戰場列

<h3>對戰 - 資訊</h3>

- T：回合/階段
- L：生命總值
- V：玩家資訊區域
- D / Shift+D：牌庫張數
- Shift+C：對手手牌張數
- M / Shift+M：你 / 對手的地彙總
- K：焦點卡牌的指示物資訊
- O：遊戲紀錄（最近的對戰播報）
- E / Shift+E：你 / 對手的計時器

<h3>對戰 - 選定目標與行動</h3>

- Tab / Ctrl+Tab：循環切換目標（全部 / 僅對手）
- Enter：選取目標
- Space：讓渡優先權，確認攻擊/阻擋，推進階段

<h3>對戰 - Full control 與階段停頓</h3>

- P：切換 full control（暫時，換階段時重設）
- Shift+P：切換鎖定的 full control（永久）
- Shift+Backspace：切換直到對手動作前結束（軟略過）
- Ctrl+Backspace：切換略過回合（強制略過整個回合）
- 1-0：切換階段停頓（1=維持，2=抽牌，3=第一主要，4=戰鬥開始，5=宣告攻擊者，6=宣告阻擋者，7=戰鬥傷害，8=戰鬥結束，9=第二主要，0=結束步驟）

<h3>對戰 - 瀏覽器（佔卜、監視、調度）</h3>

- Tab：瀏覽所有卡牌
- C/D：在上/下區域之間跳轉
- Enter：切換卡牌放置

<h2>疑難排解</h2>

<h3>啟動遊戲後沒有語音</h3>

- 請確保在啟動 MTG Arena 之前螢幕閱讀器已在執行
- 檢查 `Tolk.dll` 和 `nvdaControllerClient64.dll` 是否位於 MTGA 根資料夾（安裝程式會自動放置）
- 檢查 MTGA 資料夾中 MelonLoader 紀錄（`MelonLoader\Latest.log`）是否有錯誤

<h3>遊戲啟動時當機或 MOD 未載入</h3>

- 請確保 MelonLoader 已安裝。
- 如果遊戲最近進行了更新，MelonLoader 或 MOD 可能需要重新安裝。請再次執行安裝程式。
- 檢查 `AccessibleArena.dll` 是否位於你 MTGA 安裝中的 `Mods\` 資料夾內

<h3>MOD 原本可用，但遊戲更新後不工作了</h3>

- MTG Arena 更新可能會覆寫 MelonLoader 檔案。請再次執行安裝程式以重新安裝 MelonLoader 和 MOD。
- 如果遊戲的內部結構發生了重大變更，MOD 可能需要更新。在 GitHub 上查看新版本。

<h3>鍵盤快速鍵不工作</h3>

- 確保遊戲視窗處於焦點（點擊它或 Alt+Tab 到它）
- 按 F1 檢查 MOD 是否啟用。若聽到說明選單，則 MOD 正在執行。
- 某些快速鍵僅在特定情境下有效（對戰快速鍵只在對戰期間有效）

<h3>語言錯誤</h3>

- 按 F2 開啟設定選單，然後用 Enter 循環切換語言

<h3>Windows 警告安裝程式或 DLL 不安全</h3>

安裝程式和 MOD 的 DLL 未進行程式碼簽章。程式碼簽章憑證每年需要幾百歐元，對一個免費的無障礙專案來說不切實際。因此，Windows SmartScreen 和某些防毒工具會在你第一次執行安裝程式時發出警告，或將 DLL 標示為「未知發行者」。

為驗證你下載的檔案與 GitHub 上發佈的檔案一致，每次發佈都會列出 `AccessibleArenaInstaller.exe` 和 `AccessibleArena.dll` 的 SHA256 校驗值。你可以計算你下載檔案的雜湊並比對：

- PowerShell：`Get-FileHash <檔名> -Algorithm SHA256`
- 命令提示字元：`certutil -hashfile <檔名> SHA256`

如果雜湊與發佈說明中的一致，則檔案為真實。若要在 SmartScreen 警告下執行安裝程式，請選擇「其他資訊」，然後選擇「仍要執行」。

<h2>回報錯誤</h2>

如果你發現錯誤，可以在你找到該 MOD 發佈之處發文，或[在 GitHub 上開一個 issue](https://github.com/JeanStiletto/AccessibleArena/issues)。

請包含以下資訊：

- 錯誤發生時你在做什麼
- 你期望發生什麼
- 實際發生了什麼
- 如果想附上遊戲紀錄，請關閉遊戲，並分享 MTGA 資料夾中的 MelonLoader 紀錄檔案：
  - WotC：`C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam：`C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>已知問題</h2>
遊戲應當涵蓋幾乎每個畫面，但可能存在一些尚未完全可用的邊緣情況。PayPal 使用非音訊的非法 captcha 阻擋盲人使用者，因此如果你想在遊戲中花費真錢，你必須借助視覺上能看見的人的協助或使用其他付款方式。
一些特定活動可能未完全可用。與真實玩家的抽牌賽（draft）有一個尚未支援的大廳畫面，但在 quickdraft 中你在面對人類對手之前會與機器人一起挑選卡牌，這一模式可用，並建議給喜歡這種體驗的所有人。方塊（Cube）模式尚未觸及。我甚至不太清楚這是關於什麼的，而且它需要大量遊戲內資源。因此我會在有時間或收到請求時處理。
遊戲的裝飾性系統，包括表情、寵物、卡牌樣式與稱號，目前僅部分受支援。
MOD 僅在 Windows 上使用 NVDA 和 JAWS 進行了測試，仍依賴未修改的 Tolk 函式庫。我無法在此測試 Mac 或 Linux 的相容性，而像 Prism 這樣的跨平台函式庫目前並未完全支援遊戲依賴的舊版 .NET。因此只有當有人能幫助測試其他平台或未修改 Tolk 未完全支援的亞洲螢幕閱讀器時，我才會切換到更廣泛的函式庫。所以如果你想讓我在此方面工作，請別猶豫聯絡我。

目前的已知問題列表見 [KNOWN_ISSUES.md](KNOWN_ISSUES.md)。

<h2>免責聲明</h2>
<h3>其他類型的可存取性</h3>

這個 MOD 叫 Accessible Arena 主要是因為聽起來好聽。但目前它只是一個螢幕閱讀器可存取性 MOD。我對用此 MOD 涵蓋更多障礙、視覺障礙、動作障礙等絕對感興趣。但我只有螢幕閱讀器可存取性方面的經驗。作為完全失明的人，例如顏色和字型方面的問題對我完全抽象。因此如果你希望實作此類內容，請在能清楚描述你的需求並願意幫助我測試結果的情況下別猶豫聯絡我。
屆時我將高興地讓這個 MOD 的名字更名副其實。

<h3>與公司的聯絡</h3>

遺憾的是，我無法取得關於 Arena 團隊或非正式開發者聯絡人的可靠資訊。因此我暫時決定跳過他們的官方溝通管道。在 3 個月的開發與遊玩中我從未遇到任何機器人保護系統，因此我認為他們無法偵測到我們作為 MOD 使用者。但作為單人，我不想冒險使用官方管道進行溝通。因此請廣泛傳播這個 MOD，讓我們建立一個龐大且有價值的社群。然後如果我們決定直接聯絡他們，我們將處於更好的位置。請不要在沒有先與我溝通的情況下試圖給他們寫信。尤其是不要向他們發送關於原生可存取性或將我的 MOD 整合進他們程式碼庫的請求。兩者都不會發生。

<h3>遊戲內購買</h3>

Arena 有一些真錢相關機制，你可以購買遊戲內貨幣。這些付款方式大多可存取，PayPal 除外，因為他們在登入中加入了 captcha 保護。你可以嘗試解除安裝 MOD 來進行付款方式註冊並請求視覺上能看見的人的協助，但由於他們的 captcha 是一場可存取性惡夢，且威世智進一步糟糕且不良地實作了它，即便如此也不可靠。
但其他付款方式工作穩定。我和其他人測試過在遊戲中購買物品，使用該系統應當是安全的。但完全可能會發生錯誤，甚至 MOD 會誤導你。可能點錯東西、顯示錯誤或不完整的資訊、由於 Arena 的內部變化做錯事。我可以測試，但我無法 100% 保證你不會用真錢買錯東西。我不會對此承擔責任，而且由於這不是官方 Arena 產品，遊戲公司也不會。請在此情況下甚至不要嘗試申請退款，他們不會退。

<h3>AI 使用</h3>

該 MOD 的程式碼 100% 是在 Anthropic 的 Claude 代理協助下建立的，使用 Opus 模型：從 4.5 開始，大部分開發在 4.6 上進行，而發佈前的最後步驟在 4.7 上完成。並且感謝我最大的貢獻者，也使用了一點 Codex。我知道使用 AI 所帶來的問題。但在每個人都使用這些軟體去做許多更陰暗之事、而遊戲業界無法在品質或數量上給我們想要的可存取性的時代，我仍然決定使用這些工具。

<h2>如何貢獻</h2>

我很高興接受貢獻，並且有 [blindndangerous](https://github.com/blindndangerous)，已經有另一個人的大量有價值的工作成為了這個 MOD 的一部分。我特別感興趣的是我無法測試的改進與修正，比如不同系統設定、修正我不會的語言等。但我也接受功能請求。在你開始做某事前，請查看已知問題。

- 一般的貢獻指南見 [CONTRIBUTING.md](../CONTRIBUTING.md)
- 翻譯協助見 [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>致謝</h2>

現在我要感謝許許多多的人，因為幸運的是，這不僅僅是我和 AI 在一個黑盒中所做的工作，而是我周圍整整一個網路在幫助、賦能、僅僅是以社交與友善的方式陪伴我。
如果我忘記了你或你希望以不同名字出現或不被提及，請給我發私訊。

首先，這個工作在很大程度上建立在其他人的工作上，他們已經完成了開創性的事情，我只是為 Accessible Arena 重做一遍。
在設計方面，從 Hearthstone Access 那裡我能繼承許多東西，不僅因為所有玩過那款遊戲的人都熟悉它，而且因為它確實是很好的 UI 設計。
在模組開發方面，我要感謝 Zax 的模組開發 Discord 的成員。你們不僅已經把我只需安裝和使用的所有東西、所有工具和流程都搞清楚了。你們還直接地或者透過公開討論或協助其他新手，教給我關於 AI 模組開發所需要知道的一切。此外，你們還給了我以及我的專案一個可以存在的平台與社群。

至於大量的程式碼貢獻，我要感謝 [blindndangerous](https://github.com/blindndangerous)，他也在這個專案上做了大量工作。在專案的整個生命週期中，我想我收到了他約 50 個以上的 PR，涵蓋所有類型的問題，從需要處理的小煩惱到更大的 UI 建議以及遊戲整個畫面的可存取性。
進一步感謝 Ahix，他建立了[用於大型 AI 程式碼專案的重構提示詞](https://github.com/ahicks92/llm-mod-refactoring-prompts)，我在自己的重構之上執行它們以確保程式碼品質和可維護性。

關於測試測試版、回饋和建議，我要感謝：
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

至於為理解視覺流程並確認某些事情而進行的明眼人測試，我要感謝：
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>所用工具</h3>

- 包含所有模型的 Claude
- MelonLoader
- 用於 IL 修補的 Harmony
- 用於與螢幕閱讀器溝通的 Tolk
- 用於反組譯遊戲程式碼的 ILSpy

<h2>支持你的 MOD 開發者</h2>

製作這個 MOD 對我來說不僅有許多樂趣和賦能，而且也花費了我大量時間和真金白銀用於 Claude 訂閱。我會保留這些訂閱，用於未來幾年的進一步改進和專案維護。
因此如果你願意且能夠承擔一次性或每月的捐款，可以看看這裡。
我會非常感謝對我工作的這種認可，它為我繼續在 Arena 上工作以及希望將來在其他大型專案上工作提供了穩定的基礎。

[Ko-fi：ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>授權</h2>

本專案採用 GNU General Public License v3.0 授權。詳見 LICENSE 檔案。

<h2>連結</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>其他語言</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md)
