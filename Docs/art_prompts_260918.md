# 프롬프트 전문 — 2026-09-18 (구현-아트 세션 소유)

**왜 전문을 남기는가.** 요약은 「무엇을 시켰나」를 알려 주지만 **왜 그렇게 나왔는가**는 못 알려 준다.
다시 뽑을 때 고칠 자리를 찾으려면 **글자 그대로**가 있어야 한다. `art_prompts_260907.md` ·
`_260909.md` · `_260916.md` 와 같은 자리다.

⚠️ **`create_image_pro` 의 `description` 은 2000자 상한이 있다.** 쉴드 3차에서 넘겨 한 번 튕겼다 —
덜 중요한 절을 덜어 내고 다시 걸었다. **긴 문안은 무엇을 뺄지 미리 정해 둔다.**

---

## 1. 쉴드 발생 노드 `node_shield` — `create_image_pro` · 192 · **세 차례 60 생성**

⚠️ **192 는 후보가 한 장뿐이다**(캔버스가 후보 수를 정한다 · >170 → 1). 그래서 반려가 곧 재생성이고
세 번 뽑는 데 60 생성이 들었다.

**바탕은 보드 아트 요청 문서 6-1 공통 틀**이고 `{ROLE}`·`{SHAPE}`·`{COLOR}` 는 6-2 의 쉴드 행을
**글자 그대로** 끼웠다. 아래는 차수마다 **달라진 자리만** 적는다.

### 1차 — 문서 원문 그대로 (job `033a691e`) ❌ 치수선

문서 6-1 을 한 글자도 안 고치고 썼다. 결과가 **둘레에 도면 치수선·모서리 꺾쇠**를 달고 나왔다.

원인은 6-1 의 이 구절이다:

```
RENDERING: a technical schematic look, flat crisp shapes with three value steps, calm and even, no glow and no light bloom anywhere
```

**도구가 `technical schematic` 을 「그림의 화풍」이 아니라 「문서의 종류」로 읽었다.**
청사진 한 장을 그리라는 말로 받아들인 것이다.

### 2차 — 치수선을 막았다 (job `99bdb8de`) ❌ 육각이 아님

`RENDERING` 에서 `a technical schematic look` 을 걷고, 규칙 9 대로 **금지가 아니라 그 자리를 채우는**
절을 새로 넣었다.

```
RENDERING: the machine is rendered as a solid built object, flat crisp shapes with three value steps, calm and even, no glow and no light bloom anywhere

WHAT SURROUNDS IT: the whole area outside the hexagon's own outline is empty transparent background and holds nothing at all. It is bare emptiness, carrying no measuring line, no corner bracket, no tick mark, no faint outline and no drawing of any kind. This is a finished object photographed against nothing, not a page from a drawing set.
```

**마지막 문장이 장치다** — 도구가 낱말을 **그림의 종류**로 읽었으니 **어떤 종류인지를 되짚어** 말해야 했다.
✅ **치수선은 사라졌다.** ❌ 그런데 **육각이 아니었다**(사용자 육안). 옆면의 둥근 꼬투리와 브래킷이
육각 바깥으로 나와 변을 늘린 것이다. 문안이 「육각 몸체」만 말하고 **그 바깥으로 아무것도 안 나온다**는
것을 안 말했다.

### 3차 — 육각을 못 박았다 (job `8bf50a4b`) ✅ 승인

`SHAPE from above` 한 절을 **셋으로 쪼갰다** — 윤곽 · 부속의 자리 · 윗면.

```
OUTLINE, THE MOST IMPORTANT THING: the silhouette is one regular HEXAGON with EXACTLY SIX straight sides and EXACTLY SIX corners. Count them: six sides, six corners. The top side is horizontal and the bottom side is horizontal, parallel to the image edges, and the four remaining sides slope evenly between them.

THE WHOLE MACHINE LIVES INSIDE THAT HEXAGON: every pod, bracket, pipe, bolt and vent sits within the six sided outline and none of them reaches past it. Nothing breaks that edge, so counting the corners of the silhouette gives six every time.

UPPER FACE: a round emitter dish at the centre with one thin ring around it, and flat riveted panels filling the space between the dish and the six sides. No point and no direction anywhere.
```

**장치 셋.**
① **`Count them: six sides, six corners`** — 숫자를 **셈의 대상**으로 못 박으면 도구가 변 수를 지키는 쪽으로 기운다.
② **`THE WHOLE MACHINE LIVES INSIDE THAT HEXAGON`** — 2차의 진짜 원인을 막는 자리다.
규칙 9 대로 「튀어나오게 하지 마라」가 아니라 **「모든 부속이 어디에 사는가」**로 적었다.
③ **`OUTLINE, THE MOST IMPORTANT THING`** — 절 제목으로 우선순위를 말한다.

**참조** — `node_core` 승인본을 **URL 로** 걸었다.

```
usage: style anchor for board tiles - palette, riveted steel build, three value steps, level of detail, and the way its whole machine sits inside one clean unbroken outline with empty transparent background all around
```

⚠️ **인라인 base64 로는 못 넘긴다** — `node_core` 가 **27,480자**인데 보드 문서 11장이
**13,532자에서 이미 잘렸다**고 기록하고 있다. 리포 raw URL 이면 원본을 줄이지 않고 간다.

