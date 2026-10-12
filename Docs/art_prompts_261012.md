# 프롬프트 전문 — 2026-10-12 영상 삽화 (구현-아트 세션 소유)

**게임 자산이 아니다.** 영상 편집에만 쓰며 `Assets/` 에 한 장도 안 넣는다.
저장 자리는 `Docs/art_log/video/` 하나다. **글자는 한 장에도 안 넣었다** — 편집에서 얹는다.

도구는 전부 `create_image_pro` 이고, **캔버스가 전부 170 을 넘어 후보가 장당 1장**이다.
그래서 반려가 곧 재생성이다.

---

## 0. 모든 장에 공통으로 들어간 절 둘

### ① 톤 (아이언사가 · 무채색 금속 + 주황 한 색)

```
COLOR: desaturated gunmetal steel greys ..., with ONE saturated warm orange used only for ...

RENDERING: flat crisp pixel shapes with three value steps, calm and even, hard edges, no glow and no light bloom anywhere, no soft blur.
```

### ② 글자 막기 — **규칙 9 로 적었다**

```
WHAT THE PICTURE HOLDS: ... only. It carries no lettering, no numbers, no labels, no signage, no logos, no captions and no writing of any kind anywhere, and no frame, no border and no measuring lines. It is a picture only.
```

⚠️ **「글자를 넣지 마라」가 아니라 「이 그림이 무엇을 담고 있는가」로 적었다.**
마지막 문장 **`It is a picture only.`** 가 장치다 — 쉴드 노드에서 치수선을 걷을 때 쓴
「아무것도 없는 데 놓고 찍은 완성된 물건이지 도면집의 한 장이 아니다」와 같은 수다.
**여덟 장 전부 글자 0 으로 나왔다.**

---

## 1. 브릿지 A→B `bridge_AB_v1.png` — 512 x 288 · job `40d704a9` · **25 생성** · ✅ 1차 통과

```
A pixel art cutscene illustration, a wide horizontal band, showing a CUTAWAY CROSS-SECTION of a giant war machine's torso with a factory production line running inside it.

WHAT IS IN THE PICTURE: the armoured hull is cut open like a diagram model so we see inside. Within the opening runs one long conveyor belt line of machine cells, each cell a lit bay with rollers and crates moving along. The belt runs left to right across the band. Every bay glows with warm working light EXCEPT ONE: a single bay near the middle sits dark and dead, its rollers stopped and its light out, a black gap in a lit row.

COMPOSITION: the machine fills the band edge to edge. The dark broken bay is the one thing the eye lands on.
```

**참조** — `Units/robot_a.png` · `usage` = 「화풍과 팔레트 — 무채색 장갑판, 주황 강조 한 색, 세 단 명암, 기계 디테일 수준」

**장치** — 「한 칸만 꺼졌다」를 **`EXCEPT ONE`** 과 **`a black gap in a lit row`** 로 두 번 말했다.
한 번만 말하면 도구가 여러 칸을 끄거나 전부 켠다.

---

## 2. 소개 삽화 1차 `intro_v1.png` — 512 x 512 · job `3f75e7dc` · **25 생성** · ❌ **반려**

**로봇 A·B·합체체 셋을 격납고 벽 앞에 세우는** 문안이었다. 셋의 역할은 제대로 갈렸고
위 1/4 도 비었으나 **사용자 반려 — 「명제가 안 읽힌다」.**

⚠️ **아트가 낸 흠이 하나 더 있었다 — 벽이 안 나왔다.**
`no_background` 의 기본값이 **`true`** 라 배경을 잘라낸다. `letterbox_wall` 을 참조로 걸었는데도
벽이 안 남은 것이 그래서다. 불투명 화소가 **24%** 였다.
**이후 호출부터는 `no_background=false` 로 넣었다.**

**반려본으로 폴더에 남긴다** — 지우지 않는다. 셋을 나란히 세우는 구도가 다시 필요해질 수 있다.

---

## 3. 소개 삽화 2차 `intro_v2.png` — 512 x 512 · job `ac571df5` · **25 생성** · ✅ 통과

