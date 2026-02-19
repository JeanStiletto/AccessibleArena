# Accessible Arena

Magic: The Gathering Arena 無障礙模組，讓視障玩家能夠使用螢幕閱讀器進行遊戲。支援完整的鍵盤導航、所有遊戲狀態的螢幕閱讀器播報，以及12種語言的在地化。

**狀態：** 公開測試版。核心遊戲功能可用。仍有一些邊緣情況和小錯誤。請參閱下方的已知問題。

**注意：** 目前僅支援鍵盤操作。不支援滑鼠或觸控。僅在 Windows 11 搭配 NVDA 環境下測試過。其他 Windows 版本和螢幕閱讀器（JAWS、朗讀程式等）可能可用但未經測試。

## 功能

- 所有介面的完整鍵盤導航（首頁、商店、精通、套牌建構器、對戰）
- 透過 Tolk 函式庫整合螢幕閱讀器
- 使用方向鍵讀取卡牌資訊（名稱、法術力費用、類型、力量/防禦、規則文字、背景描述、稀有度、畫師）
- 完整的對戰支援：區域導航、戰鬥、目標選擇、堆疊、瀏覽器（占卜、刺探、調度）
- 附著和戰鬥關係播報（被結附、阻擋、被指定為目標）
- 帶有購買選項和付款對話框支援的無障礙商店
- 支援機器人對戰用於練習
- 隨處可用的設定選單（F2）和說明選單（F1）
- 12種語言：英語、德語、法語、西班牙語、義大利語、葡萄牙語（巴西）、日語、韓語、俄語、波蘭語、簡體中文、繁體中文

## 系統需求

- Windows 10 或更高版本
- Magic: The Gathering Arena（透過官方安裝程式或 Epic Games Store 安裝）
- 螢幕閱讀器（推薦 NVDA：https://www.nvaccess.org/download/）
- MelonLoader（安裝程式會自動處理）

## 安裝

### 使用安裝程式（推薦）

1. 從 GitHub 最新發佈版下載 `AccessibleArenaInstaller.exe`：https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. 如果 MTG Arena 正在執行，請先關閉
3. 執行安裝程式。它會偵測您的 MTGA 安裝，必要時安裝 MelonLoader，並部署模組
4. 啟動 MTG Arena。您應該能透過螢幕閱讀器聽到「Accessible Arena v... 已啟動」

### 手動安裝

1. 在您的 MTGA 資料夾中安裝 MelonLoader（https://github.com/LavaGang/MelonLoader）
2. 從最新發佈版下載 `AccessibleArena.dll`
3. 將 DLL 複製到：`C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. 確保 `Tolk.dll` 和 `nvdaControllerClient64.dll` 在 MTGA 根資料夾中
5. 啟動 MTG Arena

## 快速開始

如果您還沒有 Wizards 帳戶，可以在 https://myaccounts.wizards.com/ 建立一個，而無需使用遊戲內的註冊介面。

安裝後，啟動 MTG Arena。模組會透過螢幕閱讀器播報目前的介面。

- 隨時按 **F1** 開啟可導航的說明選單，列出所有鍵盤快捷鍵
- 按 **F2** 開啟設定選單（語言、詳細程度、教學訊息）
- 按 **F3** 聽取目前介面名稱
- 使用**上/下方向鍵**或 **Tab/Shift+Tab** 導航選單
- 按 **Enter** 或**空白鍵**啟動元素
- 按 **Backspace** 返回

## 鍵盤快捷鍵

### 選單

- 上/下方向鍵（或 W/S）：導航項目
- Tab/Shift+Tab：導航項目（與上/下方向鍵相同）
- 左/右方向鍵（或 A/D）：輪播和步進控制項
- Home/End：跳到第一個/最後一個項目
- Page Up/Page Down：收藏中的上一頁/下一頁
- Enter/空白鍵：啟動
- Backspace：返回

### 對戰 - 區域

- C：你的手牌
- G / Shift+G：你的墳墓場 / 對手的墳墓場
- X / Shift+X：你的放逐區 / 對手的放逐區
- S：堆疊
- B / Shift+B：你的生物 / 對手的生物
- A / Shift+A：你的地 / 對手的地
- R / Shift+R：你的非生物 / 對手的非生物

### 對戰 - 區域內

- 左/右：導航卡牌
- Home/End：跳到第一張/最後一張卡牌
- 上/下方向鍵：聚焦卡牌時讀取卡牌詳情
- I：擴充卡牌資訊（關鍵字描述、其他面）
- Shift+上/下：切換戰場列

### 對戰 - 資訊

- T：目前回合和階段
- L：生命值總計
- V：玩家資訊區域（左/右切換玩家，上/下查看屬性）
- D / Shift+D：你的牌庫數量 / 對手的牌庫數量
- Shift+C：對手手牌數量

### 對戰 - 行動

- 空白鍵：確認（讓過優先權、確認攻擊者/阻擋者、下一階段）
- Backspace：取消 / 拒絕
- Tab：循環目標或高亮元素
- Ctrl+Tab：僅循環對手目標
- Enter：選擇目標

### 對戰 - 瀏覽器（占卜、刺探、調度）

- Tab：導航所有卡牌
- C/D：跳到頂部/底部區域
- 左/右：在區域內導航
- Enter：切換卡牌放置
- 空白鍵：確認選擇
- Backspace：取消

### 全域

- F1：說明選單
- F2：設定選單
- F3：播報目前介面
- Ctrl+R：重複上一條播報
- Backspace：通用返回/關閉/取消

## 報告錯誤

如果您發現了錯誤，請在 GitHub 上提交 issue：https://github.com/JeanStiletto/AccessibleArena/issues

請包含以下資訊：

- 錯誤發生時您在做什麼
- 您期望發生什麼
- 實際發生了什麼
- 您的螢幕閱讀器及版本
- 附上 MelonLoader 日誌檔案：`C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## 已知問題

