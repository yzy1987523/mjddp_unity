# -*- coding: utf-8 -*-
"""
生成软著源代码 PDF（前/后 60 页，每页 >=50 行，页眉含软件名称与版本号）。
用法：修改下方 SOFT_NAME / SOFT_VER 后运行：
    python doc/gen_soft_src.py
"""
import os, glob
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont

ROOT = r"c:/Custom/MyGames/mjddp_unity"
SRC_DIRS = [
    os.path.join(ROOT, "Assets", "Scripts"),
    os.path.join(ROOT, "Assets", "Editor"),
]
OUT = os.path.join(ROOT, "doc", "soft_copyright_source.pdf")

# ===== 需用户填写（须与软著申请表一致） =====
SOFT_NAME = "清风拾集游戏软件"      # 软件全称
SOFT_VER = "V1.0"                # 版本号
# ===========================================

# 注册内置中文 CID 字体（支持中文注释，无需系统字体文件）
pdfmetrics.registerFont(UnicodeCIDFont("STSong-Light"))
FONT = "STSong-Light"

# 1) 收集用户自主开发的 .cs 文件（排除第三方库）
files = []
for d in SRC_DIRS:
    for f in glob.glob(os.path.join(d, "**", "*.cs"), recursive=True):
        files.append(f)
files.sort(key=lambda p: os.path.relpath(p, ROOT))

# 2) 拼接为视觉行（去空行，超长行折行，保证完整不丢代码）
lines = []
for f in files:
    rel = os.path.relpath(f, ROOT)
    lines.append("// ===== FILE: %s =====" % rel)
    with open(f, "r", encoding="utf-8", errors="ignore") as fh:
        for raw in fh:
            s = raw.rstrip("\n").rstrip("\r")
            if s.strip() == "":
                continue
            # 超长行按 58 字符折行（续行缩进 4 空格）
            MAX = 58
            if len(s) <= MAX:
                lines.append(s)
            else:
                i = 0
                while i < len(s):
                    chunk = s[i:i + MAX]
                    lines.append(("    " + chunk) if i > 0 else chunk)
                    i += MAX

total = len(lines)

# 3) 每页行数动态计算，使总页数 = 60（每页 >=50 行）
TARGET_PAGES = 60
LPP = (total + TARGET_PAGES - 1) // TARGET_PAGES  # 向上取整
if LPP < 50:
    LPP = 50
TOTAL_PAGES = (total + LPP - 1) // LPP

W, H = A4
LEFT = 40
RIGHT = W - 40
TOP = H - 38
LINE_H = 11.0
SIZE = 8.5

c = canvas.Canvas(OUT, pagesize=A4)
for p in range(TOTAL_PAGES):
    # 页眉
    c.setFont(FONT, 8)
    c.drawString(LEFT, H - 26, u"《%s》 %s 源代码" % (SOFT_NAME, SOFT_VER))
    c.drawRightString(RIGHT, H - 26, u"第 %d 页 / 共 %d 页" % (p + 1, TOTAL_PAGES))
    c.line(LEFT, H - 30, RIGHT, H - 30)
    # 代码
    c.setFont(FONT, SIZE)
    y = TOP
    start = p * LPP
    for i in range(LPP):
        idx = start + i
        if idx >= total:
            break
        text = lines[idx]
        if len(text) > 95:
            text = text[:95] + u"\u2026"
        c.drawString(LEFT, y, text)
        y -= LINE_H
    c.showPage()

c.save()
print("OK ->", OUT)
print("files=%d  visual_lines=%d  lines_per_page=%d  total_pages=%d"
      % (len(files), total, LPP, TOTAL_PAGES))
