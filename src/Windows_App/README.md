# Water Cooling Device — Windows App Source v3.1.0

標準版とLUNE Studioを統合したC# / Windows Formsソースです。
[利用方法 / User guide](APP_GUIDE.md)・[通信仕様](PROTOCOL_REFERENCE.md)を参照してください。

## Build

Windows x64、.NET 8 SDKが必要です。

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -o publish/full
dotnet publish -c Release -r win-x64 --self-contained false -o publish/minimal
```

Themesフォルダの8テーマは初回登録されます。ユーザー設定は配布しません。
The bundled themes are registered without overwriting user edits. User settings are not distributed.

## Validation

`--test-studio-flow` checks optional Studio activation, language switching, desktop display and disable behavior without saving Studio settings.
`--test-lune-panel` checks the panel protocol and renderer.
`--smoke-test-theme-editor` checks editor startup.
Run validation in a disposable output folder because it generates previews and test results.
