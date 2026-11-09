"""生成应用图标（多方案 + 对比图）。

设计依据：Microsoft《Design guidelines for Windows app icons》
  - 隐喻单一、可无标签辨认；不加装饰元素
  - 软圆角、轮廓"少形状少尖角"、小尺寸可辨认
  - 渐变克制（1~2 档，约 120°），不做紧过渡的"反光感"
  - 扁平图层叠放，层间用投影区分（亮物投在暗底效果最好）
  - 暗/中/亮三档色值都用上，保证明暗主题下都有对比
  - 正视图，不做等距/3D

用法：
  python docs/tools/make-icon.py sheet          # 只出候选对比图 icon-candidates.png
  python docs/tools/make-icon.py ico <编号> <输出路径>   # 按选定方案出 src/app.ico
"""
import io
import math
import struct
import sys

from PIL import Image, ImageDraw, ImageFilter

# ---- 调色（应用 accent #4F7391 的同色相三档，符合"暗/中/亮"要求）----
ACCENT = (79, 115, 145)        # #4F7391 中
ACCENT_DARK = (56, 84, 108)    # 暗
ACCENT_LIGHT = (108, 145, 175)  # 亮
INK = (247, 250, 252)          # #F7FAFC 近白
SHELL = (238, 241, 246)        # #FFEEF1F6 应用外壳色

SS = 4          # 超采样
BASE = 256      # 基准渲染尺寸


def squircle_mask(size: int, n: float = 3.4, inset: float = 0.0) -> Image.Image:
    """超椭圆（squircle）遮罩：比普通圆角矩形更"软"，是 Win11 观感的来源。"""
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    a = size / 2 * (1 - inset)
    cx = cy = size / 2
    pts = []
    for i in range(720):
        t = 2 * math.pi * i / 720
        ct, st = math.cos(t), math.sin(t)
        x = a * math.copysign(abs(ct) ** (2 / n), ct)
        y = a * math.copysign(abs(st) ** (2 / n), st)
        pts.append((cx + x, cy + y))
    d.polygon(pts, fill=255)
    return m


def round_mask(size: int, radius_ratio: float) -> Image.Image:
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size - 1, size - 1],
                                        radius=size * radius_ratio, fill=255)
    return m


def gradient(size: int, c1, c2, angle_deg: float = 120.0) -> Image.Image:
    """线性渐变（默认 120°，符合规范）。"""
    img = Image.new("RGB", (size, size))
    px = img.load()
    rad = math.radians(angle_deg)
    ux, uy = math.cos(rad), math.sin(rad)
    proj = [x * ux + y * uy for x in (0, size) for y in (0, size)]
    lo, hi = min(proj), max(proj)
    for y in range(size):
        for x in range(size):
            t = ((x * ux + y * uy) - lo) / (hi - lo)
            px[x, y] = tuple(round(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))
    return img


def shadow(layer: Image.Image, blur: int, offset=(0, 0), alpha=90) -> Image.Image:
    """投影层：亮色物体投在暗底上，用于层间区分。"""
    a = layer.split()[3].point(lambda v: min(255, int(v * alpha / 255)))
    sh = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    sh.putalpha(a)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    return sh.transform(layer.size, Image.AFFINE, (1, 0, -offset[0], 0, 1, -offset[1]))


def rounded_polyline(size, pts, width, color, caps=True):
    """圆头折线（用于勾/清单线）。"""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.line(pts, fill=color, width=width, joint="curve")
    if caps:
        r = width / 2
        for p in (pts[0], pts[-1]):
            d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=color)
    return layer


def canvas(size=BASE):
    return Image.new("RGBA", (size, size), (0, 0, 0, 0))


# ---------------------------------------------------------------- 方案 1
def v1_tile_check(size=BASE):
    """渐变磁贴 + 白勾（在旧版基础上按规范重做：squircle、克制渐变、勾更细致、加层间投影）"""
    img = canvas(size)
    mask = squircle_mask(size)
    tile = gradient(size, ACCENT_LIGHT, ACCENT_DARK, 120).convert("RGBA")
    img.paste(tile, (0, 0), mask)

    # 顶部柔和高光：**竖直渐变**（用椭圆硬边会在磁贴上留下可见接缝，2026-11-09 实测）
    hl = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hp = hl.load()
    for y in range(size):
        t = y / size
        a = 0 if t >= 0.42 else round(46 * (1 - t / 0.42) ** 1.6)
        for x in range(size):
            hp[x, y] = (255, 255, 255, a)
    img = Image.alpha_composite(img, Image.composite(hl, Image.new("RGBA", (size, size), (0, 0, 0, 0)), mask))

    k = size / 48.0
    pts = [(14.2 * k, 24.6 * k), (21.2 * k, 31.6 * k), (34.2 * k, 17.2 * k)]
    check = rounded_polyline(size, pts, round(5.4 * k), INK + (255,))
    img = Image.alpha_composite(img, shadow(check, blur=int(3.5 * k), offset=(0, int(2.2 * k)), alpha=75))
    return Image.alpha_composite(img, check)


