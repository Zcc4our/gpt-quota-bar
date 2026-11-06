## GPT Quota Bar v1.3.0

Windows 顶部菜单额度栏，显示官方 Codex 接口返回的订阅额度。

- 按实际数据展示一个或两个额度窗口；仅积分账号显示积分状态。
- 错误状态显示在额度栏，读取失败时清空旧值；额度重置后自动刷新。
- 剩余额度不超过 10% 时可显示托盘提醒，支持在托盘菜单关闭。
- 运行数据默认保存到用户本地数据目录；从 `PATH` 查找的 CLI 必须通过 OpenAI 签名验证。
- 保持窗口跟随、鼠标穿透、深浅主题和当前用户登录启动。

Plus 已在作者当前电脑上验证；Pro 的单窗口和积分形态已用虚构数据测试，尚未用真实 Pro 账号验证；Go 按接口返回自适应，尚未用真实 Go 账号验证。

要求 Windows 10 / 11 x64、.NET Framework 4.8、官方桌面应用和已登录的官方 Codex CLI。解压运行包后运行 `Install.cmd`；更新前先运行旧目录中的 `Uninstall.cmd`。登录任务指向的程序目录应仅当前用户可写。

下载后可用 `Get-FileHash .\GptQuotaBar-v1.3.0-windows-x64.zip -Algorithm SHA256`，与同一 Release 的 `SHA256SUMS.txt` 对比。

非 OpenAI 官方产品。具体使用方法和隐私说明见仓库 README。
