"""애니메이션 벌을 GIF 로 만든다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_anim_gif.py [셀크기]      # 기본 256 (1대 1)

**정지 시트로는 대기 진폭이 안 보인다.** 256 캔버스 안에서 10픽셀쯤 오르내리는 것은
프레임을 나란히 놓아도 눈이 못 잡는다. 움직여야 보인다 — 그래서 GIF 다.

내는 것 둘:
  - 벌마다 하나  `preview/NN_<clip>_<dir>.gif`   (`_anim_sheet.png` 와 같은 순서)
  - 대기 한 판   `preview/_idle_all.gif`         (대기 전부를 격자에 놓고 동시에 돌린다)

**대기는 되감기(핑퐁)로 낸다 — 2026-09-07 사용자 확정.**
프레임을 0·1·2·3·4·3·2·1 로 되짚어 온다. 그냥 반복하면 마지막에서 첫 프레임으로 튀는 자리가
어색했다. 양 끝은 겹치지 않는다 — 겹치면 그 프레임만 두 배로 머물러 다른 종류의 멈칫이 생긴다.

⚠️ **되감기는 프레임을 파일로 굽는 것이 아니라 재생 방식이다.** 구우면 대기가 5장에서 8장이 되어
15「동작의 크기」의 「대기 5」 규격과 `UnitAnimWiringTests` 가 깨진다. **리포의 프레임은 5장 그대로**이며
`SpriteFrameAnimator` 가 그 순서로 재생해야 한다 — 그 코드는 구현-문서·코드 세션 소유라 여기서
넣지 않았다. 이 GIF 는 **확정된 재생 방식을 그대로 보여 주는 것**이다.

**대기에는 빨간 가로 기준선을 긋는다.** `frame_000` 실루엣의 윗변 자리이며 프레임이 바뀌어도
움직이지 않는다. 어깨가 그 선에서 떨어졌다 붙었다 하는 폭이 진폭이다.

⚠️ **재생 속도는 구현이 세운 가정이다** — 대기 6fps · 이동·사망·태그 8fps.
   어느 기획 문서도 정한 적이 없으며 `260907_V01` 판정 요청 2-2 로 올라가 있다.
   설계가 값을 주면 아래 FPS 를 갈아 끼운다.

⚠️ **GIF 는 눈으로 보는 것이고 숫자는 `MBI.Editor.AnimReport` 가 낸다.**
   여기서 잰 값을 판정 근거로 쓰지 않는다 (지침 §10 — 도구가 낸 숫자만 근거).
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = "Assets/_Project/Art/Anim"
OUT_DIR = "Docs/art_log/preview"

ALPHA = 16          # MBI.Editor.AnimReport 와 같은 문턱
CANVAS = 256        # 전투 스프라이트 캔버스

# 구현 가정 — 260907_V01 ❓2-2. 확정 아님.
FPS = {"Idle": 6, "Move": 8, "Death": 8, "TagIn": 8}

# 되감기로 재생하는 상태 — 2026-09-07 사용자 확정. 코드도 여기에 맞춰야 한다.
PINGPONG_STATES = {"Idle"}


def clips():
    """`_anim_sheet.png` 와 **같은 순서**로 모은다 — 대기를 맨 위로."""
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


def state_of(clip):
    return clip.rsplit("_", 1)[1]


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
    im = Image.new("RGB", (size, size), (58, 58, 62))
    d = ImageDraw.Draw(im)
    for y in range(0, size, box):
        for x in range(0, size, box):
            if (x // box + y // box) % 2 == 0:
                d.rectangle([x, y, x + box - 1, y + box - 1], fill=(48, 48, 52))
    return im


def compose(path, frames, cell, guide_top):
    """한 벌을 셀 크기의 RGB 프레임 목록으로 만든다."""
    tile = checker(cell)
    out = []
    for name in frames:
        src = Image.open(os.path.join(path, name)).convert("RGBA")
        thumb = src if src.size[0] == cell else src.resize((cell, cell), Image.NEAREST)
        bg = tile.copy()
        bg.paste(thumb, (0, 0), thumb)
        if guide_top is not None:
            gy = int(guide_top * cell / CANVAS)
            ImageDraw.Draw(bg).line([(0, gy), (cell - 1, gy)], fill=(230, 90, 70), width=1)
        out.append(bg)
    return out


def pingpong(frames):
    """0·1·2·3·4·3·2·1 — 양 끝은 겹치지 않는다. 겹치면 그 프레임만 두 배로 머문다."""
    if len(frames) < 3:
        return list(frames)
    return list(frames) + list(reversed(frames[1:-1]))


def save_gif(path, frames, ms):
    frames[0].save(path, save_all=True, append_images=frames[1:],
                   duration=ms, loop=0, optimize=True)


def main():
    cell = int(sys.argv[1]) if len(sys.argv) > 1 else CANVAS
    os.makedirs(OUT_DIR, exist_ok=True)

    found = clips()
    if not found:
        print("벌이 하나도 없다:", ROOT)
        return

    idle_cells = []
    total = 0

    for i, (clip, direction, path, frames) in enumerate(found, start=1):
        state = state_of(clip)
        is_idle = state == "Idle"

        base_top = top_edge(Image.open(os.path.join(path, frames[0])).convert("RGBA"))
        cells = compose(path, frames, cell, base_top if is_idle else None)

        if state in PINGPONG_STATES:
            cells = pingpong(cells)

        ms = int(round(1000.0 / FPS.get(state, 8)))
        name = "%02d_%s_%s.gif" % (i, clip, direction)
        save_gif(os.path.join(OUT_DIR, name), cells, ms)
        total += os.path.getsize(os.path.join(OUT_DIR, name))
        print("  %-34s %df  %dms  %d bytes"
              % (name, len(cells), ms, os.path.getsize(os.path.join(OUT_DIR, name))))

        if is_idle:
            idle_cells.append(("%s/%s" % (clip, direction), cells))

    # ---- 대기 한 판 ----
    # 대기는 전부 5프레임이고 위에서 같은 되감기를 거쳤으므로 그대로 겹쳐 돌릴 수 있다.
    if idle_cells:
        n = len(idle_cells)
        cols = 4
        rows = (n + cols - 1) // cols
        pad = 6
        label_h = 14
        cw = cell + pad
        ch = cell + label_h + pad
        W = pad + cols * cw
        H = pad + rows * ch

        length = min(len(c[1]) for c in idle_cells)
        sheet_frames = []
        for f in range(length):
            page = Image.new("RGB", (W, H), (28, 28, 30))
            d = ImageDraw.Draw(page)
            for k, (label, cells) in enumerate(idle_cells):
                r, c = divmod(k, cols)
                x0 = pad + c * cw
                y0 = pad + r * ch
                page.paste(cells[f], (x0, y0))
                d.rectangle([x0, y0, x0 + cell - 1, y0 + cell - 1], outline=(80, 80, 86))
                d.text((x0 + 2, y0 + cell + 2), label, fill=(228, 226, 214))
            sheet_frames.append(page)

        ms = int(round(1000.0 / FPS["Idle"]))

        p = os.path.join(OUT_DIR, "_idle_all.gif")
        save_gif(p, sheet_frames, ms)
        print("  %-34s %df  %dx%d  %d bytes"
              % ("_idle_all.gif", length, W, H, os.path.getsize(p)))
        total += os.path.getsize(p)



    print("벌 %d개 · 합계 %d bytes · 셀 %d" % (len(found), total, cell))


if __name__ == "__main__":
    main()
