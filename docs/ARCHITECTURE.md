# 系統架構與技術文件

本文件說明 TaskbarFolders 的技術架構、啟動流程、服務職責、資料儲存與 Windows 整合方式。使用操作請參閱[使用指南](USER_GUIDE.md)，需求與設計決策請參閱[系統設計文件](DESIGN.md)。

## 1. 設計背景

Windows 11 移除了工作列資料夾與第三方 deskband 支援，且基於安全性理由不允許程式自動把項目釘到工作列。TaskbarFolders 因此採用「伴隨式啟動器」架構：

- 每個分類是一個獨立的 `.lnk` 捷徑，帶有專屬 AppUserModelID（AUMID）。
- 捷徑與執行中的程式使用相同 AUMID，Windows 會把它們視為同一個工作列按鈕。
- 點擊工作列按鈕時，程式以 `--group <id>` 啟動，載入該分類設定並在工具列上方顯示飛出面板。
- 最終釘選動作由使用者在檔案總管手動完成，不依賴未公開的工作列 API。

這種做法不需要系統管理員權限、不修改系統檔案，也不依賴第三方工作列修補工具。

## 2. 技術基線

| 項目 | 內容 |
| --- | --- |
| 語言 | C#（nullable、file-scoped namespace） |
| 執行環境 | .NET 10（`net10.0-windows`） |
| UI | WPF + MVVM（`CommunityToolkit.Mvvm`） |
| 相依注入 | `Microsoft.Extensions.DependencyInjection`（編輯器）；飛出面板使用精簡手動建構 |
| 序列化 | `System.Text.Json` 來源產生（source generation） |
| 測試 | xUnit |
| 平台 | Windows 11 x64 / ARM64 |
| 發佈 | 非 Self-Contained、架構特定（win-x64 / win-arm64） |

套件版本集中於 `Directory.Packages.props`，版本與組件屬性集中於 `Directory.Build.props`。

## 3. 專案結構

```
TaskbarFolders.sln
Directory.Build.props         共用編譯設定與版本
Directory.Packages.props      套件版本集中管理
src/TaskbarFolders/
  App.xaml / App.xaml.cs      應用程式入口、參數解析、全域例外處理
  Views/                      XAML 視窗與 Adorner
  ViewModels/                 MVVM ViewModel
  Models/                     設定與資料模型
  Services/                   商業邏輯與系統整合
  Helpers/                    Win32/COM interop 與共用工具
  Resources/                  resx 字串、樣式、主題字典、圖示
tests/TaskbarFolders.Tests/   xUnit 測試
docs/                         專案文件
```

## 4. 啟動流程

`App.OnStartup` 會先註冊全域例外處理，再解析命令列參數。

### 4.1 參數解析

`App.ParseGroupId` 支援：

- `TaskbarFolders.exe --group <id>`
- `TaskbarFolders.exe --group=<id>`

沒有 `--group` 時為編輯器模式；有 `--group` 時為飛出面板模式。程式會以 `SetCurrentProcessExplicitAppUserModelID` 設定程序 AUMID：

- 編輯器：`Mark.TaskbarFolders.Editor`
- 飛出面板：`Mark.TaskbarFolders.Group.<id>`

### 4.2 編輯器模式

```
App.OnStartup
  └─ EditorBootstrap.EnsureSingleInstance()
       ├─ 建立具名 Mutex「Local\TaskbarFolders.Editor」
       ├─ 若已存在：設定啟動事件後結束
       └─ 若為第一個：啟動背景執行緒等待 Activate 事件
  └─ EditorBootstrap.StartAsync()
       ├─ 建立 ServiceProvider
       ├─ StartupCoordinator.StartEditorAsync()
       │    ├─ ConfigService.LoadAsync()
       │    ├─ EditorViewModel.InitializeAsync()
       │    ├─ 建立並顯示 EditorWindow
       │    └─ 背景載入全部圖示
       └─ 啟動 FlyoutIpcServer（具名管道 Mark.TaskbarFolders.Flyout）
  └─ 背景執行 IconService.PruneDefaultCache()
```

單一實例機制使用：

- `Mutex`：`Local\TaskbarFolders.Editor`
- `EventWaitHandle`：`Local\TaskbarFolders.Editor.Activate`

第二個執行個體會觸發事件，既有視窗由背景執行緒切回前景，避免同時寫入設定與捷徑。

### 4.3 飛出面板模式

