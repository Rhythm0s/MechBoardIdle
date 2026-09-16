# 프롬프트 전문 — 2026-09-16 (구현-아트 세션 소유)

**왜 전문을 남기는가.** 요약은 「무엇을 시켰나」를 알려 주지만 **왜 그렇게 나왔는가**는 못 알려 준다.
다시 뽑을 때 고칠 자리를 찾으려면 **글자 그대로**가 있어야 한다. `Docs/art_prompts_260907.md` ·
`_260909.md` 와 같은 자리다.

**오늘의 공통 문법** — `CAMERA` · `FRAMING` · `BUILD` · `COLOR` · `RENDERING` · `EDGES`
(로봇 A / 보스 3차 틀 · `art_prompts_260909.md` 5장).

⚠️ **`view` 인자에는 「비스듬」이 없다.** `create_1_direction_object` 는 `top-down` 과
`sidescroller` 둘뿐이고 `create_image_pro` 에는 인자 자체가 없다. **시점은 전부 문안으로 넣었다.**

---

## 1. 레터박스 격납고 벽 — `create_image_pro` · 256 × 512 · 40 생성 · job `3e571f4f`

⚠️ **후보가 한 장뿐이다.** 캔버스가 후보 수를 정한다(≤42 → 64개 · ≤85 → 16 · ≤170 → 4 · **넘으면 1**).

```
Pixel art hangar wall, seen straight on from the front, filling the whole tall narrow image from edge to edge.

CAMERA: the viewer stands on the hangar floor facing the wall square-on. The wall is a flat vertical surface directly ahead. Every panel edge, every rivet line and every pipe runs either straight up and down or straight across, parallel to the edges of the image. This is a front elevation of a wall, not a floor and not a view from above.

TILING: the very top row of the image continues seamlessly into the very bottom row, so the wall can be stacked on top of itself forever without a visible join. The pipes and panel seams that reach the top edge line up exactly with the ones that reach the bottom edge.

BUILD: riveted steel wall panels in a regular grid, with vertical conduit pipes and cable runs bolted across them, a few recessed vents and bolt heads, heavy industrial hangar construction. Every panel face is bare unmarked metal, plain and blank across its whole surface.

LIGHT: very dim. A few small cold lamps set deep into the wall give weak pools of pale blue-white light, and everything between them falls away into near-black. The wall is much darker than it is bright.

COLOR: cold dark grey-blue steel and near-black shadow only, with the faint pale blue-white of the lamps. Every surface is a cool grey or a cool blue-grey.

RIGHT EDGE: the rightmost strip of the image settles into one single flat very dark colour, even and featureless, so it can continue outward off the edge.

RENDERING: flat crisp pixel shapes with a small number of value steps, calm and even, no glow and no light bloom anywhere.
```

**`no_background=false`** — 벽은 배경이라 투명이 아니다.

⚠️ **경고 띠·글자를 금지형으로 쓰지 않았다**(규칙 9). 「쓰지 마라」 대신 그 자리를 채웠다 —
**「모든 판 면은 맨 금속이고 표면 전체가 아무 표시 없이 밋밋하다」**.

**결과** — TB 이음매 **0.93배 「이어짐」** · 휘도 **20.3%** · 주황 **0.0%** · 바깥 색 **`#13111B`**.

---

## 2. 피격 VFX 셋 — `create_1_direction_object` · 128 · **20 생성으로 셋** · job `28535db2`

**비용을 절반 아래로 줄인 수** — `item_descriptions` 로 **슬롯마다 다른 것**을 시킨다.
셋을 따로 뽑으면 60~120 인데 **한 번에 20** 이다.

**바탕 `description`** (슬롯을 안 채운 넷째 칸이 여기서 나온다)

```
A small bright impact effect burst, greyscale only - white and pale grey, no colour anywhere. It hangs in the air on its own with nothing behind it, the fully transparent background. Seen from in front and slightly above, the same angle a machine standing on the ground is seen from, so the burst reads as sitting ON the machine's body rather than flat on the floor. Chunky pixel art, hard-edged shapes, a small number of value steps, no glow and no soft blur.
```

**`item_descriptions` 셋**

