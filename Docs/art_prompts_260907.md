# 생성 프롬프트 기록 — 2026-09-07

> **왜 리포에 두는가.** 프롬프트 전문을 세션 로그에만 두었다가 2026-09-06에 통째로 잃었다
> (`260906_W04` 2-8). 세션 임시 자리에 있던 것은 다음 세션에 없다 — 지침 §7.

## 합체 로봇 256 전투 스틸

`260906_W05` 5-1이 지목한 선행. **그전까지 `robot_fusion_256.png`은 512 승인본의 NEAREST
축소본(`29290d8` 「참조본」)이었고, 256 전투 스틸은 생성된 적이 없었다.**

| 항목 | 값 |
|---|---|
| 도구 | `create_image_pro` · 256×256 · `no_background=true` · 후보 1 (170 초과는 언제나 하나) |
| 앵커 | 로봇 A 승인본 — `raw.githubusercontent.com/.../Art/Units/robot_a.png` (md5 `ecffb0dec30f`) |
| 앵커 세대 확인 | raw = 리포 = 15-1 7-5 서명본, 셋이 같은 md5다 |
| 1차 | job `f7a36257` · md5 `d854f6c4` · **불합격(구현 자체 판정)** — 시점이 정면 쪽 · 검은 테두리 |
| 2차 | job `47cdb935` · md5 `b3ef28a8` · **사용자 승인 (2026-09-07)** |
| 비용 | 20 + 20 = 40 생성 |

### 1차에서 2차로 바뀐 것 둘

1. `SILHOUETTE` 앞에 `seen from above,`를 붙여 **시점 서술을 세 절로** 만들었다.
   15-3 6-1이 「결과가 정면으로 나오면 세 번째 자리를 만든다」로 미리 적어 둔 처방이며,
   근거는 캐릭터 아트 요청 문서(15) 규칙 2 「절이 많은 쪽이 이긴다」다.
2. `EDGES`를 로봇 A 승인본 프롬프트(15-1 7-1)와 같은 문안으로 늘렸다 —
   `no black outline anywhere, the silhouette meets the transparent background directly`.
   15-3 6-1은 앞 절만 갖고 있었다. 규칙 9 「금지문은 오히려 불러온다」에 걸리는 자리다.

### 2차 프롬프트 전문

```
Pixel art fused battle mech seen from a HIGH TOP-DOWN camera, looking down at the machine from above and slightly in front, alone on a fully transparent background,
reference image 1 is the approved player robot sprite - match its art style, shading and surface finish exactly, this is that same machine locked together with its partner frame into one larger unit

VIEWING ANGLE: we are above the machine. We see the TOP surfaces of its shoulders and head. Its front and its back read as different shapes. This is NOT a front view.

seen from above, SILHOUETTE from above: a clear HUMANOID frame, one head, two broad shoulders, two arms, two legs planted wide apart, heavier and wider than either source machine was on its own, the largest allied unit in the game

FUSION: this frame is symmetrical left to right. Both arms end in the straight angular gun barrels of the first machine, reaching forward. A bank of vertical launch tubes from the second machine sits across its back and shoulders, and because the camera is above we look down into their circular mouths as a grid of circles on the flat upper surface. A seam line and locking clamps run down the centre of the torso where the two frames meet.

BUILD: well maintained military hardware, panels that fit cleanly together, worn paint and scuffed edges from long service, thicker plating than a standard frame

COLOR: gunmetal blue-grey steel with a single ORANGE accent along the shoulders and chest

EDGES: no black outline anywhere, the silhouette meets the transparent background directly
```

`reference_images`의 `usage`: `art style, shading and surface finish - this is the approved player robot sprite`

### 남은 판정 하나

**시점은 2차에서도 정면 쪽이다.** 15-1 7-4 규칙 7이 「각도를 강하게 밀면 강조색을 잃고
무기가 팔에 묻힌다」로 적어 둔 맞바꿈이며, 로봇 A 승인본도 여덟 번 걸렸다.
사용자가 이 수준으로 승인했다 — 더 미는 것은 다음에 정한다.
