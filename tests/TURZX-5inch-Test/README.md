# TURZX 5inch テストプログラム / USB Screen Test

[WaterCoolingDevice本家](../../README.md)に付属する独立テストです。

WaterCoolingDeviceとは独立したWindows用C# / Windows Formsテストアプリです。
Windows 10/11の.NET Framework 4.8で実行できます。NuGetや追加SDKは不要です。

## 使い方

1. リポジトリをZIPでダウンロードして展開し、tests/TURZX-5inch-Test/Run.cmdをダブルクリックします。Windows標準のC#コンパイラでビルドして起動します。
2. 初期状態は「プレビューのみ」です。背景表示とカウントダウンを試せます。
3. 実機へ送る場合はメーカーアプリなどCOMを使用するソフトを終了します。
4. 画面上でUsbPCMonitor 5インチ（Rev.A）またはTuring / TURZX 5インチ（Rev.C）を選びます。
5. COM再検出→自動選択、またはCOMを手動選択します。
6. 「プレビューのみ」を外して「接続」を押します。
7. 「背景を送信」で800×480のテスト画像を送信します。
8. 秒数を選び「秒 カウントダウン」を押すと背景を1回送り、中央の300×160領域（X=250、Y=160）だけを更新します。周囲の背景は再送しません。
9. 「画像を開く」でPNG/JPEG/BMPを背景に使えます。縦横比を維持して800×480内に収めます。
10. 機種を変更する際は一度「切断」を押します。

カウントダウンは経過時間を基準にしています。通信が遅い場合は数字をスキップし、同じ転送を重ねません。
停止は進行中の送受信が完了／タイムアウトしてから反映します。

## COM自動検出とログ

WindowsのPnP情報と既知のUSB IDから候補を選択します。候補が0件／複数件の場合は手動選択します。
未知のCOMに問い合わせコマンドを一斉送信しません。接続時に選択した機種のHELLO応答を検証します。
Rev.CのスリープによってCOMが別IDで見える場合はメーカーアプリ等で起動後に再検出してください。
新しいUSB専用TURZX機種（COMポートを作らないモデル）は対象外です。

ログは %LOCALAPPDATA%\UsbScreenTest\Logs に自動保存します。「ログフォルダー」で開けます。
送受信方向・バイト数・先頭の16進数・応答・ROM・更新領域・例外を記録します。
全画像データのダンプはしません。

## 通信実装

- Rev.A: HELLOの02×6を確認し、横向き800×480、RGB565 little endianの矩形転送。
- Rev.C: chs_応答、ROMバージョン取得、250バイト境界のコマンド、全画面BGRA、249バイトごとの区切り挿入。
- Rev.C部分更新: 行アドレスと行幅を付加し、ROM 88以下はBGR24、89以上はBGRA32。
- Rev.C応答を読み、転送処理を直列化します。切断やエラー後は再接続して背景から送信してください。
- 初期輝度は40%です。

実機未接続のため、画面の向き・色・ファームウェア差・USB転送の実動作は未確認です。
自動テストはヘッダー、RGB565、Rev.Cの行アドレス／長さ／終端、区切り、パディングを確認します。

## ビルド

PowerShellで `powershell -ExecutionPolicy Bypass -File .\build.ps1` を実行します。
テストは `Start-Process .\UsbScreenTest.exe -ArgumentList '--self-test' -Wait`。
成功時は同じフォルダーにself-test-result.txtを作成します。

## 出典とライセンス

プロトコル部分は以下のGPL-3.0-or-later実装を参照してC#へ移植しています。
本サンプルもGPL-3.0-or-laterで提供します。WaterCoolingDevice本体への組込み時にはライセンス条件を確認してください。

- https://github.com/mathoudebine/turing-smart-screen-python
- library/lcd/lcd_comm_rev_a.py
- library/lcd/lcd_comm_rev_c.py
- library/lcd/lcd_comm.py
- Copyright (C) 2021 Matthieu Houdebine (mathoudebine)
- Copyright (C) 2023 Alex W. Baulé (alexwbaule)
- Copyright (C) 2023 Arthur Ferrai (arthurferrai)

GPL本文: https://www.gnu.org/licenses/gpl-3.0.html
