# GPT Quota Bar 1.2.0

Windows 10 / 11 x64，要求 .NET Framework 4.8、官方桌面应用和已登录的官方 Codex CLI。显示的是 Codex 订阅的 5h / 7d 剩余额度。

1. 将整个压缩包解压到当前用户可写、准备长期保留的文件夹。
2. 双击 `Install.cmd`，注册当前用户登录启动并立即运行。打开 GPT 后自动显示额度栏。
3. 右键通知区绿色图标可以立即刷新、切换深浅主题或退出。

| 入口 | 作用 |
| --- | --- |
| `Install.cmd` | 创建或更新 `GPT Quota Bar` 登录任务，并启动 |
| `Start.cmd` | 本次运行 |
| `Stop.cmd` | 停止本次运行，保留登录任务 |
| `Uninstall.cmd` | 删除登录任务并停止，可随后删除文件夹 |

移动目录前先卸载自启动，移动后重新安装。不需要管理员权限或 Windows 密码。源码仓库位于发布此运行包的 GitHub 项目页面。

显示示例：`1h58min/5h 进度条 49% | 4d3h/7d 进度条 48% · 已更新 10:40`。前缀为重置倒计时，进度条为剩余额度，末尾为成功读取时间。文字为 12 DIP；默认前台 30 秒、后台 5 分钟读取，倒计时在本地更新。缺少周期或旧数据已到重置时间时显示 `--`。窗口过窄或最小化时隐藏。

没有显示时检查：通知区图标、宿主窗口是否可见、顶部空间是否充足、官方 CLI 是否已登录。可用环境变量 `GPT_QUOTA_CODEX_PATH` 指向官方 `codex.exe`，然后重新启动工具。

## 隐私与诊断

非 OpenAI 官方产品，不修改宿主安装包，不直接读取 `auth.json`，不要求输入 API Key。认证由官方 CLI 管理。

主题、安装记录及诊断文件保存在程序目录。`installation.json` 可能包含个人安装路径；`quota-probe.json`、`live-state.json` 和截图包含真实额度或本机状态。不要把运行后的整个目录、凭据或真实截图公开上传。

开发者排查命令：`--probe` 会读取真实额度；`--diagnose` 写本机诊断；`--inspect` 请求正在运行的工具导出状态和额度栏截图。提交问题前须自己审阅、脱敏这些文件。

MIT License，详见同目录 `LICENSE`。

## English quick start

Extract to a permanent writable folder. Run `Install.cmd` to register a current-user logon task and start now. `Start.cmd` starts once; `Stop.cmd` stops now; `Uninstall.cmd` removes the task and stops the utility. Use the tray menu to refresh or switch themes. Windows x64, .NET Framework 4.8, the official desktop app and an authenticated official Codex CLI are required. Only Codex subscription limits are displayed. Local diagnostic files may contain private paths or quota data; do not publish the installed directory.
