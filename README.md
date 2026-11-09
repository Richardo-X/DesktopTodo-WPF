# 桌面待办 · WPF 版

Windows 桌面悬浮待办小组件：免登录、数据只存本地、不占任务栏、托盘控制显隐。
本仓库是**技术栈重写分支**：从 Electron + Vue 迁移到 **C# / .NET 10 / WPF**。

## 下载安装

到本仓库的 [Releases](https://github.com/Richardo-X/DesktopTodo-WPF/releases) 页面下载最新的
**安装程序**（`DesktopTodoWPF-Setup-<版本>.exe`），双击运行即可；安装时可自行选择安装位置
（默认装到用户目录，不需要管理员权限）。

## 三个仓库的关系

| 仓库 | 内容 | 状态 |
|---|---|---|
| [DesktopTodo](https://github.com/Richardo-X/DesktopTodo) | Electron + Vue3 版 | 已发布，冻结为只读参考 |
| [DesktopTodo-WPF-old](https://github.com/Richardo-X/DesktopTodo-WPF-old) | WPF **旧外观**版（照搬 Electron 观感） | **已弃用归档**，仅作复盘参考 |
| **本仓库** | WPF **新外观**版（按 1C 时间轴原型重做 UI） | 当前开发与发布版本 |

旧外观线与本版的根本区别：**核心数据层（`src/TodoWpf.Core`）是延续的**（32 个文件只改 14 个），
被整体重做的是视觉与交互层（UI 层 +7053/−954 行），且窗口结构从「三窗（含已完成窗）」
改为「两窗（已完成并入主窗）+ 设置/历史/详情等二级窗」。

## 更新

应用内自带更新：有新版本时标题栏会出现「有新版本！」提示，点击即可更新并自动重启；
也可在设置页点「检查更新」。更新从本仓库的 Releases 获取。
设置页的「新版本提示」开关可关闭自动检查（关闭后不再联网检查）。

- 数据保存在 `%APPDATA%\桌面待办-WPF\data.json`，卸载时可选是否一并删除。
- 联网口径：仅「检查/下载更新」联网，其余功能与数据均不依赖网络。

---

## 开发

Electron 版（v1.1.1，`Electron 版仓库`）已发布并冻结为只读参考；本分支只保留基础功能需求。
**2026-11-01 起视觉基准改为新外观原型** `docs/参考/新外观原型/1C-时间轴-可交互.html`，旧基准文档已标注作废。

### 阅读顺序（新会话必读）

1. `开场白.md` —— 新会话从这里开始
2. `需求文档-wpf.md` —— **现行需求基线（整合版，只描述新外观）**；历次裁定的原文与出处见文末索引，旧基线归档在 `docs/归档/`
3. `docs/规格-窗口尺寸与缩放.md` —— 新外观尺寸与缩放规则；旧 `design/设计说明.md` 已作废
4. `docs/参考/` —— 需要时查阅：Electron 版需求原文 v1.4、验收报告、问题记录、L1 体验说明书、交互原型 v3.4-L1

### 工具链

- .NET 10 SDK（`winget install Microsoft.DotNet.SDK.10`）
- IDE：Visual Studio 2022 / Rider / VS Code + C# Dev Kit 均可

### 常用命令（项目根目录）

```powershell
dotnet build          # 编译（等价 typecheck）
dotnet run --project src/TodoWpf
dotnet test           # 单元测试
```

### 发布（打包 + 上传到本仓库 Releases）

```powershell
# 需要本机 gh 已登录；会自动出安装器并上传到 GitHub Releases
powershell -File docs/tools/publish-release.ps1 -Version 1.2.9 -Notes "本次更新内容"
# 仅本地出包不上传（调试）：加 -SkipUpload
# 清理本地累积的旧包：python docs/tools/prune-releases.py --keep 3
```

### 目录结构

```
TodoWpf.slnx
src/TodoWpf.Core/      核心数据层（纯 C#，零 WPF 引用：模型/仓储/逾期/结算/迁移/启动流程）
src/TodoWpf/           WPF 应用（net10.0-windows）
src/TodoWpf.Setup/     自建安装器（可选安装位置；Velopack 自带 Setup 无目录选择界面）
src/TodoWpf.Tests/     单元测试（xunit）
src/TodoWpf.DataCheck/ 只读验收探针（读 data.json 打印统计/结算预演/迁移预演）
src/app.ico            应用图标（由 docs/tools/make-icon.py 生成）
docs/tools/            开发工具：发布脚本、图标生成、releases 清理、UIA 自检脚本
docs/参考/             Electron 版文档与原型存档（只读）
docs/                  新外观规格、验收记录与流程文档；docs/归档/ 为旧外观批次记录
需求文档-wpf.md         现行需求基线（整合版 v1.0；旧基线含 R1~R6 在 docs/归档/）
开场白.md              新会话开场提示词
AGENTS.md              协作规定（开发流程与纪律）
```

### 开发循环（每批固定走这一圈）

```
需求基线（需求文档-wpf.md）
   ↓
体验说明书（本批：看到什么/怎么操作/什么算通过）──→ 用户批准？──否──→ 修改重交
   ↓ 是
实现 + 单元测试（核心逻辑先写测试再实现；测试与实现分笔提交）
   ↓
dotnet build + dotnet test 全绿 → 推送
   ↓
真机验收 → 通过 = 下一批；不过 = 修复后重验
```

- 核心逻辑保持纯 C#（不掺 UI 依赖），保证可单测；UI 层只做展示与转发
- Electron 版的一切需求语义以 `docs/参考/需求文档-electron-v1.4.md` 为准；冲突时以本仓库 `需求文档-wpf.md` 为准