**결과** — 실루엣 174 × 147 · 여백 L9 R9 T23 B22 · 겹침 여덟 28쌍 최대 0.860 · 0.90 초과 0쌍.

---

## 2. 광역형 드론 범위 타격 `vfx_drone_aoe` — `create_1_direction_object` · 256 · 20 생성 · 1차 통과

**틀 B(바닥에 눕는다) 전문**과 **`{SHAPE}` 전문**은 연출 아트 요청 문서 것을 글자 그대로 썼다.
`{COLOR}` 만 아트가 지었다 — 문서가 「폭발탄 주황과 **한 단 떨어진 명도** — 실측 뒤 정한다」로 열어 두었다.

```
Pixel art game effect sprite seen from DIRECTLY ABOVE, lying flat on the ground plane, one single effect alone on a fully transparent background, nothing else in frame

WHAT THIS IS: the spreading ground ring left by a wide area strike

SHAPE from above: a single thin ring of pale energy lying flat on the ground, perfectly circular and centred, the middle of the ring completely empty, the ring edge softly fading outward, no burst and no debris

COLOR: pale washed-out amber, clearly lighter and paler than a bright orange explosion, one value step away from it

RENDERING: flat crisp pixel shapes with hard edges, three value steps only, bright core fading to a darker rim, readable at small size

EDGES: the effect meets the transparent background directly, a clean unbordered edge
```

**`{COLOR}` 를 지은 법** — 문서의 「한 단 떨어진 명도」를 **비교 대상과 방향으로** 옮겼다:
`clearly lighter and paler than a bright orange explosion, one value step away from it`.
「한 단」만으로는 **어느 쪽으로** 한 단인지가 없어 도구가 어둡게 갈 수도 있었다.

**결과** — 휘도 **80.2%** · 채도 0.53 대 `vfx_hit_explosive` 59.3% · 0.71.
**밝은 쪽으로 20.9%p 벌어졌고 채도가 0.18 낮다.** 가운데는 **안쪽 60% 반경의 불투명 화소 0**.

⚠️ **캔버스 256 과 프레임 수는 문서에 없어 가정이다** — 대장에 그렇게 적었다.

---

## 3. HUD 재화 그림 `icon_gold` · `icon_scrap` — `create_1_direction_object` · 24 · 20 생성 · 1차 통과

**한 호출에 둘을 넣었다.** `item_descriptions` 는 한 번의 생성으로 **서로 다른 물건 여럿**을 낸다.
아이콘 32 둘과 드롭 24 둘을 넷으로 따로 걸면 80 생성인데, **두 호출 40** 으로 끝났다.
(사용자가 24 판을 골라 32 판 20 은 결과적으로 안 쓰게 되었다 — 후보 폴더에 남는다.)

### 공통 `description` (24 판)

```
a tiny pickup lying flat on the ground seen from directly above, one single object alone on a fully transparent background, flat crisp pixel shapes with three value steps, a single dark outline all around, no glow and no light bloom, carrying its own colour and needing no tint, still readable when very small. WHAT SURROUNDS IT: the whole area outside the object's own outline is empty transparent background and holds nothing at all - no ground, no plate, no sparkle, no shadow and no drawing of any kind.
```

### `item_descriptions` 둘

```
[0] a single gold coin lying flat on the ground, a round struck coin of warm yellow gold with a darker gold rim, very simple with only a few shapes
[1] a single small piece of scrap metal lying flat on the ground, an irregular chunk of dull grey steel with a spot of rust brown, very simple with only a few shapes
```

**장치 둘.**

① **`carrying its own colour and needing no tint`** — 설계가 「코드 틴트 없음(자기 색)」으로 냈다.
「색을 칠하지 마라」로 적을 자리가 아니다. 그림에 **덧칠이 필요 없을 만큼 제 색이 있어야** 한다는 뜻이라
**그림이 갖출 성질**로 옮겼다.

② **`WHAT SURROUNDS IT` 절** — UI 아이콘 문안은 도구가 **판·틀·배지·반짝임**을 딸려 그리기 쉽다.
규칙 9 대로 「그리지 마라」가 아니라 **그 자리가 무엇인가**로 적었다:
「윤곽 바깥은 텅 빈 투명 배경이고 **아무것도 없다**」. 쉴드 노드에서 치수선을 걷어 낸 것과 같은 수다.

### 무엇이 덜 먹었나

⚠️ **`very simple with only a few shapes` 가 고철 쪽에 덜 먹었다.** 24 칸에 **색 45** 로 나왔다
(금화는 29). 「단순하게」는 **셈할 수 있는 말이 아니라** 도구가 정도를 제 맘대로 잡는다.
다음에 같은 자리를 만나면 **색 수나 도형 수를 숫자로** 적는 편이 낫다 —
쉴드에서 「세어 보라: 여섯 변, 여섯 꼭짓점」이 먹은 것과 같은 까닭이다.

⚠️ **「아이언사가 톤」은 문안에 못 넣었다.** 도구가 상표 이름을 화풍으로 읽지 않는다.
이 집 공통 틀(**세 단 명암 · 단색 어두운 테두리 · 발광 없음**)로 옮겨 적었다.
