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

---

## 애니메이션 27벌 — 클립별 인자와 프롬프트 전문

> **왜 뒤늦게 적는가.** 27벌은 2026-09-07 09:30~09:41에 생성됐는데 그때 이 파일에 아무것도 적히지 않았다.
> HANDOFF §3 「생성 호출마다 description 전문을 로그에」의 그 자리이고, `260906_W04` 2-8 프롬프트 유실과 같은 뿌리다.
> **아래 표는 그날 세션의 도구 호출 기록에서 그대로 옮긴 것이다** — 지어 채우지 않았고, 구할 수 없는 칸은 「유실」로 적었다.

**구할 수 없었던 것 둘.**

- **방향별 job id.** 도구가 돌려준 것은 호출 하나당 **애니메이션 그룹 id 하나**뿐이다(「directions: south, north, east, west (4 jobs)」처럼 개수만 알려 준다). 그래서 job 칸은 그룹 id이며, 같은 그룹의 네 방향이 같은 값을 갖는다. 방향별 job id는 **유실**이다.

- **클립별 수신 시각.** 프레임은 벌 단위가 아니라 캐릭터 zip 한 덩어리로 내려받았다. 그래서 수신 칸은 그 zip의 시각이고 클립마다 다르지 않다.


호출 열하나 중 하나(`robot_a_Move` 첫 시도)는 job 슬롯이 모자라 실패했고 2분 45초 뒤 같은 인자로 다시 걸었다. 표에는 성공한 쪽만 있다.


