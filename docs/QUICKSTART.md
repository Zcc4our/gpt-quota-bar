# GPT Quota Bar 1.3.0

Windows 桌面额度栏，显示官方 Codex 接口返回的剩余额度窗口或积分状态。Plus 已在作者当前电脑上验证；Pro 已用虚构数据测试，尚未用真实账号验证；Go 按接口返回自适应，尚未用真实账号验证。

## 安装与使用

1. 确认系统为 Windows 10 / 11 x64，并已安装 .NET Framework 4.8、官方 ChatGPT / Codex 桌面应用和支持 `app-server` 的官方 `codex.exe`；使用自己的账号登录。
2. 将整个运行包解压到准备长期保留、仅当前用户可写的文件夹。
3. 双击 `Install.cmd`，注册当前用户登录启动并立即运行。打开桌面应用后，额度栏显示在顶部菜单旁。
4. 右键通知区绿色图标，可立即刷新、切换主题、开关低额度提醒或退出。

| 文件 | 作用 |
| --- | --- |
| `Install.cmd` | 注册或更新登录启动，并立即运行 |
| `Start.cmd` | 本次运行 |
| `Stop.cmd` | 停止当前程序，保留登录启动任务 |
| `Uninstall.cmd` | 移除登录启动任务并停止程序 |

更新或移动目录前，先运行旧目录中的 `Uninstall.cmd`；解压新运行包后运行其中的 `Install.cmd`。不需要管理员权限或 Windows 密码。

界面按实际返回的数据绘制：有两个窗口时显示两个；Pro 等账号可能只返回一个周窗口；仅返回积分时显示积分。额度不超过 10% 时可弹出托盘提醒。前台约 30 秒、后台约 5 分钟读取；重置倒计时在本地更新。额度读取失败会清空旧值。

额度栏未出现时，检查桌面应用是否最小化、菜单顶部空间是否足够、通知区图标是否存在，以及官方 CLI 是否已登录。自动搜索 `PATH` 时仅接受通过 OpenAI 签名检查的 CLI；必要时可在启动程序的环境中用 `GPT_QUOTA_CODEX_PATH` 指定官方 `codex.exe` 路径。

## 隐私

非 OpenAI 官方产品。认证由官方 CLI 管理，工具不直接读取 `auth.json`，不要求输入 API Key。

设置和诊断默认保存在 `%LOCALAPPDATA%\GptQuotaBar`；创建失败时回退到程序目录。安装记录不包含可执行文件路径。`quota-probe.json`、`live-state.json` 和截图可能包含真实额度或本机状态，请勿公开上传。

MIT License，详见 `LICENSE`。

## English quick start

Windows x64, .NET Framework 4.8, the official desktop app and an authenticated official Codex CLI are required. Extract to a permanent folder writable only by the current user, then run `Install.cmd`. `Start.cmd` starts once, `Stop.cmd` stops now, and `Uninstall.cmd` removes the logon task. The tray menu provides refresh, themes and low-quota notifications. Pro and Go have not been tested with real accounts. Settings and diagnostics are stored under `%LOCALAPPDATA%\GptQuotaBar`, falling back to the program folder if creation fails.
