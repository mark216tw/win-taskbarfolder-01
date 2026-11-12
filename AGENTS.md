# Windows .NET 10 / C# 桌面應用開發規範（簡化版）

本檔是簡單型 Windows 桌面應用的共用開發筆記。開發新專案時複製到專案根目錄，opencode 會自動讀取；專案專屬規則寫在文末。

---

## 1. 技術基線

| 項目 | 版本 |
| --- | --- |
| 語言 | C#（含 nullable reference types、file-scoped namespace） |
| 執行環境 | .NET 10（`net10.0-windows`） |
| UI | WPF + MVVM（`Microsoft.Extensions.Hosting` 啟動） |
| 平台 | Windows 10 1809（build 17763）以上 |
| 架構 | x64（必要時提供 AnyCPU；不支援 ARM 需於補充說明） |
| 建置 | .NET SDK 風格專案（`*.csproj`）+ Central Package Management（`Directory.Packages.props`） |

- 套件版本一律集中寫在 `Directory.Packages.props`，不要在 `*.csproj` 寫死版本。
- 不要任意升級 .NET SDK／套件；升級需在 `docs/DEV_LOG.md` 說明原因。
- 啟用 `Nullable` 與 `TreatWarningsAsErrors`；不關閉警告。

## 2. 專案結構

```
src/<AppName>/                 # 應用程式專案
  Views/                       # XAML 視窗與使用者控制項
  ViewModels/                  # ViewModel（MVVM）
  Models/                      # 資料模型
  Services/                    # 商業邏輯、I/O、外部整合
  Helpers/                     # 轉換器、擴充方法、共用工具
  Resources/                   # 樣式、主題、圖示、字串
  App.xaml / App.xaml.cs
tests/<AppName>.Tests/         # 單元測試（選用，建議 xUnit）
<AppName>.sln
Directory.Build.props          # 共用編譯設定
Directory.Packages.props       # 套件版本集中管理
README.md
CHANGELOG.md
RELEASE_NOTES.md
docs/DEV_LOG.md                # 開發過程紀錄（必要）
```

- 命名空間用反向網域命名（例：`Com.Example.AppName`）。
- 使用者可見文案放 `Resources/Strings`（resx），不要寫死在 XAML／程式碼；預設繁體中文。
- 一個檔案一個公開型別，檔名與型別同名。

## 3. 程式碼規範

- 4 空格縮排；類名／方法用 PascalCase、區域變數／參數用 camelCase、常數用 PascalCase、私有欄位用 `_camelCase`。
- 非同步方法以 `Async` 結尾並回傳 `Task`／`Task<T>`；函式庫層加 `ConfigureAwait(false)`，UI 層不加。
- 不在 UI 執行緒做 I/O；長工作放 `Task.Run` 或 `async` 流程並回報進度。
- 依賴注入用 `Microsoft.Extensions.DependencyInjection`，在 `App.xaml.cs` 註冊服務；不要在 View 直接 new 服務。
- 設定用 `IOptions` + `appsettings.json`（或 `Microsoft.Extensions.Configuration`）；使用者設定用 `Properties.Settings` 或 JSON 檔（`%AppData%/<AppName>`）。
- 資料儲存：結構化資料用 SQLite + EF Core，其餘用檔案／JSON。
- 例外要記錄並處理，不要吞例外；記錄用 `Microsoft.Extensions.Logging`，正式版不輸出到主控台。
- 不要引入未在 `Directory.Packages.props` 宣告的依賴。

## 4. 共用界面

- 以 WPF 原生控制項搭配共用的 `Style`／`ControlTemplate`，樣式放 `Resources/Styles.xaml`、主題放 `Resources/Themes/`。
- 間距用 4 的倍數（WPF 單位為 DIP）；圓角 8。
- 顏色定義在 `Resources/Themes/Colors.xaml`，不要在 XAML 寫死 `#FF...`；以 `DynamicResource` 引用主題資源。
- 字型用統一的 `TextBlock` 樣式與 `FontSize` 資源，不要逐處指定。
- 空狀態、載入中、錯誤都要有畫面；刪除等破壞性操作需二次確認。
- 可點擊元素要有 `AutomationProperties.Name`，圖示按鈕需有 ToolTip。
- 視窗要有合理的 `MinWidth`／`MinHeight`，關閉前若有未儲存變更需提示。
- 建立應用程式圖示，並套用於執行檔、主視窗與工作列。
- 記住主視窗關閉前的大小與位置，於下次啟動時還原；還原後須確保視窗位於目前可用的螢幕範圍內。

