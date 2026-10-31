# 文件上传清单

## 提交到 Git 仓库

| 路径 | 内容 |
| --- | --- |
| `src/GptQuotaBar.cs` | 当前 1.2.0 应用、额度协议和离线测试源码 |
| `src/app.manifest` | 版本、普通权限、DPI 和 Windows 兼容性声明 |
| `scripts/*.ps1` | 构建、公开文件检查、验证、打包和交互测试 |
| `scripts/*.cmd` | 运行包的启动、停止、安装和卸载模板 |
| `tests/InputRegression.cs` | 使用独立测试宿主的跨进程窗口回归检查 |
| `assets/preview.png` | 程序生成的虚构数据预览，可公开 |
| `docs/*.md` | 文件清单、测试、发布、运行包指南和发布说明 |
| `.github/` | GitHub Actions、Issue 和 PR 模板 |
| `README.md`、`README.en.md` | 中英文说明 |
| `LICENSE`、`CHANGELOG.md`、`CONTRIBUTING.md`、`SECURITY.md` | 许可、变更记录、贡献和安全说明 |
| `.gitignore`、`.gitattributes`、`.editorconfig` | 忽略规则、换行和编辑约定 |

这些就是源码仓库的全部公开内容。源码 ZIP 也只应包含以上文件，不包含 `.git/`、构建产物或安装目录。

## 仅作为 GitHub Releases 附件

- `GptQuotaBar-v1.2.0-windows-x64.zip`。
- `SHA256SUMS.txt`。
- `release-notes.md` 的内容粘贴为 Release 描述即可，无需作为附件。

运行包内只包含 `GptQuotaBar.exe`、`LICENSE`、`README.md` 和四个 `.cmd` 入口。打包脚本逐个复制这七个文件，不会复制运行目录中的 JSON 或截图。

## 永远不从本机安装目录上传

| 文件 / 数据 | 原因 |
| --- | --- |
| `auth.json`、`credentials.json`、`.env*`、私钥和任何 token / API Key | 登录凭据或密钥 |
| `.codex/`、账号导出、浏览器配置、会话记录 | 账号和个人数据 |
| `settings.json`、`installation.json` | 本机设置和安装路径 |
| `quota-probe.json`、`live-state.json` | 真实额度、时间及窗口信息 |
| `diagnostics.json`、`error.json`、日志 | 本机状态或潜在敏感信息 |
| `live-bar.png`、真实桌面截图 | 真实额度或账号、聊天内容 |
| `test-results.json`、`ui-test-results.json`、`fixture-state.json` 等 | 可在本机重建的验证数据 |
| `artifacts/`、编辑器缓存 | 构建、测试和打包的本地输出 |
| `.git/config`、凭据助手配置、Git 全局配置 | 远端地址和本机身份设置 |

`.gitignore` 只能防止普通误添加，不能清理已提交的文件或阻止 `git add -f`。`scripts/check-public.ps1` 会额外检查公开文件和 Git 索引中的禁止文件；它不是完整的秘密检测器。发布前仍须检查暂存区、图片内容和历史提交。
