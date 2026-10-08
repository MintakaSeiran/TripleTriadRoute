# Triple Triad Route

日本語メニュー認識と、NPCの現在の対戦可否確認を追加した独立改修版です。元作者による公式版・公認版・後継版ではありません。

Based on Auto Triple Triad Grind by XeldarAlz
https://github.com/XeldarAlz/FFXIV-AutoTripleTriadGrind

元ソース: `9bc2e3f45bcf4d837b2a2ed3315a669c811421ed`（上流1.1.1.0）。改修版1.0.0.1、内部名・DLL名 `TripleTriadRoute`。作業ブランチ `fix/japanese-triad-runtime-availability`。

**状態:** ローカル改修・ビルド・自動テスト済み。ゲーム内試験は未実施。GitHub上のFork: [https://github.com/MintakaSeiran/TripleTriadRoute](https://github.com/MintakaSeiran/TripleTriadRoute)。改修は上記作業ブランチに置きます。現在の配布は v1.0.0.1（実機未確認のプレビュー版）です。

## v1.0.0.1: 通常会話だけのNPCを除外

ワワラゴで通常会話を繰り返した後に停止した報告への修正。Talk表示を観測し、選択肢／対戦要求が出ないまま会話が閉じ、操作可能な状態で750ms経過した対話を数えます。同NPCで2回確認したらTriadUnavailableとして今回のセッションから除外し、Collectは代替NPCへ再計画、Farmは次のNPCへ進みます。NPC名や台詞の固定ブラックリストではありません。新規Startでは再確認します。

途中に選択肢があった会話、メニューの認識失敗、対戦項目選択後の不発はこの判定に混ぜません。閉鎖不能な未知UIの安全停止は維持します。実機でのワワラゴ再試験は未実施です。

## 変更内容

- 既知アイコンID（60091、61721、61723）を最優先し、次に既存各言語の文言と日本語の「カード対戦」を大文字小文字を区別しない部分一致で検索します。
- 1項目だけという理由で押すフォールバックを廃止。単独のショップ・クエスト項目も選択しません。
- キャンセルは既知の文言との完全一致（前後空白除去）だけを選びます。「最後の項目だからキャンセル」とは推測しません。
- `NpcEligibility.Check()` のBattle Hall・対戦料・解放条件は維持し、移動前の判定と現地の判定を分離します。
- `TriadUnavailable`（現在トリプルトライアド対戦不可）と `MenuRecognitionFailed`（NPCとの対戦メニューを認識できませんでした）を既存enumの末尾へ追加しました。
- 現地のSelectString／SelectIconStringが既知の「話す」「ショップ」「キャンセル」等だけで構成される場合、キャンセル→閉じたことを確認→750ms待機→同NPCへ再Interactします。2回の内容・アイコン・Addon種別が同一なら対戦不可を確定します。
- 既知対戦キーワード／アイコンがあるのに認識失敗した場合、未対応の文言、空メニュー、内容が変わったメニュー、未知の確認ダイアログは対戦不可にせず認識失敗にします。
- メニューが開かない場合は従来のInteract再試行（最大6回）、NPCへ到達できない場合はUnreachable。対戦項目選択後にChallengeが開かなければ安全に閉じて既存のInteract再試行を使い、TriadUnavailableへ変更しません。
- キャンセルが不明、閉鎖が3秒以内に確認できない場合は対象をスキップし、未知のUIを残したまま他NPCを操作しないためセッションを終了します。手動で閉じてから再Startしてください。

未知の翻訳を「対戦不可」と断定しない保守的な判定です。英語・日本語以外の既知通常項目も一部含みますが、全言語の全会話分岐を網羅していません。未知の翻訳はログを基に追加してください。

## Collect／Farmと履歴

Collectはスキップ理由をセッション内の除外辞書に保持し、スキップ後すぐ再計画します。同じカードの別NPCがあれば再割当し、なければそのカードをUnavailableとして残して他のカードを続けます。従来の「除外理由をすべてUnreachableへ変換する」処理を修正しました。

Farmは現在のセッションで除外済みのNPCと重複指定を除き、スキップ後は次のNPCへ進みます。全対象がTriadUnavailableなら「選択されたNPCは現在トリプルトライアドを受け付けていません」とチャットに表示し、正常終了します。無制限Farmでは従来通り、対戦可能な対象は停止するまで続けます。

除外はAutoTriadSessionが保持し、Pause／Resumeや自動障害復帰でも維持します。新しいStartでは新規セッションとなり再確認します。Configurationへブラックリストを保存しません。

実行中のSkipped NPC表示は既存の色・レイアウトを使用します。終了後はHistory行のツールチップにNPC名・理由、取得できなかったカード名・理由を表示します。対戦0回でスキップだけの周回も履歴へ保存します。保存された履歴は次セッションの除外に使いません。履歴のNPC識別はTriadRowIdと名称、カードはCardIdです。

失敗ログにはNPC名、ENpcBaseId、TriadRowId、現在Territory、Attempt、全項目とアイコンID、検出キーワード、分類と処置を残します。ログ出力は失敗時の確認単位です。

## 名前・導入

主コマンド `/ttroute`、別名 `/triadroute`。サブコマンドは既存の `config`、`stats`、`deps`、`log`、`changelog`、`about`、`pause`、`npcs`、`board`、`request`、`goto` を維持します。

プロジェクト、フォルダー、solution、テスト成果物、AssemblyName、manifest InternalName、表示名、WindowSystem ID、ウィンドウID、ログprefix、throttleキーを分離しました。Dalamudの設定・履歴ディレクトリは新InternalNameから決まり、元版の設定は自動移行しません。C# namespaceとRootNamespaceは差分を抑えるため旧名のままです。公開Plugin IDとは別で、元版の設定保存先を参照するためのものではありません。

元画像・ゲーム素材を使わない独自のカードと経路のアイコンを `TripleTriadRoute/Images/Icon.png` に追加しました。再生成元は `scripts/New-Icon.ps1` です。

Dalamudのカスタムプラグインリポジトリに次のURLを追加し、Triple Triad Routeを検索してください。

```text
https://raw.githubusercontent.com/MintakaSeiran/TripleTriadRoute/fix/japanese-triad-runtime-availability/repo.json
```

リリース: https://github.com/MintakaSeiran/TripleTriadRoute/releases/tag/v1.0.0.1

開発用の導入は、ビルド出力 `TripleTriadRoute/bin/Release/TripleTriadRoute.dll` をDalamud開発用プラグインとして登録する方法です。付属DLL・Localization・Fontsを含む出力一式を保持してください。移動用外部プラグインは `/ttroute deps` で確認できます。元版と同時に自動操作を実行しないでください。

## ビルド・自動テスト

基準: Dalamud.NET.Sdk 15.0.0、API 15、.NET 10、C# 14、x64。依存は上流指定のcroizat.clib 1.0.98、ECommons 3.2.1.20（submodule `f1656e8885eff98d331dcb3136ae8af4d05edfb2`）。ホスト提供DLLは同一Dalamud配布から参照し、別プラグインの出力を混ぜません。

2026-10-08確認環境: `C:\Users\Orion\AppData\Local\Microsoft\dotnet10-sdk\dotnet.exe`、SDK 10.0.401 / MSBuild 18.9.11。参照先 `C:\Users\Orion\AppData\Roaming\XIVLauncher\addon\Hooks\dev`、Dalamud 15.0.3.6、FFXIVClientStructs 7.56.2.9136、Lumina 7.7.0.0 / Lumina.Excel 7.5.1.0。

```powershell
$triadDotnet = 'C:\Users\Orion\AppData\Local\Microsoft\dotnet10-sdk\dotnet.exe'
$triadDalamud = 'C:\Users\Orion\AppData\Roaming\XIVLauncher\addon\Hooks\dev\'
& $triadDotnet --info
git submodule update --init --recursive
& $triadDotnet restore TripleTriadRoute/TripleTriadRoute.csproj --locked-mode "-p:DalamudLibPath=$triadDalamud"
& $triadDotnet build TripleTriadRoute/TripleTriadRoute.csproj -c Release --no-restore -p:LangVersion=14.0 "-p:DalamudLibPath=$triadDalamud"
& $triadDotnet test TripleTriadRoute.Tests/TripleTriadRoute.Tests.csproj -c Release
```

Releaseビルド: 警告0、エラー0。自動テスト60件成功。初回は通信制限によるNU1301と、監査取得失敗のNU1900がありましたが、通信可能な復元で解消しました。監査は無効化していません。

テストは実ソースのメニュー判定、再計画、事前条件、セッション保持と、既存ソルバーを対象にします。ゲームサービス境界はスタブです。実Addon操作、移動IPC、Farm非同期ループ、Challenge→デッキ→対戦の統合動作を証明するものではありません。

## 配布管理

v1.0.0.1のGitHubプレビューリリースへ `latest.zip` を公開し、repo.jsonにそのタグ固有のURLを登録します。数値バージョンは1.0.0.1、APIは15。ゲーム内動作は未確認のため、以下の受入確認が必要です。

今回の配布は検証済みローカルビルドを使用。上流由来の自動タグ、Discord通知、配布ハブ更新は無効のままです。CIはロジックテストのみで、自動リリースworkflowはまだ有効化していません。以後の配布もビルド・ZIP・バージョン整合を確認してから公開してください。

## 実機受入確認（すべて未実施）

| ケース | 確認内容 |
| --- | --- |
| 日本語正常系 | 自動移動→Interact→「カード対戦を申し込む」選択→Challenge→デッキ→1試合完走→再戦→カード取得・登録→次NPC |
| 日本語の対戦不可 | 「話す／キャンセル」および「ショップ／話す／キャンセル」で2回の独立した対話後にTriadUnavailable |
| 安全性 | 単独ショップ、クエスト、テレポ、未知の末尾項目を押さない。キャンセルなし・閉鎖失敗時は対象をスキップして安全終了 |
| 認識失敗 | 既知対戦キーワードを含むメニューで読取り・認識が失敗してもTriadUnavailableにしない。項目ログを確認 |
| 静的条件 | 未解放・Battle Hall・対戦料上限超過は移動前に除外 |
| Collect代替あり | NPC1を除外→同カードをNPC2へ再割当→継続 |
| Collect代替なし | 対象カードだけUnavailable、他カード収集継続、終了後履歴に理由 |
| Farm | 複数指定の途中NPCをスキップ→次NPC、単独対象ならメッセージを出して正常終了 |
| セッション | 同一セッションのResume／障害復帰で再訪しない。新Startでは以前の対象を再確認 |
| Interaction失敗 | NPC不在・Interact不可・無メニュー・対戦選択後Challenge不発をTriadUnavailableと区別し、有限回で終了 |
| 英語とIconメニュー | 英語正常系とSelectIconString、既知アイコン優先、キャンセル動作 |
| ライフサイクル | 新InternalNameで設定保存・再ロード、コマンド、UI、ログ、停止・アンロード、移動IPCの解除 |

ゲームパッチによるAddon構造・アイコン変更、未対応のキャンセル文言、表示途中の読取り、外部プラグインIPC互換性は残存リスクです。表示されない理由（クエスト・時限条件等）は推定しません。改修差分の一覧は [変更報告](CHANGE_REPORT.md) を参照してください。

## ライセンスとクレジット

Copyright (c) 2026 Xeldar Alz。元の [LICENSE.md](LICENSE.md)、[NOTICE](NOTICE)、[TRADEMARK.md](TRADEMARK.md)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) は変更せず保持しています。READMEとAboutの両方に元作者・元リポジトリを明記しています。
