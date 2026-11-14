# TaskbarFolders

Windows 11 工作列分類工具。把常用應用程式整理成「分類」，每個分類在工作列上有一個獨立圖示；點擊圖示會在工作列上方展開面板，快速找到並啟動應用程式。

Windows 11 原生不支援工作列資料夾，因此本工具採用「每個分類一個釘選捷徑 + 獨立 AppUserModelID (AUMID)」的方式，讓每個分類成為獨立的工作列按鈕。

## 功能特色

- 建立、改名、刪除分類；分類清單支援拖曳排序，拖曳時有浮動縮影、插入位置指示線與放下動畫
- 從開始功能表匯入傳統捷徑與 Microsoft Store / UWP 應用程式
- 加入檔案、捷徑、網址
- 編輯器支援加入、移除、上下移動、重新命名項目
- 60 個內建分類圖示，分為工作、開發、影音、生活、遊戲、軟體 6 個主題，每主題 10 個；圖示顏色隨機分配
- 每個分類可選擇分類主題（無、黃色護眼、海洋藍、森林綠、薰衣草紫、暖陽橙），套用於該分類的飛出面板
- 工作列上方展開的飛出面板：圖示格、滑鼠與鍵盤操作，高度依可用螢幕空間自動調整；項目可右鍵編輯顯示名稱
- 淺色 / 深色 / 跟隨系統主題
- 多螢幕、Per-Monitor V2 DPI、工作列自動隱藏定位
- 主編輯視窗會記住大小、位置與最大化狀態
- 設定頁可匯出／匯入分類設定（JSON）；匯入會直接覆蓋現有分類並立即同步捷徑
- 設定自動儲存於 `%AppData%\TaskbarFolders\config.json`，並保留 `.bak` 備份
- 儲存時只重建有變更的分類捷徑與圖示；開始功能表掃描結果會快取 60 秒
- 編輯器採單一實例，重複啟動會帶回既有視窗
- 記錄檔超過 1 MB 自動輪替；超過 30 天未用的圖示快取會自動清除

## 系統需求

- Windows 11（建議 22H2 以上；已於 26H2 build 26300 測試）
- .NET 10 Desktop Runtime（非 Self-Contained 版本必須先安裝）
- 架構：x64 或 ARM64

## 下載與安裝

