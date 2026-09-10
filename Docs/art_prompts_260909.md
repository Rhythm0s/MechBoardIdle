# 프롬프트 전문 — 보드 모듈 기호 · 보스 (구현-아트 세션)

> **파일 이름의 날짜와 만든 날이 다르다.** 이 파일은 **2026-09-10 에 만들었고**, 이름의 `260909` 는
> 담고 있는 **모듈 기호 작업이 09-09 에 이뤄졌다**는 뜻이다(플랜이 지정한 이름). 보스 절 넷(5~8)은 **09-10** 작업이다.

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

---

# 보스 — 256 전투 스틸과 사망 벌 (2026-09-10)

**왜 여기 있나** — 사용자 확정 「보스는 512다」 뒤에 도구 상한이 걸렸다(아트 로그 3-24).
`create_character`·`animate_character`·`animate_image` 가 전부 **256 이 상한**이라 512 로는 회전도 애니도 못 만든다.
그래서 **512 를 참조로 256 전투 스틸을 새로 뽑고**, 그 위에 회전·사망을 얹었다. 화면 배율은 **코드 2배**다.

## 5. 보스 256 전투 스틸 — ✅ **사용자 승인 · 설치** (job `a73a0def` · md5 `a909ae6a`)

| 항목 | 값 |
|---|---|
| 도구 · 캔버스 | `create_image_pro` · **256 × 256** · 후보 **1장**(256 이라 1장이 상한) |
| 참조 | `Units/boss.png` **512 승인본**을 **176px · 16색으로 줄여** 인라인 base64 로 |
| 비용 | 20 생성 |

⚠️ **참조를 줄인 것은 그림을 줄인 것이 아니다.** 512 원본은 base64 **111,744자**, 256 팔레트본도 **13,532자**로
둘 다 클라이언트가 잘랐다. 통과한 것은 **6,528자**(176px · 16색)다. **결과물은 256 으로 새로 그려졌고**
512 와의 모양 겹침이 **0.977** 이다(구 09-04 벌 첫 칸은 0.744).

**`reference_images[0].usage`**

```
the same boss machine — its identity, hull shape, plating layout, gun placement, colour palette, and its straight-on non-tilted viewing angle
```

**`description` 전문**

```
Pixel art boss war machine, the SAME machine as the reference image, one single machine centred in the frame, alone on a fully transparent background

CAMERA: square to the machine. Its front faces the viewer directly and the left and right halves MIRROR each other about one vertical centre line. The hull edges run parallel to the edges of the image. This is a straight-on view, not a three-quarter view and not an isometric view, and the machine is not rotated or tilted on the ground.

FRAMING: the whole machine fits inside the canvas with a clear empty margin on all four sides. Nothing touches any edge. The gap below the machine at the bottom is as wide as the gap above it at the top.

BUILD: a heavy armoured tracked siege machine, riveted steel plating, a broad flat hull, gun barrels and vents protruding from both sides, hard mechanical edges, nothing organic

COLOR: desaturated gunmetal steel with dark red accent panels, exactly as the reference

RENDERING: flat crisp pixel shapes with a small number of value steps, calm and even, no glow and no light bloom anywhere

EDGES: the machine meets the transparent background directly, a clean unbordered edge
```

**FRAMING 절의 마지막 문장이 핵심이다** — 「아래 여백 = 위 여백」. 로봇 A 에서 아래 여백 0 이 진폭 측정과
걷기를 둘 다 막았던 자리이고, 결과는 **L57 R57 T59 B65** 로 지켜졌다(좌우가 같아 정면이라는 증거이기도 하다).

## 6. 회전 8방향 (character `2fd860bd` · 8 생성)

| 항목 | 값 |
|---|---|
| 도구 | `create_character` **mode="v3"** · `size=256` |
| 참조 | `reference_image_url` = 5번 결과의 **PixelLab 무인증 URL** — 줄이지 않고 원본 그대로 |

```
a heavy armoured tracked siege machine, riveted gunmetal steel plating with dark red accent panels, gun barrels and vents protruding from both sides, hard mechanical edges, nothing organic
```

