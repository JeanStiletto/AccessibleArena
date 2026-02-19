# Accessible Arena

Magic: The Gathering Arenaのアクセシビリティmod。視覚障害のあるプレイヤーがスクリーンリーダーを使用してプレイできるようにします。完全なキーボードナビゲーション、すべてのゲーム状態のスクリーンリーダー読み上げ、12言語のローカライズに対応。

**ステータス:** パブリックベータ。主要なゲームプレイは機能しています。一部のエッジケースや軽微なバグが残っています。下記の既知の問題を参照してください。

**注意:** 現在はキーボードのみ対応。マウスやタッチのサポートはありません。Windows 11とNVDAでのみテスト済み。他のWindowsバージョンやスクリーンリーダー（JAWS、Narratorなど）でも動作する可能性がありますが、未テストです。

## 機能

- すべての画面での完全なキーボードナビゲーション（ホーム、ショップ、マスタリー、デッキビルダー、デュエル）
- Tolkライブラリによるスクリーンリーダー統合
- 矢印キーでカード情報を読み上げ（名前、マナコスト、タイプ、パワー/タフネス、ルールテキスト、フレーバーテキスト、レアリティ、アーティスト）
- デュエルの完全サポート：ゾーンナビゲーション、戦闘、ターゲット選択、スタック、ブラウザー（占術、諜報、マリガン）
- 装着および戦闘関係の読み上げ（エンチャント元、ブロック、ターゲット）
- 購入オプションと支払いダイアログに対応したアクセシブルなショップ
- 練習用のボットマッチサポート
- どこでも利用可能な設定メニュー（F2）とヘルプメニュー（F1）
- 12言語：英語、ドイツ語、フランス語、スペイン語、イタリア語、ポルトガル語（BR）、日本語、韓国語、ロシア語、ポーランド語、中国語（簡体字）、中国語（繁体字）

## 必要条件

- Windows 10以降
- Magic: The Gathering Arena（公式インストーラーまたはEpic Games Storeからインストール）
- スクリーンリーダー（NVDA推奨：https://www.nvaccess.org/download/）
- MelonLoader（インストーラーが自動的に処理します）

## インストール

### インストーラーを使用（推奨）

1. GitHubの最新リリースから`AccessibleArenaInstaller.exe`をダウンロード：https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. MTG Arenaが実行中の場合は閉じてください
3. インストーラーを実行。MTGA のインストールを検出し、必要に応じてMelonLoaderをインストールし、modをデプロイします
4. MTG Arenaを起動。スクリーンリーダーを通じて「Accessible Arena v... 起動」と聞こえるはずです

### 手動インストール

1. MTGAフォルダーにMelonLoaderをインストール（https://github.com/LavaGang/MelonLoader）
2. 最新リリースから`AccessibleArena.dll`をダウンロード
3. DLLを次の場所にコピー：`C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. `Tolk.dll`と`nvdaControllerClient64.dll`がMTGAルートフォルダーにあることを確認
5. MTG Arenaを起動

## クイックスタート

まだWizardsアカウントをお持ちでない場合は、ゲーム内の登録画面の代わりに https://myaccounts.wizards.com/ でアカウントを作成できます。

インストール後、MTG Arenaを起動します。modはスクリーンリーダーを通じて現在の画面を読み上げます。

- いつでも**F1**を押すと、すべてのキーボードショートカットが一覧表示されるナビゲーション可能なヘルプメニューが開きます
- **F2**を押すと設定メニュー（言語、詳細度、チュートリアルメッセージ）が開きます
- **F3**を押すと現在の画面名を読み上げます
- **上下矢印キー**または**Tab/Shift+Tab**でメニューを移動
- **Enter**または**スペース**で要素を実行
- **Backspace**で戻る

## キーボードショートカット

### メニュー

- 上下矢印（またはW/S）：項目をナビゲート
- Tab/Shift+Tab：項目をナビゲート（上下矢印と同じ）
- 左右矢印（またはA/D）：カルーセルとステッパーの操作
- Home/End：最初/最後の項目にジャンプ
- Page Up/Page Down：コレクションの前/次のページ
- Enter/スペース：実行
- Backspace：戻る

### デュエル - ゾーン

- C：自分の手札
- G / Shift+G：自分の墓地 / 対戦相手の墓地
- X / Shift+X：自分の追放 / 対戦相手の追放
- S：スタック
- B / Shift+B：自分のクリーチャー / 対戦相手のクリーチャー
- A / Shift+A：自分の土地 / 対戦相手の土地
- R / Shift+R：自分の非クリーチャー / 対戦相手の非クリーチャー

### デュエル - ゾーン内

- 左/右：カードをナビゲート
- Home/End：最初/最後のカードにジャンプ
- 上下矢印：カードにフォーカス時にカード詳細を読み上げ
- I：拡張カード情報（キーワード説明、他の面）
- Shift+上/下：戦場の列を切り替え

### デュエル - 情報

- T：現在のターンとフェイズ
- L：ライフ合計
- V：プレイヤー情報ゾーン（左/右でプレイヤー切替、上/下でプロパティ）
- D / Shift+D：自分のライブラリー枚数 / 対戦相手のライブラリー枚数
- Shift+C：対戦相手の手札枚数

### デュエル - アクション

- スペース：確認（優先権パス、攻撃者/ブロッカー確認、次のフェイズ）
- Backspace：キャンセル / 辞退
- Tab：ターゲットまたはハイライトされた要素を巡回
- Ctrl+Tab：対戦相手のターゲットのみ巡回
- Enter：ターゲットを選択

### デュエル - ブラウザー（占術、諜報、マリガン）

- Tab：すべてのカードをナビゲート
- C/D：上部/下部ゾーンにジャンプ
- 左/右：ゾーン内をナビゲート
- Enter：カードの配置を切り替え
- スペース：選択を確認
- Backspace：キャンセル

### グローバル

- F1：ヘルプメニュー
- F2：設定メニュー
- F3：現在の画面を読み上げ
- Ctrl+R：最後の読み上げを繰り返し
- Backspace：汎用の戻る/閉じる/キャンセル

## バグ報告

バグを見つけた場合は、GitHubでissueを開いてください：https://github.com/JeanStiletto/AccessibleArena/issues

以下の情報を含めてください：

- バグが発生した時に何をしていたか
- 何が起こることを期待していたか
- 実際に何が起こったか
- お使いのスクリーンリーダーとバージョン
- MelonLoaderのログファイルを添付：`C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## 既知の問題

