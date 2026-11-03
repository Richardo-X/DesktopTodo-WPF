# 用法：python docs/tools/md2checklist.py <源.md> <输出.html>
# 把「验收清单」markdown 渲染成可勾选核对页：
#   - 勾选状态/各小节问题反馈/整体反馈 全部存 localStorage（刷新不丢）
#   - 顶部进度条 + 分组计数 + 重置 + 「复制汇总」（未勾项+反馈整理成文本，粘给开发即可）
# 源 md 是唯一事实来源；本 HTML 为派生视图。
import sys, os, re, html, hashlib, pathlib, datetime

src = pathlib.Path(sys.argv[1])
out = pathlib.Path(sys.argv[2])
lines = src.read_text(encoding='utf-8').splitlines()


def esc(t: str) -> str:
    return html.escape(t, quote=False)


def inline(t: str) -> str:
    t = esc(t)
    t = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<a href="\2">\1</a>', t)
    t = re.sub(r'`([^`]+)`', r'<code>\1</code>', t)
    t = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', t)
    return t


def item_id(text: str) -> str:
    return hashlib.md5(text.strip().encode('utf-8')).hexdigest()[:10]


title = ''
note_lines = []
sections = []  # [{title, groups:[{section, title, checks, plain, olist}]}]
cur_section = None
cur_group = None


def new_group(section_title: str, group_title: str) -> dict:
    return {'section': section_title, 'title': group_title,
            'checks': [], 'plain': [], 'olist': []}


for raw in lines:
    line = raw.rstrip()
    if line.startswith('# '):
        title = line[2:].strip()
        continue
    if line.startswith('>'):
        note_lines.append(line[1:].strip())
        continue
    if line.strip() == '---':
        continue
    if line.startswith('## '):
        cur_section = {'title': line[3:].strip(), 'groups': []}
        sections.append(cur_section)
        cur_group = None
        continue
    if line.startswith('### '):
        if cur_section is not None:
            cur_group = new_group(cur_section['title'], line[4:].strip())
            cur_section['groups'].append(cur_group)
        continue
    m = re.match(r'^- \[ \] (.*)$', line)
    if m and cur_section is not None:
        if cur_group is None:  # 无 ### 小节的章节（如「五、回归清单」）→ 隐式分组
            cur_group = new_group(cur_section['title'], '')
            cur_section['groups'].append(cur_group)
        text = m.group(1).strip()
        cur_group['checks'].append({'id': item_id(text), 'text': text})
        continue
    m = re.match(r'^- (.*)$', line)
    if m and cur_section is not None:
        if cur_group is None:
            cur_group = new_group(cur_section['title'], '')
            cur_section['groups'].append(cur_group)
        cur_group['plain'].append(m.group(1).strip())
        continue
    m = re.match(r'^\d+\. (.*)$', line)
    if m and cur_group is not None:
        cur_group['olist'].append(m.group(1).strip())
        continue
    if line.strip() and cur_group is not None and cur_group['olist']:
        cur_group['olist'][-1] += line.strip()  # 续行并进上一条编号项

total = sum(len(g['checks']) for s in sections for g in s['groups'])
gen_time = datetime.datetime.now().strftime('%Y-%m-%d %H:%M')


def render_group(g) -> str:
    label = g['title'] or g['section']
    gid = item_id(g['title'] or g['section'])
    h = f'<div class="group" data-label="{esc(label)}">'
    if g['title']:
        h += f'<h3><span class="gtitle">{inline(g["title"])}</span>'
        if g['checks']:
            h += f'<span class="gcount" data-g="{gid}">0/{len(g["checks"])}</span>'
        h += '</h3>'
    elif g['checks']:
        h += f'<h3><span class="gcount" data-g="{gid}">0/{len(g["checks"])}</span></h3>'
    if g['checks']:
        h += '<div class="checks">'
        for c in g['checks']:
            h += (f'<label class="check" data-id="{c["id"]}">'
                  f'<span class="box"></span><span class="text">{inline(c["text"])}</span></label>')
        h += '</div>'
    if g['plain']:
        h += '<ul class="plain">' + ''.join(f'<li>{inline(p)}</li>' for p in g['plain']) + '</ul>'
    if g['olist']:
        h += '<ol class="olist">' + ''.join(f'<li>{inline(p)}</li>' for p in g['olist']) + '</ol>'
    # 每小节末尾的自由反馈区（清单未覆盖的问题写这里）
    h += (f'<div class="fb"><span class="fbl">问题反馈（清单没覆盖的问题写这里，自动保存）</span>'
          f'<textarea class="fbt" data-note="{gid}" placeholder="例：悬停气泡弹出位置偶尔偏下、批量条悬停色不对……"></textarea></div>')
    h += '</div>'
    return h


