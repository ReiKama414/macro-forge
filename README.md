# MacroForge

Windows 通用滑鼠巨集管理器（Universal Mouse Macro Manager）  
作者：[**reiKama414**](https://github.com/ReiKama414/macro-forge) · 授權：MIT

倉庫：https://github.com/ReiKama414/macro-forge

以 **Raw Input / HID** 辨識實體滑鼠，不綁定任何單一品牌 SDK；可為不同按鍵設定巨集、作用範圍與觸發模式。

> **注意：** 本工具透過 Windows `SendInput` 送出輸入。請勿在啟用 BattleEye、EAC、VAC 等反作弊的線上遊戲中使用，可能違反遊戲條款並導致封號。適合單機、文書或允許巨集的環境。

## 功能

- 枚舉系統上的指向裝置（品牌無關）
- 按鍵學習／掃描、動態滑鼠版面
- 多巨集並行（各自排程，非單一阻塞迴圈）
- 觸發：單次／按住循環／切換／固定次數／定時
- 作用範圍：全域或指定應用程式（離開可暫停／停止／忽略）
- F12 緊急停止全部巨集
- 深色控制台與可調重點色（綠／青／藍／紫／橘）
- 風格化安裝程式（聲明、作者、捷徑）

## 環境需求

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（執行發佈檔時）
- 若要自行建置：[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 下載（GitHub Release）

Release 會附兩個**實際可下載的檔案**，以及一個**校驗清單**：

| 檔案 | 用途 |
|------|------|
| `MacroForge-Setup.exe` | 黑綠風格安裝介面（聲明 → 路徑 → 安裝） |
| `MacroForge-win-x64.zip` | 免安裝壓縮包，解壓即可跑 |
| `SHA256SUMS.txt` | **不是程式**，只是雜湊清單，用來確認上面兩個檔沒被竄改 |

### SHA256 是什麼？要下載嗎？

- **要下載的是** Setup 或 zip。
- `SHA256SUMS.txt` 可選下載；用途是核對「你手上的檔」是否等於「作者上傳的檔」。
- 它**不能**取代簽章，也**不會**自動解除 Win11 SmartScreen。

下載後可選驗證：

```powershell
Get-FileHash .\MacroForge-Setup.exe -Algorithm SHA256
# 對照 SHA256SUMS.txt 裡同一檔名的那一行
```

### Win11 顯示「已保護你的電腦」？

未購買 Authenticode 簽章時，SmartScreen 可能警告不明發行者。較安全的作法：

1. 優先用本 repo 自行建置（見下方）
2. 或核對 `SHA256SUMS.txt` 後，再於檔案內容勾選「解除封鎖」／`Unblock-File`
3. 不要關閉整台電腦的 Defender 來硬開

## 建置

```bash
dotnet build MacroForge.sln -c Release
dotnet test MacroForge.sln
```

## 發佈 Release（含安裝程式 + SHA256）

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

產出：`artifacts/release/`

- `MacroForge-Setup.exe`
- `MacroForge-win-x64.zip`
- `SHA256SUMS.txt`

把這三個檔上傳到 GitHub Release 即可。

僅發佈主程式（不安裝包）：

```bash
dotnet publish src/MacroForge.App/MacroForge.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/win-x64
```

## 專案結構

```
MacroForge.sln
src/
  MacroForge.Core/    # Raw Input、HID、巨集排程、儲存
  MacroForge.App/     # WPF 主程式
tools/
  MacroForge.Setup/   # 安裝介面
  MacroForge.VisualCheck/
scripts/
  publish-release.ps1
tests/
  MacroForge.Tests/
```

設定與裝置資料預設存在：`%AppData%\MacroForge\devices\`  
預設安裝路徑：`%LocalAppData%\Programs\MacroForge\`

## 授權

MIT License — 見 [LICENSE](LICENSE)  
Copyright (c) 2026 reiKama414