1. 前往 [Releases](https://github.com/mark216tw/win-taskbarfolder-01/releases) 頁面。
2. 依裝置架構下載其中一個壓縮檔：
   - `TaskbarFolders-<版本>-win-x64.zip`（多數 Intel / AMD 電腦）
   - `TaskbarFolders-<版本>-win-arm64.zip`（Windows on ARM 裝置）
3. 解壓縮到固定資料夾（例如 `C:\Apps\TaskbarFolders`），不要直接在壓縮檔內執行。
4. 安裝 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（若尚未安裝）。
5. 執行 `TaskbarFolders.exe`。

> 本專案不提供安裝程式（MSIX / EXE installer），以可攜式 zip 發佈。程式不需要系統管理員權限。
> 執行檔未簽章，首次執行可能出現 SmartScreen 提示，請選擇「仍要執行」。

## 使用流程

1. 啟動 `TaskbarFolders.exe`（不加參數會開啟編輯器視窗）。
2. 按「新增分類」建立分類並輸入名稱；預設會套用內建「資料夾」圖示，點擊名稱左側的分類圖示按鈕可選擇其他圖示；名稱下方可選擇分類主題。左側分類清單可直接拖曳調整順序。
3. 選取分類後：
   - 「從開始功能表匯入」：搜尋並多選應用程式。
   - 「加入檔案」：加入 `.exe`、`.lnk`、`.url` 等。
   - 「加入網址」：建立網頁捷徑。
4. 按「儲存」寫入設定並更新捷徑。
5. 按「產生釘選捷徑」，依畫面指示在檔案總管中對捷徑按右鍵 →「釘選到工作列」。
6. 點擊工作列上的分類圖示，面板會在工作列上方展開；點擊圖示或使用方向鍵＋Enter 即可啟動。
7. 需要調整顯示模式時，按右上角「⋯」開啟設定頁，選好模式後按「儲存」。
8. 同一個設定頁可匯出／匯入分類設定；匯出檔只包含分類資料，匯入會直接覆蓋目前所有分類並立即儲存。

> Windows 11 不允許程式自動把項目釘到工作列，最後一步必須由使用者在檔案總管中手動完成。

## 資料位置

| 內容 | 路徑 |
| --- | --- |
| 設定 | `%AppData%\TaskbarFolders\config.json` |
| 設定備份 | `%AppData%\TaskbarFolders\config.json.bak` |
| 視窗狀態 | `%AppData%\TaskbarFolders\window.json` |
| 釘選捷徑 | `%AppData%\TaskbarFolders\Shortcuts\<分類名稱>.lnk`（同名分類會加上序號） |
| 圖示快取 | `%LocalAppData%\TaskbarFolders\IconCache\` |
| 記錄檔 | `%LocalAppData%\TaskbarFolders\Logs\taskbarfolders.log`（超過 1 MB 會輪替為 `.log.1`） |

## 專案結構

```
src/TaskbarFolders/
  Views/        FlyoutWindow（向上展開）、EditorWindow、SettingsWindow、釘選精靈、選取/輸入對話框
  ViewModels/   Flyout、Editor、分類與項目 ViewModel
  Models/       AppSettings、AppGroup、AppItem、DiscoveredApp
  Services/     Config、Discovery（開始功能表 + shell:AppsFolder）、Icon、Launch、
                Shortcut（IShellLink + AUMID）、TaskbarGeometry、Theme、Dialog
  Helpers/      Win32/COM interop、搜尋、視窗主題、分類主題色盤、內建圖示與 ICO 輸出
  Resources/    字串 (resx)、樣式、淺色/深色色彩字典、圖示
tests/TaskbarFolders.Tests/
```

- 每個分類的 AUMID 格式為 `Mark.TaskbarFolders.Group.<id>`；捷徑與執行中的程序使用相同 AUMID，Windows 才會將其視為同一個工作列按鈕。
- 飛出視窗以 `Shell_TrayWnd` / `Shell_SecondaryTrayWnd` 取得工作列位置，依螢幕與 DPI 換算後貼齊工作列。
- UWP / Store 應用透過 `shell:AppsFolder` 列舉，啟動時使用 `IApplicationActivationManager`。

## 文件導覽

- [使用指南](docs/USER_GUIDE.md)：安裝、操作流程、疑難排解。
- [系統架構與技術文件](docs/ARCHITECTURE.md)：啟動流程、服務職責、Win32/COM 整合、資料流。
- [系統設計文件](docs/DESIGN.md)：需求、使用案例、資料結構、設計決策與風險。
- [開發與發佈指南](docs/DEVELOPMENT.md)：建置、測試、格式檢查、封裝與發行流程。
- [開發紀錄](docs/DEV_LOG.md)：各版本開發過程與問題解法。
- [變更記錄](CHANGELOG.md)、[版本說明](RELEASE_NOTES.md)。

## 建置與測試

```powershell
dotnet build TaskbarFolders.sln
dotnet run --project src/TaskbarFolders/TaskbarFolders.csproj

dotnet test TaskbarFolders.sln
dotnet format TaskbarFolders.sln --verify-no-changes
```

發佈非 Self-Contained 壓縮檔：

```powershell
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj -c Release -r win-arm64 --self-contained false -o publish/win-arm64
```

> `dotnet run` 會先建置再啟動，量測啟動速度請直接執行建置輸出的 `TaskbarFolders.exe`。

## 已知限制

- 最終釘選步驟需手動完成；Windows 更新後可能需重新釘選。
- 重新命名分類會變更捷徑檔名；工作列上已釘選的副本仍可運作，但建議重新產生捷徑並再次釘選。
- 尚未支援巢狀分類、全域熱鍵、系統匣常駐、開機自動啟動、自動更新。
- 已測試單螢幕 100% DPI；多螢幕與高 DPI 的完整矩陣仍建議實機驗證。
- 未簽章的執行檔可能出現 SmartScreen 提示。

## 授權與隱私

本專案以 [MIT License](LICENSE) 授權。

本工具只讀取開始功能表與 Store 應用程式資訊、寫入自己的設定與捷徑；不會上傳任何資料、不收集遙測、不要求系統管理員權限。
