# Water Cooling Device — WCD-01

## 日本語

WaterCoolingDevice.exe を起動してください。初期状態では状態表示・ファン出力設定・OLED表示・設定が使えます。

### LUNE Studio（任意）
設定 →「LUNE Studioを有効にする」でStudioタブが現れます。
サムネイルからテーマを選び、「Windowsに表示」でデスクトップに表示できます。
「次回起動時にもデスクトップに表示」は別設定です。有効化だけでは表示窓は開きません。
「USB液晶にも表示」で対応USB液晶にも表示できます。向き・180度反転・明るさ・接続先を設定してください。
無効にしても保存テーマと設定は残ります。
純正UsbMonitorが起動していると接続が競合します。完全に終了してから接続してください。

### 保存先
設定・保存テーマ: %LOCALAPPDATA%\WaterCoolingDevice
テーマ: LuneThemes\テーマ名\テーマ名.lunetheme
各テーマのBackgrounds、GIFs、layout.jsonは同じテーマフォルダに保存されます。
アプリ内のテーマファイル読込で .lunetheme を登録できます。
アプリフォルダのtheme-assetsは編集用素材です。

### 必要なファイル
EXE・DLL・deps.json・runtimeconfig.jsonを一緒に保持してください。
Tools\PawnIO_setup.exe はハードウェア温度取得用のオプションインストーラーです。
THIRD_PARTY_NOTICES.md に同梱ライブラリの案内があります。
.NET 8 Desktop Runtime（Windows x64）が必要です。

## English

Run WaterCoolingDevice.exe. The default interface provides Status, Fan Output Settings, OLED Display, and Settings.

### Optional LUNE Studio
Enable “LUNE Studio” in Settings to reveal its tab.
Choose a theme thumbnail and click “Show in Windows” to display it on your desktop.
“Show on desktop when the app starts” is independent; enabling Studio alone does not open a display window.
Use “Display on USB monitor” for a compatible USB display. Configure layout, rotation, brightness, and port.
Disabling Studio preserves your themes and settings.
Fully close the vendor's UsbMonitor application before connecting to avoid port conflicts.

### Storage
Settings and saved themes: %LOCALAPPDATA%\WaterCoolingDevice
Themes: LuneThemes\Theme Name\Theme Name.lunetheme
Backgrounds, GIFs, and layout.json are stored alongside each theme.
Import .lunetheme files from Studio. The application's theme-assets folder contains editable source assets.

### Required files
Keep the EXE, DLLs, deps.json, and runtimeconfig.json together.
Tools\PawnIO_setup.exe is the optional hardware temperature driver installer.
See THIRD_PARTY_NOTICES.md for bundled library notices.
Requires the .NET 8 Desktop Runtime for Windows x64.