入口由 `Program.Main` 先行判斷，避免每次都完整初始化 WPF：

```
Program.Main(argv)
  └─ CommandLineParser.ParseGroupId(argv)
       ├─ 無 --group：建立 App → app.Run()（編輯器模式）
       └─ 有 --group：
            ├─ FlyoutIpc.IsEditorRunning()（檢查 Local\TaskbarFolders.Editor Mutex）
            │    └─ 是：FlyoutIpc.TrySend(groupId) → 成功則結束（由編輯器代開）
            ├─ FlyoutInstanceGuard.TryAcquire(groupId)
            │    └─ 失敗：代表已有飛出面板程序，直接結束
            ├─ 建立 App → app.Run()
            └─ App.OnStartup → FlyoutLauncher.Show(groupId)
                 ├─ ConfigService.Load()（同步）
                 ├─ 依 id 尋找 AppGroup（找不到顯示錯誤）
                 ├─ 同分類短時間內重複請求只 Activate，不新增視窗
                 ├─ ThemeService.Apply(settings.Theme)
                 ├─ 建立 FlyoutViewModel 與 FlyoutWindow
                 ├─ 套用分類主題色盤
                 ├─ Show()
                 └─ 背景載入圖示
```

編輯器模式會在 `EditorWindow` 顯示後啟動 `FlyoutIpcServer`（具名管道 `Mark.TaskbarFolders.Flyout`）。捷徑觸發的輕量程序只送出 group id 並等待 `OK`，由編輯器以已預熱的服務即時建立飛出面板，不需要重新建立程序。

去重機制：

- `Mutex`：`Local\TaskbarFolders.Flyout.<group hash>`（冷啟動程序唯一）
- `NamedPipeServerStream`：`Mark.TaskbarFolders.Flyout`（`PipeOptions.CurrentUserOnly`）
- 快速連點同一捷徑時，500ms 內的第二次請求只會啟用既有視窗，不會產生第二個。

飛出面板模式不建立 `Microsoft.Extensions.Hosting` 主機，也不載入編輯器相關服務，以縮短冷啟動時間。程序結束時由 `App.OnExit` 釋放 `StaExecutor`、記錄器與實例 Mutex。

## 5. 服務職責

| 服務 | 職責 |
| --- | --- |
| `AppPaths` | 集中管理設定、捷徑、記錄與快取路徑 |
| `ConfigService` | 讀寫 `config.json`；原子寫入、`.bak` 備份、損毀回退；以 `SemaphoreSlim` 序列化存取 |
| `DiscoveryService` | 掃描開始功能表與 `shell:AppsFolder`；60 秒 TTL 快取與去重 |
| `IconService` | 取得應用程式圖示；記憶體與 PNG 磁碟快取；背景清除舊快取 |
| `LaunchService` | 以 `Process.Start` 啟動檔案／網址；以 `IApplicationActivationManager` 啟動 UWP |
| `ShortcutService` | 以 `IShellLinkW` 建立 `.lnk`、寫入 AUMID、產生內建圖示 ICO、增量重建與清理 |
| `GroupTransferService` | 匯出／匯入分類設定 JSON；驗證格式、正規化分類 ID、原子寫入匯出檔 |
| `TaskbarGeometryService` | 取得工作列與螢幕資訊，計算飛出面板位置與可用高度 |
| `WindowPlacementService` | 儲存與還原編輯器視窗大小、位置、最大化狀態，並夾回可用範圍 |
| `ThemeService` | 套用淺色／深色字典；依分類主題覆寫視窗資源 |
| `DialogService` | 封裝確認、錯誤與輸入對話框 |
| `StaExecutor` | 提供單一 STA 執行緒執行 Shell/COM 操作 |
| `FlyoutLauncher` | 建立飛出面板所需的最小服務集合 |
| `EditorBootstrap` | 單一實例、DI 容器與編輯器啟動 |
| `StartupCoordinator` | 載入設定、建立編輯器 ViewModel 與視窗 |
| `FileLoggerProvider` | 寫入檔案記錄，1 MB 輪替 |

## 6. 執行緒模型

- UI 執行緒負責所有 WPF 控制項與視窗操作。
- `StaExecutor` 延遲建立單一 STA 執行緒，供 Shell COM 與圖示操作使用；第一次 `InvokeAsync` 才啟動。
- 圖示磁碟快取讀取、檔案 stat 與部分清理工作在 thread pool 執行；只有快取未命中才進 STA。
- `DiscoveryService` 掃描序列化執行，避免重複掃描造成 UI 卡頓。
- 飛出面板顯示後才載入圖示，避免等待圖示而延後視窗出現。

