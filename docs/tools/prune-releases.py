"""清理 releases/ 里过旧的更新包（同时清掉 feed 里的引用，避免留下坏 feed）。

背景：releases/ 是发布脚本的输出目录（staging）。历次测试累积了 20+ 个版本的
full/delta 包（每个约 75MB），会到 GB 级。应用本身已从 GitHub Releases 取更新，
本地目录只用于 vpk 生成增量包——保留最近若干版本即可（更旧的版本不再有增量、走全量下载）。

用法：
  python docs/tools/prune-releases.py --dry-run        # 只看会删什么
  python docs/tools/prune-releases.py --keep 3         # 保留最新 3 个版本
"""
import argparse
import glob
import io
import json
import os
import re
import sys

NUPKG_RE = re.compile(r"^TodoWpf-(?P<ver>\d+(?:\.\d+)*)-(?P<kind>full|delta)\.nupkg$", re.I)


def ver_key(v: str):
    return tuple(int(x) for x in v.split("."))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="releases")
    ap.add_argument("--keep", type=int, default=3, help="保留最新几个版本（默认 3）")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--drop-version", default=None,
                    help="只剔除指定版本（发布脚本重试用：vpk 拒绝同版本重打包）")
    args = ap.parse_args()

    d = args.dir
    if not os.path.isdir(d):
        print("no such dir:", d)
        return 1

    # 1) 收集版本
    versions = {}
    for f in glob.glob(os.path.join(d, "*.nupkg")):
        m = NUPKG_RE.match(os.path.basename(f))
        if m:
            versions.setdefault(m.group("ver"), []).append(f)
    if not versions:
        print("没有 nupkg 可清理")
        return 0

    ordered = sorted(versions, key=ver_key, reverse=True)
    if args.drop_version:
        keep = set(v for v in ordered if v != args.drop_version)
    else:
        keep = set(ordered[: args.keep])
    drop = [v for v in ordered if v not in keep]
    drop_files = [f for v in drop for f in versions[v]]

    freed = sum(os.path.getsize(f) for f in drop_files)
    print(f"版本 {len(ordered)} 个：保留 {sorted(keep, key=ver_key)}，清理 {len(drop)} 个版本 / {len(drop_files)} 个包")
    print(f"预计释放 {freed / 1048576:.0f} MB")
    if args.dry_run:
        for f in sorted(drop_files):
            print("  将删除", os.path.basename(f))
        return 0
    if not drop_files:
        return 0

    drop_names = {os.path.basename(f) for f in drop_files}
    for f in drop_files:
        os.remove(f)

    # 2) 清 feed：releases.win.json（JSON）与 RELEASES（行式）
    jp = os.path.join(d, "releases.win.json")
    if os.path.exists(jp):
        raw = io.open(jp, encoding="utf-8-sig").read()
        doc = json.loads(raw)
        assets = doc.get("Assets", [])
        kept = [a for a in assets if a.get("FileName") not in drop_names]
        doc["Assets"] = kept
        io.open(jp, "w", encoding="utf-8", newline="").write(
            json.dumps(doc, ensure_ascii=False, indent=2))
        print(f"releases.win.json: {len(assets)} → {len(kept)} 条")

    rp = os.path.join(d, "RELEASES")
    if os.path.exists(rp):
        lines = io.open(rp, encoding="utf-8-sig").read().splitlines(True)
        kept_lines = [ln for ln in lines
                      if not any(name in ln for name in drop_names)]
        io.open(rp, "w", encoding="utf-8", newline="").write("".join(kept_lines))
        print(f"RELEASES: {len(lines)} → {len(kept_lines)} 行")

    print("完成")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