body = []
for s in sections:
    body.append(f'<section><h2>{inline(s["title"])}</h2>')
    for g in s['groups']:
        body.append(render_group(g))
    body.append('</section>')
# 整体反馈
body.append('<div class="group global"><h3><span class="gtitle">整体反馈（任何清单没地方写的问题）</span></h3>'
            '<textarea class="fbt" id="globalNote" placeholder="例：整体动效偏快、某窗口位置越界、验收流程建议……"></textarea></div>')
body_html = '\n'.join(body)
note_html = '<br>'.join(inline(n) if n else '&nbsp;' for n in note_lines)

CSS = """
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:"Segoe UI Variable Text","Segoe UI","Microsoft YaHei UI",sans-serif;
  background:#eef1f6;color:#33383f;font-size:13px;line-height:1.6;padding-bottom:60px}
.topbar{position:sticky;top:0;z-index:10;background:rgba(238,241,246,.96);backdrop-filter:blur(6px);
  border-bottom:1px solid rgba(30,35,42,.12);padding:10px 16px 8px}
.topbar .row{max-width:920px;margin:0 auto;display:flex;align-items:center;gap:10px}
.topbar h1{font-size:15px;font-weight:600;flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.topbar button{height:26px;padding:0 12px;border:1px solid rgba(30,35,42,.12);border-radius:6px;
  background:transparent;color:#5c636c;font-size:11.5px;cursor:pointer;font-family:inherit;white-space:nowrap}
.topbar button:hover{background:rgba(30,35,42,.05);color:#33383f}
.progress{max-width:920px;margin:6px auto 0;display:flex;align-items:center;gap:10px}
.progress .track{flex:1;height:6px;background:rgba(30,35,42,.10);border-radius:3px;overflow:hidden}
.progress .bar{height:100%;width:0;background:#4f7391;border-radius:3px;transition:width .2s}
.progress .num{font-size:12px;color:#5c636c;font-variant-numeric:tabular-nums;white-space:nowrap}
.wrap{max-width:920px;margin:14px auto 0;padding:0 16px}
.note{background:#fff;border:1px solid rgba(30,35,42,.08);border-radius:10px;
  padding:12px 16px;color:#5c636c;font-size:12.5px;margin-bottom:14px}
.note code{background:rgba(30,35,42,.06);border-radius:4px;padding:1px 5px;font-size:11.5px}
section{margin-bottom:18px}
section > h2{font-size:14px;font-weight:600;margin:0 0 10px}
.group{background:#fff;border:1px solid rgba(30,35,42,.08);border-radius:10px;
  padding:10px 16px 12px;margin-bottom:10px}
.group.global{border-color:rgba(79,115,145,.35)}
.group h3{font-size:13px;font-weight:600;display:flex;align-items:center;gap:8px;margin:2px 0 8px}
.gcount{font-size:11px;color:#fff;background:#4f7391;border-radius:9px;padding:1px 8px;
  font-weight:500;font-variant-numeric:tabular-nums}
.gcount.done{background:#3d8361}
.checks .check{display:flex;align-items:flex-start;gap:10px;padding:7px 8px;margin:0 -8px;
  border-radius:8px;cursor:pointer;user-select:none}
.checks .check:hover{background:rgba(30,35,42,.04)}
.checks .check + .check{border-top:1px dashed rgba(30,35,42,.06)}
.check .box{flex:none;width:16px;height:16px;margin-top:2px;border:1px solid rgba(30,35,42,.26);
  border-radius:4px;background:#fff;position:relative;transition:background .12s,border-color .12s}
.check.on .box{background:#4f7391;border-color:#4f7391}
.check .box::after{content:"";position:absolute;left:4.5px;top:1.5px;width:4px;height:8px;
  border:solid #f7fafc;border-width:0 2px 2px 0;transform:rotate(45deg);opacity:0}
.check.on .box::after{opacity:1}
.check .text{flex:1;min-width:0}
.check.on .text{color:#676d76;text-decoration:line-through;text-decoration-color:rgba(103,109,118,.5)}
.check .text code,.note code,ul.plain code,ol.olist code{background:rgba(30,35,42,.06);
  border-radius:4px;padding:1px 5px;font-size:11.5px}
ul.plain{margin:6px 0 0 8px;color:#5c636c}
ul.plain li{margin:3px 0;list-style:none;position:relative;padding-left:12px}
ul.plain li::before{content:"·";position:absolute;left:0;color:#676d76}
ol.olist{margin:6px 0 0 8px;color:#5c636c;padding-left:18px}
ol.olist li{margin:3px 0}
.fb{margin-top:10px;border-top:1px dashed rgba(30,35,42,.08);padding-top:8px}
.fbl{display:block;font-size:11px;color:#676d76;margin-bottom:4px}
textarea.fbt{width:100%;min-height:44px;resize:vertical;border:1px solid rgba(30,35,42,.14);
  border-radius:6px;padding:6px 9px;font:inherit;font-size:12px;color:#33383f;background:#fbfcfd;outline:none}
textarea.fbt:focus{border-color:#4f7391;box-shadow:0 0 0 3px rgba(79,115,145,.14)}
footer{max-width:920px;margin:16px auto 0;padding:0 16px;color:#9099a3;font-size:11px}
a{color:#4f7391}
#toast{position:fixed;left:50%;bottom:26px;transform:translateX(-50%);z-index:20;
  background:rgba(20,24,30,.94);color:#eef3fa;font-size:12px;padding:8px 14px;border-radius:8px;
  opacity:0;pointer-events:none;transition:opacity .15s}
#toast.show{opacity:1}
"""