# ---------------------------------------------------------------- 方案 2
def v2_light_card_check(size=BASE):
    """浅色卡片 + 蓝勾（呼应应用浅色外壳；亮底物体投在浅背景上）"""
    img = canvas(size)
    mask = round_mask(size, 0.235)
    img.paste(Image.new("RGBA", (size, size), SHELL + (255,)), (0, 0), mask)
    # 卡片描边
    edge = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle([1, 1, size - 2, size - 2],
                                           radius=size * 0.235, outline=(150, 163, 178, 90), width=max(1, size // 128))
    img = Image.alpha_composite(img, edge)

    k = size / 48.0
    pts = [(14.2 * k, 24.6 * k), (21.2 * k, 31.6 * k), (34.2 * k, 17.2 * k)]
    check = rounded_polyline(size, pts, round(6.0 * k), ACCENT + (255,))
    img = Image.alpha_composite(img, shadow(check, blur=int(2.5 * k), offset=(0, int(1.5 * k)), alpha=60))
    return Image.alpha_composite(img, check)


# ---------------------------------------------------------------- 方案 3
def v3_list_check(size=BASE):
    """清单两行 + 勾（小尺寸可辨认优先；隐喻贴"待办"）"""
    img = canvas(size)
    mask = squircle_mask(size)
    img.paste(gradient(size, ACCENT_LIGHT, ACCENT, 120).convert("RGBA"), (0, 0), mask)

    k = size / 48.0
    body = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)
    for y, x1, x2 in [(19.5, 14.5, 34.5), (29.5, 14.5, 30.0)]:
        d.line([(x1 * k, y * k), (x2 * k, y * k)], fill=INK + (235,), width=round(3.4 * k))
        r = 1.7 * k
        for x in (x1, x2):
            d.ellipse([x * k - r, y * k - r, x * k + r, y * k + r], fill=INK + (235,))
    check = rounded_polyline(size, [(7.0 * k, 19.5 * k), (9.8 * k, 22.3 * k), (15.5 * k, 16.2 * k)],
                             round(3.0 * k), INK + (255,))
    body = Image.alpha_composite(body, check)
    img = Image.alpha_composite(img, shadow(body, blur=int(2 * k), offset=(0, int(1 * k)), alpha=55))
    return Image.alpha_composite(img, body)


# ---------------------------------------------------------------- 方案 4
def v4_dark_tile_check(size=BASE):
    """深色磁贴 + 浅色勾（更沉稳；勾用近白而非纯白，留一点层次）"""
    img = canvas(size)
    mask = squircle_mask(size)
    img.paste(gradient(size, ACCENT, (62, 92, 118), 120).convert("RGBA"), (0, 0), mask)

    k = size / 48.0
    check = rounded_polyline(size, [(13 * k, 25 * k), (21 * k, 33 * k), (36 * k, 16 * k)],
                             round(6.5 * k), INK + (255,))
    img = Image.alpha_composite(img, shadow(check, blur=int(3 * k), offset=(0, int(2 * k)), alpha=80))
    return Image.alpha_composite(img, check)


# ---------------------------------------------------------------- 方案 5
def v5_note_check(size=BASE):
    """便签（折角）+ 勾：更"待办清单"的具象隐喻"""
    img = canvas(size)
    fold = size * 0.26
    # 纸张轮廓（右下折角）
    body = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(body)
    r = size * 0.20
    d.rounded_rectangle([size * 0.06, size * 0.06, size * 0.94, size * 0.94], radius=r, fill=SHELL + (255,))
    d.polygon([(size * 0.94 - fold, size * 0.94), (size * 0.94, size * 0.94 - fold),
               (size * 0.94, size * 0.94)], fill=(205, 214, 224, 255))
    img = Image.alpha_composite(img, shadow(body, blur=int(size * 0.02), offset=(0, int(size * 0.012)), alpha=90))
    img = Image.alpha_composite(img, body)

    k = size / 48.0
    check = rounded_polyline(size, [(12 * k, 22 * k), (19 * k, 29 * k), (34 * k, 14 * k)],
                             round(5.2 * k), ACCENT + (255,))
    return Image.alpha_composite(img, check)


# ---------------------------------------------------------------- 方案 6
def v6_circle_check(size=BASE):
    """圆形徽章 + 勾（更柔和，与方形图标区分度高）"""
    img = canvas(size)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, size - 1, size - 1], fill=255)
    img.paste(gradient(size, ACCENT_LIGHT, ACCENT, 120).convert("RGBA"), (0, 0), mask)

    k = size / 48.0
    check = rounded_polyline(size, [(14.5 * k, 24.5 * k), (21.5 * k, 31.5 * k), (34 * k, 17 * k)],
                             round(5.6 * k), INK + (255,))
    img = Image.alpha_composite(img, shadow(check, blur=int(3 * k), offset=(0, int(2 * k)), alpha=70))
    return Image.alpha_composite(img, check)