## 7. 資料模型

```
AppSettings
  Version : int
  Theme   : "System" | "Light" | "Dark"
  Groups  : List<AppGroup>

AppGroup
  Id         : string (GUID N 格式)
  Name       : string
  IconPath   : string?（builtin:<id>）
  ThemeColor : "None" | "Yellow" | "Blue" | "Green" | "Purple" | "Orange"
  Order      : int
  Items      : List<AppItem>
  Aumid      : "Mark.TaskbarFolders.Group.<Id>"（僅運算，不序列化）

AppItem
  Id, Name, Target, Arguments?, WorkingDirectory?
  Kind       : Shortcut | Executable | Url | Uwp
  IconPath?  : string?
  Aumid?     : string?（UWP）
```

設定 JSON 範例：

```json
{
  "Version": 1,
  "Theme": "System",
  "Groups": [
    {
      "Id": "d3f1a0c4b5e6478f9a0b1c2d3e4f5a6b",
      "Name": "開發工具",
      "IconPath": "builtin:development-code",
      "ThemeColor": "Blue",
      "Order": 0,
      "Items": [
        {
          "Id": "0a1b2c3d4e5f60718293a4b5c6d7e8f9",
          "Name": "Visual Studio Code",
          "Target": "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
          "Kind": "Executable"
        }
      ]
    }
  ]
}
```

## 8. 儲存與快取

### 8.1 設定檔

`ConfigService.SaveAsync` 的流程：

1. 取得 `SemaphoreSlim`。
2. 若舊檔存在，複製為 `config.json.bak`。
3. 序列化到 `config.json.tmp`。
4. 以 `File.Move(..., overwrite: true)` 原子取代。

載入時若主檔失敗，會嘗試 `.bak`；兩者都失敗才回傳預設設定。序列化使用 `AppSettingsJsonContext` 來源產生，避免執行期反射。

### 8.2 捷徑與圖示

