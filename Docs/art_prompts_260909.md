# 프롬프트 전문 — 보드 모듈 기호 (2026-09-09 · 구현-아트 세션)

**어떻게 만들었나 — 도구 · 인자 · 프롬프트 전문. 재현용이다.**
`Docs/art_prompts_260907.md`와 같은 책임이며, 그 파일은 **애니메이션 27벌**을 담아 분리했다.
지금 무엇이 있나(경로 · md5 · 승인 상태)는 `Docs/art_manifest.md`와 `Docs/art_log/260907_anim.md` 3-22가 갖는다.

⚠️ **이 문서를 만든 이유** — 설계가 **보드 아트 요청 문서 10-5를 이 문안으로 갈아 끼운다.**
지금 문서에 실린 `mod_m` 문안은 **도구가 X로 그린다**는 것이 16장으로 확인됐다(아래 1차).

---

## 공통 — 도구와 인자

| 항목 | 값 |
|---|---|
| 도구 | `create_image_pro` |
| 캔버스 | **64 × 64** |
| 배경 제거 | ON(`no_background` 기본값) |
| 참조 | **없다** — 10-5 가 「앵커를 붙이지 않는다」로 정했다(노드 앵커는 192 라 못 댄다) |
| 후보 수 | **16장**(캔버스 ≤85px 규칙) |
| 비용 | 차수마다 20 생성 |

⚠️ **6-4 의 `Detail: Highly detailed`·`Outline: Lineless`는 `create_image_pro`에 인자가 없다.**
10-5 문안이 「no small detail of any kind」·「clean unbordered edge」로 같은 것을 문장으로 말한다.

**아래 넷은 모두 10-5 의 틀을 그대로 쓰고 `{WHAT}`·`{SHAPE}` 두 자리만 다르다.**

---

## 1. `mod_r` 1차 — ✅ **c00 사용자 승인 · 설치** (job `4b7570b1` · md5 `2b1d32f3`)

```
ONE single small pixel art SYMBOL, filling most of the canvas, alone on a fully transparent background

CAMERA: flat, the symbol faces the viewer directly, no perspective of any kind

WHAT IT IS: a mark meaning IT COMES OUT FASTER

SHAPE: THREE parallel slanted strokes all leaning the same way, evenly spaced, reading as motion lines

COLOR: desaturated steel, ONE STEP BRIGHTER than the machine it sits on, no accent color of any kind

RENDERING: a technical schematic mark, flat crisp shapes with only TWO value steps, no small detail of any kind

EDGES: clean unbordered edge against the transparent background
```

**결과** — 16장 중 열둘이 쓸 만했다. 걸린 셋: `c04` 빗금이 하나 · `c15` **강조색이 들어갔다**(평균 색상각 30° 갈색 · 나머지 열다섯은 199° 강청색) · `c11` 디더가 섞여 명도 두 단이 아니다.

---

## 2. `mod_m` 1차 — ❌ **16장 전량 X** (job `0c48a155` · 폐기)

**이 문안이 지금 보드 문서 10-5 에 실려 있는 것이다.**

```
ONE single small pixel art SYMBOL, filling most of the canvas, alone on a fully transparent background

CAMERA: flat, the symbol faces the viewer directly, no perspective of any kind

WHAT IT IS: a mark meaning MORE COMES OUT

SHAPE: TWO nested chevrons opening OUTWARD, narrow at the inner end and wide at the outer end, clearly flaring

COLOR: desaturated steel, ONE STEP BRIGHTER than the machine it sits on, no accent color of any kind

RENDERING: a technical schematic mark, flat crisp shapes with only TWO value steps, no small detail of any kind

EDGES: clean unbordered edge against the transparent background
```

⚠️ **도구가 `TWO nested chevrons opening OUTWARD` 를 「등을 맞댄 꺾쇠 둘 = X」로 읽는다.**
세로축에 대고 접었을 때의 일치도가 **16장 중 11장에서 100%** 였다 — 완전히 포개진다는 뜻이고,
같은 방향으로 겹친 꺾쇠라면 나올 수 없는 값이다. **`vfx_ammoout` 이 세 번 X 를 그린 것과 같은 자리다.**

---

## 3. `mod_m` 2차 — ✅ **c10 사용자 승인 · 설치** (job `c897983b` · md5 `d8985ba2`)

**규칙 9 장치를 붙였다 — 금지하는 대신 그 자리에 무엇이 있는지를 적는다.**
`{SHAPE}` 외의 여섯 줄은 1차와 **한 글자도 다르지 않다.**

```
ONE single small pixel art SYMBOL, filling most of the canvas, alone on a fully transparent background

CAMERA: flat, the symbol faces the viewer directly, no perspective of any kind

WHAT IT IS: a mark meaning MORE COMES OUT

SHAPE: TWO nested chevron strokes, one sitting inside the other with an even gap between them, BOTH bent the SAME way and BOTH opening toward the RIGHT edge of the canvas, the inner chevron small and narrow, the outer chevron large and wide, so the pair flares outward as it goes right. The whole LEFT half of the canvas is EMPTY transparent background, holding no stroke of any kind. There is no mirrored copy on the left, the two strokes never cross each other, and no stroke runs from the lower left corner to the upper right corner.

COLOR: desaturated steel, ONE STEP BRIGHTER than the machine it sits on, no accent color of any kind

RENDERING: a technical schematic mark, flat crisp shapes with only TWO value steps, no small detail of any kind

EDGES: clean unbordered edge against the transparent background
```

**결과** — 16장 전부 겹꺾쇠다. 좌우 접기 일치도가 **37~41%** 로 떨어졌다(1차는 열한 장이 100%).
16장이 거의 같아(전부 33 × 59) 고를 폭은 좁았다.

**장치의 뼈대 넷** — ① 어느 쪽을 향하는지를 못 박는다(`toward the RIGHT edge`) ② **반대쪽 절반이 비어 있다**고 적는다
③ 거울상 사본이 없다 ④ **X 의 나머지 한 획**을 이름으로 지목해 없다고 적는다(`no stroke runs from the lower left corner to the upper right corner`).

---

## 4. `mod_m` 위쪽 화살표 — **미채택** (job `3746f996` · 설치 안 함)

사용자가 「화살표로 바꿔라」고 했다가 회전본을 본 뒤 **회전 없이 2차 c10** 으로 정해 쓰이지 않았다.
후보 16장은 `Docs/art_log/candidates/mod_v3/mod_m_arrow/` 에 남는다. `{WHAT}` 은 2차와 같다.

```
SHAPE: ONE arrow pointing straight UP toward the top edge of the canvas. A wide triangular arrow head sits at the top, and a single straight vertical shaft runs down from it to the bottom of the mark. The shaft is exactly vertical and the head is exactly centred above it, so the mark is symmetric left to right about one vertical line. There is only ONE head and ONE shaft, no second arrow, no arrow head at the bottom, and no diagonal stroke of any kind.
```

---

## 설계에 넘기는 것

- **10-5 의 `mod_m` `{SHAPE}` 를 3번 문안으로 갈아 끼운다.** 2번은 「도구가 X 로 읽는다」를 근거와 함께 폐기 표기.
- **`mod_r` 문안은 그대로 두어도 된다** — 1차에서 통과했다.
- 규칙 9 장치를 노드·품목 문안에도 넓힐지는 설계 판정이다. **아트는 걸린 자리와 통한 문안만 낸다.**
