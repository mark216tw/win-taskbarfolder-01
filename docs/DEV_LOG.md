# 開發紀錄

## 2026-11-21 - v1.13.0

### 目標

「加入檔案」後改用執行檔的「檔案描述」（FileDescription）作為項目名稱，讓清單顯示更接近使用者熟悉的程式名稱。

### 變更

- `Services/AppFileInfoService.cs`：新增服務，於背景執行緒讀取 `FileVersionInfo.FileDescription`（空白或例外時回傳 `null`）；`.lnk` 會透過 STA 解析目標執行檔再讀取描述，並跳過帶有 AppUserModelID 的捷徑。
- `Helpers/ShortcutLinkReader.cs`：抽出 `.lnk` 讀取邏輯（目標、參數、工作目錄、`shell:AppsFolder` AUMID 與 AppUserModelID 屬性），供探索與命名共用。
- `Services/DiscoveryService.cs`：`CreateFromLink` 改用 `ShortcutLinkReader`，行為維持不變；移除重複的 COM 讀取程式碼。
- `ViewModels/EditorViewModel.cs`：`AddFileAsync` 先以檔名建立項目並載入圖示，同時非同步套用檔案描述，描述為空時保留檔名。
- `Services/StartupCoordinator.cs`、`Services/EditorBootstrap.cs`：注入並註冊 `AppFileInfoService`。
- `tests/TaskbarFolders.Tests/AppFileInfoServiceTests.cs`：新增描述讀取、非 PE 檔、檔案不存在、`.lnk` 目標解析與 AUMID 捷徑維持檔名等測試。
- `Directory.Build.props`：版本 1.13.0、`FileVersion` 1.13.0.0（`AssemblyVersion` 維持 1.7.0.0）。

### 問題與解法

- 非 PE 的 `.exe` 檔案呼叫 `FileVersionInfo.GetVersionInfo` 不會丟例外，而是回傳空描述，因此統一以「空白視為失敗」並 fallback 檔名。
- UWP／Store 捷徑的目標常是 `explorer.exe`，直接讀描述會得到錯誤名稱；改為偵測 AUMID（參數中的 `shell:AppsFolder` 或捷徑屬性）後不覆寫名稱。
- `FileVersionInfo` 可能因網路磁碟或損毀檔而阻塞，改在 `Task.Run` 執行，避免占用 UI 執行緒。

### 待辦 / 已知限制

- 尚未實測各種第三方安裝程式建立的 `.lnk`（含自訂 AppUserModelID）在真實開始功能表中的命名結果。

## 2026-11-20 - v1.12.2

### 目標

提高應用程式啟動速度，並降低常駐背景時的電腦記憶體使用量。

### 變更

- `Program.cs`、`Services/AppPaths.cs`：啟用 .NET Multicore JIT（`ProfileOptimization`），為編輯器與飛出面板分別建立 JIT 設定檔以加速後續冷啟動。
- `TaskbarFolders.csproj`：明確設定 Workstation GC、停用 `RetainVMGarbageCollection` 並啟用 `TieredPGO`，讓釋放的記憶體頁能歸還作業系統。
- `Services/FileLoggerProvider.cs`、`Services/ConfigService.cs`、`Services/IconService.cs`、`Services/ShortcutService.cs`：移除建構函式中的同步磁碟 I/O（目錄建立與記錄檔輪替檢查延後至首次寫入）。
- `Services/StartupCoordinator.cs`、`Views/EditorWindow.xaml.cs`：並行非同步載入 `config.json` 與 `window.json`，避免視窗建構時在 UI 執行緒同步讀檔。
- `ViewModels/EditorViewModel.cs`：延遲建立內建圖示與分類主題選單集合（首次開啟選單才建立）；啟動時僅載入目前選取分類的圖示，其餘分類切換時才載入。
- `Helpers/BuiltInIconCatalog.cs`、`ViewModels/BuiltInIconOption.cs`：新增 `CreatePreviewImage`（64×64 預覽圖，較原本 256×256 節省約 16 倍像素記憶體），`SaveIco` 改為即時繪製 256×256 而不常駐快取；新增 `ClearCaches`。
- `Services/IconService.cs`：過大的 Shell 圖示與既有磁碟快取 PNG 依顯示尺寸上限（48–96 px）縮放／解碼；新增 `ClearMemoryCache`。
- `Services/EditorBootstrap.cs`、`Services/FlyoutIpc.cs`：單一實例喚醒改用 `ThreadPool.RegisterWaitForSingleObject`（省去常駐執行緒）；IPC 監聽實例由 4 降為 2 並直接以非同步 I/O 啟動。
- `Services/MemoryTrimService.cs`、`Helpers/NativeMethods.cs`、`Services/FlyoutLauncher.cs`、`Views/FlyoutWindow.xaml.cs`：新增常駐記憶體整理服務，於視窗最小化（2 秒）、飛出面板關閉（15 秒）或視窗退至背景（30 秒）時清除暫存快取、釋放非選取分類圖示、執行壓縮 GC 並以 `SetProcessWorkingSetSizeEx` 裁減工作集；飛出面板關閉時解除事件與 `DataContext`。
- `tests/TaskbarFolders.Tests/`：新增 `MemoryTrimServiceTests`、內建圖示 64×64 預覽與快取清除測試、圖示解碼上限測試。
- `Directory.Build.props`：版本 1.12.2、`FileVersion` 1.12.2.0（`AssemblyVersion` 維持 1.7.0.0）。

### 問題與解法

- `BuiltInIconCatalogTests` 驗證 `CreateImage` 回傳 256×256 影像 → 保留 `CreateImage` 原行為，另提供 `CreatePreviewImage` 給 UI 預覽使用。
- 避免對小圖示進行放大失真 → `IconService.TryLoadFromDisk` 先讀取 PNG IHDR 寬高，僅在原圖大於目標像素上限時設定 `DecodePixelWidth`。

### 待辦 / 已知限制

- 尚未於實機工作管理員量測長時間常駐後的實際工作集曲線與各 DPI 下縮放圖示清晰度。

## 2026-11-14 - v1.12.1

### 目標

讓編輯器的分類圖示預覽不再套用分類主題色，改用與圖示選擇面板一致的深灰底色；工作列捷徑圖示仍跟隨分類主題色。

### 變更

- `ViewModels/EditorViewModel.cs`：`LoadGroupIconAsync` 改以 `BuiltInIconCatalog.CreateImage` 的預設深灰底色繪製預覽；移除 `ThemeColor` 變更時的預覽重繪。
- `Resources/Strings.resx`：圖示選擇面板提示改為「工作列捷徑圖示底色會跟隨分類主題色」。
- `README.md`、`docs/USER_GUIDE.md`：更新圖示底色說明，區分編輯器預覽與工作列捷徑圖示。
- `Directory.Build.props`：版本 1.12.1、`FileVersion` 1.12.1.0（`AssemblyVersion` 維持 1.7.0.0）。

### 問題與解法

- 分類圖示預覽與工作列 `.ico` 原本共用 `GroupThemeCatalog.ResolveIconBackground`；只在預覽端省略底色參數，讓兩者需求分開，不必調整捷徑產生邏輯。

### 待辦 / 已知限制

- 尚未實機確認深色模式下預覽深灰底色與圖示線條的對比度。

## 2026-11-14 - v1.12.0

### 目標

讓分類圖示底色自動跟隨分類主題色；圖示選單內的圖示底色統一為深灰。

### 變更

