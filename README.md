# 桌面待办 · WPF 分支

Windows 桌面待办组件的技术栈重写分支：从 Electron + Vue 迁移到 **C# / .NET 10 / WPF**。
Electron 版（v1.1.1，`F:\AI\VBcode\worktrees\ToDoList`）已发布并冻结为只读参考；本分支只保留基础功能需求，设计层交给 design-taste-frontend 技能。

## 阅读顺序（新会话必读）

1. `开场白.md` —— 新会话从这里开始
2. `需求文档-wpf.md` —— 需求基线（A 类定稿 + C 类判定 + 暂不做清单）
3. `design/设计说明.md` —— 设计层范围与"技能→XAML"翻译表
4. `docs/参考/` —— 需要时查阅：Electron 版需求原文 v1.4、验收报告、问题记录、L1 体验说明书、交互原型 v3.4-taste（浏览器打开）

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
TodoWpf.sln
src/TodoWpf/          WPF 应用（net10.0-windows）
src/TodoWpf.Tests/    单元测试（xunit）
design/               设计层说明（design-taste-frontend 移交范围）
docs/参考/            Electron 版文档与原型存档（只读）
.agents/skills/       项目级技能（design-taste-frontend）
需求文档-wpf.md        需求基线
开场白.md             新会话开场提示词
```

## 分支策略

- 本目录 = 独立仓库（main 分支起步）；与 Electron 版仓库互不干扰
- Electron 版的一切需求语义以 `docs/参考/需求文档-electron-v1.4.md` 为准；冲突时以本仓库 `需求文档-wpf.md` 为准
