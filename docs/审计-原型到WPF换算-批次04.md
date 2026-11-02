# 原型 → WPF 换算与对照审计（批次 04）

> 目的：把「照着原型开发」拆成可核对的**换算公式**与**逐条对照表**，替代凭观感估。
> 范围：主窗 + 模板窗的视觉层（批次 04）；不涉及交互行为。
> 状态：**只读审计，未改任何代码**。
> 基线：`docs/参考/新外观原型/1C-时间轴-可交互.html`（v2.69，CSS px / viewBox 24）。

---

## 一、结论摘要

1. **坐标系是 1:1**：原型 360×640 CSS px 与 WPF 360×640 DIP 同值，**不需要全局缩放系数**。DPI 125% 只改物理像素，不改 DIP 布局。
2. 真正的偏差全在**语义层**，共 5 类：
   - **图标渲染**：SVG `viewBox=24` vs WPF `Stretch=Uniform` —— 系统性，影响全部图标；
   - **表面色令牌值**：`card` / `card-border` / `hover`（日）/ `field`；
   - **盒模型未换算**：计数 chip 高度、模板窗动作行内边距；
   - **样式类映射错误**：模板窗「＋新建」用了 `Sq` 而非 `addbtn`；
   - **令牌陈旧**：`SideWindowWidth 310`、`ClockTimeFont 38`、`ClockDateFont 15`、`RowFont 13.5`、`Radius.Row 8`。

---

## 二、坐标系与单位对齐（①）

| 项 | 原型 | WPF | 结论 |
|---|---|---|---|
| 单位 | CSS px | DIP | 1:1 |
| 主窗布局盒 | 360 × 640 | 360 × 640 | ✓ |
| 模板窗宽 | 260 | 260 | ✓ |
| 模板窗高上限 | max-height 640（原型） | 448（规格定稿） | 按规格，非偏差 |
| 缝隙 | 4 | 4 | ✓ |
| DPI | 浏览器 100% 缩放 | 系统 125% → 物理 450×800 | 不影响 DIP 布局 |
| 字体族 | Segoe UI Variable Text / Display | 同（+ PingFang SC 回退） | ✓ |
| 基准字号 | body 13px | 行文本显式 13 | ✓ |
| 行高 | body 1.5；关键盒显式 | TextBlock 默认（≈1.33） | 固定高行无影响；**自适应盒需显式换算** |

⚠ **前提**：与原型对比时浏览器缩放必须 100%，否则截图比例不可比。

---

## 三、换算规则（②）

### 3.1 颜色：CSS rgba → WPF `#AARRGGBB`

```
A = round(a × 255)，R/G/B 原值转两位十六进制
```

例：`rgba(30,35,42,.12)` → A = 31 = `0x1F` → `#1F1E232A`
例：`rgba(255,255,255,.10)` → A = 26 = `0x1A` → `#1AFFFFFF`

### 3.2 图标：SVG viewBox 24 → WPF

原型：`<svg viewBox="0 0 24 24">`，盒 `P×P`，`stroke-width: S`（视箱单位）。

```
墨迹尺寸 = P × 几何包围盒 / 24
实际线宽 = S × P / 24
```

WPF 现用 `Stretch="Uniform"` + `Width/Height=P`：几何被拉伸填满 P → **墨迹 = P**（偏大），线宽按 1:1 绘制（偏粗）。

**正确做法**：24×24 画布内 `Stretch=None`，外层按 `P/24` 缩放（Viewbox）。

例（主窗新建 ＋）：P=15、包围盒 14 →
- 原型：墨迹 `15×14/24 = 8.75px`，线宽 `2.2×15/24 = 1.375px`
- WPF 现状：墨迹 `15px`（大 71%），线宽 `2.2px`（粗 60%）

### 3.3 盒模型：CSS 文字盒 → WPF

```
容器高 = line-height + 2 × padding
```

例（计数 chip）：`line-height:18 + padding:3×2 = 24`；WPF 未锁行高 ≈ `21`（11px 默认行高约 15 + 6）。

---

## 四、颜色对照表（日 / 夜）

> 原型值已按 3.1 换算为 `#AARRGGBB` 以便逐值比对。