- `Helpers/BuiltInIconCatalog.cs`：新增 `DefaultBackground`（`#4B5563`）；`CreateImage`／`SaveIco` 接受底色參數，記憶體快取鍵改為「圖示 Id + 底色」；移除固定 `IconColors` 與 `PickColor`。
- `Helpers/BuiltInIconDefinition.cs`：移除已不再使用的 `Color` 欄位。
- `Helpers/GroupThemeCatalog.cs`：新增 `ResolveIconBackground`，有主題色時回傳該主題 `Swatch`，否則回傳深灰。
- `ViewModels/EditorViewModel.cs`：分類圖示預覽改依 `ThemeColor` 取底色；切換主題色時即時重繪。
- `Services/ShortcutService.cs`：產生的 `.ico` 使用主題色為底色；檔名加入主題色識別（舊色檔由既有清理邏輯移除）；簽章納入主題色；字母備援圖示也使用主題色。
- `Views/EditorWindow.xaml`、`Resources/Strings.resx`：圖示選單新增「圖示底色會跟隨分類主題色」提示。
- `tests/TaskbarFolders.Tests/`：更新圖示底色、快取分離、主題色變更重建 `.ico` 等測試。
- `Directory.Build.props`：版本 1.12.0、`FileVersion` 1.12.0.0（`AssemblyVersion` 維持 1.7.0.0）。

### 問題與解法

- 原本 `IconService` 的圖示快取與分類圖示無關，無需調整；分類圖示快取只在 `BuiltInIconCatalog`，將底色加入鍵值即可避免換色後拿到舊圖。
- `.ico` 若沿用固定檔名，換色後會因檔案已存在而跳過重繪；改以主題色納入檔名並讓既有 `CleanupStaleBuiltInIcons` 清除舊檔。

### 待辦 / 已知限制

- 尚未實機檢視各主題色在深色模式與高 DPI 下的圖示對比度。

## 2026-11-14 - v1.11.0

### 目標

再擴充分類主題顏色，新增青檸綠、深海藍、洋紅、珊瑚橘、酒紅 5 色。

### 變更

- `Helpers/GroupThemeCatalog.cs`：新增 5 組主題，皆含淺色與深色色盤；主題總數 17。
- `Resources/Strings.resx`：新增 5 個主題名稱字串，供無障礙名稱使用。
- `tests/TaskbarFolders.Tests/GroupThemeCatalogTests.cs`：預設主題數量更新為 17。
- `Directory.Build.props`：版本 1.11.0、`FileVersion` 1.11.0.0（`AssemblyVersion` 維持 1.7.0.0）。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/ARCHITECTURE.md`：更新主題數量與顏色清單。

### 問題與解法

- 色塊選單為 6 欄配置，加入 5 色後共 18 個選項（含「無」），剛好形成 3 列，不需調整 `WrapPanel` 寬度。

### 待辦 / 已知限制

- 尚未實機檢視 17 色色塊選單在深色模式與高 DPI 下的顯示。

## 2026-11-14 - v1.10.0

### 目標

擴充分類主題顏色，並將主題選擇改為與圖示選單一致的色塊彈出視窗；同時把分類圖示與分類主題按鈕排在同一列。

### 變更

- `Helpers/GroupThemeCatalog.cs`：新增玫瑰紅、櫻花粉、湖水綠、天空青、靛藍、咖啡棕、石墨灰 7 組主題，皆含淺色與深色色盤。
- `Resources/Strings.resx`：新增 7 個主題名稱字串，供無障礙名稱使用。
- `ViewModels/GroupThemeOption.cs`：改為 `ObservableObject`，新增 `IsSelected` 與 `IsNone`。
- `ViewModels/GroupViewModel.cs`：新增 `ThemePreviewBrush`、`IsThemeNone`，隨 `ThemeColor` 更新。
- `ViewModels/EditorViewModel.cs`：新增 `IsThemePickerOpen`、`OpenThemePickerCommand`、`SelectThemeColorCommand` 與 `SyncThemeSelection`；切換分類時關閉主題選單並同步選取狀態。
- `Views/EditorWindow.xaml`：移除主題 `ComboBox`，改為色塊 Popup；分類圖示與分類主題按鈕並列，名稱輸入框改為整列寬度。
- `tests/TaskbarFolders.Tests/GroupThemeCatalogTests.cs`：預設主題數量更新為 12。
- `Directory.Build.props`：版本 1.10.0、`FileVersion` 1.10.0.0（`AssemblyVersion` 維持 1.7.0.0）。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`、`docs/ARCHITECTURE.md`：更新主題數量、色塊選單與版面說明。

### 問題與解法

- 主題選單不再顯示名稱，仍需保留無障礙標示 → 繼續以字串資源建立 `GroupThemeOption.Name`，僅供 `AutomationProperties.Name` 使用。
- 「無」主題沒有代表色 → 以色塊加斜線表示，並以 `IsThemeNone` 控制斜線顯示。

### 待辦 / 已知限制

- 尚未實機檢視 12 色色塊選單在深色模式、高 DPI 與小螢幕下的換行與位置。

## 2026-11-14 - v1.9.3

### 目標

在設定頁新增「關於」區塊，讓使用者可直接查看版本、授權與隱私資訊。

### 變更

- `Helpers/AboutInfo.cs`（新增）：由組件屬性讀取資訊版本、檔案版本與著作權；資訊版本會移除 `+` 後的建置中繼資料。
- `ViewModels/EditorViewModel.cs`：新增 `AppVersion`、`AppFileVersion`、`AppCopyright` 唯讀屬性供設定頁繫結。
- `Views/SettingsWindow.xaml`：在分類設定下方新增「關於」卡片，顯示應用程式名稱、版本、檔案版本、授權、著作權與隱私說明。
- `Resources/Strings.resx`：新增「關於」區塊相關繁體中文字串。
- `Directory.Build.props`：加入 `Copyright` 組件屬性；版本 1.9.3、`FileVersion` 1.9.3.0（`AssemblyVersion` 維持 1.7.0.0）。
- `tests/TaskbarFolders.Tests/AboutInfoTests.cs`（新增）：驗證版本、檔案版本與著作權皆取自組件屬性。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`、`docs/DEVELOPMENT.md`：補充關於區塊說明並更新版本範例。

### 問題與解法

- 資訊版本可能包含 `+<commit>` 中繼資料，直接顯示會過長 → 顯示前移除 `+` 之後的內容。
- 設定頁沿用 `EditorViewModel` 作為 `DataContext` → 以唯讀屬性提供版本資訊，避免在 XAML 直接呼叫組件反射。

### 待辦 / 已知限制

- 尚未實機檢視「關於」卡片在深色模式與高 DPI 下的間距。

## 2026-11-14 - v1.9.2

### 目標

改善工作列捷徑飛出面板的開啟速度，並解決快速連點時同時出現多個飛出面板的問題。

### 變更

- `Program.cs`：新增輕量入口，先解析 `--group`；編輯器執行中時透過具名管道送出請求後直接結束，不初始化 WPF。冷啟動則先取得具名 Mutex 去重，再建立 `App`。
- `Helpers/CommandLineParser.cs`：從 `App` 抽出 `--group`／`--group=` 解析邏輯。
- `Services/FlyoutIpc.cs`：新增具名管道客戶端與伺服器（`Mark.TaskbarFolders.Flyout`、`PipeOptions.CurrentUserOnly`），客戶端等待 `OK` 才視為成功。
- `Services/FlyoutInstanceGuard.cs`：以分組 ID 雜湊建立 `Local\TaskbarFolders.Flyout.<hash>` Mutex，避免冷啟動重複程序。
- `Services/FlyoutLauncher.cs`：支援由 DI 注入服務（`CreateForHost`），新增 `Show` 同步流程；同分類 500ms 內重複請求只啟用既有視窗；先顯示視窗再載入圖示。
- `Services/EditorBootstrap.cs`：編輯器視窗顯示後啟動 `FlyoutIpcServer`，將請求派送至 UI 執行緒顯示飛出面板；DI 註冊 `FlyoutLauncher`。
- `Services/ConfigService.cs`：新增同步 `Load()` 供飛出面板冷啟動使用。
- `Services/ThemeService.cs`：主題未變更時跳過重建資源字典。
- `TaskbarFolders.csproj`：設定 `StartupObject` 為 `Mark.TaskbarFolders.Program`。
- `AssemblyInfo.cs`：加入 `InternalsVisibleTo("TaskbarFolders.Tests")`。
- 新增 `CommandLineParserTests`、`FlyoutInstanceGuardTests`、`FlyoutIpcTests`。
- `Directory.Build.props`：版本 1.9.2、`FileVersion` 1.9.2.0（`AssemblyVersion` 維持 1.7.0.0）。

### 問題與解法

- 具名管道的 `PipeStream.ReadTimeout` 在 Windows 同步管道上會拋出 `InvalidOperationException`（Timeouts are not supported on this stream），改用 `PipeOptions.Asynchronous` 搭配 `CancellationTokenSource` 控制連線與讀取逾時。

### 待辦 / 已知限制

- 編輯器開著 modal 對話框時，飛出面板可能顯示在對話框後方。
- 飛出面板內改名與編輯器未儲存狀態仍可能競爭寫入 `config.json`（與舊版跨程序行為相同）。

## 2026-11-14 - v1.9.1

### 目標

調整內建圖示為 60 個、6 個主題，移除水果與動物並新增軟體；圖示顏色改為隨機分配，選單標題與圖示同列，設定頁儲存按鈕套用分類主題色。

### 變更

- `Helpers/BuiltInIconCatalog.cs`：主題改為工作、開發、影音、生活、遊戲、軟體；移除水果與動物共 20 個圖示，新增軟體主題 10 個圖示；以固定雜湊從色盤隨機分配每個圖示的顏色。
- `Helpers/BuiltInIconTheme.cs`：移除固定主題色欄位。
- `Views/EditorWindow.xaml`：圖示選單改為主題標題與 10 個圖示同一列，並調整捲動高度。
- `Services/ThemeService.cs`：新增 `ApplyAccentTheme`，只覆寫視窗的強調色資源。
- `Views/SettingsWindow.xaml.cs`：套用目前分類主題色到設定視窗；顯示模式變更時重新套用。
- `Resources/Strings.resx`：新增軟體主題與 10 個軟體圖示名稱，移除水果與動物字串。
- `tests/TaskbarFolders.Tests/BuiltInIconCatalogTests.cs`：更新為 60 個圖示、6 個主題，新增顏色多樣性與已移除主題回退測試。
- `Directory.Build.props`：版本 1.9.1、`FileVersion` 1.9.1.0（`AssemblyVersion` 維持 1.7.0.0）。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`：更新圖示數量、主題、選單配置與設定頁按鈕說明。

