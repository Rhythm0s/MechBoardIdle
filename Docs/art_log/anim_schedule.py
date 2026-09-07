"""칸 목록을 만든다 — `260907_W01` 4장 길이 규칙의 파이썬 구현.
(구현-아트 세션 소유 · 플랜 §20-1)

**C# `MBI.Core.AnimSchedule` 과 같은 식이어야 한다.** 문서·코드 세션이 그것을 만들고,
이 파일은 GIF 를 만들 때 같은 결과를 내야 한다. 어긋나면 사용자가 본 GIF 와 화면이 달라진다.
`self_check()` 가 W01 4-5 표 넷으로 대조한다.

말 넷 (W01 4-1):
  그림   폴더의 PNG 한 장
  칸     재생 목록의 한 자리. 같은 그림을 여러 칸이 가리킬 수 있다
  기준 칸 시간  1/16초 고정
  필요 칸  목표 초 × 16
"""

SLOT_SECONDS = 1.0 / 16.0          # W01 4-1 · 고정
SLOT_MS = 1000.0 * SLOT_SECONDS    # 62.5ms


def base_slots(frame_count, ping_pong):
    """칸 목록을 만든다 (W01 4-2 1번). 그림 번호는 0부터.

    왕복(대기)은 1→N→2 로 편다 — 끝을 한 번만 쓴다(W01 4-5).
    5장이면 0·1·2·3·4·3·2·1 로 여덟 칸.
    """
    order = list(range(frame_count))
    if ping_pong and frame_count >= 3:
        order = order + list(reversed(order[1:-1]))
    return order


def _spread(n, r):
    """`r` 칸을 `n` 칸에 고르게 뿌린다 (W01 4-3 배분식). 1부터 센 칸 번호 집합을 준다.

        (i × r) ÷ n 의 몫  >  ((i−1) × r) ÷ n 의 몫

    앞에서부터 세어 나가다 「지금까지 준 개수」가 하나 늘어나는 자리에서만 준다.
    손으로 고른 숫자를 쓰지 않기 위한 것이다.
    """
    if r <= 0:
        return set()
    return {i for i in range(1, n + 1)
            if (i * r) // n > ((i - 1) * r) // n}


def build(frame_count, target_seconds, ping_pong=False, dwell_slots=None):
    """칸 목록과 실제 초를 돌려준다.

    dwell_slots — 머무름 칸 번호(1부터). 「발이 닿는 프레임을 한 칸 더」(W01 4-3).
                  왕복 벌에서는 쓰지 않는다.
    """
    order = base_slots(frame_count, ping_pong)
    n = len(order)
    need = int(round(target_seconds * 16))

    if need == n:
        return list(order), n * SLOT_SECONDS, {"기본 칸": n, "필요 칸": need, "복제": 0, "삭제": 0}

    if need > n:
        q, r = divmod(need, n)
        out = []
        # 균등 배수 먼저 (W01 4-3)
        counts = [q] * n
        if not ping_pong:                      # 왕복은 q 만 쓴다 (W01 4-3 마지막)
            extra = set()
            if dwell_slots:
                for s in dwell_slots:          # 머무름 칸부터
                    if len(extra) < r and 1 <= s <= n:
                        extra.add(s)
            if len(extra) < r:
                for s in sorted(_spread(n, r - len(extra))):
                    if s not in extra:
                        extra.add(s)
                        if len(extra) == r:
                            break
            for s in extra:
                counts[s - 1] += 1
        for i, c in enumerate(counts):
            out.extend([order[i]] * c)
        return out, len(out) * SLOT_SECONDS, {
            "기본 칸": n, "필요 칸": need, "복제": len(out) - n, "삭제": 0,
            "배분": sorted(s for s in range(1, n + 1) if counts[s - 1] > q),
        }

    # 삭제 (W01 4-4) — 첫·끝 보존 · 짝수 칸 후보 · 왕복은 삭제 안 함
    d = n - need
    if ping_pong:
        raise ValueError("왕복 벌은 삭제하지 않는다 (W01 4-4). 목표 초를 기본 칸의 배수에서 고른다.")
    cand = [i for i in range(2, n) if i % 2 == 0]
    if len(cand) < d:
        raise ValueError("후보 칸이 %d 개인데 %d 개를 지워야 한다 — 멈추고 보고한다 (W01 4-4)."
                         % (len(cand), d))
    drop = {cand[i] for i in sorted(_spread(len(cand), d))} if d else set()
    drop = {cand[i - 1] for i in sorted(_spread(len(cand), d))}
    out = [order[i - 1] for i in range(1, n + 1) if i not in drop]
    return out, len(out) * SLOT_SECONDS, {
        "기본 칸": n, "필요 칸": need, "복제": 0, "삭제": d,
        "지운 칸": sorted(drop),
        "화면에 안 나오는 그림": sorted({order[i - 1] for i in drop}
                                       - {order[i - 1] for i in range(1, n + 1) if i not in drop}),
    }


def slot_durations_cs(slot_count, total_seconds):
    """GIF 한 칸의 지속을 **1/100초 단위**로 나눠 준다.

    ⚠️ GIF 는 칸 시간을 1/100초 단위로만 적을 수 있다. 기준 칸 62.5ms 는 표현되지 않는다.
    그대로 60ms 나 70ms 로 반올림하면 한 바퀴가 목표 초에서 어긋난다.
    그래서 **W01 4-3 의 배분식을 그대로 써서** 6cs 와 7cs 를 섞어 합이 목표와 정확히 맞게 한다.
    16칸 1.00초면 12칸 6cs + 4칸 7cs = 100cs.
    """
    total_cs = int(round(total_seconds * 100))
    base = total_cs // slot_count
    r = total_cs - base * slot_count
    plus = _spread(slot_count, r)
    return [base + (1 if (i + 1) in plus else 0) for i in range(slot_count)]


def self_check():
    """W01 4-5 표 넷으로 대조한다."""
    rows = []

    order, sec, info = build(5, 1.00, ping_pong=True)
    rows.append(("대기 1.00", len(order) == 16 and abs(sec - 1.0) < 1e-9, info))

    order, sec, info = build(5, 1.50, ping_pong=True)
    rows.append(("대기 1.50", len(order) == 24 and abs(sec - 1.5) < 1e-9, info))

    order, sec, info = build(6, 1.00)
    ok = len(order) == 16 and info.get("배분") == [2, 3, 5, 6]
    rows.append(("이동 1.00", ok, info))

    order, sec, info = build(9, 2.00)
    rows.append(("사망 2.00", len(order) == 32, info))

    order, sec, info = build(9, 0.75)
    ok = len(order) == 12 and info.get("배분") == [3, 6, 9]
    rows.append(("태그 0.75", ok, info))

    return rows


if __name__ == "__main__":
    print("W01 4-5 표 대조")
    allok = True
    for name, ok, info in self_check():
        allok = allok and ok
        print("  %-10s %s  %s" % (name, "통과" if ok else "**어긋남**", info))
    print("전체:", "통과" if allok else "**어긋남**")