- 讓過優先權的空白鍵並不總是可靠（模組會直接點擊按鈕作為後備方案）
- 套牌建構器中的套牌列表卡牌僅顯示名稱和數量，不顯示完整卡牌詳情
- PlayBlade 佇列類型選擇（排名、開放對戰、Brawl）可能並不總是設定正確的遊戲模式

完整列表請參閱 docs/KNOWN_ISSUES.md。

## 疑難排解

**啟動遊戲後沒有語音輸出**
- 確保在啟動 MTG Arena 之前螢幕閱讀器已經在執行
- 檢查 `Tolk.dll` 和 `nvdaControllerClient64.dll` 是否在 MTGA 根資料夾中（安裝程式會自動放置）
- 檢查 `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` 中的 MelonLoader 日誌是否有錯誤

**啟動時遊戲當機或模組未載入**
- 確保 MelonLoader 已安裝。
- 如果遊戲最近更新過，可能需要重新安裝 MelonLoader 或模組。再次執行安裝程式。
- 檢查 `AccessibleArena.dll` 是否在 `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**模組之前正常運作但在遊戲更新後停止了**
- MTG Arena 更新可能會覆蓋 MelonLoader 檔案。再次執行安裝程式以重新安裝 MelonLoader 和模組。
- 如果遊戲大幅更改了內部結構，模組可能需要更新。請在 GitHub 上檢查新版本。

**鍵盤快捷鍵不起作用**
- 確保遊戲視窗處於焦點狀態（點擊它或使用 Alt+Tab 切換到它）
- 按 F1 檢查模組是否活躍。如果您聽到說明選單，說明模組正在執行。
- 某些快捷鍵僅在特定情境中有效（對戰快捷鍵僅在對戰期間有效）

**語言錯誤**
- 按 F2 開啟設定選單，然後使用 Enter 切換語言

## 從原始碼建置

需求：.NET SDK（任何支援 net472 目標的版本）

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

建置的 DLL 位於 `src/bin/Debug/net472/AccessibleArena.dll`。

遊戲組件參考應在 `libs/` 資料夾中。從您的 MTGA 安裝中複製這些 DLL（`MTGA_Data/Managed/`）：
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader DLL（`MelonLoader.dll`、`0Harmony.dll`）來自您的 MelonLoader 安裝。

## 授權條款

本專案採用 GNU General Public License v3.0 授權。詳情請參閱 LICENSE 檔案。

## 連結

- GitHub：https://github.com/JeanStiletto/AccessibleArena
- NVDA 螢幕閱讀器（推薦）：https://www.nvaccess.org/download/
- MelonLoader：https://github.com/LavaGang/MelonLoader
- MTG Arena：https://magic.wizards.com/mtgarena
