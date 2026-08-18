# Water Cooling Device - Windows App Source

購入者が表示、配色、グラフ、レイアウトなどをカスタマイズするためのWindowsアプリソースです。

## 必要な環境

- Windows 11
- Visual Studio 2022（「.NET デスクトップ開発」を選択）または .NET 8 SDK
- インターネット接続（初回のHidSharp復元時）

## ビルド

1. `WaterCoolingDevice.csproj` をVisual Studioで開きます。
2. 構成を `Release`、対象を `x64` にします。
3. 「ビルド」から「ソリューションのビルド」を実行します。

コマンドを使用する場合:

```powershell
dotnet restore
dotnet build -c Release
```

## 主なファイル

- `MainForm.cs`: 画面、タブ、設定、通知領域
- `DutyGraphControl.cs`: ファンカーブグラフ
- `StyledTabControl.cs`: タブの外観
- `HidDeviceClient.cs`: 本体とのUSB HID通信
- `AppSettings.cs`: Windows側設定の保存

## センサー故障表示

本体から取得した水温が `-20°C未満`または`70°C超`の場合、温度欄に
`センサー故障`（英語表示では `Sensor Fault`）と表示します。
通知領域アイコンは赤色の `!` になります。

この版は現在の本体と互換性のあるVendor HID Report ID `7`を使用します。
対応する本体ファームウェアも同じReport IDに設定されています。

接続対象はVendor ID `0x2E8A`、Product ID `0x1144`です。

## PUMPモード

- FAN2端子をPWMポンプ用の固定Duty出力として使用できます。
- 設定範囲は35～100%です。
- PUMPモード中はFAN2カーブ編集が無効になります。
- 回転数が300 RPM未満の状態が5秒続くと、本体のフェイルセーフが出力を100%にします。
- 対応するPWMポンプで、確実に始動・連続回転できるDutyを実機で確認してください。

USB切断または通信エラーを検出すると通信ストリームを閉じ、3秒後から自動的に
再接続を試みます。再接続後は本体設定と現在状態を自動で再取得します。

## 注意

- 通信コマンド、VID/PID、レポート形式を変更すると本体と通信できなくなる場合があります。
- 本体ファームウェアのソースコードは付属しません。
- 改造前にフォルダー全体をバックアップしてください。
- 改造版アプリは製品保証および通常サポートの対象外です。
- 再配布や販売については `../Licenses/SourceLicense.txt` を確認してください。