| 令牌 | 用途 | 原型（日） | WPF 日 | 原型（夜） | WPF 夜 | 结论 |
|---|---|---|---|---|---|---|
| Accent | 强调 | `#4F7391` | `#4F7391` | `#8FB0C9` | `#8FB0C9` | ✓ |
| AccentFg | 强调前景 | `#F7FAFC` | `#F7FAFC` | `#0E1418` | `#0E1418` | ✓ |
| Text | 正文 | `#33383F` | `#33383F` | `#DDE2E8` | `#DDE2E8` | ✓ |
| Muted | 次级 | `#5C636C` | `#5C636C` | `#A8B0BA` | `#A8B0BA` | ✓ |
| Faint | 弱化 | `#676D76` | `#676D76` | `#9099A3` | `#9099A3` | ✓ |
| Shell | 窗底 | `#EEF1F6` | `#FFEEF1F6` | `#171A1F` | `#FF171A1F` | ✓ |
| **Card（表面）** | sidebtn 底 | `#F7F8F9` | `Brush.Card #B3FFFFFF`<br>`CardSolid #F9FAFC` | `#1D2127` | `Brush.Card #0EFFFFFF`<br>`CardSolid #2C2C2E` | **✗ 无一相等** |
| **CardBorder** | 描边 | `#1F1E232A` | `#17000000` | `#1AFFFFFF` | `#1CFFFFFF` | **✗** |
| **RowHover/Hover** | 悬停底 | `#0D1E232A` | `#0D000000` | `#0FFFFFFF` | `#0FFFFFFF` | 夜 ✓ / **日 ✗** |
| CounterChip | 计数底 | `#0F1E232A` | `#0F1E232A` | `#14FFFFFF` | `#14FFFFFF` | ✓ |
| RowCard | 行卡底 | `#FFFFFF` | `#FFFFFFFF` | `#252930` | `#FF252930` | ✓ |
| RowCardBorder | 行卡边 | `#0F1E232A` | `#0F1E232A` | `#0EFFFFFF` | `#0EFFFFFF` | ✓ |
| RowCardHover | 行卡悬停 | `#F5F7F9` | `#FFF5F7F9` | `#2B3038` | `#FF2B3038` | ✓ |
| Line | 勾选框边 | `#421E232A` | `#421E232A` | `#38FFFFFF` | `#38FFFFFF` | ✓ |
| Overdue | 逾期 | `#A83A34` | `#A83A34` | `#E08A83` | `#E08A83` | ✓ |
| OverdueSoft | 逾期淡底 | `#1AA83A34` | `#1AA83A34` | `#24E08A83` | `#24E08A83` | ✓ |
| OverdueBorder | 逾期描边 | `#59A83A34` | `#59A83A34` | `#66E08A83` | `#66E08A83` | ✓ |
| DoneStamp | 完成时间 | `#5C636C` | `#FF5C636C` | `#A8B0BA` | `#FFA8B0BA` | ✓ |
| **Field** | 输入面 | `#FFFFFF` | `Brush.Field #E6FFFFFF`<br>`FieldSurface #FFFFFFFF` | `#252930` | `Brush.Field #12FFFFFF`<br>`FieldSurface #FF252930` | Field 令牌 ✗；FieldSurface ✓ |
| FieldBorder | 输入边 | `#1A1E232A` | `#1A1E232A` | `#17FFFFFF` | `#17FFFFFF` | ✓ |
| AccentSoft | 选中淡底 | `#244F7391` | 待核对 | `#2E8FB0C9` | 待核对 | 待核对 |

---

## 五、主窗盒模型对照