### 問題與解法

- 靜態初始化順序導致 `IconColors` 尚未建立就被 `CreateIcons` 使用 → 將色盤宣告移到 `All` 之前。
- 若使用每次啟動都不同的亂數，編輯器與飛出面板可能顯示不同顏色 → 以圖示 ID 的固定雜湊選色，外觀隨機但跨程序一致。

### 待辦 / 已知限制

- 尚未實機檢視 6 個主題與新選單列在各螢幕解析度下的顯示。
- 分類主題為「無」時，設定頁儲存按鈕使用全域強調色。

## 2026-11-14 - v1.9.0

### 目標

將內建分類圖示改為 7 個主題、每主題 10 個，共 70 個；圖示選擇面板依主題分區，並讓舊版圖示 ID 平順回退。

### 變更

- `Helpers/BuiltInIconTheme.cs`：新增圖示主題模型（ID、名稱資源鍵、代表色）。
- `Helpers/BuiltInIconDefinition.cs`：新增 `ThemeId`。
- `Helpers/BuiltInIconCatalog.cs`：以 70 個新圖示取代舊目錄，分為工作、開發、影音、生活、遊戲、水果、動物；新增 `IsKnown`、`NormalizeReference`，預設圖示改為 `work-folder`。
- `ViewModels/BuiltInIconThemeGroup.cs`：新增分區顯示用的主題群組。
- `ViewModels/BuiltInIconOption.cs`、`EditorViewModel.cs`：建立主題分組集合，載入／匯入／儲存改用 `IsKnown` 與 `NormalizeReference` 修復舊設定。
- `Views/EditorWindow.xaml`：圖示選擇面板改為 7 個主題區塊，每區一列 10 個圖示，加入垂直捲動。
- `Services/ShortcutService.cs`：產生捷徑前將未知內建圖示正規化為預設圖示。
- `Resources/Strings.resx`：新增 7 個主題與 70 個圖示名稱，移除舊圖示字串。
- `tests/TaskbarFolders.Tests/BuiltInIconCatalogTests.cs`：新增 70 個圖示、7 個主題、每主題 10 個、舊 ID 回退與全部渲染測試。
- `Directory.Build.props`：版本 1.9.0、`FileVersion` 1.9.0.0（`AssemblyVersion` 維持 1.7.0.0）。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`、`docs/ARCHITECTURE.md`：更新圖示數量、主題與範例。

### 問題與解法

- 巢狀 `ItemsControl` 內的按鈕找不到外層 `EditorViewModel` 的指令 → 將 `RelativeSource` 改為 `AncestorLevel=2` 取得外層 `ItemsControl`。
- 舊版 `builtin:*` 仍符合前綴判斷，但已不在新目錄中，可能導致渲染失敗 → 新增 `IsKnown` 與 `NormalizeReference`，於載入與捷徑產生時回退預設圖示。
- 70 個新路徑需確認都能被 WPF 解析 → 以 `RendersAllDefinitions` 實際渲染所有圖示驗證。

### 待辦 / 已知限制

- 尚未實機檢視 7 個主題在小螢幕上的彈出面板位置與捲動體驗。
- 舊設定會在下次載入並儲存後才寫回新圖示 ID。

## 2026-11-13 - v1.8.6

### 目標

讓飛出面板可直接編輯應用程式顯示名稱，並修正編輯器分類圖示按鈕未對齊名稱輸入框的問題。

### 變更

- `Views/EditorWindow.xaml`：名稱標籤、圖示按鈕／名稱輸入框、分類主題改為分列配置；圖示按鈕維持左側但與名稱輸入框垂直置中。
- `Services/ConfigService.cs`：新增 `RenameItemAsync`，在單一鎖內載入、修改指定項目名稱並儲存；抽出 `SaveCoreAsync` 共用寫檔與 `.bak` 備份。
- `ViewModels/FlyoutViewModel.cs`：注入 `ConfigService` 與分類 ID，新增 `RenameItemAsync` 更新畫面名稱並持久化。
- `Services/FlyoutLauncher.cs`：建立飛出面板 ViewModel 時傳入設定服務。
- `Views/FlyoutWindow.xaml`、`FlyoutWindow.xaml.cs`：項目右鍵選單「編輯顯示名稱」，以既有 `InputDialog` 修改；選單與對話框期間暫停失焦關閉，失敗時顯示錯誤。
- `Resources/Strings.resx`：新增編輯顯示名稱相關字串。
- `Resources/Styles.xaml`：新增 `ContextMenu`、`MenuItem` 主題樣式，支援淺色與深色模式。
- `tests/TaskbarFolders.Tests/ConfigServiceTests.cs`：新增改名持久化與找不到項目時回傳 `false` 的測試。
- `Directory.Build.props`：版本 1.8.6、`FileVersion` 1.8.6.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`：補充功能與操作說明。

### 問題與解法

- 飛出面板在開啟右鍵選單或對話框時會因失焦自動關閉 → 開啟選單與對話框期間暫停失焦關閉，關閉後再恢復焦點。
- 右鍵選單的 `DataContext` 不一定能直接由 `MenuItem` 取得 → 改由 `ContextMenu.PlacementTarget` 取得被右鍵的項目。
- 直接寫入飛出面板啟動時載入的舊設定，可能蓋掉期間其他程序已儲存的變更 → 改名時重新載入最新設定再修改單一項目名稱。

