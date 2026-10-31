# 桌面待办 · WPF 分支

Windows 桌面待办组件的技术栈重写分支：从 Electron + Vue 迁移到 **C# / .NET 10 / WPF**。
Electron 版（v1.1.1，`F:\AI\VBcode\worktrees\ToDoList`）已发布并冻结为只读参考；本分支只保留基础功能需求。**2026-11-01 起视觉基准改为新外观原型** `docs/参考/新外观原型/1C-时间轴-可交互.html`，旧基准文档已标注作废。

## 阅读顺序（新会话必读）

1. `开场白.md` —— 新会话从这里开始
2. `需求文档-wpf.md` —— 需求基线（A 类定稿 + C 类判定 + 暂不做清单）
3. `docs/规格-窗口尺寸与缩放.md` —— 新外观尺寸与缩放规则；旧 `design/设计说明.md` 已作废
4. `docs/参考/` —— 需要时查阅：Electron 版需求原文 v1.4、验收报告、问题记录、L1 体验说明书、交互原型 v3.4-L1（浏览器打开，应用 v1.1.1 视觉终态）

## 工具链

- .NET 10 SDK（本机已装 10.0.401；`winget install Microsoft.DotNet.SDK.10`）
- IDE：Visual Studio 2022 / Rider / VS Code + C# Dev Kit 均可

## 常用命令（项目根目录）

```powershell
dotnet build          # 编译（等价 typecheck）
dotnet run --project src/TodoWpf
dotnet test           # 单元测试
```

## 目录结构

```
TodoWpf.slnx
src/TodoWpf.Core/     核心数据层（纯 C#，零 WPF 引用：模型/仓储/逾期/结算/迁移/启动流程）
src/TodoWpf/          WPF 应用（net10.0-windows）
src/TodoWpf.Tests/    单元测试（xunit）
src/TodoWpf.DataCheck/ 只读验收探针（读 data.json 打印统计/结算预演/迁移预演）
design/               设计层说明（design-taste-frontend 移交范围）
docs/参考/            Electron 版文档与原型存档（只读）
docs/                 新外观规格、验收记录与流程文档；docs/归档/ 为旧外观批次记录与技术手册
.agents/skills/       项目级技能（design-taste-frontend）
需求文档-wpf.md        需求基线（含文末修订记录）
开场白.md             新会话开场提示词
```

## 分支策略

- 本目录 = 独立仓库（main 分支起步）；与 Electron 版仓库互不干扰

## 开发循环（每批固定走这一圈）

```
需求基线（需求文档-wpf.md）
   ↓
体验说明书（本批：看到什么/怎么操作/什么算通过）──→ 用户批准？──否──→ 修改重交
   ↓ 是
实现 + 单元测试（分笔提交：先测试后实现或同批分笔）
   ↓
dotnet build + dotnet test 全绿 → 推送/CI
   ↓
用户真机验收 → 通过 = 下一批；不过 = 修复后重验
```

- 批次顺序建议：核心数据层（模型/仓储/逾期/结算+单测）→ 三窗壳与设计系统 → 逐模块功能（主页/模板/已完成/历史/设置）
- 核心逻辑保持纯 C#（不掺 UI 依赖），保证可单测；UI 层只做展示与转发
- Electron 版的一切需求语义以 `docs/参考/需求文档-electron-v1.4.md` 为准；冲突时以本仓库 `需求文档-wpf.md` 为准
