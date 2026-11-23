# Water Cooling Device for Windows — v3.4.4

標準版とLUNE Editionを1つのアプリに統合しました。

## ダウンロード / Download

- **[Full版（推奨・.NET 8同梱）](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.4.4/WaterCoolingDevice-Windows-v3.4.4-full.zip)**
- [Minimal版（.NET 8 Desktop Runtimeが必要）](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.4.4/WaterCoolingDevice-Windows-v3.4.4-minimal.zip)
- [更新履歴・SHA-256](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/tag/v3.4.4)

Windows 11 x64向け。ZIPをすべて展開し、WaterCoolingDevice.exeを実行してください。
初回は通常の状態表示で起動します。

## v3.4.4の変更 / Changes

スリープ復帰時のUSB通信待ちによるフリーズを修正し、復帰後に再接続します。v3.4.3のゲームオーバーレイ、温度取得の自動復旧、水温連動ARGBも含みます。

Fixes UI blocking during USB cleanup and reconnects after resume. Includes the v3.4.3 game overlay, telemetry recovery, and coolant ARGB. Simulated recovery tests passed; actual hardware sleep/resume verification is pending.

## LUNE Studio

設定の「LUNE Studioを有効にする」でテーマ機能が使えます。
サムネイルから選択し「Windowsに表示」で開きます。次回起動時の自動表示は別設定です。
対応USB液晶への表示、縦横・180度反転・明るさ、文字・背景・小さなGIFの編集と保存に対応します。
USB表示は動作確認済み3.5インチ機器向けです。すべてのUSB液晶への対応を保証するものではありません。
純正UsbMonitorは完全に終了してから接続してください。

内蔵LUNE・CYBER HUDに加え、White Cooling、Neon Overdrive、Thermionic Console、Soft Signal、Obsidian Flow、Prism Grid、Pop Spark、Pop Spark Chibiを同梱しています。
保存先は `%LOCALAPPDATA%\WaterCoolingDevice\LuneThemes`。既存テーマを上書きせず、削除した同梱テーマも自動復活しません。
再登録したい場合はアプリのThemesフォルダからテーマファイルを読み込めます。

本体の水温連動ファン制御・OLED・ARGB設定は引き続き利用できます。CPU/GPU温度取得には環境依存があります。CPU温度には同梱のPawnIOが必要な場合があります。
本体制御には対応するWater Cooling Device本体・ファームウェアが必要です。ファームウェアの変更・同梱はありません。


## 製品について

水温に連動するUSBファン・ARGBコントローラーです。保存したファン設定は、アプリを終了しても本体で動作します。仕様・写真・接続方法・販売情報は[公式製品ページ](https://tkslotty.github.io/TKSLOTTY-WaterCoolingDevice/)をご覧ください。

## 対応環境

- **Windowsアプリ：** Windows 11 x64と対応するWater Cooling Device本体・ファームウェアが必要です。専用USBドライバは不要です。
- **Dynamic Lighting：** Windows 11標準機能から2系統のARGBを個別に操作できます。ARGB制御用アプリの追加インストールは不要です。SignalRGBは非公式対応です。
- **CPU／GPU温度表示（任意）：** CPU温度取得には同梱のPawnIOセットアップが必要です。環境によって取得できない温度があります。
- **Linux：** 公式設定アプリは現在公開されていません。本体の基本制御はアプリなしで動作します。カスタムUI向けの通信仕様は下記をご覧ください。

## ドキュメント

- [接続方法・使用上の注意](https://tkslotty.github.io/TKSLOTTY-WaterCoolingDevice/#connection)（説明書・検査証・インストール手順は本体の仮想USBドライブにも収録）
- [Windowsアプリの使い方・動作環境](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows#readme)
- [公開C#ソース・ビルド手順](src/Windows_App/README.md)
- [カスタムUI向け通信仕様](src/Windows_App/PROTOCOL_REFERENCE.md)

配線はPCの電源を切って行ってください。ファンは4ピンPWM、ARGBは3PIN 5V専用です（12V RGB接続不可）。

## ライセンス

利用・改変・再配布の条件は[ソースコード利用条件](licenses/SourceLicense.txt)をご確認ください。第三者ソフトウェアは[ライセンス一覧](licenses/ThirdPartyLicenses.txt)および[Windowsアプリの第三者ソフトウェア通知](src/Windows_App/THIRD_PARTY_NOTICES.md)をご覧ください。


## Window preferences / ウィンドウ設定

起動時の最小化は設定で有効にできます（初期値OFF）。LUNE Studioの前回位置の復元は初期値ONです。

Enable **Start minimized in the notification area** in Settings (default: off). **Restore previous window position** in LUNE Studio defaults to on.


## TURZX 5inch テストプログラム

### メインアプリで5インチを試す（v3.4.5-beta.2）

**[Windowsベータ版のZIPをダウンロード](https://github.com/TKSLOTTY/TKSLOTTY-WaterCoolingDevice/releases/download/v3.4.5-beta.2/WaterCoolingDevice-Windows-v3.4.5-beta.2-x64.zip)**（Windows x64、.NET 8 Desktop Runtime x64が必要）

1. 旧版と純正UsbMonitorを終了します。ZIPをすべて展開し、`WaterCoolingDevice.exe` を起動します。
2. 設定で「LUNE Studioを有効にする」をオンにして、LUNE Studioタブを開きます。
3. 「5インチに表示」にチェックを入れて、接続する機種（Rev.A／Rev.C）を選びます。COMは `AUTO` で検索できます。検出できない場合はCOMを手動で選びます。

3.5インチも別のCOMポートで同時に表示できます。横向きの5インチは「5インチを横いっぱいに引き伸ばす」で全画面表示と中央表示を切り替えられます。「5インチプレビュー」を押すと、USB液晶をつながずに表示を確認できます。

5インチの明るさは0〜100%で調整できます。「180°反転」は各USB液晶に個別に適用されます。機種、COM、明るさ、表示設定は次回起動時に復元します。beta.2で追加した明るさと反転のUSB実機動作は確認中です。

### 単体テストプログラム

[独立したWindows用C#テストアプリ](tests/TURZX-5inch-Test/README.md)：UsbPCMonitor 5インチ（Rev.A）／Turing・TURZX 5インチ（Rev.C）を選択し、800×480背景表示・中央カウントダウンの部分更新・COM選択・通信ログ保存を試せます。


**[TURZX 5inch テストプログラム：EXE入りZIPを直接ダウンロード](https://github.com/TKSLOTTY/TKSLOTTY-WaterCoolingDevice/releases/download/turzx-test-v0.1.0/TURZX-5inch-Test-v0.1.0.zip)**

ZIPを展開してUsbScreenTest.exeを起動してください。Rev.A／Rev.C選択・800×480背景・カウントダウン部分更新に対応する独立テスト版です。