`%AppData%\TaskbarFolders\Shortcuts\` 內包含：

- `<分類名稱>.lnk`：釘選用捷徑。
- `<groupId>.sig`：捷徑簽章（分類 Id、名稱、圖示參照與執行檔路徑的 SHA-256）。
- `<groupId>.<iconId>.builtin.ico`：內建圖示輸出。
- `<groupId>.auto.ico`：無內建圖示時的字母備援圖示。

增量重建條件：捷徑、簽章、圖示檔都存在，且簽章內容一致時不重建。`SyncAllAsync` 會刪除不再對應任何分類的 `.lnk`、`.ico`、`.sig`。

### 8.3 圖示快取

- 記憶體快取鍵：圖示來源 + 尺寸（不做檔案 stat）。
- 磁碟快取鍵：來源 + 檔案最後寫入時間 + 尺寸，經 SHA-256 取前 40 個十六進位字元作為檔名。
- 超過 30 天未修改的 `.png` 會清除；以 `.lastprune` 標記節流為 24 小時。

### 8.4 記錄

`FileLoggerProvider` 只輸出 `Information` 以上等級，格式為：

```
yyyy-MM-dd HH:mm:ss.fff [Level] Category: Message
```

檔案達 1 MB 時輪替為 `taskbarfolders.log.1`。全域例外（Dispatcher、AppDomain、TaskScheduler）會由 `App.LogFatal` 直接附加寫入。

## 9. Windows 整合

### 9.1 捷徑與 AUMID

- `IShellLinkW` + `IPersistFile` 建立 `.lnk`。
- `SetArguments("--group \"<id>\"")` 讓捷徑啟動對應分類。
- 以 `SHGetPropertyStoreFromParsingName` 取得 `IPropertyStore`，寫入 `System.AppUserModel.ID`。
- 執行中的程序也以相同 AUMID 啟動，Windows 才會合併工作列按鈕。

### 9.2 應用程式探索

- 開始功能表：列舉使用者與全機器 `Programs` 目錄下的 `.lnk`、`.url`、`.appref-ms`。
- Store / UWP：取得 `FOLDERID_AppsFolder` 的 `IShellItem2`，透過 `BHID_EnumItems` 取得 `IEnumShellItems`，讀取 `PKEY_AppUserModel_ID`。
- 名稱若為 `ms-resource:` 或 `@{...}` 等未解析資源字串會跳過。

### 9.3 圖示取得

優先順序：

1. UWP 使用 `shell:AppsFolder\<AUMID>`。
2. 以 `IShellItemImageFactory.GetImage` 取得高品質圖示。
3. 失敗時以 `SHGetFileInfo` 取得檔案圖示。

取得的 `HBITMAP` / `HICON` 會轉為 `BitmapSource` 並 `Freeze()`，可跨執行緒使用。

### 9.4 啟動應用程式

- 一般項目：`ProcessStartInfo { UseShellExecute = true }`，支援檔案、捷徑、網址與參數。
- UWP：`IApplicationActivationManager.ActivateApplication(aumid, ...)`。

### 9.5 工作列定位

`TaskbarGeometryService`：

1. 以 `GetForegroundWindow` 判斷目標螢幕；前景為本程序時退回游標位置。
2. 以 `EnumWindows` 風格的 `FindWindow` / `FindWindowEx` 找出 `Shell_TrayWnd` 與 `Shell_SecondaryTrayWnd`。
3. 以 `GetDpiForWindow` 取得縮放比例，將 DIP 換算為像素。
4. 判斷工作列方向與位置，計算可用高度並夾住請求高度。
5. 以 `SetWindowPos`（`SWP_NOACTIVATE`）套用實際像素位置；WPF 端再換算回 DIP。

### 9.6 視窗外觀

- `WindowThemeHelper` 以 `DwmSetWindowAttribute` 設定圓角與深色標題列。
- 飛出面板不使用 `AllowsTransparency`，避免軟體渲染，改用實色背景 + DWM 圓角。

## 10. 效能策略

- 飛出面板模式不載入 Hosting 與編輯器服務。
- 視窗先顯示，圖示在背景載入。
- 內建圖示延遲渲染，第一次開啟選單才建立 `BitmapSource`。
- 開始功能表掃描結果快取 60 秒。
- 捷徑增量重建，避免每次儲存重寫全部檔案。
- Release + RuntimeIdentifier 啟用 ReadyToRun，改善冷啟動。
- 記錄檔以累計位元組數輪替，避免每次寫入都 stat 檔案。

## 11. 例外處理與記錄

- `DispatcherUnhandledException`：記錄、顯示錯誤、關閉程式。
- `AppDomain.UnhandledException`：記錄。
- `TaskScheduler.UnobservedTaskException`：記錄並標記已觀察。
- 服務層多數錯誤會記錄 Warning 並降級（例如圖示快取失敗不影響啟動）。
- 設定損毀會回退到 `.bak` 或預設值。

## 12. 測試策略

`tests/TaskbarFolders.Tests` 為 xUnit 專案，涵蓋：

- 設定讀寫、雙檔損毀回退、JSON 序列化。
- 開始功能表搜尋與快取 TTL。
- 捷徑檔名淨化、同名去重、AUMID 寫入（需 Windows Shell）。
- 內建圖示目錄與主題目錄完整性。
- 圖示快取清理與 24 小時節流。
- 記錄檔輪替。
- 視窗位置正規化與工作區夾取。

執行：

```powershell
dotnet test TaskbarFolders.sln
```

部分測試依賴 Windows Shell 與檔案系統，僅能在 Windows 執行。

## 13. 建置與發佈

開發使用 `Debug`。功能完成後執行：

```powershell
dotnet build TaskbarFolders.sln
dotnet test TaskbarFolders.sln
dotnet format TaskbarFolders.sln --verify-no-changes
```

發佈非 Self-Contained：

```powershell
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
dotnet publish src/TaskbarFolders/TaskbarFolders.csproj -c Release -r win-arm64 --self-contained false -o publish/win-arm64
```

詳細流程與發行檢查清單請參閱[開發與發佈指南](DEVELOPMENT.md)。

## 14. 安全與隱私

- 不需要系統管理員權限（`asInvoker`）。
- 不連線網際網路、不上傳資料、不收集遙測。
- 只讀取開始功能表、Store 應用程式，以及使用者主動加入的檔案與網址。
- 只寫入 `%AppData%\TaskbarFolders` 與 `%LocalAppData%\TaskbarFolders`。
- 執行檔目前未簽章，發行時應提醒使用者 SmartScreen 行為。
