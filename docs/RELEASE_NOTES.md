## GPT Quota Bar v1.2.0

Windows x64 顶部菜单额度栏，显示 Codex 订阅 5h / 7d 剩余额度。

- 显示顺序：重置倒计时 / 周期、进度条、剩余百分比，末尾为最近更新时间。
- 12 DIP 统一字号，等长进度条和统一间距。
- 原生子窗口同步跟随宿主，鼠标穿透和被动显示。
- 支持当前用户登录启动、通知区刷新和深浅主题。
- 按白名单打包，不包含登录凭据、本机设置、诊断或真实截图。

要求 Windows 10 / 11 x64、.NET Framework 4.8、官方桌面应用和已登录的官方 Codex CLI。解压后运行 `Install.cmd`；移除登录任务使用 `Uninstall.cmd`。

下载 ZIP 后可用 `Get-FileHash .\GptQuotaBar-v1.2.0-windows-x64.zip -Algorithm SHA256`，与同一 Release 的 `SHA256SUMS.txt` 对比。

非 OpenAI 官方产品。源码与构建方式见仓库 README；使用前请阅读隐私及兼容性说明。
