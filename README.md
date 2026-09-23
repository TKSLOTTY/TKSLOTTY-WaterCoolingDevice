# Water Cooling Device for Windows — v3.1.1

標準版とLUNE Editionを1つのアプリに統合しました。

## ダウンロード / Download

- **[Full版（推奨・.NET 8同梱）](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.1.1/WaterCoolingDevice-Windows-v3.1.1-full.zip)**
- [Minimal版（.NET 8 Desktop Runtimeが必要）](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.1.1/WaterCoolingDevice-Windows-v3.1.1-minimal.zip)
- [更新履歴・SHA-256](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/tag/v3.1.1)

Windows 11 x64向け。ZIPをすべて展開し、WaterCoolingDevice.exeを実行してください。
初回は通常の状態表示で起動します。

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
