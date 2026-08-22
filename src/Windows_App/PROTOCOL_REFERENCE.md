# Water Cooling Device - Vendor HID Interface Reference

この文書は、購入者がWindowsアプリの画面や構成をカスタマイズするための通常機能向けインターフェース仕様です。工場検査、検査証書書き込み、ファームウェア更新などのシステム系コマンドは公開対象に含めません。

## 接続情報

| 項目 | 値 |
| --- | --- |
| USB Vendor ID | `0x2E8A` |
| USB Product ID | `0x1144` |
| Vendor HID Report ID | `7` |
| HIDレポート長 | 64 bytes（Report ID 1 byte + Payload 63 bytes） |
| 整数形式 | 32-bit signed integer / little-endian |
| 推奨タイムアウト | 1500 ms |

同一VID/PIDの本体が複数ある場合は、USB Serial Numberで個体を識別してください。VendorコマンドはARGB2と同じHIDインターフェース上のReport ID `7`を使用します。

## パケット形式

ホストから本体への出力レポート:

| Byte | 内容 |
| --- | --- |
| `0` | Report ID = `7` |
| `1` | Command |
| `2` | Channel |
| `3..6` | Value（Int32 little-endian） |
| `7..63` | `0`で埋める |

本体からホストへの入力レポート:

| Byte | 内容 |
| --- | --- |
| `0` | Report ID = `7` |
| `1` | Response = `Command | 0x80` |
| `2` | 要求と同じChannel |
| `3..6` | Result（Int32 little-endian） |
| `7..63` | 予約領域 |

応答はReport ID、`Command | 0x80`、Channelの3項目が一致するものだけを採用します。不正なChannelやValueでは応答が返らずタイムアウトする場合があります。

## 読み取りコマンド（監視ループで使用可能）

| Command | Channel | Result |
| --- | --- | --- |
| `0x30 GET_FAN_RPM` | `0`=FAN1、`1`=FAN2 | 回転数 RPM |
| `0x31 GET_DUTY` | `0`=FAN1、`1`=FAN2 | 現在Duty `0..100` % |
| `0x32 GET_WATER_TEMP` | `0` | 水温 x100。例: `2834` = 28.34℃。センサー異常は`32767` |
| `0x45 GET_DUTY1_TABLE` | `0..4` | FAN1カーブ各点のDuty % |
| `0x46 GET_DUTY2_TABLE` | `0..4` | FAN2カーブ各点のDuty % |
| `0x47 GET_WARNING_TEMP` | `0` | 警告温度 ℃ |
| `0x4A GET_PUMP_STATUS` | `0` | 実験的PUMP状態（下記参照） |
| `0x4B GET_SENSOR_STATUS` | `0` | `0`=正常、`1`=センサー異常 |
| `0x4C GET_SETTINGS_VERSION` | `0` | 設定変更カウンター |
| `0x4E GET_LED_COUNT` | `0`=ARGB1、`1`=ARGB2 | LED数 `1..64` |
| `0x5A GET_LED_LAYOUT` | `0`=ARGB1、`1`=ARGB2 | LEDレイアウトのパック値 |
| `0x70 PING` | 任意 | 送信したValueをそのまま返す |

通常の状態監視は`0x30`、`0x31`、`0x32`、`0x4B`を使用します。設定画面を自動更新したい場合は`0x4C`を監視し、値が変化したときだけ設定値を再取得します。

## 設定コマンド（変更時だけ使用）

| Command | Channel | Value / 動作 |
| --- | --- | --- |
| `0x41 SET_DUTY1_TABLE` | `0..4` | FAN1カーブDuty `0..100` % |
| `0x42 SET_DUTY2_TABLE` | `0..4` | FAN2カーブDuty `0..100` % |
| `0x44 SET_WARNING_TEMP` | `0` | 警告温度 `0..100` ℃ |
| `0x48 SET_PUMP_MODE` | `0` | `0`=無効、`1`=有効（実験的機能） |
| `0x49 SET_PUMP_DUTY` | `0` | 固定Duty `35..100` %（実験的機能） |
| `0x4D SET_LED_COUNT` | `0`=ARGB1、`1`=ARGB2 | LED数 `1..64` |
| `0x59 SET_LED_LAYOUT` | `0`=ARGB1、`1`=ARGB2 | LEDレイアウトのパック値 |
| `0x4F APPLY_LED_CONFIG` | `0` | Value=`0x0044454C`。ACK後、約1.2秒でUSB再認識 |

SET系は、正規化して本体が受理した値をResultとして返します。送信値と応答値を照合してからUIへ反映してください。

> **重要 - SET系を監視ループへ入れないでください。** これらの設定は本体のEEPROM領域（RP2040内蔵フラッシュ）へ保存されます。ファームウェア側は連続変更を約500 msまとめてから保存しますが、SET系を定期ポーリングすると不要なフラッシュ書き込みを繰り返し、寿命を縮める原因になります。SET系はユーザーが「保存」「適用」を押したとき、または値が実際に変わったときだけ送信してください。監視ループはGET系だけにしてください。

ファンカーブ5点の標準温度は20、30、40、50、60℃です。複数点を書き換える場合は短時間にまとめて送信し、各ACKを確認してください。

## PUMPステータス（実験的機能）

`0x4A`のResult:

- bit 0: PUMPモード有効
- bit 1: 低回転フェイルセーフ発動中
- bits 8..15: 設定Duty %

PUMPモードは主機能・動作保証の対象外です。対応する4ピンPWMポンプで、確実に始動・連続回転できるDutyを実機確認してください。

## LEDレイアウト値

`0x59`と`0x5A`のValue/Resultは次の4 byteをInt32としてパックします。

| Byte | 内容 |
| --- | --- |
| `0` | Layout: `0`=Fan、`1`=Strip、`2`=Matrix |
| `1` | Matrix width |
| `2` | Matrix height |
| `3` | Options bit 0: Serpentine（ジグザグ配線） |

Matrixでは`width x height`が設定済みLED数と一致し、合計64以下である必要があります。LED数とレイアウトを設定してACKを確認後、最後に`0x4F APPLY_LED_CONFIG`を1回だけ送信してください。USBが再認識されるため、現在のHIDストリームを閉じてSerial Numberを基準に再接続します。

## AIへ渡すときの推奨ファイル

- `PROTOCOL_REFERENCE.md`（この仕様）
- `HidDeviceClient.cs`（接続、排他制御、送受信の基準実装）
- `MainForm.cs`（現在のUIと利用例）
- `AppSettings.cs`（Windows側設定）

AIには「VID/PID、Report ID、パケット形式、応答照合、安全表示を変更しない」「SET系を監視ループへ入れない」「PUMPモードを実験的機能として表示する」という条件も一緒に伝えてください。

## 非公開・使用禁止の範囲

`0x50..0x58`は製造、検査証書、復旧処理などに使用するシステム専用範囲です。通常アプリやカスタムUIから送信しないでください。本書に含まれないコマンドは未対応または内部用として扱ってください。
