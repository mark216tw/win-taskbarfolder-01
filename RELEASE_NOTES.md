# TaskbarFolders 1.8.0

首個公開釋出版本，提供 win-x64 與 win-arm64 非 Self-Contained 可攜式 zip。

## 新增

- **繁體中文文件**：使用指南、系統架構與技術文件、系統設計文件、開發與發佈指南
- **MIT License**：本專案以 MIT 授權釋出
- **雙架構發佈**：`TaskbarFolders-1.8.0-win-x64.zip` 與 `TaskbarFolders-1.8.0-win-arm64.zip`

## 功能總覽

- 建立、改名、刪除分類，拖曳排序並提供動畫回饋
- 從開始功能表匯入傳統捷徑與 Microsoft Store / UWP 應用程式
- 加入檔案、捷徑與網址
- 20 個內建分類圖示與 6 種分類主題（含黃色護眼）
- 工作列上方飛出面板，支援滑鼠與鍵盤操作
- 淺色 / 深色 / 跟隨系統主題
- 多螢幕、Per-Monitor V2 DPI、工作列自動隱藏定位
- 單一實例編輯器、設定 `.bak` 備份、記錄檔輪替與圖示快取清理

## 修正

- **搜尋欄游標可見**：「從開始功能表匯入」對話視窗的搜尋欄，輸入游標不再被提示文字蓋住。

## 安裝方式

1. 依裝置架構下載對應 zip。
2. 解壓縮到固定資料夾。
3. 安裝 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。
4. 執行 `TaskbarFolders.exe`。

本版本不提供安裝程式。

## 注意事項

- 非 Self-Contained 版本必須先安裝 .NET 10 Desktop Runtime（x64 或 arm64 需與程式版本一致）。
- Windows 11 不允許程式自動釘選工作列，最後一步仍需在檔案總管手動完成。
- 重新命名分類會改變捷徑檔名；工作列上已釘選的副本仍可運作，但建議重新產生捷徑並再次釘選。
- 執行檔未簽章，Windows 可能顯示 SmartScreen 提示。