**남면 대조 결과** — md5 는 갈렸으나(`a909ae6a` → `ebd7b29d`) **겹침 1.000 · 보이는 화소 12,544개의 색이 전부 같다.**
다른 것은 알파 6화소와 PNG 인코딩뿐이다. **도구가 첫 칸을 다시 그리지 않았다.**

## 7. 사망 2차 — ⚠️ **폐기** (animation `4e6a9bb4` · group `8ac7a6a4` · 8 생성)

| 항목 | 값 |
|---|---|
| 도구 | `animate_character` **mode="v3"** · `directions=["south"]` · `frame_count=8` · `keep_first_frame=true` → **9칸** |
| 끝 자세 | 승인본을 **세로 ×0.72 · 가로 ×1.06**, 아랫변 고정(151 × 95) · **새 획 0** |

```
the machine is destroyed and settles onto the ground: its suspension gives way so the whole hull sinks straight down, the side gun barrels droop, and the tracks splay outward as it flattens
```

**측정은 통과했다** — 세로 132 → 95, **변화 37px = 28.0%**(판정선 10%) · 단조 감소 · 잘린 칸 0.
⚠️ **폐기 사유 둘** — ① **8칸에서 튄다**: 가로가 204까지 벌어졌다가 마지막에 **151로 되돌아온다(−53px)**,
7칸과 8칸의 실루엣 겹침 **0.683**. 원인은 **끝 자세가 도구가 간 폭보다 좁아서**다(8칸이 준 끝 자세와 겹침 0.999).
② **사용자 육안** — 「포탑이 삐뚤어지면서 부포들이 떨어져 나가는 느낌으로」.

## 8. 사망 3차 (animation group `b1044cb9` · 8 생성)

| 항목 | 값 |
|---|---|
| 도구·인자 | 7번과 같다 |
| 끝 자세 | 승인본을 **세로 ×0.72 · 가로 ×1.40**, 아랫변 고정(**199 × 95**) · 새 획 0 |

**가로 배율을 1.06에서 1.40으로 올린 것이 2차의 튐을 고치는 자리다** — 도구가 자연스럽게 간 폭이 204였다.

```
the machine is destroyed and comes apart where it stands: the central turret slews hard to one side and sags over askew, the four side gun mounts tear loose from the hull and tumble away outward leaving torn empty sockets, and the wrecked hull settles down and spreads out onto its tracks
```

⚠️ **「포탑이 기울고 부포가 떨어져 나간」 끝 자세도 만들었으나 넘길 수 없었다** (`candidates/boss_Death_pose/end3.png`).
인라인 base64 한계를 실측했다 — **7,332자에서도 끝 4자가 잘리고**, **4비트 팔레트 PNG 는 디코딩이 안 된다**(8비트여야 한다).
그 판은 8비트 최소가 7,332자라 통과선을 못 넘었다. **그래서 부위 이탈은 문안이 끌게 두었다.**

⚠️ **메탈슬러그 참고는 문장으로만 반영했다** (사용자 지시 3번). **원본 스프라이트를 참조로 걸지 않는다** —
남의 저작물이고, 참조로 걸면 그 화풍이 결과에 직접 들어온다. 옮긴 것은 **구조**다:
한 번에 사라지지 않고 부위가 차례로 떨어져 나가며, 폭발이 여러 번 터지고, 잔해가 남는다.
폭발은 벌이 아니라 **`VFX/vfx_death.png`(초안 · 미승인)를 코드가 뿌리는 자리**다.

## 9. `boss_Idle` 2차 — ⚠️ **폐기** (animation group `17d6f7e9` · 4 생성)

| 항목 | 값 |
|---|---|
| 도구 | `animate_character` **mode="v3"** · `directions=["south"]` · `frame_count=4` · `keep_first_frame=true` → **5칸** |
| 앵커 | 캐릭터 `2fd860bd`(256 승인본 회전 · **남면 겹침 1.000**) |
| 끝 자세 | **주지 않았다** — 대기는 옮김·축소가 아니고 새 획도 없다 |
| 재생 | **핑퐁** — 0·1·2·3·4·3·2·1. 프레임을 굽지 않고 코드가 그렇게 돌린다 |

**문안의 출처** — 보스 아트 요청 문서(15-5) **5장 「대기」 행**이다. 동작만 영문으로 옮겼다.