### 待辦 / 已知限制

- 主編輯器若同時開著且有未儲存變更，之後在主編輯器儲存仍可能覆蓋飛出面板的改名；目前以文件說明，尚未加入跨程序即時同步。
- 尚未實測高 DPI 與 Windows 10 上的右鍵選單樣式。

## 2026-11-13 - v1.8.5

### 目標

設定頁沒有調整任何設定時，停用「儲存」按鈕，避免無意義的儲存操作。

### 變更

- `Views/SettingsWindow.xaml`：儲存按鈕的 `IsEnabled` 繫結 `HasUnsavedSettingsChanges`。
- `ViewModels/EditorViewModel.cs`：顯示模式變更時與上次儲存值比較，相同時不標記為未儲存（改回原值也會停用）。
- `Directory.Build.props`：版本 1.8.5、`FileVersion` 1.8.5.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`：補充儲存按鈕停用規則。

### 問題與解法

- 原本只要顯示模式變更過就視為未儲存，即使改回原值仍可儲存 → 改為與已儲存值比較，無實際差異時停用按鈕。

### 待辦 / 已知限制

- 無。

## 2026-11-13 - v1.8.4

### 目標

調整設定頁儲存成功後的提示訊息，避免沿用主畫面「捷徑已更新」的字樣造成誤解。

### 變更

- `Resources/Strings.resx`：新增 `SettingsSaved`（「設定已儲存。」）。
- `ViewModels/EditorViewModel.cs`：設定頁儲存成功改用 `SettingsSaved`；主畫面儲存維持 `StatusSaved`。
- `Directory.Build.props`：版本 1.8.4、`FileVersion` 1.8.4.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：補充提示文字調整。

### 問題與解法

- 設定頁儲存原本共用主畫面 `StatusSaved`，訊息包含「捷徑已更新」，但設定頁儲存不會重建捷徑 → 新增設定頁專用字串。

### 待辦 / 已知限制

- 無。

## 2026-11-13 - v1.8.3

### 目標

將設定頁與主畫面的儲存分開處理，避免兩邊互相覆蓋或讓未儲存的顯示模式影響主畫面的儲存狀態。

### 變更

- `ViewModels/EditorViewModel.cs`：新增 `HasUnsavedSettingsChanges` 與 `SaveSettingsCommand`；顯示模式變更只標記設定頁未儲存；新增 `RevertThemeChanges`；主畫面儲存改用上次儲存的顯示模式，不再寫入目前 `ThemeMode`。
- `Views/SettingsWindow.xaml.cs`：儲存改用 `SaveSettingsCommand`；關閉設定視窗時還原未儲存的顯示模式變更；成功儲存才關閉。
- `Directory.Build.props`：版本 1.8.3、`FileVersion` 1.8.3.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`docs/USER_GUIDE.md`、`docs/DESIGN.md`：補充儲存分工與未儲存還原說明。

### 問題與解法

- 原本設定頁直接呼叫主畫面 `SaveCommand`，會一併儲存分類、同步捷徑並清除主畫面未儲存狀態 → 改為獨立設定儲存命令，只寫入顯示模式並保留上次已儲存的分類快照。
- 顯示模式變更原本會標記 `HasUnsavedChanges`，導致主畫面誤判有分類變更 → 拆分為獨立的 `HasUnsavedSettingsChanges`。

### 待辦 / 已知限制

- 尚未實測儲存失敗後再次儲存的完整流程。

## 2026-11-13 - v1.8.2

### 目標

修正深色模式下「分類主題」與設定頁「顯示模式」下拉選單文字看不清楚的問題。

### 變更

- `Resources/Styles.xaml`：以主題筆刷自訂 `ComboBox` 與 `ComboBoxItem` 樣式；下拉箭頭、選中項目、滑鼠移入、選項清單彈出視窗背景與文字皆改用 `DynamicResource`。

### 問題與解法

- WPF 預設 `ComboBox` 樣式的彈出清單使用系統視窗背景（淺色），且控制項未套用主題背景，導致深色模式下淺色文字落在淺色底上而看不清楚 → 自訂完整控制項範本，統一使用 `SurfaceBrush`、`SurfaceAltBrush`、`TextBrush` 等主題資源。

### 待辦 / 已知限制

- 尚未實測高對比模式下的下拉選單顯示。

## 2026-11-12 - v1.8.1

### 目標

在設定頁提供分類設定的匯出與匯入，方便備份、還原或移到其他電腦使用；匯入後直接覆蓋目前所有分類。

### 變更

- `Models/GroupExportDocument.cs`、`Models/GroupExportJsonContext.cs`（新增）：分類匯出檔格式與來源產生 JSON 序列化。
- `Services/GroupTransferService.cs`（新增）：匯出／匯入分類 JSON；匯出先寫暫存檔再覆蓋，匯入驗證格式並為空白或重複分類 ID 重新產生 GUID。
- `Services/IDialogService.cs`、`Services/DialogService.cs`：新增儲存檔案對話框。
- `ViewModels/EditorViewModel.cs`：新增匯出／匯入命令；匯入確認後取代分類、立即儲存並同步捷徑，再重新載入圖示。
- `Views/SettingsWindow.xaml`：新增「分類設定」卡片與匯出／匯入按鈕。
- `Resources/Strings.resx`：新增匯出／匯入相關繁體中文字串。
- `Services/EditorBootstrap.cs`、`Services/StartupCoordinator.cs`：註冊並注入 `GroupTransferService`。
- `tests/TaskbarFolders.Tests/GroupTransferServiceTests.cs`（新增）：匯出匯入來回、排除顯示模式、無效 JSON、空分類、重複 ID 正規化。
- `Directory.Build.props`：版本 1.8.1、`FileVersion` 1.8.1.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`、`docs/USER_GUIDE.md`、`docs/ARCHITECTURE.md`、`docs/DESIGN.md`、`docs/DEVELOPMENT.md`：補充功能說明與版本範例。

### 問題與解法

- 匯出檔若包含 `theme` 欄位會與「僅匯出分類」的設計不符 → 改用獨立的 `GroupExportDocument`，只序列化分類資料。
- 不同來源的分類 JSON 可能有空白或重複 ID，造成捷徑與簽章檔衝突 → 匯入時重新產生重複 ID。
- 測試以 `DoesNotContain("theme")` 誤判 `themeColor` → 改為檢查 JSON 的 `"theme":` 欄位。

### 待辦 / 已知限制

- 匯入項目若指向其他電腦不存在的路徑，仍會保留設定，啟動時顯示既有錯誤。
- 匯入不包含顯示模式，匯入後維持目前主題設定。
- 尚未加入匯入預覽；確認對話框僅顯示分類數量。

## 2026-11-12 - v1.8.0

### 目標

建立完整的繁體中文專案文件，將專案公開到 GitHub，設定 MIT License，並以 x64 與 arm64 非 Self-Contained zip 形式發佈。

### 變更

- `README.md`：改寫為完整專案介紹，加入功能、系統需求、下載安裝、使用流程、資料位置、文件導覽、建置與授權。
- `LICENSE`（新增）：MIT License，著作權人 Mark (mark216tw)。
- `docs/USER_GUIDE.md`（新增）：安裝、介面操作、分類與項目管理、儲存與釘選、飛出面板、疑難排解。
- `docs/ARCHITECTURE.md`（新增）：技術基線、啟動流程、服務職責、執行緒模型、資料模型、儲存與快取、Windows 整合、效能與測試策略。
- `docs/DESIGN.md`（新增）：目的與範圍、名詞定義、使用案例、功能與非功能需求、系統情境、資料流、介面設計、錯誤處理、設計決策、風險與驗收條件。
- `docs/DEVELOPMENT.md`（新增）：開發環境、建置、測試、封裝、版本規則、GitHub 發行流程與提交規範。
- `Directory.Build.props`：版本 1.8.0、`FileVersion` 1.8.0.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.8.0 說明。
- 發佈 `publish/TaskbarFolders-1.8.0-win-x64.zip` 與 `publish/TaskbarFolders-1.8.0-win-arm64.zip`。

