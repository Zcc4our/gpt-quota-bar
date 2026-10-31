# 发布到 GitHub

## 1. 检查公开内容

仅使用这份干净的源码目录，不要从已经运行过的安装目录直接上传。按 [文件清单](FILES.md) 检查文件；`assets/preview.png` 必须保持为虚构数据预览。若从源码 ZIP 解压、目录中没有 `.git/`，请先运行 `git init -b main`，再执行下面的 Git 检查命令。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
git status --short --untracked-files=all
```

构建和测试文件均在被忽略的 `artifacts/` 中。提交前查看暂存区：

```powershell
git add .
git diff --cached --stat
git diff --cached
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-public.ps1
```

不要使用 `git add -f` 绕过忽略规则。检查说明文字、图片和历史提交，确保没有账号、个人路径、API Key、token、真实额度或聊天内容。模式扫描不保证能识别所有秘密。

## 2. 创建源码仓库

首次提交前自行确认 Git 作者信息；如希望隐藏邮箱，请使用 GitHub 账号设置中提供的 noreply 邮箱。工具不会替你设置姓名、邮箱或创建带本机身份的提交。

```powershell
git commit -m "Initial public release"
```

在 GitHub 网站创建名为 `gpt-quota-bar` 的空仓库，不预先生成 README 或 LICENSE。按照 GitHub 页面显示的实际地址设置 `origin`，再执行：

```powershell
git push -u origin main
```

`origin` 地址由你创建的仓库决定，不需要写死在源码中。提交此仓库不会自动公开本机安装目录。推送前可以在 GitHub 仓库设置中启用可用的 Secret scanning / Push protection 功能。

## 3. 发布运行包

1. 确认本机 `scripts/package.ps1` 成功，按照 [测试说明](TESTING.md) 做本机验收。
2. 推送版本标签：

   ```powershell
   git tag -a v1.2.0 -m "GPT Quota Bar v1.2.0"
   git push origin v1.2.0
   ```

3. 等待 GitHub Actions 的 `Build and package` 成功。CI 上传 `release-assets` 构建产物，包含运行包、校验值和说明。下载 artifact 后先解开外层归档。
4. 在 GitHub **Releases → Draft a new release**，选择 `v1.2.0`，标题填写 `GPT Quota Bar v1.2.0`，说明使用 `docs/RELEASE_NOTES.md`。
5. 附加 **内层的** `GptQuotaBar-v1.2.0-windows-x64.zip` 和同一次构建的 `SHA256SUMS.txt`，检查后发布。不要上传 artifact 外层归档或整份安装目录。

也可以直接上传本机 `artifacts/releases/` 的 ZIP 和配套校验值。必须使用同一次构建的两个文件；GitHub 自动提供的源码 ZIP 不包含可执行程序。

本工作流只构建和上传 Actions artifact，**不会自动创建 GitHub Release**，也不需要在仓库中配置账号凭据或 PAT。

## 4. 后续版本

同步修改 `src/app.manifest` 的版本和源码中 `initialize` 的 `clientInfo.version`，更新 README、运行包指南、CHANGELOG 与发布说明。新标签必须与 manifest 前三段版本一致；验证脚本会拒绝不一致的标签。

重新构建、检查和发布。不要重用已有版本标签覆盖已发布的运行包。
