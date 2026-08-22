# Water Cooling Device - PUMP Edition

VID `0x2E8A` / PID `0x1144` の水冷デバイス向けWindowsアプリと、アプリソースの管理用リポジトリです。

## v1.0

- Windowsアプリ: `WaterCoolingDevice-App-v1.0.zip`
- Windowsアプリソース: `WaterCoolingDevice-Source-v1.0.zip`
- USB Serial Numberによる複数台識別・選択
- Windowsアプリの複数起動と同一個体の二重制御防止
- 選択個体を優先した自動再接続
- 日本語・英語の接続ステータス切替

配布ファイルはGitHubのReleasesから取得してください。Windowsアプリのソースは `src/Windows_App` にあります。

## 注意

- PUMPモードはFAN2端子をPWMポンプ用に使用します。
- 利用・改変・再配布の条件は `licenses` 内の文書を確認してください。
- このリポジトリはライセンス条件に配慮してPrivateで管理します。
