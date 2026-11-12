# 開發與發佈指南

本文件說明 TaskbarFolders 的開發環境、建置、測試、封裝與 GitHub 發行流程。

## 1. 開發環境

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows 11（建議 22H2 以上） |
| .NET SDK | .NET 10 SDK 10.0.4xx |
| IDE | Visual Studio 2022/2026、JetBrains Rider 或 VS Code + C# Dev Kit |
| Git | 任意近期版本 |

確認環境：

```powershell
dotnet --info
```

## 2. 取得原始碼

```powershell
git clone https://github.com/mark216tw/win-taskbarfolder-01.git
cd TaskbarFolders
```

## 3. 建置

開發與測試一律使用 `Debug`：

```powershell
dotnet build TaskbarFolders.sln
```

若本機已有執行中的 `TaskbarFolders.exe` 鎖住輸出檔，可指定其他輸出目錄：

```powershell
dotnet build TaskbarFolders.sln -p:OutDir=D:\TEMP\TaskbarFolders\build\
```

### 執行

```powershell
dotnet run --project src/TaskbarFolders/TaskbarFolders.csproj
```

若 `dotnet run` 啟動過慢，直接執行建置輸出中的 `TaskbarFolders.exe`。飛出面板模式可手動測試：

```powershell
.\src\TaskbarFolders\bin\Debug\net10.0-windows\TaskbarFolders.exe --group <分類Id>
```

## 4. 測試與格式檢查

```powershell
dotnet test TaskbarFolders.sln
dotnet format TaskbarFolders.sln --verify-no-changes
```

- 測試框架為 xUnit，部分測試依賴 Windows Shell。
- 功能變更完成後，至少執行 `dotnet build` 與 `dotnet test`。
- 提交前執行 `dotnet format --verify-no-changes`，確保格式一致。

## 5. 專案設定

- `Directory.Build.props`：目標框架、nullable、警告視為錯誤、版本與組件資訊。
- `Directory.Packages.props`：所有 NuGet 套件版本。
- `src/TaskbarFolders/TaskbarFolders.csproj`：WPF、應用程式資訊清單、圖示與套件參照。
- `app.manifest`：`asInvoker`、Per-Monitor V2 DPI、long path aware。

不要在 `*.csproj` 寫死套件版本；新增套件時同時更新 `Directory.Packages.props`。

## 6. 版本規則

版本集中於 `Directory.Build.props`：

```xml
<Version>1.8.1</Version>
<AssemblyVersion>1.7.0.0</AssemblyVersion>
<FileVersion>1.8.1.0</FileVersion>
```

- `Version`：`主.次.修`，每次功能變更遞增。
- `FileVersion`：通常跟隨 `Version`。
- `AssemblyVersion`：僅在主次版變更時調整，避免破壞相容性。

同步更新：

- `CHANGELOG.md`：使用者可見的版本異動。
- `RELEASE_NOTES.md`：最新版本給使用者看的說明。
- `docs/DEV_LOG.md`：本次目標、變更、問題與解法、待辦。

## 7. 封裝非 Self-Contained 版本

本專案不提供安裝程式，只發佈可攜式 zip。

### 7.1 發佈 x64

```powershell
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj `
  -c Release -r win-x64 --self-contained false `
  -o publish/win-x64
```

### 7.2 發佈 arm64

```powershell
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj `
  -c Release -r win-arm64 --self-contained false `
  -o publish/win-arm64
```

> Release 且指定 `RuntimeIdentifier` 時，`Directory.Build.props` 會啟用 ReadyToRun。若遇到跨架構編譯問題，可加上 `-p:PublishReadyToRun=false` 後重新發佈。

### 7.3 建立 zip

```powershell
$version = "1.8.1"
Compress-Archive -Path publish/win-x64/* -DestinationPath "publish/TaskbarFolders-$version-win-x64.zip" -Force
Compress-Archive -Path publish/win-arm64/* -DestinationPath "publish/TaskbarFolders-$version-win-arm64.zip" -Force
```

### 7.4 發佈內容檢查

zip 解壓後應包含：

- `TaskbarFolders.exe`
- `TaskbarFolders.dll`
- `TaskbarFolders.deps.json`
- `TaskbarFolders.runtimeconfig.json`
- 相依組件（例如 `CommunityToolkit.Mvvm.dll`、`Microsoft.Extensions.*.dll`）
- `Resources/`（若輸出保留資源目錄）

不應包含：

- `bin/`、`obj/`
- 測試組件
- 本機設定、記錄檔或快取
- 憑證或金鑰

## 8. GitHub 發行流程

1. 確認 `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes` 皆通過。
2. 更新 `Directory.Build.props` 版本與 `CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/DEV_LOG.md`。
3. 提交並推送：
   ```powershell
   git add .
   git commit -m "Release v1.8.1"
   git push origin master
   ```
4. 建立版本標籤：
   ```powershell
   git tag v1.8.1
   git push origin v1.8.1
   ```
5. 使用 GitHub CLI 建立 Release 並上傳 zip：
   ```powershell
   gh release create v1.8.1 `
     "publish/TaskbarFolders-1.8.1-win-x64.zip" `
     "publish/TaskbarFolders-1.8.1-win-arm64.zip" `
     --title "TaskbarFolders 1.8.1" `
     --notes-file RELEASE_NOTES.md
   ```
6. 確認 Release 頁面包含兩個架構的 zip，且標記為 Latest。

## 9. 提交規範

- 提交者：`mark216tw <mark216tw@gmail.com>`。
- 提交訊息簡潔描述變更，例如：
  - `Add Traditional Chinese documentation`
  - `Fix flyout placement on secondary monitor`
  - `Release v1.8.1`
- 不要提交 `bin/`、`obj/`、`.vs/`、`*.user`、`publish/`、記錄檔、快取或任何機密。
- 不要更動無關檔案。

## 10. 程式碼風格

- 4 空格縮排。
- 類別／方法 PascalCase；區域變數／參數 camelCase；私有欄位 `_camelCase`。
- 非同步方法以 `Async` 結尾，函式庫層加 `ConfigureAwait(false)`。
- 使用者可見文字放在 `Resources/Strings.resx`，不要寫死在 XAML 或程式碼。
- UI 執行緒不做 I/O；長工作使用 `Task.Run` 或非同步流程。
- View 不直接 new 服務，透過相依注入取得。

## 11. 疑難排解

| 問題 | 處理方式 |
| --- | --- |
| 建置失敗並提示檔案被鎖定 | 關閉執行中的 `TaskbarFolders.exe`，或改用 `-p:OutDir=` |
| 測試失敗於 Shell 相關項目 | 確認在互動式 Windows 工作階段執行 |
| arm64 發佈失敗 | 確認 SDK 支援 win-arm64；必要時停用 ReadyToRun |
| zip 內含多餘檔案 | 只壓縮 `publish/win-<rid>` 目錄內容，不要包含上層資料夾 |
| SmartScreen 警告 | 未簽章的正常行為；文件已說明 |

## 12. 授權

本專案以 [MIT License](../LICENSE) 釋出。貢獻程式碼即表示同意以相同授權釋出。
