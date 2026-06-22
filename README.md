# Focus Mode / 专注模式

.NET 8 WPF 桌面应用，用于在专注时间内屏蔽非白名单应用和网页，强制保持专注。

## 功能

- **应用白名单** — 专注时只有白名单应用可以运行，其他应用自动强制关闭
- **网址白名单** — 浏览器打开非白名单网页时自动关闭标签页（通过 UI Automation 检测 URL）
- **专注计时器** — 可配置 5-120 分钟倒计时
- **系统托盘** — 开始专注后最小化到系统托盘，专注完成前关闭窗口不会退出
- **中/英切换** — 一键切换界面语言，选择持久化
- **开机自启** — 开机自动启动开关，会在启动文件夹创建快捷方式
- **任务列表** — 三种任务类型：
  - 短期任务：完成一次即归档
  - 每日任务：每天自动重置
  - 长期任务：每天重置直到手动点「Done」永久归档
- **归档** — 已完成的任务移入归档列表，可展开查看
- **成就系统** — 10 种成就徽章：首次专注、连续天数、累计次数、累计时长
- **等级系统** — 7 个等级（新手→传奇），根据总专注时长升级
- **深色模式** — 一键切换深色/浅色主题

## 默认白名单

| 应用 | 进程名 |
|------|--------|
| Visual Studio | devenv |
| VS Code | Code |
| Microsoft Edge | msedge |
| Google Chrome | chrome |
| Notepad | Notepad |
| File Explorer | explorer |
| Windows Terminal | wt / WindowsTerminal |
| Focus App（自身） | WpfApp1 |

| 网址 | 说明 |
|------|------|
| github.com | GitHub |
| stackoverflow.com | Stack Overflow |
| learn.microsoft.com | Microsoft Learn |
| bilibili.com/video/BV | Bilibili BV号视频 |

## 构建

### 前提

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（Windows 版）
- Windows 10 或 Windows 11

### 编译运行

```bash
# 克隆仓库
git clone https://github.com/<your-username>/Focus.git
cd Focus/WpfApp1

# 编译
dotnet build

# 运行
dotnet run
```

或在 Visual Studio 2022 中打开 `WpfApp1.csproj`，直接 F5 运行。

### 发布为单文件

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

输出在 `bin/Release/net8.0-windows/win-x64/publish/WpfApp1.exe`。

## 技术栈

- .NET 8 + WPF
- P/Invoke：窗口操作（EnumWindows, SetForegroundWindow, keybd_event）、系统托盘（Shell_NotifyIcon）
- Managed UI Automation：浏览器 URL 检测
- System.Text.Json：数据持久化
- 纯 WPF，无 WinForms 依赖

## 数据存储

所有个人数据保存在 `%LocalAppData%\FocusApp\` 目录下：

- `settings.json` — 设置、等级、成就
- `app_whitelist.json` — 应用白名单
- `url_whitelist.json` — 网址白名单
- `todos.json` — 任务列表
- `todos_archive.json` — 已归档任务
- `locale.txt` — 语言偏好

卸载时手动删除该目录即可清除所有数据。不会修改注册表或系统文件。
