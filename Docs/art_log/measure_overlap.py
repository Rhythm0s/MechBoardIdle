"""노드끼리 실루엣이 얼마나 겹치는가를 잰다. (구현-아트 세션 소유)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_overlap.py <png> <png> [...]

**재는 법 (보드 아트 요청 문서 3-1-1 인용 그대로).**

> 두 스프라이트를 **각자의 캔버스 그대로** 겹쳐, 알파가 함께 차 있는 넓이를
> 둘을 합친 넓이로 나눈다. **잘라내지 않고 늘이지도 않는다** — 잘라내면 여백이
> 사라지고 늘이면 가로세로비가 사라지는데, 그 문서는 여백과 크기를 둘 다
> 정보로 쓰고 있다.

곧 교집합 ÷ 합집합(IoU)이며 알파 문턱은 16이다.

**판정선 0.90 은 상한이다 — 넘으면 실패다.** 다만 그 문서가 스스로 적어 두었듯
이 값은 구 측정법에서 나왔고 캔버스 기준에서는 느슨하다(09-06 노드 21쌍 최대 0.860).
**0.90 아래는 「실패가 아니다」이지 「좋다」가 아니다.**

⚠️ **겹침은 톤을 재지 않는다.** 실루엣이 갈려도 명암·질감·강조색 자리가 어긋나면
한 세트로 안 보인다. 그것은 수로 못 재고 **사람이 나란히 놓고 봐야 한다.**
"""
import itertools
import os
import sys

from PIL import Image

ALPHA = 16
LIMIT = 0.90


def mask(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    a = im.split()[-1].load()
    return {(x, y) for y in range(h) for x in range(w) if a[x, y] > ALPHA}, (w, h)


def main(paths):
    ms = {}
    size = None
    for p in paths:
        m, sz = mask(p)
        if size and sz != size:
            print("<!> 캔버스가 다르다: %s 는 %dx%d, 앞은 %dx%d - "
                  "같은 캔버스끼리만 잰다" % ((os.path.basename(p),) + sz + size))
        size = sz
        ms[os.path.basename(p)[:-4]] = m

    rows = []
    for a, b in itertools.combinations(sorted(ms), 2):
        u = len(ms[a] | ms[b])
        rows.append((len(ms[a] & ms[b]) / float(u) if u else 0.0, a, b))
    rows.sort(reverse=True)

    over = [r for r in rows if r[0] > LIMIT]
    print("%d 쌍 · 캔버스 %dx%d · 판정선 %.2f (상한)" % (len(rows), size[0], size[1], LIMIT))
    print("최대 %.3f  (%s ↔ %s)" % rows[0])
    print("0.90 초과 %d 쌍%s" % (len(over), "" if over else " (통과)"))
    print("")
    print("상위 열 쌍")
    for r, a, b in rows[:10]:
        print("  %.3f  %-20s %s%s" % (r, a, b, "  <!> 초과" if r > LIMIT else ""))


if __name__ == "__main__":
    main(sys.argv[1:])
