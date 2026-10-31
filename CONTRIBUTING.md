# 贡献指南

请先阅读 README 和 [测试说明](docs/TESTING.md)。保持 C# 5 / .NET Framework 编译器兼容，不引入不必要的依赖。

- 修改应聚焦一个问题，保留鼠标穿透、不抢焦点和原生子窗口跟随行为。
- 提交前运行 `scripts/verify.ps1` 和 `scripts/package.ps1`；窗口相关修改另做交互回归与人工验收。
- Issue 和 PR 只描述复现步骤、版本、Windows 版本、DPI、预期及实际结果。所有截图和诊断先自行脱敏。
- 不提交账号、API Key、token、个人绝对路径、真实额度记录或构建产物。不要修改忽略规则来上传本机诊断。
- 安全问题请按 [SECURITY.md](SECURITY.md) 处理。

提交贡献即表示同意以本仓库的 MIT License 许可该贡献。