**재시안 지시**(사용자) = 「플레이어 로봇이 몬스터와 싸우는 구도」.

```
A pixel art action illustration: ONE large player mech holding off a pack of smaller enemy machines on open ground.

THE HERO, LARGE AND CENTRAL: a rifle mech ... stands in the middle-lower part of the picture, much bigger than everything else, planted wide and braced. Its gun barrel is FIRING toward the enemies: a hard-edged muzzle flash at the barrel mouth and two or three bright tracer rounds streaking away from it in a straight line.

THE ENEMIES, SMALL AND MANY: three or four squat dark-red enemy walkers rush in at the hero from the sides and the far ground. They are clearly smaller than the hero. One is close enough to be mid-stride with its legs apart; the others are further back and lower in the picture plane.

THE GROUND: bare packed dirt stretching back, with a few scattered rocks and shallow ruts. Dust is kicked up around the running enemies' feet.

COMPOSITION, IMPORTANT: the UPPER QUARTER of the picture is plain empty sky - one flat dark tone with nothing standing in it at all, no machine, no debris, no cloud, no smoke. Everything happens below that line.
```

**참조 둘** — `robot_a.png`(주인공 · 화풍) · `mob_infantry.png`(적 기계의 몸집과 진홍 팔레트)

**결과** — **위 1/4 이 색 1 · 표준편차 7.4** 로 완전히 비었다. 로고가 들어갈 자리다.

⚠️ **적이 「보병·포격」으로 안 갈렸다** — 다섯이 거의 같은 계열이고 포격체의 긴 포신이 없다.
**사용자가 그 사실을 알고 통과시켰다.**

**무엇이 1차와 달랐나** — 1차는 **셋을 세워 놓았고**, 2차는 **하나가 싸운다.**
「명제가 안 읽힌다」는 말이 가리킨 자리가 **구도**였다. 삽화는 **상태**가 아니라 **사건**을 보여야 읽힌다.

---

## 4. 브릿지 B→C `bridge_BC_v1.png` — 512 x 288 · job `11e9ee72` · **25 생성** · ✅ 1차 통과

```
... SPLIT DOWN THE MIDDLE into two halves by a single hard vertical line.

LEFT HALF: a top-down factory board of square tiles with conveyor belts running between machine nodes. The belts are jammed - crates are piled up nose to tail along the belt and cannot move, and the stalled stretch is marked in a hard warning RED that appears nowhere else in the picture. The nodes sit dark and idle.

RIGHT HALF: the upper body of a rifle mech seen from the front, its gun arm raised. Its ammunition magazine is OPEN and EMPTY - a hollow dark box with bare feed rails inside and not a single round in it.

THE TWO HALVES READ AS CAUSE AND EFFECT: the jam on the left, the empty magazine on the right.
```

**장치 둘** — ① **`a single hard vertical line`** 으로 가르는 선을 **물건**으로 적었다.
「반쪽 화면」만으로는 도구가 부드럽게 섞는다. ② **`THE TWO HALVES READ AS CAUSE AND EFFECT`** —
두 그림이 **왜 한 장에 있는지**를 말해 준다. 안 적으면 무관한 둘이 나란히 놓인다.

⚠️ **빨강을 한 곳에 가뒀다** — `a hard warning RED that appears nowhere else in the picture`.
빨강이 오른쪽에도 번지면 「정체 때문에 탄이 없다」가 안 읽힌다.

---

## 5. 브릿지 C→D `bridge_CD_v1.png` — 512 x 288 · job `6993c79d` · **25 생성** · ✅ 1차 통과

```
two giant war machines standing BACK TO BACK at the centre of the frame ... Where the two bodies meet runs ONE single bright warm orange light seam - a clean straight line of light down the join, and it is the only bright thing in the picture. Both machines are lit dimly otherwise, as if the power has gathered into that seam.

RENDERING: ... The seam is a hard-edged band of light, not a haze.
```

**장치** — **`it is the only bright thing in the picture`** 와 **`lit dimly otherwise`** 를 짝으로 썼다.
빛줄 하나를 눈에 걸리게 하려면 **그것을 밝게** 하는 것보다 **나머지를 어둡게** 하는 쪽이 확실하다.

