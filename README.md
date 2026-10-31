# GPT Quota Bar

[English](README.en.md)

在 Windows 版 ChatGPT / Codex 桌面应用的顶部菜单空白处显示 Codex 订阅额度的独立小工具，非 OpenAI 官方扩展。

## 功能介绍

![深色和浅色显示示例，使用虚构数据](assets/preview.png)

```text
1h58min/5h ━━━━━━━ 49% | 4d3h/7d ━━━━━━━ 48% · 已更新 10:40
```

- 同时显示 5 小时和 7 天周期的重置倒计时、剩余进度条与剩余百分比，末尾显示最近一次成功读取时间。
- 文字统一为 12 DIP，两条进度条等长，固定在顶部菜单旁；支持深色和浅色主题。
- 随宿主窗口同步移动、最小化和恢复，鼠标点击与拖动穿透，不主动抢占焦点。窗口空间不足时先省略更新时间，再隐藏额度栏。
- 支持 Windows 登录启动。监听器在后台等待，打开 GPT 后自动显示额度栏，关闭 GPT 后继续等待下次启动。
- 前台每 30 秒、后台每 5 分钟查询额度，并接收额度变更通知；倒计时在本地每秒重绘。可从通知区菜单立即刷新。

显示的是 **Codex 订阅的 5h / 7d 额度**，不代表所有 ChatGPT 模型的消息额度或 API 账单金额。缺失周期和已经到达重置时间的旧额度显示为 `--`。

## 代码运行思路

程序使用 C#、WinForms 和 Windows 原生窗口接口实现，主要逻辑位于 `src/GptQuotaBar.cs`。

1. **启动与监听**：`StartupTask` 通过 Windows 任务计划程序注册当前用户的登录启动任务；程序使用互斥量避免重复运行。`BarContext` 管理通知区图标、宿主窗口检测和额度栏的显示状态。
2. **读取额度**：`QuotaClient` 寻找官方 `codex.exe`，启动 `app-server`，通过标准输入输出交换 JSON-RPC 消息。完成初始化后，调用 `account/read` 和 `account/rateLimits/read`，并处理额度与账号变更通知。认证由官方 CLI 管理，本工具不直接读取 `auth.json`，不要求输入 API Key。
3. **更新数据**：`Poller` 按前台、后台状态调整查询间隔，在 GPT 进程退出后关闭查询子进程；`Snapshot` 解析 5h / 7d 周期，将已用比例换算为剩余比例。`Renderer` 根据重置时间计算倒计时并绘制文字、进度条和更新时间。
4. **附着窗口**：`BarForm` 在匹配宿主 DPI 模式后创建原生子窗口，让 Windows 同步处理宿主移动。窗口采用透明、鼠标穿透和不激活样式；布局根据菜单预留空间调整，无需修改宿主安装包或注入代码。

`src/app.manifest` 声明普通用户权限、Windows 兼容性和 DPI 模式；`scripts/` 提供构建、验证、打包及启动入口。

## 使用方法

### 运行条件

- Windows 10 / 11 x64，.NET Framework 4.8。
- 已安装并登录的官方 ChatGPT / Codex 桌面应用，以及支持 `app-server` 订阅额度查询的官方 `codex.exe`。工具会搜索桌面应用内置 CLI、官方安装目录和 `PATH`。
- 当前登录方式能够返回 Codex 订阅额度。应用和额度栏应以同一用户、普通权限运行。

仅支持 Windows 桌面应用。宿主菜单结构或协议变化可能需要适配。

### 安装与操作

1. 从仓库 **Releases** 下载 Windows x64 运行包，完整解压到准备长期保留、当前用户可写的文件夹。若尚无发布包，可按下方说明自行构建。
2. 双击 `Install.cmd`，注册名为 `GPT Quota Bar` 的当前用户登录任务并立即启动。无需管理员权限，不保存 Windows 密码。
3. 打开 GPT 桌面应用，额度栏会出现在顶部菜单空白处。
4. 右键通知区绿色图标，可立即刷新、切换深浅主题或退出。

| 入口 | 作用 |
| --- | --- |
| `Install.cmd` | 注册登录启动，并立即运行 |
| `Start.cmd` 或 `GptQuotaBar.exe` | 仅启动本次监听器 |
| `Stop.cmd` | 停止当前监听器，保留登录启动任务 |
| `Uninstall.cmd` | 移除登录启动任务，并停止监听器 |

以上入口从解压后的运行包使用。移动文件夹前先运行 `Uninstall.cmd`，移动后重新运行 `Install.cmd`；彻底卸载时，移除任务后删除文件夹即可。

若额度栏未显示，请检查通知区图标、宿主是否最小化、顶部空间是否足够，以及官方 CLI 是否已登录。需要手动指定 CLI 时，在运行包目录打开 PowerShell，执行：

```powershell
$env:GPT_QUOTA_CODEX_PATH = (Get-Command codex.exe).Source
.\GptQuotaBar.exe
```

若 `Get-Command` 找不到程序，请将变量设为实际安装的官方 `codex.exe` 完整路径，再启动工具。

### 从源码运行

在仓库根目录用 Windows PowerShell 5.1 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
.\artifacts\bin\GptQuotaBar.exe
```

构建使用 Windows 自带的 .NET Framework C# 编译器，无需 NuGet。需要生成包含四个 `.cmd` 入口的完整运行包时，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

运行包生成于 `artifacts/releases/`。程序运行后的主题设置、安装记录和诊断文件保存在可执行文件所在目录，其中可能包含个人路径、真实额度或窗口信息，请勿公开上传整个运行目录。
