# 验证说明

## 离线验证（可用于 CI）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

检查所有 PowerShell 脚本语法、公开文件、版本一致性，编译并执行 59 项离线断言。覆盖单窗口、积分、未知周期、套餐清洗、非 Codex 桶隔离、渲染、布局空间、DPI 和倒计时边界。Go 按接口返回自适应，尚未用真实 Go 账号验证；Pro 真实账号和不同来源的签名验证仍需实测。结果在 `artifacts/bin/test-results.json`。

该过程不安装登录任务、不调用账号接口、不查询额度、不触发模型推理。CI 只使用这个验证及白名单打包。

## 跨进程窗口交互检查（本机可选）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-interaction.ps1
```

必须在交互式 Windows 桌面运行。脚本创建自己的独立测试窗口，不操作 GPT。测试期间请不要切换前台应用，否则焦点检查可能失败。

检查 30 次显示和更新不抢焦点、30 次鼠标命中穿透、原生子窗口关系、被动隐藏和重新附着，以及 30 次宿主移动时子窗口同步跟随。结果在 `artifacts/tests/results.json`。命中测试不能替代手动点击、输入和拖动验证。

## 人工验收

使用自行构建的运行包和自己的账号，在本机确认：

1. 顶部文字为 12 DIP、基线及间距合适；深浅主题正常，两个进度条等长。
2. 额度栏附近可点击菜单、输入文字、拖动宿主，且没有焦点争夺或独立窗口残影。
3. 移动、最大化、最小化、恢复、调整窗口宽度和跨 DPI 显示器后位置正常。
4. 前台及后台读取、刷新失败、到达重置时间、账号切换和缺失周期的状态符合 README。
5. 安装登录任务后重启宿主可重新显示；Windows 重新登录后监听器启动；卸载任务后停止并不再登录启动。

宿主版本或 Windows 版本变化后，应重新进行上述验收。自动离线通过不等于已验证所有宿主版本与显示器组合。

## 生成安全预览

```powershell
$exe = (Resolve-Path .\artifacts\bin\GptQuotaBar.exe).Path
$target = Join-Path (Get-Location) 'assets/preview.png'
Start-Process -FilePath $exe -ArgumentList ('--render-preview "' + $target + '"') -WindowStyle Hidden -Wait
```

`--render-preview` 仅绘制内置虚构数据，不读取账号。请不要用 `--inspect` 导出的真实图片替换仓库预览。

`--ui-test` 是额外的应用内窗口检查，`--probe`、`--diagnose`、`--inspect` 用于本机排查。后面三个命令可能生成真实额度或本机元数据，不在 CI 中运行，其输出不提交。