⚠️ **`not a haze`** 를 넣은 까닭 — 이 집 공통 틀이 「발광 없음」인데 빛줄은 빛이다.
**번짐 없이 테두리가 선 빛**으로 좁혀 픽셀 화풍을 지켰다.

---

## 6. 설명 띠 넷 — 512 x 128 · 각 **15 생성** · ✅ 1차 통과

| 띠 | 파일 | job | 왼쪽 모티프 |
|---|---|---|---|
| A | `band_A_v1.png` | `15e0a91b` | 벨트 바닥 타일 한 칸 |
| B | `band_B_v1.png` | `ae0ff394` | 가득 찬 탄창 (마운트 거치) |
| C | `band_C_v1.png` | `ef67097c` | 합체 접합부 클로즈업 |
| D | `band_D_v1.png` | `8f286c13` | 노드 셋 (반응로·가공·호퍼) |

**넷이 쓴 공통 절 — 빈 판을 규칙 9 로 적었다**

```
RIGHT TWO THIRDS: a plain dark panel, flat and completely bare. It is one even dark surface with nothing standing on it at all - no machinery, no pipes, no rivets, no pattern, no texture, no marks of any kind. It is an empty dark plate, and it stays empty right to the edge of the picture.
```

**`and it stays empty right to the edge of the picture` 가 장치다** — 「비어 있다」만 적으면
도구가 가장자리에 장식을 붙인다. **어디까지 비는지**를 말해야 끝까지 빈다.

**실측 — 빈 판은 넷 다 평평하다**

| 띠 | 오른쪽 판 열편차 | 그림이 끝나는 자리 |
|---|---|---|
| A | 7.0 | **28%** ✅ |
| B | 4.7 | **40%** ⚠️ |
| C | 5.0 | **49%** ⚠️ |
| D | 5.4 | **49%** ⚠️ |

**재는 법** — 열마다 화소 표준편차를 내고 **판 바탕 수준의 두 배를 넘는 마지막 열**을 그림의 끝으로 잡았다.

⚠️ **B·C·D 의 그림이 왼쪽 1/3(33%)을 넘었다.** 문안에 「왼쪽 3분의 1」을 적었지만
**「3분의 1」이 셀 수 있는 말이 아니라** 도구가 정도를 제 맘대로 잡았다 —
고철 아이콘의 「아주 단순하게」, 포트 배지의 「네 변에 넉넉한 여백」과 **같은 자리**다.
**사용자가 이 값을 보고 그대로 통과시켰다** — 글자 자리가 51~60% 남는다.

**다음에 같은 자리를 만나면 화소 수를 박는다** —
「그림은 왼쪽 170화소 안에 들어가고 171번째 열부터 오른쪽 끝까지는 빈 판이다」.
쉴드의 「세어 보라: 여섯 변, 여섯 꼭짓점」이 먹은 것과 같은 까닭이다.

---

## 7. 비용

| 장 | 캔버스 | 생성 |
|---|---|---|
| 브릿지 A→B | 512 x 288 | 25 |
| 소개 1차 (반려) | 512 x 512 | 25 |
| 브릿지 B→C | 512 x 288 | 25 |
| 브릿지 C→D | 512 x 288 | 25 |
| 소개 2차 | 512 x 512 | 25 |
| 띠 A·B·C·D | 512 x 128 | 15 x 4 = 60 |
| **합** | | **185** |

⚠️ **처음 어림은 160~320 이었고 아래 끝에서 끝났다.** 두 가지가 아꼈다 —
① **세로 128 짜리 띠가 25 가 아니라 15 로 붙었다**(넷에 40 절약)
② **성격이 다른 둘을 먼저 시험하고 나머지를 걸었다** — 톤이 어긋났으면 160 을 버릴 자리였다.

⚠️ **반려는 한 장(소개 1차)뿐이고 25 다.** 캔버스가 170 을 넘어 **후보가 장당 1장**이라
반려가 곧 재생성인 구조였는데, **여덟 중 일곱이 1차 통과**였다.