| 区域 | 原型 | WPF | 结论 |
|---|---|---|---|
| 标题行 | `padding 12 16 4`；gap 8 | `Margin 16,12,16,4`；品牌 `Margin 8` | ✓ |
| 品牌勾 | 16×16 r5；svg 11 / 2.4 | 同 | ✓ |
| 标题按钮 | 28×28 r6；间距 2；svg 14 / 1.8 | `Ib` 28×28 r6 `Margin 1,0`；14 / 1.8 | 尺寸 ✓ / **渲染 ✗（3.2）** |
| 标题按钮悬停 | bg row-hover；color text | bg `Brush.Hover` | **✗ 日色相** |
| 时钟区 | `padding 6 16 14`；gap 2 | `Margin 0,6,0,14`；日期 `Margin 0,2` | ✓ |
| 时钟字 | 28 / 500 / tabular | 28 / Medium / tabular | ✓ |
| 日期字 | 11 | 13（用户指定） | 按用户 |
| 动作行 | `padding 2 16 12`；gap 8 | `Margin 16,2,16,12`；`Margin 8` | ✓ |
| **计数 chip** | 高 **24**（lh18 + p3×2）r99 | `Padding 9,3` r99，高 ≈ **21** | **✗ 高度** |
| **方形按钮** | 28×28 r6 边 card-border **底 card** | `Sq`：底 **Transparent** | **✗ 底色** |
| **方形按钮悬停** | 边 line + 字 text + 底 row-hover | 仅底 Hover | **✗ 三态不全** |
| 新建按钮 | 28×28 r6 底 accent；svg 15 / 2.2 | 同 | 尺寸 ✓ / 渲染 ✗ |
| 列表容器 | `padding 0 9 26 12` | `Padding 12,0,4,26` | 左/下 ✓；右待实测 |
| 行卡 | min-h 34；`p 3 8`；gap 8；r8；`m 4 0` | H34；`p 3,8`；r8；`m 0,4` | ✓ |
| 点阵 | 14；`margin-left -5` | 14；`Margin -5,0,0,0` | ✓ |
| 行文本 | 13；`padding 0 8 0 6` + gap 8 | 13；`Margin 8,0,8,0` | 左距待实测（按公式差 6） |
| **勾选框** | 16×16 r4 边 line；勾 svg **12** | 16×16 r4；勾 svg **10** | **✗ 勾尺寸** |
| 已完成段标题 | h34 `mt 6`；10px `ls 1` | H34 `m 0,6`；10px；未设 ls | 基本 ✓；字距待核对 |
| 排序按钮 | 16×16；svg 12 / 1.8 | 同 | ✓ |
| 时间轴 | 宽 2、opacity .55 | 宽 2、opacity .55 | ✓ |
| 节点 | 7×7 | 7×7 | ✓ |
| 已完成行 | h34；列 34/16/*/16 | 同 | ✓ |
| 完成时间 | 11 / done-time / pr8 | 同 | ✓ |

---

## 六、模板窗盒模型对照

| 区域 | 原型 | WPF | 结论 |
|---|---|---|---|
| 标题 | `padding 12 14 10`；brand 13 | 行高 40；`Margin 14` | 待实测 |
| **动作行** | `padding 2 10 6`；顺序 chip → **addbtn** → spacer → **trash** | `Margin 14,0,14,8`；顺序 chip → **Sq(＋)** | **✗ 内边距 / 样式 / 缺按钮** |
| 计数 chip | 同主窗（高 24） | chip 11，高 ≈21 | ✗ 高度 |
| **新建** | `addbtn` 28×28 底 accent；svg 15 / 2.2 | `Sq` 透明底；svg 14 / 1.8 | **✗** |
| **批量删除** | `sidebtn` 垃圾桶 28×28 | 无 | **✗ 批次 06** |
| 列表 | `padding 4 5 8 8` | `Padding 8,4,2,8` | 上/左/下 ✓；右待实测 |
| 行 | h36；`p 0 8`；gap 8 | H36；列 `*` / 22 | ✓ |
| 行文本 | 13；左距 8 | 13；`Margin 6,0,8,0` | 左距待实测 |
| **行尾 ＋** | 22×22 r5；svg **12 / 2.2** | 22×22；svg **11 / 2** | **✗ 图标** |

---

## 七、令牌陈旧清单

| 令牌 | 现值 | 原型 / 规格 | 说明 |
|---|---|---|---|
| `Size.SideWindowWidth` | 310 | 260 | 未使用（面板宽走 `PanelLayout`），但会误导 |
| `Size.ClockTimeFont` | 38 | 28 | 未使用（XAML 字面 28） |
| `Size.ClockDateFont` | 15 | 11 | 仅旧预览窗使用 |
| `Size.RowFont` | 13.5 | 13 | 未使用（XAML 字面 13） |
| `Radius.Row` | 8 | 6（`--radius-row`） | 行卡用 8 是对的；令牌语义与原型不同，易误用 |

---

## 八、偏差汇总与建议修复顺序

**P0 · 全局**
1. 图标渲染口径（24 视箱）—— 一条消掉「太大 / 太粗」。

**P1 · 表面观感**
2. 表面色令牌：`CardBorder`、`Card / CardSolid`、`Hover`（日）、`Field`。
3. 方形按钮三态：底色 `card`、边框 `card-border`、悬停（边 line + 字 text + 底 row-hover）。

**P2 · 组件级**
4. 计数 chip 高度 24（锁行高或显式 Height）。
5. 模板窗「＋新建」改 `addbtn`（accent 实心、svg 15 / 2.2）。
6. 模板窗动作行内边距/顺序；行尾 ＋ 改 12 / 2.2；勾选框勾改 12。
7. 列表与行文本左右距微调（待实测后定）。

**P3 · 清理**
8. 陈旧令牌更新或删除。

**批次 06**
9. 模板窗批量删除按钮。

---

## 九、待实测项（不在本表下结论）

- 列表容器右内边距（WPF 4 + 滚动条 8 vs 原型 `padding-right 9`）。
- 行文本左距（WPF `Margin 8` vs 原型 gap 8 + padding-left 6）。
- 模板窗标题行/动作行的垂直内边距。
- `AccentSoft`、`Hairline` 令牌值。
- 标题 `letter-spacing .4`、日期 `letter-spacing .2` 未设 `CharacterSpacing`。
