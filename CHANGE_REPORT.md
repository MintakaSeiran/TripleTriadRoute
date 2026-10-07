# 変更報告（2026-10-08）

Fork: https://github.com/MintakaSeiran/TripleTriadRoute
ブランチ: `fix/japanese-triad-runtime-availability`

## 変更理由と実装

| ファイル（TripleTriadRoute配下） | 変更理由 |
| --- | --- |
| Core/Game/Ops/TriadMenuPolicy.cs（新規） | 日本語部分一致、既知アイコン優先、キャンセル完全一致、2回確認、認識失敗と対戦不可を分離する純粋ロジック |
| Core/Game/Ops/TriadDialog.cs | 安全でない単独項目／末尾クリックを除去、項目・アイコンのsnapshot取得、実際の対戦項目選択を通知 |
| Core/Tasks/AutoCommon.Triad.cs | 自分のInteract後に判定、750ms待機と閉鎖確認、試行回数と選択済み状態をNPC単位で保持、詳細失敗ログ、安全に閉じられない場合の終了 |
| Core/Planning/SkipReason.cs | TriadUnavailableとMenuRecognitionFailedを末尾へ追加、既存値を維持 |
| Core/Planning/CollectPlanner.cs | 除外辞書から実理由を使い、Unavailableカードへ保存 |
| Core/Tasks/AutoCollect.cs | スキップ後即再計画、代替なしだけUnavailable、未解決メニューがあれば他NPCを触らず終了 |
| Core/Tasks/AutoFarm.cs | セッション除外と重複を除き、静的スキップも記録、対戦不可なら次対象、全対象不可時の日本語／英語メッセージ |
| Core/Tasks/AutoTriadSession.cs | 再開にも有効なセッション限定除外辞書、Unavailableカード一覧、重複Skip抑制 |
| Core/Tasks/AutoTriadController.RunLifecycle.cs / Core/Stats/RunRecord.cs | 対戦0回のスキップも履歴へ記録、安定IDと名称・理由を保存。履歴を実行時除外へ戻さない |
| Windows/TriadLabels.cs / Windows/Pages/HistoryPage.cs | スキップ理由と終了後の未取得カードを既存UIで表示 |
| Core/Localization/L.cs / Localizationの全9カタログ | 新しい理由・Farm終了文・未取得枚数の表示。日本語と英語、他言語には英語文を使用しキー整合性を維持 |
| Core/AttgConstants.cs / Core/Game/Ops/DialogDriver.cs / NpcInteraction.cs / Core/RunLog.cs / Core/Tasks/AutoGoto.cs | コマンド・ログ・throttle名前空間を分離 |
| Plugin.cs / Windows/Shell/AppWindow.cs / HeaderBar.cs / Windows/Pages/AboutPage.cs | 表示名・ウィンドウID・クレジットを変更。元作者のサポート／寄付をForkのものとして表示しない |
| TripleTriadRoute.csproj / TripleTriadRoute.json | AssemblyNameとInternalNameを分離、C#14固定、Fork URL設定。内部C# namespace維持 |

ルートのDirectory.Build.propsはForkの版・作者表記・URLを設定し元Copyrightを保持。READMEは日本語の改修仕様・ビルド・導入・残存リスク・実機受入手順へ変更。旧アイコンを除去し公開前の独自アイコン準備を明示。repo.jsonはリリース未作成のため空配列にし、元版を配布するURLを除去。

.githubは新しいパスに合わせ、元の自動タグ・通知・配布ハブ更新を停止。CIはゲーム非依存テストのみ。release workflowは未設定の公開前提を説明して失敗する状態。Issueテンプレートを元作者への報告へ誘導しないよう変更。

AutoTripleTriadGrind/ → TripleTriadRoute/、AutoTripleTriadGrind.Tests/ → TripleTriadRoute.Tests/、solution・csproj・manifestを改名。内容無変更の移動は機械的変更で、namespace全面変更やゲームロジック全体のリファクタリングは行っていません。

## 検証