| 15-5 5장 원문 | 옮긴 자리 |
|---|---|
| 제자리 가동 | `running in place` · `It stays on the same spot and does not travel.` |
| 무거운 기계가 돌아가는 느낌 | `a heavy machine ticking over` |
| **진폭은 실루엣 높이의 4~6%** | `rises and settles by about four to six percent of the machine's own height` |
| 로봇보다 **느린 주기**로 움직여 덩치가 살아야 함 | `the whole cycle is slow and ponderous, slower than a smaller machine would move, so its bulk reads` |

```
running in place, a heavy machine ticking over: the hull rises and settles by about four to six percent of the machine's own height, the turret and the pipework on the top deck breathe with it, and the whole cycle is slow and ponderous, slower than a smaller machine would move, so its bulk reads. It stays on the same spot and does not travel.
```

**목표 진폭** — 첫 칸 실루엣 높이가 **132** 이므로 4~6% 는 **5.3 ~ 7.9px** 이다.
`the turret and the pipework on the top deck breathe with it` 한 마디는 문서에 없다 —
**어디가 움직이는지를 도구에 말해 주는 자리**이며, 15-5 3-3 의 「상판에 파이프와 통풍구가 지난다」에서 부위 이름만 가져왔다.

---

## 10. `boss_Idle` 3차 — ✅ **사용자 육안 통과 · 설치** (animation group `a7de890a` · 4 생성)

| 항목 | 값 |
|---|---|
| 도구·인자 | 9번과 같다 (`v3` · south · `frame_count=4` · `keep_first_frame=true` → 5칸) |
| 재생 | 핑퐁 · **1.00초**(사용자 확정 2026-09-10) |

**2차 폐기 사유(사용자 육안)** — 「위아래로 들썩이는 것이 아니라 **전차가 덜덜거리는 느낌**이어야 한다.
**전차의 느낌이 적다**.」 그래서 문안에서 **`breathe`·`rises and settles`·`slow and ponderous` 셋을 뺐다.**

```
a heavy tracked tank sitting still with its engine running, juddering in place. The whole hull shakes in short sharp jolts, jerking a little sideways as well as up and down by about four to six percent of the machine's own height. Its tracks and road wheels shudder the most, the gun barrels quiver at their tips, and loose plates on the top deck rattle against the body. The shaking is quick and uneven, a rough mechanical vibration rather than a slow smooth rise and fall, and the machine never leaves its spot or changes its outline.
```

**2차에서 무엇을 바꿨나**

| 2차 | 3차 |
|---|---|
| `a heavy machine ticking over` | `a heavy tracked tank sitting still with its engine running, juddering in place` |
| `rises and settles` | `shakes in short sharp jolts, jerking a little sideways as well as up and down` |
| `slow and ponderous` | `quick and uneven, a rough mechanical vibration rather than a slow smooth rise and fall` |
| `the turret and the pipework breathe with it` | `its tracks and road wheels shudder the most, the gun barrels quiver at their tips, and loose plates on the top deck rattle` |
| — | `never leaves its spot or changes its outline` |

**「전차의 느낌」을 부위로 못 박은 것이 이 문안의 핵심이다** — 궤도·전륜·포신 끝·상판의 헐거운 판.
결과는 **가로 흔들림 2px**(2차는 0)과 **칸 사이 화소 차 최대 13.4%**(2차 7.5%)로 나타났다(로그 3-25).

⚠️ **`never changes its outline` 은 안 먹었다** — 세로 실루엣이 여전히 132 → 139 로 늘어난다.

⚠️ **15-5 5장의 「로봇보다 느린 주기」와 어긋난다.** 사용자 지시가 위라 빠른 쪽으로 갔다.
**5장 개정은 설계 몫**이며 아트는 문안과 값만 낸다.

---

---

## 설계에 넘기는 것

- **10-5 의 `mod_m` `{SHAPE}` 를 3번 문안으로 갈아 끼운다.** 2번은 「도구가 X 로 읽는다」를 근거와 함께 폐기 표기.
- **`mod_r` 문안은 그대로 두어도 된다** — 1차에서 통과했다.
- 규칙 9 장치를 노드·품목 문안에도 넓힐지는 설계 판정이다. **아트는 걸린 자리와 통한 문안만 낸다.**