JS = """
const KEY = 'checklist-05to09-v2';
const OLD = 'checklist-05to09-v1';
let state = null;
try { state = JSON.parse(localStorage.getItem(KEY) || 'null'); } catch (e) { state = null; }
if (!state || typeof state !== 'object' || !state.checks) {
  let old = {};
  try { old = JSON.parse(localStorage.getItem(OLD) || '{}'); } catch (e) { old = {}; }
  state = { checks: old, notes: {}, global: '' };  // 兼容旧版纯勾选数据
}
const checks = Array.from(document.querySelectorAll('.check'));
const noteBoxes = Array.from(document.querySelectorAll('.fbt'));
const groups = Array.from(document.querySelectorAll('.group'));
const toast = document.getElementById('toast');

function save() { localStorage.setItem(KEY, JSON.stringify(state)); }

function showToast(msg) {
  toast.textContent = msg;
  toast.classList.add('show');
  clearTimeout(showToast._t);
  showToast._t = setTimeout(() => toast.classList.remove('show'), 1800);
}

function groupLabel(el) {
  const g = el.closest('.group');
  if (!g) return '其他';
  const t = g.querySelector('.gtitle');
  return (t && t.textContent.trim()) || g.dataset.label || '其他';
}

function apply() {
  let done = 0;
  for (const el of checks) {
    const on = !!state.checks[el.dataset.id];
    el.classList.toggle('on', on);
    if (on) done++;
  }
  document.getElementById('num').textContent = '已核对 ' + done + ' / ' + checks.length;
  document.getElementById('bar').style.width = (checks.length ? done * 100 / checks.length : 0) + '%';
  for (const g of groups) {
    const list = g.querySelectorAll('.check');
    if (!list.length) continue;
    const d = Array.from(list).filter(x => x.classList.contains('on')).length;
    const badge = g.querySelector('.gcount');
    if (badge) {
      badge.textContent = d + '/' + list.length;
      badge.classList.toggle('done', d === list.length);
    }
  }
}

for (const el of checks) {
  el.addEventListener('click', () => {
    const id = el.dataset.id;
    if (state.checks[id]) delete state.checks[id]; else state.checks[id] = true;
    save();
    apply();
  });
}

for (const ta of noteBoxes) {
  const key = ta.dataset.note;
  ta.value = key === 'global' ? (state.global || '') : (state.notes[key] || '');
  const grow = () => { ta.style.height = 'auto'; ta.style.height = Math.max(44, ta.scrollHeight + 2) + 'px'; };
  ta.addEventListener('input', () => {
    if (key === 'global') state.global = ta.value; else state.notes[key] = ta.value;
    save();
    grow();
  });
  grow();
}

function summary() {
  const lines = [];
  const done = checks.filter(x => x.classList.contains('on')).length;
  lines.push('验收进度：' + done + ' / ' + checks.length);
  const undone = checks.filter(x => !x.classList.contains('on'));
  if (undone.length) {
    lines.push('', '未勾选项：');
    const byGroup = new Map();
    for (const el of undone) {
      const label = groupLabel(el);
      if (!byGroup.has(label)) byGroup.set(label, []);
      byGroup.get(label).push(el.querySelector('.text').textContent.trim());
    }
    for (const [label, arr] of byGroup) {
      lines.push('【' + label + '】');
      for (const t of arr) lines.push('  - ' + t);
    }
  }
  const fb = [];
  for (const ta of noteBoxes) {
    const v = ta.value.trim();
    if (!v) continue;
    const label = ta.id === 'globalNote' ? '整体' : groupLabel(ta);
    fb.push('【' + label + '】');
    for (const ln of v.split(/\\r?\\n/)) fb.push('  ' + ln);
  }
  if (fb.length) {
    lines.push('', '问题反馈：');
    for (const l of fb) lines.push(l);
  }
  return lines.join('\\n');
}

document.getElementById('copyBtn').addEventListener('click', () => {
  const text = summary();
  const done = () => showToast('汇总已复制，粘贴给开发即可');
  const fail = () => {
    const ta = document.createElement('textarea');
    ta.value = text;
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); done(); } catch (e) { showToast('复制失败，请手动选中复制'); }
    ta.remove();
  };
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).then(done, fail);
  } else {
    fail();
  }
});

document.getElementById('reset').addEventListener('click', () => {
  if (!confirm('清空全部勾选与反馈记录？')) return;
  state = { checks: {}, notes: {}, global: '' };
  localStorage.removeItem(KEY);
  localStorage.removeItem(OLD);
  for (const ta of noteBoxes) ta.value = '';
  apply();
  for (const ta of noteBoxes) ta.dispatchEvent(new Event('input'));
  showToast('已重置');
});

apply();
"""