### 問題與解法

- 文件初稿出現少數簡體字（盡量、讀取、複製）→ 以字元檢查確認並改為繁體中文。
- 既有文件以 PowerShell 直接輸出時顯示亂碼，是主控台編碼問題，檔案本身為 UTF-8；以編輯器與 UTF-8 讀取確認內容正確。
- 非 Self-Contained 發佈需確認 x64 與 arm64 皆能產出；arm64 由 x64 主機跨架構發佈，若 ReadyToRun 失敗則以 `-p:PublishReadyToRun=false` 重新發佈。

### 待辦 / 已知限制

- 尚未設定程式碼簽章，SmartScreen 提示仍會出現。
- 多螢幕與高 DPI 完整矩陣仍建議持續實機驗證。
- 後續版本可考慮 GitHub Actions 自動建置與發佈。

## 2026-11-12 - v1.7.4

### 目標

修正「從開始功能表匯入」對話視窗搜尋欄的輸入游標被提示文字蓋住的問題。

### 變更

- `Views/AppPickerWindow.xaml`：搜尋欄改為三層結構——外層 `Border` 提供 `SearchBackgroundBrush` 背景與邊框、提示 `TextBlock` 置中、`TextBox` 背景與邊框設為透明。`TextBox` 最後繪製，因此游標與輸入文字會顯示在提示文字上層。
- `Directory.Build.props`：版本 1.7.4、`FileVersion` 1.7.4.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.7.4 說明。

### 問題與解法

- 原本提示 `TextBlock` 疊在 `TextBox` 之後（上層），游標位於提示文字底下被遮住。改為提示先繪製、`TextBox` 透明背景後繪製，游標即可顯示在提示之上。
- `TextBox` 維持 `BorderThickness` 1 與 `Padding` 10,7，內容起點仍是 11 DIP，與提示 `Margin="11"` 對齊；聚焦時的藍框由全域 `TextBox` 樣式的 `IsKeyboardFocused` trigger 繪製，位置與外層邊框重疊，不影響外觀。

### 待辦 / 已知限制

- 游標與提示第一個字仍會重疊（原生控制項與網頁 placeholder 的常見行為），只是游標改為顯示在上層。

## 2026-11-12 - v1.7.3

### 目標

讓飛出面板一列顯示 5 個圖示，改善目前部分設定下一列只看得到 3 個、需要多捲動的問題。

### 變更

- `Views/FlyoutWindow.xaml`：視窗寬度 460 → 500；`ListBox` 增加 `ItemContainerStyle`，把項目 `Padding` 由全域的 8 改為 2、`Margin` 改為 `0,1`（僅影響飛出面板）；項目 `StackPanel` 寬度 96 → 88、`Margin` 2 → 0。
- `Views/FlyoutWindow.xaml.cs`：`Columns` 4 → 5（高度換算與上下鍵跳躍同步）、`Width` 500、`ItemRowHeight` 92 → 88。
- `Directory.Build.props`：版本 1.7.3、`FileVersion` 1.7.3.0（`AssemblyVersion` 維持 1.7.0.0）。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：新增 1.7.3 說明。

### 問題與解法

- 一列只顯示 3 個的根因是全域名為 `ListBoxItem` 的樣式使用 `Padding="8,6"`，加上項目 `StackPanel Width=96`、`Margin=2`，每個項目實際佔 116 DIP；面板內寬約 433 DIP 只放得下 3 個。
- 改為面板內寬約 474 DIP、每項 88 + 4（Padding）= 92 DIP，5 個共 460 DIP，留有約 14 DIP 餘裕；因 `WrapPanel` 會依實際寬度換行，仍需保留餘裕避免因主題或字型差異退回 4 個。

### 待辦 / 已知限制

- 項目寬度 88 會讓較長的名稱較早換行或截斷（最多兩行），可再依實際使用狀況調整面板寬度或項目寬度。

## 2026-11-12 - v1.7.2

### 目標

提高編輯器與飛出面板的啟動速度；維持「先顯示、後補圖示」的體驗，不加入常駐暖程序。

### 變更

- `App.xaml.cs`：移除啟動時的 `EnsureDirectories`；編輯器主機與單一實例邏輯抽到 `EditorBootstrap`，飛出模式不再載入 Hosting 相關組件；圖示快取清掃改為顯示後背景執行。
- `Services/EditorBootstrap.cs`（新增）：以 `ServiceCollection` / `ServiceProvider` 建立輕量 DI，取代 Hosting；集中處理 mutex 與既有視窗喚回。
- `Services/StartupCoordinator.cs`、`ViewModels/EditorViewModel.cs`：先 `Show()` 視窗再於背景載入圖示；優先載入目前選取分類的項目。
- `ViewModels/BuiltInIconOption.cs`：內建圖示改為延遲渲染，第一次開啟圖示選單才建立 `BitmapSource`。
- `Services/StaExecutor.cs`：改為第一次 `InvokeAsync` 才啟動 STA 執行緒。
- `Services/IconService.cs`：`BuildKey` 與磁碟快取讀取整段移入背景；記憶體快取改用不需要檔案 stat 的鍵；快取清理加入 24 小時節流。
- `Services/ConfigService.cs`、`Services/WindowPlacementService.cs`、`Models/AppSettingsJsonContext.cs`、`Models/WindowPlacementJsonContext.cs`：改用 `System.Text.Json` 來源產生序列化。
- `Services/FlyoutLauncher.cs`、`Views/FlyoutWindow.xaml(.cs)`：`Show()` 前就開始載入圖示；移除 `ContentRendered` 的重複觸發；飛出視窗不再指定視窗圖示。
- `Directory.Build.props`：Release 且指定 `RuntimeIdentifier` 時啟用 ReadyToRun。
- 測試：新增 `PruneDefaultCache` 24 小時節流測試。

### 問題與解法

- 量測時發現 `Process.MainWindowHandle` 對 `ShowInTaskbar=False` 的飛出視窗永遠是 0 → 改用 `EnumWindows` 尋找程序的可見視窗。
- 新路徑的 exe 首次啟動會被 Microsoft Defender 掃描，量測出現 4–7 秒異常值 → 先暖機再取樣，並以中位數比較。
- Debug 下飛出面板的可見時間主要來自 CLR/WPF 初始化與第一個視窗建立，無法靠應用程式邏輯大幅消除 → 後續以 Release + ReadyToRun 驗證。

### 量測（Debug、建置輸出 exe、取中位數）

- 編輯器：約 1240ms → 約 1110ms（約 -10%），且視窗不再等待全部圖示載入。
- 飛出面板：外部可見時間落在 870–930ms 的雜訊範圍；內部階段顯示圖示在顯示後即完成，UI 執行緒不再進行檔案 stat。

### 待辦 / 已知限制

- 未以 Release 實測 ReadyToRun 的實際改善幅度。
- 要再大幅降低飛出面板冷啟動，需考慮常駐暖程序或改由單一程序承載面板（本次未採用）。

## 2026-11-12 - v1.7.1

### 目標

調整飛出面板的捲軸與高度行為，並移除釘選精靈中已失效的自動釘選說明。

### 變更

