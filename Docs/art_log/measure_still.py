"""스틸 한 장의 여백·실루엣·겹침을 잰다. (구현-아트 세션 소유 · 플랜 §20-1 · §24-1 게이트)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_still.py <png> [비교대상.png ...]

측정법
  여백 L R T B — 알파 문턱 16 초과를 「있다」로 보고 캔버스 네 변까지의 빈 픽셀 수
  실루엣 세로 % — (아랫변 − 윗변 + 1) ÷ 캔버스 높이
  겹침 — **두 스프라이트를 각자의 캔버스 그대로 겹쳐, 알파가 함께 찬 넓이를 합친 넓이로 나눈다**
         (`Docs/measure/overlap_260907_rotcheck_a.md` 와 같은 식 · `MBI.Core.SilhouetteOverlap`)

§24-1 게이트: 아래 ≥ 10 · 위 ≥ 8 · 좌우 ≥ 8 · 세로 ≤ 93% · robot_b 와의 겹침 ≤ 0.90
"""
import hashlib
import os
import sys

from PIL import Image

ALPHA = 16
GATE = {"bottom": 10, "top": 8, "side": 8, "fill": 93.0, "overlap": 0.90}


def mask(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    a = im.split()[-1].load()
    return [[a[x, y] > ALPHA for x in range(w)] for y in range(h)], w, h


def box(m, w, h):
    xs = [x for y in range(h) for x in range(w) if m[y][x]]
    ys = [y for y in range(h) for x in range(w) if m[y][x]]
    return min(xs), max(xs), min(ys), max(ys)


def md5(path):
    return hashlib.md5(open(path, "rb").read()).hexdigest()[:8]


def report(path):
    m, w, h = mask(path)
    x0, x1, y0, y1 = box(m, w, h)
    L, R, T, B = x0, w - 1 - x1, y0, h - 1 - y1
    sw, sh = x1 - x0 + 1, y1 - y0 + 1
    fill = 100.0 * sh / h
    print("%-46s md5 %s · %dx%d" % (os.path.basename(path), md5(path), w, h))
    print("   여백 L%d R%d T%d B%d · 실루엣 %dx%d · 세로 %.1f%%" % (L, R, T, B, sw, sh, fill))
    ok = (B >= GATE["bottom"] and T >= GATE["top"]
          and min(L, R) >= GATE["side"] and fill <= GATE["fill"])
    print("   게이트 ①②: 아래 %d≥10 %s · 위 %d≥8 %s · 좌우 %d≥8 %s · 세로 %.1f≤93 %s → %s"
          % (B, "○" if B >= 10 else "✗", T, "○" if T >= 8 else "✗",
             min(L, R), "○" if min(L, R) >= 8 else "✗",
             fill, "○" if fill <= 93.0 else "✗", "통과" if ok else "**미달**"))
    return m, w, h, ok


def overlap(a, b):
    ma, wa, ha = a
    mb, wb, hb = b
    w, h = max(wa, wb), max(ha, hb)
    inter = union = 0
    for y in range(h):
        for x in range(w):
            pa = y < ha and x < wa and ma[y][x]
            pb = y < hb and x < wb and mb[y][x]
            if pa and pb:
                inter += 1
            if pa or pb:
                union += 1
    return inter / float(union) if union else 0.0


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    target = sys.argv[1]
    m, w, h, ok = report(target)
    for other in sys.argv[2:]:
        mo, wo, ho, _ = report(other)
        v = overlap((m, w, h), (mo, wo, ho))
        print("   겹침 %s × %s = %.3f (상한 0.90) %s"
              % (os.path.basename(target), os.path.basename(other), v,
                 "○" if v <= 0.90 else "**초과**"))
    return 0


if __name__ == "__main__":
    sys.exit(main())


# ── 옆모습 잣대 ────────────────────────────────────────────────────────────
# 옆모습은 두 다리가 겹쳐 좌우로 못 가른다 — 「좌우 아랫변 차의 부호 반전」(규칙 15)이 성립하지 않는다.
# 그 자리에서 쓰는 것이 **발 구간 가로폭**이다.
#
#   발 구간 = 그 프레임 실루엣의 아랫변에서 위로 BAND(24) px
#   가로폭  = 그 띠 안에서 알파 문턱 16 초과인 픽셀의 (가장 오른쪽 − 가장 왼쪽 + 1)
#   벌의 값 = 칸별 가로폭의 목록과 (최대 − 최소)
#
# ⚠️ **이것은 참고값이고 판정 근거가 아니다.** 규격에 없는 잣대이며, 벌어졌다 모이는 폭이
# 걸음으로 읽히는지는 사람이 화면을 보고 정한다.
BAND = 24


def foot_span(path, band=BAND):
    """한 프레임의 발 구간 가로폭."""
    m, w, h = mask(path)
    x0, x1, y0, y1 = box(m, w, h)
    xs = [x for y in range(max(0, y1 - band + 1), h) for x in range(w) if m[y][x]]
    return (max(xs) - min(xs) + 1) if xs else 0


def foot_span_clip(folder, band=BAND):
    """벌 하나의 칸별 가로폭 목록."""
    names = sorted(f for f in os.listdir(folder)
                   if f.startswith("frame_") and f.endswith(".png"))
    return [foot_span(os.path.join(folder, n), band) for n in names]
