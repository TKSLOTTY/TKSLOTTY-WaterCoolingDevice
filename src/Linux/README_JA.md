# WaterCoolingDevice Ubuntu v0.2.3

Windows v2.2.1 の配置を基準に再構成した Ubuntu/GNOME 版です。

- 「状態表示 / ファン出力編集 / 設定」の3タブ
- 状態表示: 水温・FAN1 Duty/RPM・FAN2/PUMP Duty/RPM + ファンカーブ
- ファン出力編集: 20/30/40/50/60°C の5点Dutyテーブル + マウス編集グラフ
- 設定: 言語 / Ubuntu自動起動 / 温度色 / 警告音 / 常に手前 / PUMP / ARGB LED構成
- 最小化または閉じるとトレイへ常駐（AppIndicatorが使える場合）
- トップバー/トレイに現在水温を表示
- USB HID Interface 4 / Report ID 7

## 依存関係
```bash
sudo apt update
sudo apt install python3-hid python3-gi gir1.2-gtk-3.0 gir1.2-ayatanaappindicator3-0.1
```

`libayatana-appindicator is deprecated` の警告は、現版でGNOME通知領域互換のためAppIndicatorを使用していることによる警告で、実行エラーではありません。

## 起動
```bash
python3 watercoolingdevice.py
```


## v0.2.3
- Englishチェックを即時反映し、日本語/英語の混在を修正。
- ARGB Fan Ring / LED StripではMatrixの幅・高さ・ジグザグ設定を非表示。Matrix選択時のみ表示。
- install.shにpython3-cairo / python3-gi-cairoを追加。