- `Views/FlyoutWindow.xaml`：`VerticalScrollBarVisibility` 由 `Auto` 改為 `Hidden`。
- `Views/FlyoutWindow.xaml.cs`：移除 `MaxFlyoutHeight`，`CalculateHeight` 只保留最小高度，實際高度交由螢幕可用空間決定。
- `Services/TaskbarGeometryService.cs`：`GetPlacement` 依工作列方向計算可用高度並夾住請求高度，再計算面板位置，避免超出螢幕。
- `Resources/Strings.resx`：移除 `PinGuideInstructions` 最後一段「也可以先按『嘗試自動釘選』…」。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.7.1。

### 問題與解法

- 只把捲軸設為 `Hidden` 仍會因 `MaxFlyoutHeight` 固定為 560 而過早進入捲動 → 移除固定上限，改由 `TaskbarGeometryService` 依工作列位置計算真正可用高度。
- 工作列在上／左／右時可用高度不同 → 依工作列方向分別計算（上緣、下緣或螢幕高度），垂直工作列使用螢幕高度。

### 待辦 / 已知限制

- 多螢幕、不同工作列方向與 125%/150%/200% DPI 的實際外觀仍建議實機確認。

## 2026-11-12 - v1.7.0

### 目標

在不改變主要使用流程的前提下，改善啟動與儲存效率、補強穩定性與資源清理，並修正多螢幕下飛出面板與視窗位置的問題。

### 變更

- `App.xaml.cs`：
  - 編輯器模式加入具名 `Mutex` 單一實例；第二個實例透過具名 `EventWaitHandle` 通知既有視窗回到前景。`--group` 飛出面板不受影響。
  - 註冊 `DispatcherUnhandledException`、`AppDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException`，共用 `LogFatal` 寫入記錄檔。
  - `--group` 模式改用 `FlyoutLauncher` 輕量啟動，不再建立 `Microsoft.Extensions.Hosting` 主機。
- 新增 `Services/FlyoutLauncher.cs`：只建構飛出面板需要的服務與精簡 `LoggerFactory`，並在結束時釋放 `StaExecutor`。
- `Services/StartupCoordinator.cs`：移除飛出面板分支，改名為 `StartEditorAsync`，只負責編輯器。
- `Services/ConfigService.cs`：載入失敗時攔截所有非取消例外，主檔失敗改讀 `.bak`，備份也失敗才回傳預設值；可注入 `ILogger`。
- `Services/FileLoggerProvider.cs`：記錄檔達 1 MB 時輪替為 `.1`，並以累計位元組數避免每次 stat 檔案。
- `Services/IconService.cs`：
  - 建構子清除超過 30 天未使用的 `*.png` 快取；抽出可測試的 `PruneCache`。
  - 磁碟快取讀取改在 thread pool 執行，只有快取未命中才進 STA。
- `Services/ShortcutService.cs`：
  - 每組捷徑加入簽章檔 `<groupId>.sig`；名稱、圖示、執行檔未變且圖示存在時不重建 `.lnk`。
  - 內建圖示檔名改為 `<groupId>.<iconId>.builtin.ico`，已存在就跳過重新編碼，並清理同組舊圖示。
  - `SyncAllAsync` 同步清理已移除分類的 `.lnk`、`.ico`、`.sig`。
- `Services/DiscoveryService.cs`：加入 60 秒 TTL 快取與 `SemaphoreSlim`，避免重複掃描開始功能表與 `shell:AppsFolder`。
- `Services/TaskbarGeometryService.cs`：改用前景視窗所在螢幕（前景為本程序時才退回游標），修正鍵盤開啟面板跑錯螢幕。
- `Views/FlyoutWindow.xaml(.cs)`：移除 `AllowsTransparency` 與 `DropShadowEffect`，改用實色背景與 DWM 圓角；交由 `WindowThemeHelper` 設定 `DWMWA_WINDOW_CORNER_PREFERENCE`。
- `ViewModels/FlyoutViewModel.cs`、`ViewModels/EditorViewModel.cs`：圖示改用 `Task.WhenAll` 平行載入。
- `Services/WindowPlacementService.cs`、`Views/EditorWindow.xaml.cs`：新增 `ClampToWorkArea`，並在視窗建立後以 `MonitorFromWindow`、`GetMonitorInfo`、`TransformFromDevice` 將視窗夾回目前螢幕工作區。
- 測試：新增日誌輪替、圖示快取清理、設定雙檔損毀回退、捷徑增量重建、視窗工作區夾取測試。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.7.0。

### 問題與解法

- `FileLoggerProvider` 第一版在建構子無條件輪替，導致 20 KB 的既有記錄檔被搬成 `.1` → 改為只有檔案超過 1 MB 才輪替，並補測試。
- 捷徑增量重建若只比對簽章，圖示檔被外部刪除時會留下失效圖示 → 簽章比對時同時要求預期圖示檔存在，否則走完整重建。
- 飛出面板使用 `AllowsTransparency` 會讓該視窗走軟體渲染 → 改為實色視窗 + DWM 圓角；視覺風險已在待辦標註。
- 多螢幕 DPI 轉換若直接以監視器 DPI 換算 WPF 座標容易失真 → 先以 `GetWindowRect` 取得實際像素，再用 WPF `CompositionTarget.TransformFromDevice` 轉回 DIP。
- 建置與測試使用 `-p:OutDir=D:\TEMP\opencode\taskbarfolders\...`，避免鎖住使用者執行中的輸出檔。

### 待辦 / 已知限制

- 飛出面板移除陰影後的外觀需實機確認；若圓角或視覺不理想，可保留 DWM 圓角並改回原本的透明度陰影寫法。
- 多螢幕、125%/150%/200% DPI 與工作列在上／左／右的完整矩陣仍待實機驗證。
- 首次啟動沒有既有簽章檔時，仍會重建一次所有捷徑。

## 2026-11-12 - v1.6.0

### 目標

改善分類設定的文字與對齊、為分類拖曳排序加入明顯動態回饋，並記住主編輯視窗狀態、在無變更時停用儲存按鈕。

### 變更

- `Resources/Strings.resx`：「內建圖示」改為「分類圖示」，「主題顏色」改為「分類主題」。
- `Views/EditorWindow.xaml`：分類圖示按鈕改為垂直置中；分類清單新增 `DragLeave` 事件。
- `Views/DragGhostAdorner.cs`、`Views/DropInsertionAdorner.cs`：新增拖曳浮動縮影與插入位置指示線 Adorner。
- `Views/EditorWindow.xaml.cs`：拖曳開始降低來源透明度、顯示浮動縮影與指示線；放下後移除 Adorner 並以淡入與縮放動畫呈現移動結果；加入視窗位置還原與關閉時儲存。
- `Models/WindowPlacement.cs`、`Services/WindowPlacementService.cs`、`Services/AppPaths.cs`：新增視窗狀態模型與 JSON 存取，並在還原時將超出螢幕的視窗修正回可用範圍。
- `App.xaml.cs`、`Services/StartupCoordinator.cs`：註冊並注入 `WindowPlacementService`。
- `ViewModels/EditorViewModel.cs`：`SaveCommand` 加入 `CanExecute`，`HasUnsavedChanges` 變更時通知；首次建立預設分類或轉換舊自訂圖示時視為需要儲存。
- 測試：新增 `WindowPlacementServiceTests`（存檔內容、缺檔、位置正規化與無效尺寸）。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.6.0。

### 問題與解法

- Adorner 直接對 `UIElement` 呼叫 `TryFindResource` 無法編譯 → 改由 `Application.Current.TryFindResource` 取得主題強調色。
- 視窗位置可能是負值（左側或上方螢幕）→ 尺寸與座標分開驗證，座標只檢查是否為有限數值。

### 待辦 / 已知限制

- 視窗位置正規化以 WPF 虛擬螢幕範圍（DIP）判斷，尚未逐一以各螢幕工作區與 Per-Monitor DPI 精算。
- 拖曳動畫尚未在不同 DPI 與高項目數量下實機驗證。