```
[0] A small burst of sharp sparks thrown outward in every direction from one central point of impact, short straight spikes of white and pale grey, bright at the centre and thinning at the tips, no colour anywhere, seen from in front and slightly above.

[1] One long thin piercing spark: a narrow bright white streak driving straight through, with a few fine slivers trailing along its length, much longer than it is wide, pale grey at its edges, no colour anywhere, seen from in front and slightly above.

[2] A small round explosion: one compact ball of white and pale grey at the centre with a hard-edged ring of debris flung out around it in a circle, dense in the middle and broken at the rim, no colour anywhere, seen from in front and slightly above.
```

**무채색은 「no colour anywhere」를 세 슬롯에 다 박아 얻었다** — 탄종색은 코드가 틴트한다.
**시점은 「기계의 몸에 붙어 있는 것으로 읽힌다」** 로 넣었다(바닥에 누운 것이 아니라).

### 벌 셋 — `animate_object` v3 · `frame_count=4` · `keep_first_frame=false` · **각 1 생성**

4칸 × 128 × 128 = 65,536 이라 방향당 **1 생성**이다. 참조 칸을 떼어 **정확히 4장**.

```
standard  : a single impact flash: the sparks start tight and bright at the centre, spread outward fast, thin out as they travel, and the last frame is almost gone. It plays once and does not come back.

pierce    : a single piercing flash: the streak is at its brightest and sharpest at the start, then stretches and thins along its own length, and the last frame is almost gone. It plays once and does not come back.

explosive : a single explosion: the ball at the centre is small and bright at the start, swells outward while the ring of debris flies apart around it, and the last frame is almost gone. It plays once and does not come back.
```

⚠️ **「마지막은 거의 사라진다」가 `explosive` 에서 안 먹었다** — 화소 4429 · 3787 · 3490 · **4712**
(마지막/첫 = **1.06**). 사용자 판정으로 **다시 안 뽑고 코드가 알파로 뺀다.**

---

## 3. 연출 넷 재생성 — 크기 축 · **호출 두 번 40 생성**

사용자 반려 **「다 이미지가 너무 크다」**. 따로 넷을 뽑으면 80~160 인데 **둘씩 묶어 40** 이다.

⚠️ **크기를 금지형으로 쓰지 않았다**(규칙 9) — 「크게 그리지 마라」 대신 **빈자리가 무엇인지**를 적었다.

### 3-1. 64 캔버스 · 후보 **16장** · job `2fc2ff21`

```
[바탕] A very small pixel art combat effect, alone on a fully transparent background, floating in the air with nothing behind it. Seen from in front and slightly above, the same angle a machine standing on the ground is seen from. Warm pale yellow-white and grey only. Chunky pixel art, hard-edged shapes, a small number of value steps, no glow and no soft blur. The effect is tiny and compact and leaves most of the picture empty.

[0] muzzleflash - A small muzzle flash at the tip of a gun barrel: one short cone of pale yellow-white fire pointing away from the gun, with two or three tiny sparks at its mouth. The flash is small and stubby, no longer than a hand, and the rest of the picture stays empty. Seen from in front and slightly above.

[1] shelleject - One single spent brass shell casing tumbling through the air: a small cylinder with a rim at one end, pale brass and grey, a couple of tiny motion slivers behind it. Just the one casing, very small, and the rest of the picture stays empty. Seen from in front and slightly above.
```

**크기를 묶은 말** — 「효과는 **작고 옹골지며 그림의 대부분을 비워 둔다**」 ·
「**손 길이보다 길지 않은** 짧고 뭉툭한 불꽃 원뿔 하나」 · 「**딱 한 개**의 탄피」.

### 3-2. 128 캔버스 · 후보 4장 · job `767d065b`

```
[바탕] A compact pixel art combat effect, alone on a fully transparent background, floating in the air with nothing behind it. Seen from in front and slightly above, the same angle a machine standing on the ground is seen from. Chunky pixel art, hard-edged shapes, a small number of value steps, no glow and no soft blur. The effect sits in the middle of the picture and leaves a clear empty margin on all four sides.

[0] death - A machine breaking apart at the moment it dies: one short burst of pale grey smoke at the centre with a few hard metal fragments thrown outward around it and two or three small sparks. It bursts from one point and stays compact, with a clear empty margin on all four sides. Cold grey and near-white only. Seen from in front and slightly above.

[1] burst (2차 · 반려) - A single heavy impact strike: one compact wedge of pale white force driving in from one side, with a tight cluster of sharp shards thrown off where it lands. It is one blow at one spot, compact, with a clear empty margin on all four sides. Cold white and pale grey only. Seen from in front and slightly above.
```

