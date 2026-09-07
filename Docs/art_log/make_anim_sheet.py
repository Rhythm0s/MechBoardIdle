"""`Assets/_Project/Art/Anim/_anim_sheet.png` 를 만든다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_anim_sheet.py [셀크기]      # 기본 128

**이 시트의 목적은 하나다 — 대기 진폭을 눈으로 판정하는 것.**
벌마다 `frame_000` 의 실루엣 윗변에 가로 기준선을 긋는다. 몸이 오르내리면 그 선과 어깨 사이가
벌어졌다 좁아지는 것이 바로 보인다. 선이 없으면 256 캔버스 안에서 10픽셀쯤 움직이는 것은
눈에 안 들어온다.

⚠️ **이 시트는 눈으로 보는 것이고 숫자는 `MBI.Editor.AnimReport` 가 낸다.**
시트에서 잰 값을 판정 근거로 쓰지 않는다 (지침 §10 — 도구가 낸 숫자만 근거).

배경을 격자로 까는 이유: 투명 배경을 단색으로 깔면 어두운 기체의 가장자리가 배경에 묻힌다.

이 스크립트를 리포에 두는 이유: 세션이 끝나면 임시 스크립트는 사라지고, 그러면 다음 사람이
같은 시트를 다시 만들 수 없다 (지침 §7 「승인은 파일이 놓인 자리를 바꾸지 않는다」와 같은 뿌리).
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = "Assets/_Project/Art/Anim"
OUT = os.path.join(ROOT, "_anim_sheet.png")

ALPHA = 16          # MBI.Editor.AnimReport 와 같은 문턱
CANVAS = 256        # 전투 스프라이트 캔버스 (15-1 · 15-2 · 15-3)
PAD = 6
LABEL_W = 210
ROW_GAP = 10


def clips():
    """`<robot>_<State>/<dir>` 을 모은다. 대기를 맨 위로 올린다 — 판정 대상이다."""
    found = []
    for clip in sorted(os.listdir(ROOT)):
        clip_dir = os.path.join(ROOT, clip)
        if not os.path.isdir(clip_dir):
            continue
        if not (clip.startswith("robot_") or clip.startswith("fusion")):
            continue
        for direction in sorted(os.listdir(clip_dir)):
            d = os.path.join(clip_dir, direction)
            if not os.path.isdir(d):
                continue
            frames = sorted(f for f in os.listdir(d)
                            if f.startswith("frame_") and f.endswith(".png"))
            if frames:
                found.append((clip, direction, d, frames))

    found.sort(key=lambda c: (0 if c[0].endswith("_Idle") else 1, c[0], c[1]))
    return found


def top_edge(img):
    """알파 bbox 의 윗변(원본 캔버스 좌표). 없으면 None."""
    alpha = img.split()[-1]
    w, h = img.size
    px = alpha.load()
    for y in range(h):
        for x in range(w):
            if px[x, y] > ALPHA:
                return y
    return None


def checker(size, box=8):
    """투명 배경 자리에 깔 격자."""
    im = Image.new("RGB", (size, size), (58, 58, 62))
    d = ImageDraw.Draw(im)
    for y in range(0, size, box):
        for x in range(0, size, box):
            if (x // box + y // box) % 2 == 0:
                d.rectangle([x, y, x + box - 1, y + box - 1], fill=(48, 48, 52))
    return im


def main():
    cell = int(sys.argv[1]) if len(sys.argv) > 1 else 128

    found = clips()
    if not found:
        print("벌이 하나도 없다:", ROOT)
        return

    max_frames = max(len(c[3]) for c in found)
    row_h = cell + ROW_GAP
    width = LABEL_W + max_frames * (cell + PAD) + PAD
    height = PAD + len(found) * row_h + PAD

    sheet = Image.new("RGB", (width, height), (28, 28, 30))
    draw = ImageDraw.Draw(sheet)
    tile = checker(cell)

    for i, (clip, direction, path, frames) in enumerate(found):
        y0 = PAD + i * row_h
        is_idle = clip.endswith("_Idle")

        draw.text((PAD, y0 + cell // 2 - 4), "%s/%s" % (clip, direction),
                  fill=(228, 226, 214))
        draw.text((PAD, y0 + cell // 2 + 8), "%df" % len(frames), fill=(150, 150, 150))

        base_top = None
        for j, name in enumerate(frames):
            src = Image.open(os.path.join(path, name)).convert("RGBA")
            if j == 0:
                base_top = top_edge(src)          # 원본 캔버스에서 잰다

            thumb = src if src.size[0] == cell else src.resize((cell, cell), Image.NEAREST)

            x0 = LABEL_W + j * (cell + PAD)
            bg = tile.copy()
            bg.paste(thumb, (0, 0), thumb)
            sheet.paste(bg, (x0, y0))

            # 기준선은 대기에만 긋는다 — 이동·사망·태그는 진폭 규격이 없다.
            if is_idle and base_top is not None:
                gy = y0 + int(base_top * cell / CANVAS)
                draw.line([(x0, gy), (x0 + cell - 1, gy)], fill=(230, 90, 70), width=1)

            draw.rectangle([x0, y0, x0 + cell - 1, y0 + cell - 1], outline=(80, 80, 86))

    sheet.save(OUT)
    print("wrote %s  %dx%d  벌 %d  셀 %d  %d bytes"
          % (OUT, width, height, len(found), cell, os.path.getsize(OUT)))


if __name__ == "__main__":
    main()