## 2026-11-12 - v1.5.0

### 目標

取消自訂圖示功能，只保留內建圖示選擇器，並將主題顏色選項移到分類名稱下方。

### 變更

- `Views/EditorWindow.xaml`：移除自訂圖示列（路徑輸入與「瀏覽」按鈕），右側卡片列定義由 6 列縮為 5 列並重排；主題顏色下拉選單移至分類名稱輸入框下方的垂直堆疊。
- `ViewModels/EditorViewModel.cs`：移除 `BrowseIconAsync`；載入設定時將非內建圖示路徑正規化為預設圖示；`LoadGroupIconAsync` 僅處理內建圖示；儲存與驗證也以內建圖示為準。
- `Services/ShortcutService.cs`：移除自訂圖示檔案（ICO／PNG／JPG／BMP）處理與 `ConvertImageToIcon`，保留內建圖示與字母備援圖示。
- `Resources/Strings.resx`：移除 `CustomIconHint`、`Browse`、`IconFilter`、`GroupIcon`。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.5.0。

### 問題與解法

- `LoadGroupIconAsync` 不再需要非同步 I/O，但呼叫端仍使用 `await` → 改為回傳 `Task.CompletedTask`，避免 `async` 無 `await` 的警告被視為錯誤。

### 待辦 / 已知限制

- 尚未實測多螢幕與高 DPI 的完整矩陣。
- 舊設定中的自訂圖示會在載入後轉為預設內建圖示，原自訂圖示檔不會被刪除。

## 2026-11-12 - v1.4.0

### 目標

調整編輯器版面與飛出面板操作：隱藏 AUMID、把主題顏色移到分類名稱右側、將內建圖示改為按鈕彈出選單，並在飛出面板右上角加入關閉按鈕。

### 變更

- `Views/FlyoutWindow.xaml(.cs)`：標題列改為標題 + 右上角「✕」按鈕，新增 `OnCloseClick`。
- `Views/EditorWindow.xaml`：移除 AUMID 標籤與唯讀欄位及對應列定義；分類列改為圖示按鈕、名稱（`*`）、主題顏色（固定 150）三欄。
- `Views/EditorWindow.xaml`：移除常駐內建圖示格與 `IconRequiredHint`，改為 `Popup` 圖示選單，由分類圖示按鈕開啟；自訂圖示列保留。
- `ViewModels/EditorViewModel.cs`：新增 `IsIconPickerOpen`、`OpenIconPickerCommand`；選取圖示或切換分類時關閉選單。
- `Resources/Strings.resx`：移除 `GroupAumid`、`IconRequiredHint`。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.4.0。

### 問題與解法

- Popup 不在主視窗視覺樹中，原本以 `AncestorType=Window` 綁定圖示命令會失效 → 改由 `PlacementTarget.DataContext` 提供 DataContext，命令改綁最近的 `ItemsControl`。
- 點擊圖示選單後需自動收起 → `SelectBuiltInIconAsync` 選取後將 `IsIconPickerOpen` 設為 false，切換分類時亦同。

### 待辦 / 已知限制

- 圖示選單與飛出面板關閉按鈕尚未實機驗證高 DPI 下的位置。
- 尚未實測多螢幕與高 DPI 的完整矩陣。

## 2026-11-12 - v1.3.0

### 目標

改善編輯器可用性與飛出面板外觀：修正儲存按鈕文字對比、移除不可靠的自動釘選、擴充內建圖示、提供分類拖曳排序與分類主題顏色。

### 變更

- `Resources/Styles.xaml`：移除隱含 `TextBlock` 樣式的 `Foreground` setter，避免覆蓋 Button 樣板傳入的 `AccentTextBrush`。
- 各視窗根節點新增 `Foreground="{DynamicResource TextBrush}"`；`SettingsWindow` 的 `ComboBoxItem` 明確指定文字色。
- `Services/ShortcutService.cs`、`Views/PinGuideWindow.*`：移除 `TryPinToTaskbar`、「嘗試自動釘選」按鈕與相關字串。
- `Helpers/BuiltInIconCatalog.cs`、`Resources/Strings.resx`：新增 10 個內建圖示（共 20 個）與名稱字串。
- `Views/EditorWindow.xaml(.cs)`、`ViewModels/EditorViewModel.cs`：分類清單改為拖曳排序，移除分類上移／下移命令與按鈕，新增 `MoveGroupTo`。
- `Models/AppGroup.cs`、`ViewModels/GroupViewModel.cs`：新增 `ThemeColor`（預設 `None`）。
- 新增 `Helpers/GroupThemePalette.cs`、`GroupThemeDefinition.cs`、`GroupThemeCatalog.cs`：提供 5 個預設主題的淺色／深色色盤。
- `Services/ThemeService.cs`：新增 `ApplyGroupTheme`，以視窗資源覆寫飛出面板配色；`StartupCoordinator` 在顯示飛出視窗前套用。
- `ViewModels/GroupThemeOption.cs`、`EditorViewModel`、`EditorWindow.xaml`：新增主題顏色選項與色塊預覽下拉選單。
- 測試：新增 `GroupThemeCatalogTests`、內建圖示 ID 唯一性測試，並在設定讀寫測試涵蓋 `ThemeColor`。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.3.0。

### 問題與解法

- 儲存按鈕深藍底黑字 → 根因是隱含 `TextBlock` 樣式的 `Foreground` setter 優先於 Button 的 Foreground 繼承；移除該 setter 並由視窗層級提供一般文字色後，強調按鈕可正確使用 `AccentTextBrush`。
- 分類主題顏色需要依淺色／深色模式切換 → 以 `GroupThemeCatalog` 分別定義兩套色盤，套用時只寫入該飛出視窗的 `Resources`，不影響全域主題。
- 拖曳排序落在項目上／下半部需要不同插入位置 → 依滑鼠相對 `ListBoxItem` 高度判斷，再交由 `MoveGroupTo` 調整索引。

### 待辦 / 已知限制

- 分類主題顏色的色塊預覽使用固定代表色，實際飛出面板仍依目前淺色／深色模式取色。
- 尚未實測多螢幕與高 DPI 的完整矩陣。

## 2026-11-12 - v1.2.0

### 目標

修正缺少圖示時儲存按鈕無反應的問題，並新增設定頁、把顯示模式移出編輯器主畫面。

### 變更

- `Helpers/BuiltInIconCatalog.cs`：新增 `DefaultIconId`（`folder`）與預設圖示參照。
- `ViewModels/EditorViewModel.cs`：
  - 載入設定、建立新分類與儲存前，若分類沒有圖示就自動套用內建「資料夾」圖示。
  - 移除儲存與產生捷徑命令的 `CanExecute` 綁定，按鈕一律可點，避免再次出現靜默失效。
  - 右側提示保留，文字改為說明會自動套用預設圖示。
- `Views/SettingsWindow.xaml`、`SettingsWindow.xaml.cs`：新增設定頁，包含顯示模式與儲存／關閉按鈕。
- `Views/EditorWindow.xaml`、`EditorWindow.xaml.cs`：移除主畫面的顯示模式下拉選單，在「產生釘選捷徑」右側新增「⋯」設定按鈕。
- `Resources/Strings.resx`：新增設定頁標題，更新圖示提示，移除不再使用的 `StatusNeedIcon`。
- 文件與版本：更新 README、CHANGELOG、RELEASE_NOTES，版本升至 1.2.0。

### 問題與解法

- 使用者回報「資料已輸入但儲存沒反應」→ 檢查 `%AppData%\TaskbarFolders\config.json` 發現分類的 `iconPath` 為 `null`，導致 1.1.0 的 `CanSave` 一直為 false、儲存按鈕停用。改為自動套用預設圖示並移除命令停用機制。
- 建置時執行中的 `TaskbarFolders.exe` 鎖住輸出檔 → 驗證時改用 `-p:OutDir=D:\TEMP\opencode\...` 建置與測試，不中斷使用者正在執行的程式。