# ---------------------------------------------------------------- 方案 7
def v7_layered_card(size=BASE):
    """分层：浅色清单卡浮在 accent 磁贴上（规范"扁平图层 + 层间投影"的典型做法）"""
    img = canvas(size)
    img.paste(gradient(size, ACCENT_LIGHT, ACCENT, 120).convert("RGBA"), (0, 0), squircle_mask(size))

    k = size / 48.0
    card = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(card).rounded_rectangle(
        [10 * k, 9 * k, 38 * k, 39 * k], radius=3.2 * k, fill=INK + (255,))
    img = Image.alpha_composite(img, shadow(card, blur=int(2.6 * k), offset=(0, int(1.6 * k)), alpha=95))
    img = Image.alpha_composite(img, card)

    lines = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(lines)
    for y, x1, x2 in [(16.5, 17.5, 33.0), (22.0, 17.5, 30.0), (27.5, 17.5, 33.0)]:
        d.line([(x1 * k, y * k), (x2 * k, y * k)], fill=(150, 162, 176, 255), width=round(1.7 * k))
        r = 0.85 * k
        for x in (x1, x2):
            d.ellipse([x * k - r, y * k - r, x * k + r, y * k + r], fill=(150, 162, 176, 255))
    d.line([(14.2 * k, 16.5 * k), (15.6 * k, 18.0 * k), (17.4 * k, 15.4 * k)], fill=ACCENT + (255,), width=round(1.5 * k), joint="curve")
    img = Image.alpha_composite(img, lines)

    check = rounded_polyline(size, [(17.5 * k, 33.0 * k), (21.5 * k, 36.5 * k), (31.0 * k, 27.5 * k)],
                             round(3.4 * k), ACCENT + (255,))
    return Image.alpha_composite(img, check)


VARIANTS = {
    1: ("渐变磁贴+白勾", v1_tile_check),
    2: ("浅卡+蓝勾", v2_light_card_check),
    3: ("清单+勾", v3_list_check),
    4: ("深磁贴+白勾", v4_dark_tile_check),
    5: ("便签折角+勾", v5_note_check),
    6: ("圆形徽章+勾", v6_circle_check),
    7: ("分层清单卡", v7_layered_card),
}


def render_variant(idx: int, size: int) -> Image.Image:
    big = VARIANTS[idx][1](BASE)
    return big.resize((size, size), Image.LANCZOS)


def make_sheet(path="icon-candidates.png"):
    sizes = [128, 48, 32, 16]
    pad, label_h = 14, 26
    row_h = 128 + pad
    width = pad + 128 + pad + sum(s + 10 for s in sizes[1:]) + pad
    height = pad + len(VARIANTS) * row_h + pad
    sheet = Image.new("RGBA", (width, height), (245, 246, 248, 255))
    d = ImageDraw.Draw(sheet)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 15)
    except Exception:
        font = None
    y = pad
    for idx in sorted(VARIANTS):
        name = f"{idx}. {VARIANTS[idx][0]}"
        d.text((pad, y + 4), name, fill=(40, 45, 52, 255), font=font)
        x = pad
        for i, s in enumerate(sizes):
            im = render_variant(idx, s)
            sheet.alpha_composite(im, (x, y + label_h + (128 - s) // 2))
            x += (128 if i == 0 else s) + (pad if i == 0 else 10)
        y += row_h
    sheet.save(path)
    print("wrote", path)


def make_ico(idx: int, out: str):
    sizes = [16, 24, 32, 48, 64, 128, 256]
    imgs = [render_variant(idx, s) for s in sizes]
    entries, blobs, offset = b"", b"", 6 + 16 * len(imgs)
    for im in imgs:
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        data = buf.getvalue()
        w = 0 if im.width >= 256 else im.width
        entries += struct.pack("<BBBBHHII", w, w, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    with open(out, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(imgs)) + entries + blobs)
    print("wrote", out, "variant", idx, "sizes", sizes)


def main() -> int:
    if len(sys.argv) >= 2 and sys.argv[1] == "ico":
        make_ico(int(sys.argv[2]), sys.argv[3] if len(sys.argv) > 3 else "src/app.ico")
        return 0
    make_sheet()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