## 5. 常用功能（需要時再實作）

- **顯示模式**：提供跟隨系統、淺色、深色三種模式，切換 `Application.Current.Resources` 主題字典並保存使用者選擇。
- **主題設定**：提供「黃色護眼」及另外 4 個色彩主題，共 5 個主題；每個主題都必須同時適用於淺色與深色顯示模式，並保存使用者選擇。
- **設定頁**：`IOptions` + 設定檔，或獨立 Settings 視窗／頁籤。
- **About**：顯示 `Version`、`FileVersion`、授權與隱私說明（由組件屬性讀取）。
- **通知**：使用 Windows Toast（`Microsoft.Toolkit.Uwp.Notifications` 或 `AppNotificationBuilder`）。
- **單一實例**：用 `Mutex` 或具名 Pipe 避免多開，第二次啟動時帶回原視窗。
- **系統匣**：需要時用 `NotifyIcon`（`System.Windows.Forms` 互通）並提供結束選單。
- **深色標題列**：Windows 11 可用 `DwmSetWindowAttribute` 調整標題列顏色。

## 6. 建置與版本

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

- 開發與測試一律使用 `Debug`；不要自動建置或發佈 `Release`，只有使用者明確要求時才執行。
- 每次功能變更完成後，自動執行 `dotnet build` 確認可編譯。
- 版本放在 `Directory.Build.props` 的 `Version`／`AssemblyVersion`／`FileVersion`；`Version` 採 `主.次.修`。
- 每次功能變更遞增版本；`AssemblyVersion` 主次版變更時才調整。
- 指令：`dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。
- 發佈（僅限明確要求）：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`；需要安裝檔時再使用 MSIX 打包。

## 7. 開發過程紀錄（docs/DEV_LOG.md）

每次開發工作都要更新 `docs/DEV_LOG.md`，不要事後補寫。內容包含：

1. **本次目標**：要做什麼、為什麼。
2. **變更內容**：改了哪些檔案／功能。
3. **遇到的問題與解法**：錯誤訊息、嘗試過的方法、最後怎麼解。
4. **待辦／已知限制**：尚未完成或未驗證的部分。
5. **日期與版本**：對應的版本號。

格式建議（最新紀錄放最上面）：

```markdown
## 2026-10-30 - v1.1.0

### 目標
新增深色模式切換。

### 變更
- `Resources/Themes/`：加入深色主題字典
- `SettingsWindow.xaml`：新增主題切換項目

### 問題與解法
- 主題切換後部分樣式未更新 → 改用 `DynamicResource`

### 待辦
- 尚未實測 Windows 10 1809
```

- 只記重點，不貼整段程式碼。
- 不要記錄 API Key、密碼等敏感資訊。

## 8. 文件與禁止事項

- `README.md` 寫功能與建置方式；`CHANGELOG.md` 記錄版本異動；`RELEASE_NOTES.md` 寫給使用者看。
- 不要提交 `bin/`、`obj/`、`.vs/`、`*.user`、`*.suo`、發佈輸出與憑證檔。
- 不要留 `TODO` 當完成功能、不要留測試假資料或除錯輸出（`Console.WriteLine`／`Debug.WriteLine` 需移除）。
- 不要更動無關檔案；未經確認不要改組件名稱、簽章或目標框架版本。
- 不要把 API Key、密碼、金鑰寫進原始碼或版本控制；機密用 User Secrets、環境變數或 Windows 認證管理員。

---

## 專案專屬補充

- 專案名稱：
- 命名空間／組件名稱：
- 特殊需求：
- 已知限制：
