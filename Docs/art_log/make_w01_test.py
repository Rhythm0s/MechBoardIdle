"""`260907_W01` 6장 테스트 파일을 만든다. (구현-아트 세션 소유 · 플랜 §23-3 A)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_w01_test.py

W01 6장이 지시한 것 그대로:
  - 대상은 `robot_a_Idle` **남면 한 벌**. 새로 뽑지 않고 있는 것을 쓴다
    (남면인 근거: 15-1 7-3 승인본 방향이 `South (facing camera)` — 앵커와 같은 면)
  - **두 벌** — 목표 **1.00초**(16칸)와 **1.50초**(24칸). 둘 다 전 칸 균등 배수
  - **핑퐁 8칸** — 1-2-3-4-5-4-3-2. 끝을 한 번만 쓴다
  - **나란히 놓고 동시에 돌린다.** 따로 보면 비교가 안 된다
  - **이동 한 벌을 함께 붙인다** — 목표 1.00초. 초당 뚜렷한 포즈 여섯이 걸음으로 읽히는지
  - **보는 자리** — 보드 배율 ×1.00, 웹빌드 기본 창 960×600. 가장 작게 보이는 자리에서 판정

두 배율로 낸다 (플랜 §23-3 A):
  - **50px** — 실제 화면 크기. 960×600 · 카메라 시야 8 이면 1월드 = 600÷16 = 37.5px 이고
    로봇 캔버스 256 은 PPU 192 에서 256÷192 = 1.333월드 → **50.0px**
  - **256px** — 원본

칸 목록은 `Docs/art_log/anim_schedule.py` 가 만든다 — W01 4장 규칙의 구현이며
`self_check()` 가 4-5 표 넷과 대조한다.

⚠️ **기준 칸 62.5ms 는 GIF 가 그대로 못 적는다.** GIF 는 1/100초 단위만 쓴다.
반올림해 60ms 로 두면 한 바퀴가 목표에서 4% 어긋난다. 그래서 **W01 4-3 의 배분식으로**
6cs 와 7cs 를 섞어 합을 정확히 맞춘다 (48칸 3.000초 = 36칸 6cs + 12칸 7cs).

⚠️ **기준선을 긋지 않는다.** 이 테스트의 물음은 「아장아장에서 벗어났는가」 하나이며 육안 판정이다
(W01 6장 — 로봇 A 대기는 실루엣이 캔버스에 닿아 `AnimReport` 가 못 잰다).
기준선은 재는 눈을 부르므로 여기서는 뺀다.

⚠️ **50px 판은 바탕을 격자로 깔지 않는다.** 8픽셀 격자가 50px 스프라이트 위에서 어른거려
동작을 가린다. 어두운 단색으로 둔다 — 전투 배경이 어두운 쪽이다. 256px 판은 격자 그대로.
"""
import os
import sys

from PIL import Image, ImageDraw

# 리포 루트에서 돌리되 같은 폴더의 anim_schedule 을 쓴다.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import anim_schedule as sched  # noqa: E402

ANIM = "Assets/_Project/Art/Anim"
OUT_DIR = "Docs/art_log/preview/w01_test"

# 960×600 · 카메라 시야 8 → 1월드 = 37.5px · 로봇 256 = 256/192 월드
SCREEN_PX = 50
CANVAS = 256

PANELS = [
    ("robot_a_Idle/south", "idle 1.00s", 5, 1.00, True),
    ("robot_a_Idle/south", "idle 1.50s", 5, 1.50, True),
    ("robot_a_Move/south", "move 1.00s", 6, 1.00, False),
]


def load_frames(rel):
    folder = os.path.join(ANIM, rel)
    names = sorted(f for f in os.listdir(folder)
                   if f.startswith("frame_") and f.endswith(".png"))
    return [Image.open(os.path.join(folder, n)).convert("RGBA") for n in names]


def checker(size, box=8):
    im = Image.new("RGB", (size, size), (58, 58, 62))
    d = ImageDraw.Draw(im)
    for y in range(0, size, box):
        for x in range(0, size, box):
            if (x // box + y // box) % 2 == 0:
                d.rectangle([x, y, x + box - 1, y + box - 1], fill=(48, 48, 52))
    return im


def build_panels():
    out = []
    for rel, label, frame_count, seconds, ping in PANELS:
        frames = load_frames(rel)
        if len(frames) != frame_count:
            raise SystemExit("%s 그림이 %d 장이어야 하는데 %d 장이다" % (rel, frame_count, len(frames)))
        order, actual, info = sched.build(frame_count, seconds, ping_pong=ping)
        out.append({"rel": rel, "label": label, "frames": frames,
                    "order": order, "seconds": actual, "info": info})
    return out


def render(panels, cell, use_checker, path):
    slots = [len(p["order"]) for p in panels]
    total = slots[0]
    for s in slots[1:]:                       # 최소공배수 — 셋을 한 타임라인에 올린다
        a, b = total, s
        while b:
            a, b = b, a % b
        total = total * s // a

    cycle_seconds = total * sched.SLOT_SECONDS
    durations = sched.slot_durations_cs(total, cycle_seconds)

    pad = 8
    label_h = 14
    W = pad + len(panels) * (cell + pad)
    H = pad + label_h + cell + pad

    tile = checker(cell) if use_checker else Image.new("RGB", (cell, cell), (30, 30, 34))
    thumbs = []
    for p in panels:
        cache = {}
        for idx in set(p["order"]):
            src = p["frames"][idx]
            th = src if src.size[0] == cell else src.resize((cell, cell), Image.NEAREST)
            bg = tile.copy()
            bg.paste(th, (0, 0), th)
            cache[idx] = bg
        thumbs.append(cache)

    pages = []
    for t in range(total):
        page = Image.new("RGB", (W, H), (24, 24, 27))
        d = ImageDraw.Draw(page)
        for k, p in enumerate(panels):
            x0 = pad + k * (cell + pad)
            y0 = pad + label_h
            idx = p["order"][t % len(p["order"])]
            page.paste(thumbs[k][idx], (x0, y0))
            d.rectangle([x0, y0, x0 + cell - 1, y0 + cell - 1], outline=(80, 80, 86))
            d.text((x0, pad - 1), p["label"], fill=(228, 226, 214))
        pages.append(page)

    pages[0].save(path, save_all=True, append_images=pages[1:],
                  duration=[c * 10 for c in durations], loop=0, optimize=True)
    return total, cycle_seconds, sum(durations) / 100.0


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    panels = build_panels()

    print("칸 목록 (W01 4장)")
    for p in panels:
        print("  %-22s %-11s %2d칸 · %.2f초 · %s"
              % (p["rel"], p["label"], len(p["order"]), p["seconds"], p["info"]))

    for cell, use_checker, name in ((SCREEN_PX, False, "w01_test_%dpx_screen.gif" % SCREEN_PX),
                                    (CANVAS, True, "w01_test_%dpx_source.gif" % CANVAS)):
        path = os.path.join(OUT_DIR, name)
        total, want, got = render(panels, cell, use_checker, path)
        print("  %-30s %d칸 · 한 바퀴 %.3f초(GIF 실제 %.2f초) · %d bytes"
              % (name, total, want, got, os.path.getsize(path)))

    print("칸 시간 %.1fms · GIF 는 1/100초 단위라 6cs·7cs 를 섞어 합을 맞춘다" % sched.SLOT_MS)


if __name__ == "__main__":
    main()