### 待辦 / 已知限制

- 重新命名分類會產生新檔名的捷徑；已釘選的工作列副本仍可運作，但建議重新釘選。
- 設定頁關閉時不會還原未儲存的顯示模式變更；主視窗關閉前仍會提示未儲存。
- 尚未實測多螢幕與高 DPI 的完整矩陣。

## 2026-11-12 - v1.1.0

### 目標

改善儲存按鈕可讀性、簡化工作列飛出面板、提供內建分類圖示、讓儲存結果更明顯，並讓捷徑檔名可直接辨識分類。

### 變更

- `Resources/Styles.xaml`、`Resources/Themes/Colors.*.xaml`：
  - 按鈕範本明確綁定 `TextElement.Foreground`。
  - `AccentButtonStyle` 改用專屬範本與 `AccentHoverBrush`／`AccentPressedBrush`，修正深藍底白字在 hover／pressed／停用時不清楚的問題。
  - 新增 `IconChoiceButtonStyle`、`StatusBannerStyle` 與成功／錯誤／資訊背景色。
- `Views/FlyoutWindow.xaml`、`FlyoutWindow.xaml.cs`、`ViewModels/FlyoutViewModel.cs`：移除搜尋列與搜尋邏輯，調整高度與焦點，保留方向鍵／Enter／Esc 操作。
- `Helpers/BuiltInIconCatalog.cs`、`BuiltInIconDefinition.cs`、`IcoWriter.cs`：新增 10 個生產力導向的自繪向量圖示，支援預覽與 ICO 輸出；ICO 寫入改為共用。
- `Services/ShortcutService.cs`：
  - 支援 `builtin:<id>` 圖示並輸出 `<groupId>.builtin.ico`。
  - 捷徑檔名改用分類名稱，加入非法字元淨化、保留名稱處理、長度限制與同名序號。
  - `GetShortcutPath` 改為需要分類物件與可選的分類清單，讓同一次同步能得到一致檔名。
- `ViewModels/EditorViewModel.cs`、`BuiltInIconOption.cs`、`StatusSeverity.cs`：
  - 新增內建圖示集合與選取命令，預覽支援內建圖示。
  - 新增 `CanSave`／`HasMissingIcons` 驗證，缺少圖示時停用儲存與產生捷徑。
  - 新增狀態橫幅狀態與自動隱藏計時器。
- `Views/EditorWindow.xaml`：新增內建圖示選取區、自訂圖示列與底部狀態橫幅。
- `Resources/Strings.resx`：新增內建圖示、提示與 10 個圖示名稱字串；移除飛出面板搜尋字串。
- 測試：更新捷徑檔名預期，新增檔名淨化、空名稱、同名去重與內建圖示輸出測試。

### 問題與解法

- 狀態列屬性若命名為 `StatusKind` 會與列舉型別同名，造成 `StatusKind.Error` 解析衝突 → 列舉改名為 `StatusSeverity`，屬性仍為 `StatusKind` 供 XAML 繫結。
- 強調色按鈕原本沿用一般按鈕的 hover 背景，淺色主題會變成近白底白字 → 改為強調色專屬的深藍 hover／pressed。

### 待辦 / 已知限制

- 重新命名分類會產生新檔名的捷徑；已釘選的工作列副本仍可運作，但建議重新釘選。
- 既有設定中沒有圖示的分類必須補選圖示才能儲存。
- 尚未實測多螢幕與高 DPI 的完整矩陣。

## 2026-11-12 - v1.0.0

### 目標

在 Windows 11 工作列上提供「分類 / 資料夾」體驗，讓大量應用程式圖示可以分組，點擊分類後向上展開快速啟動。

Windows 11 原生不支援工作列資料夾或 deskband，因此採用「每個分類一個釘選捷徑 + 獨立 AppUserModelID」的伴隨式啟動器架構，不依賴第三方工作列修補工具。

### 變更

- 建立 .NET 10 WPF + MVVM + `Microsoft.Extensions.Hosting` 專案骨架，套件集中於 `Directory.Packages.props`。
- 新增 `AppSettings` / `AppGroup` / `AppItem` 模型與 `ConfigService`（JSON、原子寫入、`.bak` 備份）。
- 新增 `DiscoveryService`：
  - 掃描使用者與全機器開始功能表，解析 `.lnk` / `.url` / `.appref-ms`。
  - 透過 `shell:AppsFolder` 的 `IShellItem2` / `IEnumShellItems` 列舉 Store / UWP 應用程式。
- 新增 `IconService`：`IShellItemImageFactory` 取得高解析圖示、`SHGetFileInfo` fallback、PNG 磁碟快取。
- 新增 `LaunchService`：`Process.Start` 啟動檔案/網址，`IApplicationActivationManager` 啟動 UWP。
- 新增 `ShortcutService`：
  - `IShellLinkW` / `IPersistFile` 產生 `.lnk`。
  - 以 `IPropertyStore` 寫入 `System.AppUserModel.ID`，讓每個分類成為獨立工作列按鈕。
  - 支援 PNG/JPG 轉 ICO、開啟捷徑資料夾、嘗試 `taskbarpin` verb。
  - 未指定圖示時自動產生「字母 + 顏色」的分類圖示，避免所有分類看起來相同。
- 新增 `TaskbarGeometryService`：以 `Shell_TrayWnd` / `Shell_SecondaryTrayWnd` 取得工作列位置，處理多螢幕、DPI 與自動隱藏，將飛出視窗貼齊工作列上緣。
- 新增 `FlyoutWindow`：圖示格、搜尋、方向鍵/Enter/Esc、單擊啟動、失焦關閉。
- 新增 `EditorWindow`：分類與項目管理、開始功能表匯入、加入檔案/網址、排序、圖示選擇、主題切換、未儲存提示。
- 新增釘選精靈、應用程式挑選器、輸入對話框。
- 新增淺色/深色色彩字典、共用樣式、應用程式圖示。
- 新增 xUnit 測試：設定讀寫/損毀回退、搜尋、捷徑 AUMID。
- Windows 11 26H2 (build 26300) 實機驗證：
  - 開始功能表 + Store 探索共 259 個項目（62 個 UWP）。
  - 圖示擷取正常（含 UWP）。
  - 飛出視窗位置：工作列 `Top=816`，視窗 `Bottom=808`，符合 8px 間距。
  - 設定視窗與飛出視窗皆可正常啟動。

### 問題與解法

- WPF 暫存專案的 `ImplicitUsings` 未帶入 `System.IO`，導致 `Path` / `File` / `Directory` 找不到 → 明確加入 `using System.IO;`。
- `IShellLinkW` 的 vtable 中 `SetPath` 位於最後一個方法，不是第一個 → 依實際順序宣告。
- `PROPVARIANT` 在 x64 為 24 bytes；若只宣告 16 bytes 會被 COM 寫入越界 → 加上 `Size = 24`。
- `IEnumShellItems.Next` 需要 `[PreserveSig]` 才能用傳回值判斷結束。
- Windows 11 無法以程式可靠自動釘選工作列 → 改為產生捷徑 + 釘選精靈，並提供 `taskbarpin` 嘗試按鈕。

### 待辦 / 已知限制

- 多螢幕、125%/150%/200% DPI、工作列在上/左/右的完整矩陣尚未逐一實測。
- 尚未支援巢狀分類、全域熱鍵、系統匣常駐、開機自動啟動、自動更新。
- 自動釘選仍可能被 Windows 封鎖，需手動完成。
- 執行檔尚未簽章，可能觸發 SmartScreen。
