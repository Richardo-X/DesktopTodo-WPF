# docs/tools · 自检脚本

## md2checklist.py
把「验收清单」markdown 渲染成可勾选核对页（勾选/反馈存 localStorage，含复制汇总）。
用法：`python docs/tools/md2checklist.py <源.md> <输出.html>`

## ui-selfcheck.ps1
UI 自动化冒烟自检（UIA 驱动，不依赖合成鼠标）。
覆盖：主窗定位与尺寸、常规态、模板窗开关、批量条出现、**勾选框可交互**、**快速连点切换**、全选、收尾复位。
用法：`powershell -NoProfile -ExecutionPolicy Bypass -File docs/tools/ui-selfcheck.ps1`

## verify-dialog.ps1
用**截图像素差**判定二级窗是否真的绘制/关闭（绕开 UIA 对模态窗的可见性差异）。
用法：同上。改脚本内的按钮 AutomationId 可换验不同窗。

## 本机环境限制（重要）
- 屏幕 1024×768 @ DPI 96 → 应用按 scale 0.3 运行（该屏下限），观感不代表用户 2K@125% 真机。
- **本环境不投递合成鼠标点击**（mouse_event 无效）：验证只能走 UIA Invoke / 键盘 / 截图像素差。
  因此**拖拽排序无法自动化验证**，须真机验收。