⚠️ **`[1]` 이 효과가 아니라 석궁 같은 물건으로 나왔다.** 크기는 0.33배로 맞았다.
**「한 방이 한 곳에 떨어진다」가 도구를 연상시킨 것**으로 본다.

---

## 4. `vfx_burst` 3차 — 원형 파장 · 128 · 20 생성 · job `3c38318e`

사용자 지시 **「원형의 파장이 커져야 함」**.

```
[바탕] A circular shockwave impact effect, alone on a fully transparent background, floating in the air with nothing behind it. Seen from in front and slightly above, the same angle a machine standing on the ground is seen from. This is a wave of force spreading outward through the air, not an object and not a weapon - there is no handle, no frame, no shaft and nothing solid to hold. Cold white and pale grey only. Chunky pixel art, hard-edged shapes, a small number of value steps, no glow and no soft blur. The ring is wide and reaches close to all four edges of the picture.

[0] 승인 - One wide circular shockwave ring spreading outward from a small bright point at the centre: a single hard-edged white ring far out from the centre, thick at the top and thinning around its circumference, with the middle of the ring left empty. The ring is large and reaches close to all four edges. It is a wave of air, nothing solid.

[1] Three concentric circular shockwave rings racing outward from one central impact point, the innermost small and bright, the outermost widest and faintest, each ring a thin hard-edged band of white with empty space between them. The outermost ring reaches close to all four edges. Rings of force only, nothing solid.

[2] A wide ring of shockwave spreading outward with short sharp shards flung along it, brightest where it began at the centre and stretching thinner as the circle grows. The circle is large and open in the middle and reaches close to all four edges. A spreading wave, nothing solid.
```

**석궁을 막은 두 문장** — 규칙 9 대로 **그 자리가 무엇인지 먼저** 적었다:
「이것은 **공기를 타고 퍼져 나가는 힘의 물결**이지 물건도 무기도 아니다 —
**손잡이도, 틀도, 자루도, 쥘 만한 단단한 것도 없다**」.

**크기는 2차를 반대로 풀었다** — 2차 「네 변에 뚜렷한 빈 여백을 남긴다」 →
3차 **「고리가 넓고 네 변에 가깝게 닿는다」**. 결과 여백 L6 R6 T6 B6 · **0.53배**.

---

## 5. `ammo_standard` — `create_1_direction_object` · 64 · 후보 **16장** · 20 생성 · job `3139ed6e`

**참조 없음.** 형제 자산(`ammo_pierce`)을 눈으로 보고 원인을 짚어 문안만으로 갔다.

```
A single short stout brass rifle cartridge standing upright on its flat base, seen from in front and slightly above.

SHAPE: it is clearly taller than it is wide, standing on end. The base is a flat rim that steps out wider than the body. Above the rim the brass case rises straight, then shoulders inward partway up, and a short blunt rounded grey-steel slug sits on top. The shoulder step and the rim step are both plainly visible as changes in width, so the outline is a stack of distinct blocks rather than one smooth tube.

SIZE: short and stubby - the whole cartridge is only about twice as tall as it is wide, a fat little round, not a long slim one.

COLOR: warm brass and amber for the case, cool grey steel for the slug on top, a dark outline all around.

FRAMING: one single cartridge alone in the middle of the picture on a fully transparent background, with clear empty space on all four sides and nothing else in the frame.

RENDERING: chunky pixel art, hard-edged shapes, a small number of value steps, readable as a cartridge even when shrunk very small, no glow and no soft blur.
```

**세 축을 문안으로 묶었다** — ① 「**세워 서 있다** · 폭보다 분명히 높다」(20px 에서 세로가 살게)
② 「폭의 **두 배 정도 높이**뿐인 뚱뚱하고 작은 한 발 · 길고 날씬한 것이 아니다」(관통탄과 갈리게)
③ 「**턱 둘**이 폭이 변하는 자리로 또렷이 보여 윤곽이 **블록을 쌓은 모양**」(채움 낮추기).

**결과** — 세로 10 → **20** ✅ · 관통탄과의 구분 8×20 대 5×20 ⚠️ · 채움 94% → **90.0%** ⚠️(거의 안 내려갔다).
③ 이 목표에 못 미친 것이 그대로 보인다 — **다음에 고칠 자리는 여기다.**