- .NET SDK 10.0.401 / Dalamud.NET.Sdk 15.0.0 / 同一配布Dalamud 15.0.3.6、C#14、x64。
- Releaseビルド成功、警告0・エラー0。初回NU1301/NU1900は通信可能な再復元で解消。
- 57件成功（既存ソルバー＋新規認識・安全性・静的条件・再計画・セッション保持テスト）。ゲームAPI境界はスタブ。
- LICENSE.md、NOTICE、TRADEMARK.md、THIRD-PARTY-NOTICES.mdは上流と同一。
- 全9翻訳カタログのJSON・キー順一致、manifestの名称・版・API、ZIP内容を検証。
- 実機試験未実施。移動、Addon、Challenge、試合、再戦、カード登録、IPC、Farm非同期全体は未検証。詳細手順はREADME。

## 残存リスク・公開条件

未知文言は認識失敗として扱うため対戦不可と断定できないNPCが残る。キャンセル不明／閉鎖失敗は安全終了する。ゲーム更新によるアイコン・構造体変更、読み取り途中のUI、他の自動操作プラグインとの競合は実機確認が必要。実機試験はビルド成功から保証しない。

独自公開アイコン、実在するリリースURLとrepo.json、API15を指定した配布workflowは公開前に設定する。今回はリリースを作成しない。

## 内容変更の全ファイル一覧（Git表記）

以下は内容変更のあるファイルのみ。`{旧 => 新}` はパス変更。内容無変更のフォルダー移動は上記の通り。

- `.github/ISSUE_TEMPLATE/bug_report.yml`
- `.github/ISSUE_TEMPLATE/config.yml`
- `.github/ISSUE_TEMPLATE/feature_request.yml`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/dependabot.yml`
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `AutoTripleTriadGrind.Tests/AutoTripleTriadGrind.Tests.csproj`
- `AutoTripleTriadGrind/AutoTripleTriadGrind.json`
- `AutoTripleTriadGrind/Core/AttgConstants.cs`
- `AutoTripleTriadGrind/Images/Icon.png`
- `Directory.Build.props`
- `README.md`
- `TripleTriadRoute.Tests/AvailabilityTests.cs`
- `TripleTriadRoute.Tests/PlanningStubs.cs`
- `TripleTriadRoute.Tests/TripleTriadRoute.Tests.csproj`
- `AutoTripleTriadGrind.sln => TripleTriadRoute.sln`
- `TripleTriadRoute/Core/AttgConstants.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Game/Ops/DialogDriver.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Game/Ops/NpcInteraction.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Game/Ops/TriadDialog.cs`
- `TripleTriadRoute/Core/Game/Ops/TriadMenuPolicy.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Localization/L.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Planning/CollectPlanner.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Planning/SkipReason.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/RunLog.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Stats/RunRecord.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoCollect.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoCommon.Triad.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoFarm.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoGoto.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoTriadController.RunLifecycle.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Core/Tasks/AutoTriadSession.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Fonts/NotoSans-Medium-Latin.ttf`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/de.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/en.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/es.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/fr.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/ja.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/pt.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/ru.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/tr.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Localization/zh.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Plugin.cs`
- `AutoTripleTriadGrind/AutoTripleTriadGrind.csproj => TripleTriadRoute/TripleTriadRoute.csproj`
- `TripleTriadRoute/TripleTriadRoute.json`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Windows/Pages/AboutPage.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Windows/Pages/HistoryPage.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Windows/Shell/AppWindow.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Windows/Shell/HeaderBar.cs`
- `{AutoTripleTriadGrind => TripleTriadRoute}/Windows/TriadLabels.cs`
- `repo.json`
- `CHANGE_REPORT.md`（本報告）

追加の実行環境メモ：最終再ビルドの制限環境ではPackagerの一時ZIP移動がAccess deniedとなり、テストホストも完了しなかったため中断した。通常権限で同じビルド・テストを実行し、警告0・エラー0、57件成功、ZIP生成と内容を再確認した。LICENSE・NOTICE等はcsprojのContentとしてZIPに含めている。

一覧補足：`.github/ISSUE_TEMPLATE/translation_report.yml` も担当者をFork所有者へ変更し、元作者へ自動割当しないようにした。全内容変更は `git show --stat --find-renames` でも確認できる。ローカルの親プロジェクト文書 `docs/DEVELOPMENT.md`、`docs/BUILD_AND_DEPENDENCIES.md`、`docs/SPECIFICATION.md` は、この独立チェックアウトへの参照・仕様・検証結果を追記した（ForkのGit管理範囲外）。