| 대상 | 상태 | 방향 | 도구 | `frame_count` | `keep_first_frame` | 프레임 | 그룹 job | 호출(KST) | 수신(KST) | `action_description` 전문 |
|---|---|---|---|---|---|---|---|---|---|---|
| `fusion` | Death | south | `animate_character mode=v3` | 8 | true | 9 | `838040c2-7d41-49d2-90b4-3a28109055c3` | 09-07 09:37 | 09-07 09:40 | `the seam down its torso splitting open first as the locking clamps release, the two frames starting to come apart from each other and then sagging down onto the ground together, no explosion` |
| `fusion` | Idle | east | `animate_character mode=v3` | 4 | true | 5 | `5c5e37ad-655b-4170-a9ee-352aff6c3f43` | 09-07 09:32 | 09-07 09:40 | `idling with its weight settled, the shoulders rising and falling by about one twentieth of the machine's own height, steam venting from the exhaust ports` |
| `fusion` | Idle | north | `animate_character mode=v3` | 4 | true | 5 | `5c5e37ad-655b-4170-a9ee-352aff6c3f43` | 09-07 09:32 | 09-07 09:40 | `idling with its weight settled, the shoulders rising and falling by about one twentieth of the machine's own height, steam venting from the exhaust ports` |
| `fusion` | Idle | south | `animate_character mode=v3` | 4 | true | 5 | `5c5e37ad-655b-4170-a9ee-352aff6c3f43` | 09-07 09:32 | 09-07 09:40 | `idling with its weight settled, the shoulders rising and falling by about one twentieth of the machine's own height, steam venting from the exhaust ports` |
| `fusion` | Move | east | `animate_character mode=v3` | 6 | false | 6 | `ab34a6cc-0910-42b6-8073-66f65ac5c77f` | 09-07 09:37 | 09-07 09:40 | `a heavy clanking walk, the whole machine compressing down on each landing and holding there for a beat, its joints lagging a half step behind the body` |
| `fusion` | Move | north | `animate_character mode=v3` | 6 | false | 6 | `ab34a6cc-0910-42b6-8073-66f65ac5c77f` | 09-07 09:37 | 09-07 09:40 | `a heavy clanking walk, the whole machine compressing down on each landing and holding there for a beat, its joints lagging a half step behind the body` |
| `fusion` | Move | south | `animate_character mode=v3` | 6 | false | 6 | `ab34a6cc-0910-42b6-8073-66f65ac5c77f` | 09-07 09:37 | 09-07 09:40 | `a heavy clanking walk, the whole machine compressing down on each landing and holding there for a beat, its joints lagging a half step behind the body` |
| `robot_a` | Death | south | `animate_character mode=v3` | 8 | true | 9 | `fa3c68ee-61bb-46d0-bae5-47baba8ff945` | 09-07 09:36 | 09-07 09:40 | `power draining away, the machine sagging and settling down onto the ground, its joints buckling as it comes to a stop, no explosion` |
| `robot_a` | Idle | east | `animate_character mode=v3` | 4 | true | 5 | `500682b1-74fe-4279-9f52-9f469e28eedb` | 09-07 09:29 | 09-07 09:40 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, its exhaust vents puffing in a steady rhythm` |
| `robot_a` | Idle | north | `animate_character mode=v3` | 4 | true | 5 | `500682b1-74fe-4279-9f52-9f469e28eedb` | 09-07 09:29 | 09-07 09:40 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, its exhaust vents puffing in a steady rhythm` |
| `robot_a` | Idle | south | `animate_character mode=v3` | 4 | true | 5 | `500682b1-74fe-4279-9f52-9f469e28eedb` | 09-07 09:29 | 09-07 09:40 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, its exhaust vents puffing in a steady rhythm` |
| `robot_a` | Idle | west | `animate_character mode=v3` | 4 | true | 5 | `500682b1-74fe-4279-9f52-9f469e28eedb` | 09-07 09:29 | 09-07 09:40 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, its exhaust vents puffing in a steady rhythm` |
| `robot_a` | Move | east | `animate_character mode=v3` | 6 | false | 6 | `7d282139-0390-41c4-a1d9-788d4e3f08ac` | 09-07 09:32 | 09-07 09:40 | `walking forward, legs crossing past each other, the upper body sinking down with every footfall and holding for a beat on the frame where the foot lands` |
| `robot_a` | Move | north | `animate_character mode=v3` | 6 | false | 6 | `7d282139-0390-41c4-a1d9-788d4e3f08ac` | 09-07 09:32 | 09-07 09:40 | `walking forward, legs crossing past each other, the upper body sinking down with every footfall and holding for a beat on the frame where the foot lands` |
| `robot_a` | Move | south | `animate_character mode=v3` | 6 | false | 6 | `7d282139-0390-41c4-a1d9-788d4e3f08ac` | 09-07 09:32 | 09-07 09:40 | `walking forward, legs crossing past each other, the upper body sinking down with every footfall and holding for a beat on the frame where the foot lands` |
| `robot_a` | Move | west | `animate_character mode=v3` | 6 | false | 6 | `7d282139-0390-41c4-a1d9-788d4e3f08ac` | 09-07 09:32 | 09-07 09:40 | `walking forward, legs crossing past each other, the upper body sinking down with every footfall and holding for a beat on the frame where the foot lands` |
| `robot_a` | TagIn | south | `animate_character mode=v3` | 8 | true | 9 | `2a7f698f-de22-48bf-a7bf-00c82e396096` | 09-07 09:36 | 09-07 09:40 | `already in its firing stance as it arrives, the gun barrel levelled, the whole body shoved backwards by recoil and then settling forward again into a ready pose` |
| `robot_b` | Death | south | `animate_character mode=v3` | 8 | true | 9 | `5a0d217a-13fc-47fb-be8c-e1e500a902e3` | 09-07 09:37 | 09-07 09:41 | `power draining away, the machine sagging and settling down onto the ground with the hatches over its back launch tubes left standing open, no explosion` |
| `robot_b` | Idle | east | `animate_character mode=v3` | 4 | true | 5 | `4e1691eb-5466-4bf1-bb26-e591402160ff` | 09-07 09:29 | 09-07 09:41 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, the hatches over its back launch tubes easing open and shut` |
| `robot_b` | Idle | north | `animate_character mode=v3` | 4 | true | 5 | `4e1691eb-5466-4bf1-bb26-e591402160ff` | 09-07 09:29 | 09-07 09:41 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, the hatches over its back launch tubes easing open and shut` |
| `robot_b` | Idle | south | `animate_character mode=v3` | 4 | true | 5 | `4e1691eb-5466-4bf1-bb26-e591402160ff` | 09-07 09:29 | 09-07 09:41 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, the hatches over its back launch tubes easing open and shut` |
| `robot_b` | Idle | west | `animate_character mode=v3` | 4 | true | 5 | `4e1691eb-5466-4bf1-bb26-e591402160ff` | 09-07 09:29 | 09-07 09:41 | `idling in place with its weight settled, the whole machine rising and falling by about one twentieth of its own height, the hatches over its back launch tubes easing open and shut` |
| `robot_b` | Move | east | `animate_character mode=v3` | 6 | false | 6 | `45675e71-fd29-4941-a9cd-e618da4da796` | 09-07 09:34 | 09-07 09:41 | `walking forward, legs crossing past each other, the heavy upper body dropping further with every footfall than a lighter frame would and holding for a beat on the frame where the foot lands` |
| `robot_b` | Move | north | `animate_character mode=v3` | 6 | false | 6 | `45675e71-fd29-4941-a9cd-e618da4da796` | 09-07 09:34 | 09-07 09:41 | `walking forward, legs crossing past each other, the heavy upper body dropping further with every footfall than a lighter frame would and holding for a beat on the frame where the foot lands` |
| `robot_b` | Move | south | `animate_character mode=v3` | 6 | false | 6 | `45675e71-fd29-4941-a9cd-e618da4da796` | 09-07 09:34 | 09-07 09:41 | `walking forward, legs crossing past each other, the heavy upper body dropping further with every footfall than a lighter frame would and holding for a beat on the frame where the foot lands` |
| `robot_b` | Move | west | `animate_character mode=v3` | 6 | false | 6 | `45675e71-fd29-4941-a9cd-e618da4da796` | 09-07 09:34 | 09-07 09:41 | `walking forward, legs crossing past each other, the heavy upper body dropping further with every footfall than a lighter frame would and holding for a beat on the frame where the foot lands` |
| `robot_b` | TagIn | south | `animate_character mode=v3` | 8 | true | 9 | `7c7f0a1a-e0e4-4064-b963-54cdb105fe73` | 09-07 09:37 | 09-07 09:41 | `arriving with the hatches over its back launch tubes already thrown open, the frame pressed down towards the ground by the launch and then rising back into a ready pose` |

**캐릭터 id (8방향 회전 단계).** `robot_a` `1b1117e3-1807-471e-9f72-b1c8b3d6334c` · `robot_b` `888deba4-d749-40f5-a158-224f20f643f6` · `fusion` `8ec7231b-e054-4a74-b252-ac158f5cd01b`. 셋 다 `create_character mode=v3` · 256 × 256 · `view="high top-down"`이며 참조는 각 개체의 승인본이다.