- 優先権パスのスペースキーは常に信頼できるとは限りません（modがフォールバックとしてボタンを直接クリックします）
- デッキビルダーのデッキリストカードは名前と数量のみ表示し、完全なカード詳細は表示しません
- PlayBladeのキュータイプ選択（ランク、オープンプレイ、Brawl）が常に正しいゲームモードを設定するとは限りません

完全なリストはdocs/KNOWN_ISSUES.mdを参照してください。

## トラブルシューティング

**ゲーム起動後に音声出力がない**
- MTG Arenaを起動する前にスクリーンリーダーが実行されていることを確認してください
- `Tolk.dll`と`nvdaControllerClient64.dll`がMTGAルートフォルダーにあることを確認してください（インストーラーが自動的に配置します）
- `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`のMelonLoaderログでエラーを確認してください

**起動時にゲームがクラッシュするかmodが読み込まれない**
- MelonLoaderがインストールされていることを確認してください。
- ゲームが最近更新された場合、MelonLoaderまたはmodの再インストールが必要な場合があります。インストーラーを再度実行してください。
- `AccessibleArena.dll`が`C:\Program Files\Wizards of the Coast\MTGA\Mods\`にあることを確認してください

**modは動作していたがゲーム更新後に停止した**
- MTG Arenaの更新でMelonLoaderファイルが上書きされることがあります。インストーラーを再度実行してMelonLoaderとmodの両方を再インストールしてください。
- ゲームが内部構造を大幅に変更した場合、modの更新が必要な場合があります。GitHubで新しいリリースを確認してください。

**キーボードショートカットが機能しない**
- ゲームウィンドウがフォーカスされていることを確認してください（クリックするかAlt+Tabで切り替え）
- F1を押してmodが有効かどうか確認してください。ヘルプメニューが聞こえれば、modは実行中です。
- 一部のショートカットは特定のコンテキストでのみ機能します（デュエルショートカットはデュエル中のみ）

**言語が間違っている**
- F2を押して設定メニューを開き、Enterで言語を切り替えてください

## ソースからビルド

必要条件：.NET SDK（net472ターゲットをサポートする任意のバージョン）

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

ビルドされたDLLは`src/bin/Debug/net472/AccessibleArena.dll`にあります。

ゲームアセンブリ参照は`libs/`フォルダーに配置してください。MTGAインストールからこれらのDLLをコピーしてください（`MTGA_Data/Managed/`）：
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader DLL（`MelonLoader.dll`、`0Harmony.dll`）はMelonLoaderインストールから取得します。

## ライセンス

このプロジェクトはGNU General Public License v3.0の下でライセンスされています。詳細はLICENSEファイルを参照してください。

## リンク

- GitHub：https://github.com/JeanStiletto/AccessibleArena
- NVDAスクリーンリーダー（推奨）：https://www.nvaccess.org/download/
- MelonLoader：https://github.com/LavaGang/MelonLoader
- MTG Arena：https://magic.wizards.com/mtgarena
