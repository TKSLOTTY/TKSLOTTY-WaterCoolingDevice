# TKSLOTTY Water Cooling Device

## [⬇ Windows App Download — ZIPをダウンロード](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.0.2/WaterCoolingDevice-Windows-v3.0.2-full.zip)

**標準版 v3.0.2・Full版（推奨）／Windows 11 x64／.NET 8同梱**

ZIPをすべて展開し、`WaterCoolingDevice.exe`を実行してください。

| その他のダウンロード | Full（.NET 8同梱） | Minimal |
| --- | --- | --- |
| 標準版 v3.0.2 | [ZIP](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.0.2/WaterCoolingDevice-Windows-v3.0.2-full.zip) | [ZIP](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/v3.0.2/WaterCoolingDevice-Windows-v3.0.2-minimal.zip) |
| LUNE Edition v1.0.0（LUNE画面に切替可能） | [ZIP](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/lune-v1.0.0/WaterCoolingDevice-Windows-LUNE-v1.0.0-full.zip) | [ZIP](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases/download/lune-v1.0.0/WaterCoolingDevice-Windows-LUNE-v1.0.0-minimal.zip) |

Minimal版は.NET 8 Desktop Runtimeの事前インストールが必要です。迷った場合は冒頭のFull版を選んでください。

**[最新版・更新履歴・チェックサム（WindowsアプリのReleases）](https://github.com/TKSLOTTY/WaterCoolingDevice-Windows/releases)**  
Windowsアプリの正式な配布先は上記Releasesです。このリポジトリの旧版Releasesや「Source code (zip)」は、現在のアプリのダウンロード先ではありません。

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

