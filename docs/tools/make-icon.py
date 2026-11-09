"""生成应用图标 src/app.ico（多尺寸）。

与应用标题栏的品牌标记保持同一套视觉：
  圆角方块，圆角半径 = 5/16 边长；填充 = 主题 accent #4F7391；勾 = #F7FAFC，线宽 = 2.4/24 边长。
  （出处：src/TodoWpf/Windows/MainTodoWindow.xaml 标题栏 Border 16×16 r5 + Icons.xaml Icon.Check）

用法：python docs/tools/make-icon.py [输出路径]
"""
import io
import struct
import sys

from PIL import Image, ImageDraw

ACCENT = (79, 115, 145, 255)      # #4F7391
CHECK = (247, 250, 252, 255)      # #F7FAFC
CORNER_RATIO = 5 / 16             # 圆角半径 / 边长
STROKE_RATIO = 2.4 / 24           # 线宽 / 画布边长
CHECK_PTS = [(5.0, 12.5), (9.5, 17.0), (19.0, 7.5)]   # 24×24 画布上的勾
CANVAS = 24.0
SS = 8                            # 超采样倍数（抗锯齿）


def render(size: int) -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=CORNER_RATIO * s, fill=ACCENT)

    k = s / CANVAS
    pts = [(x * k, y * k) for x, y in CHECK_PTS]
    w = max(2, round(STROKE_RATIO * s))
    d.line(pts, fill=CHECK, width=w, joint="curve")
    r = w / 2
    for p in (pts[0], pts[2]):   # 圆头端帽
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=CHECK)

    return img.resize((size, size), Image.LANCZOS)


def save_ico(images, path: str) -> None:
    """手写 ICO 容器（Vista+ 支持内嵌 PNG），避免依赖 PIL 的 ico 写出限制。"""
    entries = b""
    blobs = b""
    offset = 6 + 16 * len(images)
    for im in images:
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        data = buf.getvalue()
        w = 0 if im.width >= 256 else im.width
        h = 0 if im.height >= 256 else im.height
        entries += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    with open(path, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(images)) + entries + blobs)


def main() -> int:
    out = sys.argv[1] if len(sys.argv) > 1 else "src/app.ico"
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [render(s) for s in sizes]
    save_ico(images, out)
    print("wrote", out, "sizes:", sizes)

    # 预览图（供人工核对观感）：各尺寸并排 + 放大显示
    pad, top = 12, 24
    width = pad + sum(im.width + pad for im in images)
    height = top + 256 + pad
    sheet = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    x = pad
    for im in images:
        sheet.alpha_composite(im, (x, top + (256 - im.height)))
        x += im.width + pad
    big = images[-1]
    sheet.alpha_composite(big, (pad, top + (256 - big.height)))
    sheet.save("icon-preview.png")
    print("wrote icon-preview.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