page = ("<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n<meta charset=\"UTF-8\">\n"
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n"
        f"<title>{esc(title)}</title>\n<style>{CSS}</style>\n</head>\n<body>\n"
        "<div class=\"topbar\">\n  <div class=\"row\">\n"
        f"    <h1>{esc(title)}</h1>\n"
        "    <button id=\"copyBtn\">复制汇总</button>\n"
        "    <button id=\"reset\">重置全部</button>\n"
        "  </div>\n"
        "  <div class=\"progress\">\n"
        "    <div class=\"track\"><div class=\"bar\" id=\"bar\"></div></div>\n"
        f"    <span class=\"num\" id=\"num\">已核对 0 / {total}</span>\n"
        "  </div>\n</div>\n"
        f"<div class=\"wrap\">\n  <div class=\"note\">{note_html}</div>\n  {body_html}\n</div>\n"
        f"<footer>由 验收清单-一次性落地-批次05to09.md 渲染（docs/tools/md2checklist.py）· 生成于 {gen_time} ·"
        " 勾选与反馈保存在本浏览器（localStorage）</footer>\n"
        f"<div id=\"toast\"></div>\n<script>{JS}</script>\n</body>\n</html>\n")

out.write_text(page, encoding='utf-8')
print(f'generated: {out}  ({total} items)')
os.startfile(str(out))
print('opened in browser')
