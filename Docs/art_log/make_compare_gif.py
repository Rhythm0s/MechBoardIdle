"""차수 두 개를 나란히 놓은 GIF 를 만든다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_compare_gif.py <왼쪽 폴더> <왼쪽 이름> <오른쪽 폴더> <오른쪽 이름> [fps]

    예) python Docs/art_log/make_compare_gif.py \\
          Assets/_Project/Art/Anim/robot_a_Move 2차 \\
          Docs/art_log/candidates/robot_a_Move_v3 3차 8

**왜 있는가.** 개정 플랜 §20-3 이 「3차부터는 이전 차를 덮지 않고 나란히 두어 사용자가 고른다」로
정했다. 나란히 **움직이는 채로** 놓아야 고를 수 있다 — 정지 시트로는 판정이 안 된다는 것이
2026-09-07 에 실측으로 나왔다(§20-1 개정).

방향마다 하나씩 `Docs/art_log/preview/compare/<방향>.gif` 로 낸다.
두 쪽의 프레임 수가 다르면 **긴 쪽에 맞춰 짧은 쪽을 되풀이한다** — 길이를 맞추려고 자르지 않는다.
"""
import os
import sys

from PIL import Image, ImageDraw

OUT_DIR = "Docs/art_log/preview/compare"
PAD = 8
LABEL_H = 16


def frames_of(folder):
    if not os.path.isdir(folder):
        return []
    return sorted(f for f in os.listdir(folder)
                  if f.startswith("frame_") and f.endswith(".png"))


def checker(size, box=8):
    im = Image.new("RGB", (size, size), (58, 58, 62))
    d = ImageDraw.Draw(im)
    for y in range(0, size, box):
        for x in range(0, size, box):
            if (x // box + y // box) % 2 == 0:
                d.rectangle([x, y, x + box - 1, y + box - 1], fill=(48, 48, 52))
    return im


def load(folder, files, cell, tile):
    out = []
    for name in files:
        src = Image.open(os.path.join(folder, name)).convert("RGBA")
        thumb = src if src.size[0] == cell else src.resize((cell, cell), Image.NEAREST)
        bg = tile.copy()
        bg.paste(thumb, (0, 0), thumb)
        out.append(bg)
    return out


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 1

    left_root, left_name = sys.argv[1], sys.argv[2]
    right_root, right_name = sys.argv[3], sys.argv[4]
    fps = int(sys.argv[5]) if len(sys.argv) > 5 else 8
    ms = int(round(1000.0 / fps))

    os.makedirs(OUT_DIR, exist_ok=True)

    dirs = sorted(d for d in os.listdir(left_root)
                  if os.path.isdir(os.path.join(left_root, d)))
    made = 0
    for direction in dirs:
        lf = frames_of(os.path.join(left_root, direction))
        rf = frames_of(os.path.join(right_root, direction))
        if not lf or not rf:
            print("  %s: 한쪽이 없다 (왼쪽 %d · 오른쪽 %d) — 건너뛴다" % (direction, len(lf), len(rf)))
            continue

        cell = Image.open(os.path.join(left_root, direction, lf[0])).size[0]
        tile = checker(cell)
        left = load(os.path.join(left_root, direction), lf, cell, tile)
        right = load(os.path.join(right_root, direction), rf, cell, tile)

        # 프레임 수가 다르면 긴 쪽에 맞춰 짧은 쪽을 되풀이한다 — 자르지 않는다.
        length = max(len(left), len(right))

        W = PAD + cell + PAD + cell + PAD
        H = PAD + LABEL_H + cell + PAD

        pages = []
        for i in range(length):
            page = Image.new("RGB", (W, H), (28, 28, 30))
            d = ImageDraw.Draw(page)
            for k, (cells, label) in enumerate(((left, left_name), (right, right_name))):
                x0 = PAD + k * (cell + PAD)
                y0 = PAD + LABEL_H
                page.paste(cells[i % len(cells)], (x0, y0))
                d.rectangle([x0, y0, x0 + cell - 1, y0 + cell - 1], outline=(80, 80, 86))
                d.text((x0 + 2, PAD), "%s  (%df)" % (label, len(cells)), fill=(228, 226, 214))
            pages.append(page)

        p = os.path.join(OUT_DIR, "%s.gif" % direction)
        pages[0].save(p, save_all=True, append_images=pages[1:],
                      duration=ms, loop=0, optimize=True)
        made += 1
        print("  %-10s %s %df | %s %df  ->  %s  %d bytes"
              % (direction, left_name, len(left), right_name, len(right),
                 p, os.path.getsize(p)))

    print("%d방향 · %dfps(%dms)" % (made, fps, ms))
    return 0


if __name__ == "__main__":
    sys.exit(main())
