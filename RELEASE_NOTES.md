# v1.0.0.1 通常会話だけのNPCをスキップ

ワワラゴなど通常会話だけを繰り返すNPCは、会話が閉じた後750ms待って再確認し、2回ともメニュー・対戦画面がなければ今回のセッションから除外します。Collectは再計画、Farmは次のNPCへ継続します。新規Start時は再確認します。

未知の選択肢は押しません。安全に閉じられない未知UIでは従来の安全停止を維持します。

API15 / .NET10 / x64。ビルド警告0・エラー0、60テスト成功。実機での再確認は未実施。

Based on Auto Triple Triad Grind by XeldarAlz
https://github.com/XeldarAlz/FFXIV-AutoTripleTriadGrind
