"""`icon_logi_normal` 의 물류 상자를 청록에서 종이 박스로 간다. (구현-아트 세션 소유)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/recolor_crates.py

사용자 반려 (09-15): 「현재 이미지에 물류 박스 색상만 종이 박스로 변경」.
**색만** 이라고 했으므로 다시 뽑지 않는다 — 화소를 직접 간다. 생성 0.

**새 색을 만들지 않는다.** 갈아 끼울 다섯 색은 전부 형제 아이콘 `icon_logi_slow` 의
종이 박스에서 그대로 가져온다. 두 아이콘이 같은 팔레트를 쓰게 되는 것이 덤이다.

짝은 **밝기 차례가 아니라 맡은 자리**로 맺었다:
    윗면 highlight -> 윗면 밝은 면 · 몸통 앞면 -> 앞면 · 그늘 둘 -> 어두운 옆면 둘

**테두리(`#233C44` `#274048`)는 건드리지 않는다** — 그 둘은 상자와 벨트가 **함께 쓰는**
어두운 선이라, 상자 쪽만 갈면 벨트의 선이 갈라진다. 종이 박스도 어두운 선을 두르므로 그대로 둔다.
"""
import hashlib
import os

from PIL import Image

SRC = "Assets/_Project/Art/UI/icon_logi_normal.png"

# 청록 상자 -> 종이 박스 (오른쪽 다섯은 전부 icon_logi_slow 에 이미 있는 색이다)
SWAP = {
    (0x9F, 0xD6, 0xD3): (0xED, 0xB3, 0x70),   # 윗면 highlight
    (0x7E, 0xBB, 0xB6): (0xED, 0xB3, 0x70),   # 윗면 아래 한 줄
    (0x52, 0x88, 0x89): (0xD0, 0x92, 0x50),   # 몸통 앞면 (가장 넓다)
    (0x3B, 0x72, 0x73): (0x97, 0x64, 0x28),   # 그늘
    (0x37, 0x6C, 0x70): (0x85, 0x56, 0x1C),   # 더 짙은 그늘
}


def md5(path):
    with open(path, "rb") as fh:
        return hashlib.md5(fh.read()).hexdigest()[:8]


def recolor(src, dst):
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    px = im.load()
    changed = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a <= 16:
                continue
            new = SWAP.get((r, g, b))
            if new is not None:
                px[x, y] = (new[0], new[1], new[2], a)
                changed += 1
    im.save(dst)
    return changed, w * h


if __name__ == "__main__":
    out = os.environ.get("TEMP", ".") + "/icon_logi_normal_paper.png"
    before = md5(SRC)
    n, total = recolor(SRC, out)
    print("원본 md5 %s" % before)
    print("바뀐 화소 %d / %d = %.1f%%" % (n, total, 100.0 * n / total))
    print("결과 %s  md5 %s" % (out, md5(out)))
