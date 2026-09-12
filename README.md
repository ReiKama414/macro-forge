# MacroForge

Windows 通用滑鼠巨集管理器（Universal Mouse Macro Manager）。

以 **Raw Input / HID** 辨識實體滑鼠，不綁定任何單一品牌 SDK；可為不同按鍵設定巨集、作用範圍與觸發模式。

> **注意：** 本工具透過 Windows `SendInput` 送出輸入。請勿在啟用 BattleEye、EAC、VAC 等反作弊的線上遊戲中使用，可能違反遊戲條款並導致封號。適合單機、文書或允許巨集的環境。

## 功能

- 枚舉系統上的指向裝置（品牌無關）
- 按鍵學習／掃描、動態滑鼠版面
- 多巨集並行（各自排程，非單一阻塞迴圈）
- 觸發：單次／按住循環／切換／固定次數／定時
- 作用範圍：全域或指定應用程式（離開可暫停／停止／忽略）
- F12 緊急停止全部巨集
- 黑綠賽博龐克風格 WPF 介面

## 環境需求

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 建置

```bash
dotnet build MacroForge.sln -c Release
dotnet test MacroForge.sln
```

## 發佈與執行

```bash
dotnet publish src/MacroForge.App/MacroForge.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/win-x64
```

執行檔：`artifacts/win-x64/MacroForge.exe`

## 專案結構

```
MacroForge.sln
src/
  MacroForge.Core/    # Raw Input、HID、巨集排程、儲存
  MacroForge.App/     # WPF 介面
tests/
  MacroForge.Tests/
```

設定與裝置資料預設存在：`%AppData%\MacroForge\devices\`

## 授權

MIT License — 見 [LICENSE](LICENSE)
