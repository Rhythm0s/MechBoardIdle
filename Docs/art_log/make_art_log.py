"""`Docs/art_log/260907_anim.md` 를 만든다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_art_log.py

**md5 · 프레임 수 · 캔버스 · 수신 시각은 실물에서 스크립트가 찍는다.**
사람이 적는 줄은 빠지고 도구가 찍는 줄은 안 빠진다 (2026-09-06 교훈).
도구 · 인자 · 프롬프트 전문 · job id 는 호출한 값을 아래 표에 그대로 옮겨 둔다.
"""
import datetime
import hashlib
import io
import os
import struct

ROOT = "Assets/_Project/Art/Anim"
OUT = "Docs/art_log/260907_anim.md"
STILL = "Assets/_Project/Art/Units/robot_fusion_256.png"

RAW = "https://raw.githubusercontent.com/Rhythm0s/MechBoardIdle/main/Assets/_Project/Art/Units/%s.png"

CHARS = [
    ("robot_a", "1b1117e3-1807-471e-9f72-b1c8b3d6334c", RAW % "robot_a",
     "A worn industrial top-down battle mech. Desaturated gunmetal grey-blue body with one saturated "
     "accent colour on the shoulder plates and the weapon housing. The right arm is thicker and ends in "
     "an angular gun barrel pointing outward. Weld seams, exposed bolts, large readable panels."),
    ("robot_b", "888deba4-d749-40f5-a158-224f20f643f6", RAW % "robot_b",
     "A worn industrial top-down battle mech. Desaturated gunmetal grey-blue body with one saturated "
     "accent colour on the shoulder plates. Its shoulders are flat and empty; its weapons are vertical "
     "launch tubes set into its back, seen from above as a grid of circles on the flat upper surface. "
     "Weld seams, exposed bolts, large readable panels."),
    ("fusion", "8ec7231b-e054-4a74-b252-ac158f5cd01b",
     "https://api.pixellab.ai/mcp/images/47cdb935-cde6-4194-9103-557bd41d1195/download",
     "A fused battle mech: two machines locked into one larger humanoid frame. One head, two broad "
     "shoulders, both arms ending in straight angular gun barrels reaching forward, a bank of vertical "
     "launch tubes across its back and shoulders seen from above as a grid of circles, a seam line and "
     "locking clamps down the centre of the torso. Gunmetal blue-grey steel with an orange accent on "
     "the shoulders and chest."),
]

# (폴더, 그룹 id, 방향들, frame_count, keep_first, 생성 수, 차수, 동작 묘사 전문)
GROUPS = [
    ("robot_a_Idle", "500682b1-74fe-4279-9f52-9f469e28eedb", ["south", "north", "east", "west"], 4, True, 16, "1차 · 통과",
     "idling in place with its weight settled, the whole machine rising and falling by about one "
     "twentieth of its own height, its exhaust vents puffing in a steady rhythm"),
    ("robot_a_Move", "5414e959-922e-4bab-87f3-65f52b87cbae", ["south", "north", "east", "west"], 6, False, 24, "**2차**",
     "a full walking cycle: the left leg swings forward and plants while the right leg pushes off "
     "behind, then the two swap over, so the legs are clearly in different positions in every frame. "
     "The arms swing in opposition to the legs, the left arm forward when the right leg is forward. "
     "The upper body sinks down on each footfall and holds there for a beat on the frame where the "
     "foot lands."),
    ("robot_a_Death", "fa3c68ee-61bb-46d0-bae5-47baba8ff945", ["south"], 8, True, 8, "1차",
     "power draining away, the machine sagging and settling down onto the ground, its joints buckling "
     "as it comes to a stop, no explosion"),
    ("robot_a_TagIn", "2a7f698f-de22-48bf-a7bf-00c82e396096", ["south"], 8, True, 8, "1차",
     "already in its firing stance as it arrives, the gun barrel levelled, the whole body shoved "
     "backwards by recoil and then settling forward again into a ready pose"),
    ("robot_b_Idle", "4e1691eb-5466-4bf1-bb26-e591402160ff", ["south", "north"], 4, True, 16, "1차",
     "idling in place with its weight settled, the whole machine rising and falling by about one "
     "twentieth of its own height, the hatches over its back launch tubes easing open and shut"),
    ("robot_b_Idle", "b9115ccf-27bb-4074-b6ea-9d138cfbe480", ["east"], 4, True, 4, "**2차**",
     "idling in place, the whole frame rising and falling as one rigid piece by about one twentieth of "
     "its own height. Its shoulders and back plating stay locked to the body and move only with it, "
     "every panel stays shut, and the highlights on its plating stay steady."),
    ("robot_b_Move", "45675e71-fd29-4941-a9cd-e618da4da796", ["south", "north", "east", "west"], 6, False, 24, "1차",
     "walking forward, legs crossing past each other, the heavy upper body dropping further with every "
     "footfall than a lighter frame would and holding for a beat on the frame where the foot lands"),
    ("robot_b_Death", "5a0d217a-13fc-47fb-be8c-e1e500a902e3", ["south"], 8, True, 8, "1차",
     "power draining away, the machine sagging and settling down onto the ground with the hatches over "
     "its back launch tubes left standing open, no explosion"),
    ("robot_b_TagIn", "7c7f0a1a-e0e4-4064-b963-54cdb105fe73", ["south"], 8, True, 8, "1차",
     "arriving with the hatches over its back launch tubes already thrown open, the frame pressed down "
     "towards the ground by the launch and then rising back into a ready pose"),
    ("fusion_Idle", "2192a5d3-a32f-4183-aaaa-12e1d4d77512", ["south", "north", "east"], 4, True, 12, "**2차**",
     "idling with its weight settled, the shoulders rising and falling by about one twentieth of the "
     "machine's own height. Its plating is matte and dry, and the air all around the machine stays "
     "completely clear and empty."),
    ("fusion_Move", "ab34a6cc-0910-42b6-8073-66f65ac5c77f", ["south", "north", "east"], 6, False, 18, "1차",
     "a heavy clanking walk, the whole machine compressing down on each landing and holding there for "
     "a beat, its joints lagging a half step behind the body"),
    ("fusion_Death", "838040c2-7d41-49d2-90b4-3a28109055c3", ["south"], 8, True, 8, "1차",
     "the seam down its torso splitting open first as the locking clamps release, the two frames "
     "starting to come apart from each other and then sagging down onto the ground together, "
     "no explosion"),
]

RETIRED = [
    ("robot_a_Move", "7d282139-0390-41c4-a1d9-788d4e3f08ac", "다리가 움직이지 않았다 — 사용자 판정 2026-09-07",
     "walking forward, legs crossing past each other, the upper body sinking down with every footfall "
     "and holding for a beat on the frame where the foot lands"),
    ("fusion_Idle", "5c5e37ad-655b-4170-a9ee-352aff6c3f43", "김이 연기로 보였다 — 사용자 판정 2026-09-07",
     "idling with its weight settled, the shoulders rising and falling by about one twentieth of the "
     "machine's own height, steam venting from the exhaust ports"),
]

FUSION_STILL_PROMPT = """Pixel art fused battle mech seen from a HIGH TOP-DOWN camera, looking down at the machine from above and slightly in front, alone on a fully transparent background,
reference image 1 is the approved player robot sprite - match its art style, shading and surface finish exactly, this is that same machine locked together with its partner frame into one larger unit

VIEWING ANGLE: we are above the machine. We see the TOP surfaces of its shoulders and head. Its front and its back read as different shapes. This is NOT a front view.

seen from above, SILHOUETTE from above: a clear HUMANOID frame, one head, two broad shoulders, two arms, two legs planted wide apart, heavier and wider than either source machine was on its own, the largest allied unit in the game

FUSION: this frame is symmetrical left to right. Both arms end in the straight angular gun barrels of the first machine, reaching forward. A bank of vertical launch tubes from the second machine sits across its back and shoulders, and because the camera is above we look down into their circular mouths as a grid of circles on the flat upper surface. A seam line and locking clamps run down the centre of the torso where the two frames meet.

BUILD: well maintained military hardware, panels that fit cleanly together, worn paint and scuffed edges from long service, thicker plating than a standard frame

COLOR: gunmetal blue-grey steel with a single ORANGE accent along the shoulders and chest

EDGES: no black outline anywhere, the silhouette meets the transparent background directly"""


def frames_of(folder):
    if not os.path.isdir(folder):
        return []
    return sorted(f for f in os.listdir(folder)
                  if f.startswith("frame_") and f.endswith(".png"))


def clip_md5(folder, files):
    """벌 전체의 md5 — 프레임을 이름 순으로 이어 붙여 한 번에 해싱한다."""
    h = hashlib.md5()
    for f in files:
        h.update(open(os.path.join(folder, f), "rb").read())
    return h.hexdigest()[:8]


def first_md5(folder, files):
    return hashlib.md5(open(os.path.join(folder, files[0]), "rb").read()).hexdigest()[:8]


def mtime(folder, files):
    ts = os.path.getmtime(os.path.join(folder, files[0]))
    return datetime.datetime.fromtimestamp(ts).strftime("%Y-%m-%d %H:%M")


def canvas(folder, files):
    b = open(os.path.join(folder, files[0]), "rb").read(24)
    return struct.unpack(">II", b[16:24])


out = []
A = out.append

A("# 아트 작업 기록 — 애니메이션 (2026-09-07)")
A("")
A("> **구현-아트 세션의 기록이다** (플랜 §20-1 소유 표 · HANDOFF §1-1). 이 세션은 Notion 과")
A("> `Docs/HANDOFF.md` 를 쓰지 않는다 — 대신 여기에 적고, 구현-문서·코드 세션이 이 표를 읽어")
A("> `260907_V01` 6장과 HANDOFF 로 옮긴다. **옮길 때 요약하지 않는다** (지침 §8).")
A("")
A("**md5 · 프레임 수 · 캔버스 · 수신 시각은 이 파일을 만들 때 스크립트가 실물에서 찍는다.**")
A("사람이 적는 줄은 빠지고 도구가 찍는 줄은 안 빠진다.")
A("이 파일을 만드는 것은 `Docs/art_log/make_art_log.py` 다.")
A("")
A("---")
A("")
A("## 1. 합체 256 전투 스틸 — 선행")
A("")
A("**그전까지 `robot_fusion_256.png` 는 512 승인본을 NEAREST 로 정확히 반으로 줄인 참조본이었다**")
A("(픽셀 차 0 · 커밋 `29290d8`). **256 전투 스틸은 생성된 적이 없었다.**")
A("`260906_W05` 5-1 의 「앵커로 걸 승인본이 어느 세대인지가 확실해야 한다」가 이 자리다.")
A("")
A("| 항목 | 값 |")
A("|---|---|")
A("| 도구 | `create_image_pro` |")
A("| 인자 | `width=256` · `height=256` · `no_background=true` · 후보 1 (170 초과 캔버스는 언제나 하나) |")
A("| 참조 | 로봇 A 승인본 · `usage`: `art style, shading and surface finish - this is the approved player robot sprite` |")
A("| 앵커 세대 확인 | 리포본 · 15-1 7-5 서명본 · raw URL 셋이 md5 `ecffb0dec30f` 로 같다 |")
A("| 1차 | job `f7a36257-9752-4811-adda-4acc57296f07` · md5 `d854f6c4` · **불합격(구현 자체 판정)** — 시점이 정면 쪽 · 검은 테두리 |")
A("| 2차 | job `47cdb935-cde6-4194-9103-557bd41d1195` · **사용자 승인 2026-09-07** |")
A("| 비용 | 20 + 20 = 40 생성 |")

if os.path.exists(STILL):
    b = open(STILL, "rb").read()
    w, h = struct.unpack(">II", b[16:24])
    ts = datetime.datetime.fromtimestamp(os.path.getmtime(STILL)).strftime("%Y-%m-%d %H:%M")
    A("| 파일 | `%s` · %d × %d · md5 `%s` · %s |" % (STILL, w, h, hashlib.md5(b).hexdigest()[:8], ts))

A("")
A("**1차에서 2차로 바꾼 것 둘.** 각각 문서가 미리 적어 둔 처방이다.")
A("")
A("1. `SILHOUETTE` 앞에 `seen from above,` 를 붙여 **시점 서술을 세 절로** 만들었다 — 15-3 6-1 이")
A("   「결과가 정면으로 나오면 세 번째 자리를 만든다」로 적어 둔 것이고, 근거는 규칙 2 「절이 많은 쪽이 이긴다」다")
A("2. `EDGES` 를 로봇 A 승인본 프롬프트(15-1 7-1)와 같은 문안으로 늘렸다 — 15-3 6-1 은 앞 절만 갖고 있어")
A("   규칙 9 「금지문은 오히려 불러온다」에 걸리는 자리였다")
A("")
A("⚠️ **시점은 2차에서도 정면 쪽이다.** 15-1 7-4 규칙 7 이 「각도를 강하게 밀면 강조색을 잃고 무기가")
A("팔에 묻힌다」로 적어 둔 맞바꿈으로 보이며, 로봇 A 승인본도 여덟 번 걸렸다. 사용자가 이 수준으로 승인했다.")
A("")
A("### 1-1. 2차 프롬프트 전문")
A("")
A("```")
for line in FUSION_STILL_PROMPT.split("\n"):
    A(line)
A("```")
A("")
A("---")
A("")
A("## 2. 8방향 회전 — 애니메이션의 선행")
A("")
A("`create_character` **`mode=\"v3\"`** 에 승인본을 `reference_image_url` 로 건다.")
A("스키마가 「rotates YOUR sprite into 8 directions」로 명시하며, **캐릭터에는 `create_8_direction_object` 를 쓰지 않는다.**")
A("`size` 는 넘기지 않았다 — 참조 이미지의 크기(256)를 그대로 쓴다.")
A("")
A("| 대상 | 캐릭터 id | 참조 | 인자 | 비용 |")
A("|---|---|---|---|---|")
for name, cid, ref, desc in CHARS:
    A("| `%s` | `%s` | `%s` | `view=\"high top-down\"` · `detail=\"high detail\"` · `outline=\"lineless\"` | 8 생성 |"
      % (name, cid, ref))
A("")
A("**남면 검증** — `260906_W05` 5-1 이 요구한 세대 확인이다. 회전 남면을 내려받아 승인본과 실루엣 겹침을 쟀다:")
A("**`a_origin` × `a_south` = 1.000** (상한 0.95). 도구는 `MBI.Editor.OverlapReport`, 보고서는")
A("`Docs/measure/overlap_260907_rotcheck_a.md` — 그 보고서는 구현-문서·코드 세션 소유 경로에 있다.")
A("")
A("### 2-1. 회전 묘사 전문 (대상마다)")
A("")
for name, cid, ref, desc in CHARS:
    A("**`%s`**" % name)
    A("")
    A("```")
    A(desc)
    A("```")
    A("")
A("---")
A("")
A("## 3. 사용자 판정과 재생성 (2026-09-07)")
A("")
A("GIF 로 움직여 본 뒤 사용자가 넷을 판정했다.")
A("")
A("| # | 판정 | 처리 |")
A("|---|---|---|")
A("| 1 | `robot_b_Idle` 동면·서면 — 반짝이는 부분과 어깨가 어색하다. **동면·서면 중 하나만 생성해 좌우 대칭으로 쓸 것** | 동면만 2차 재생성하고 **서면 폴더를 지웠다**. 코드가 동면을 뒤집어 쓴다 |")
A("| 2 | `fusion_Idle` 셋 — 광택이 연기로 보인다. 제거할 것 | 셋 다 2차 재생성. 아래 ⚠️ 참조 |")
A("| 3 | `robot_a_Idle` — **통과** | 손대지 않았다 |")
A("| 4 | `robot_a_Move` — 다리가 움직이지 않는다. 좌우 다리가 교차하고 팔이 함께 움직여야 한다 | 넷 다 2차 재생성 |")
A("")
A("⚠️ **연기는 도구가 지어낸 것이 아니라 내가 시킨 것이다.** 1차 동작 묘사에")
A("`steam venting from the exhaust ports` 가 들어 있었고, 그것은 **15-3 5-2 가 「배기구에서 김이 샌다」로**")
A("**규정한 것을 그대로 옮긴 결과**다. 2차에서 그 절을 빼고 「도장은 무광이고 기체 주변 공기는 완전히 비어")
A("있다」로 바꿨다. **문서와 사용자 판정이 어긋나는 자리이므로 설계가 15-3 5-2 를 고칠지 정해야 한다** —")
A("구현이 문서를 고치지 않았다(§10 · 값·규칙은 판정 요청).")
A("")
A("⚠️ **`robot_b` 서면을 지운 것은 벌 수 규격을 건드린다.** 15 §4 와 15-2 는 대기·이동을 **4방향**으로")
A("정해 두었고, 지금 로봇 B 대기는 **3방향 + 미러**다. 사용자 확정으로 그렇게 했다.")
A("")
A("**미러링은 로봇 B 에만 쓴다.** 15-2 가 로봇 B 를 「어깨는 평평하고 비어 있으며 무기는 등의 수직")
A("발사관」으로 좌우 대칭으로 규정하므로 뒤집어도 맞다. **로봇 A 는 뒤집으면 안 된다** — 15-1 3-2 가")
A("「마운트가 붙은 쪽 팔이 명확히 두껍다」로 비대칭을 규정해서, 뒤집으면 무기가 반대팔로 간다.")
A("")
A("**코드는 이미 된다.** `CombatEntityView.PlayState` 의 서면→동면 `flipX` 폴백이 로봇을 가리지 않는다 —")
A("서면 폴더가 없으면 동면을 뒤집어 쓴다. 확인만 했고 손대지 않았다(그 경로는 문서·코드 세션 소유).")
A("")
A("### 3-1. 대기는 되감기로 재생한다 — 2026-09-07 사용자 확정")
A("")
A("대기를 그냥 반복하면 **마지막 프레임에서 첫 프레임으로 튀는 자리가 어색하다**(사용자 판정).")
A("프레임을 **0·1·2·3·4·3·2·1** 로 되짚어 오면 그 자리가 없어진다.")
A("**양 끝은 겹치지 않는다** — 겹치면 그 프레임만 두 배로 머물러 다른 종류의 멈칫이 생긴다.")
A("")
A("⚠️ **이것은 재생 방식이지 프레임이 아니다.** 되감기를 파일로 구우면 대기가 5장에서 8장이 되어")
A("15「동작의 크기」의 「대기 5」 규격과 `UnitAnimWiringTests` 가 깨진다. **리포의 프레임은 5장 그대로 둔다.**")
A("`Docs/art_log/preview/` 의 대기 GIF 는 확정된 재생 방식을 그대로 보여 준다(8프레임).")
A("")
A("### 3-2. 문서·코드 세션에 넘기는 것 둘")
A("")
A("**하나 — `SpriteFrameAnimator` 에 되감기 재생을 넣어야 한다.** 지금은 `Play(clip, loop)` 가")
A("0→4 를 반복하고 4 다음에 0 으로 돌아간다. 대기는 0·1·2·3·4·3·2·1 순서로 돌아야 한다.")
A("프레임 파일은 그대로 두고 재생 순서만 바꾸는 일이다.")
A("")
A("**둘 — `UnitAnimWiringTests` 가 로봇 B 대기를 4방향으로 기대한다** (`fourWay`). 서면이 없어졌으므로")
A("`AllClipFolders_Exist` 가 「아직 생성 전인 벌 1/27」로 건너뛴다. **27벌이 아니라 26벌 + 미러 하나**로")
A("고쳐야 한다.")
A("")
A("둘 다 `Assets/_Project/Scripts/**` 라 이 세션 소유가 아니다 — 손대지 않았다.")
A("")
A("### 3-3. `robot_a_Move` 차수 이력 — 개정 §20-3 이 「차수마다 한 줄」을 요구한다")
A("")
A("| 차 | 방식 | 그룹 | 결과 |")
A("|---|---|---|---|")
A("| 1차 | `animate_character` v3 · 문안 | `7d282139` | **불합격** — 다리가 움직이지 않았다 (사용자) |")
A("| 2차 | v3 · 문안을 「좌우 다리 교차 + 팔 반대 스윙 + 프레임마다 위치가 다르다」로 강화 | `5414e959` | **불합격** — 두 다리가 같이 움직였다 (사용자) |")
A("| 3차 | **`template_animation_id=\"walking-6-frames\"`** — 방식을 바꿨다 | `88751c0f` | **진행 중** — 아래 참조 |")
A("")
A("**3차에서 방식을 바꾼 근거.** 개정 §20-3 이 「3차부터는 방식을 바꾼다(문안 → 템플릿 등)」로 정했고,")
A("도구 문서도 품질 단계를 **template → v3 → pro** 로 적어 두었다. 글로 두 번 실패했으므로 골격 쪽으로")
A("내려왔다 — 다리 교차가 묘사가 아니라 스켈레톤으로 강제되고, 프레임 수도 6 으로 규격과 맞으며")
A("비용은 방향당 1 생성이다.")
A("")
A("⚠️ **3차가 느리다** (2026-09-07 12~13시대 실측). 도구가 안내한 「2~4분」을 크게 넘겼다.")
A("")
A("| 경과 | 상태 |")
A("|---|---|")
A("| 20분 | 한 방향이 **실패**로 표시 · 나머지 셋 35~52% · ETA 900초 |")
A("| 24분 | 셋 40~67% · ETA 900초 (줄지 않음) |")
A("| 38분 | **실패로 보이던 방향이 완료로 바뀌었다** · 나머지 셋 84~87% · ETA 347~442초 |")
A("")
A("**「실패」는 최종이 아니었다** — 도구가 중간에 그렇게 표시했다가 완료로 돌아섰다.")
A("`list_jobs` 의 한 시점 상태를 결과로 읽지 않는다. 이것도 「루프의 정상 종료는 완료가 아니다」와 같은 종류다.")
A("")
A("**취소하지 않고 기다린다** — `download` 는 한 벌이라도 생성 중이면 잠겨 완료분만 먼저 꺼낼 수 없고,")
A("38분 시점에 85% 까지 와 있어 다시 뽑으면 그만큼을 버린다.")
A("")
A("**2차를 덮지 않는다.** 개정 §20-3 대로 3차가 나오면 `Docs/art_log/candidates/` 에 두고 2차와")
A("나란히 놓아 **사용자가 고른다.** 고른 뒤에야 `Art/Anim/robot_a_Move/` 에 들어간다.")
A("")
A("### 3-4. 물러난 동작 묘사 (기록)")
A("")
A("**지우지 않고 남긴다** — 왜 바꿨는지가 다음에 같은 함정을 피하게 한다.")
A("")
for folder, gid, why, action in RETIRED:
    A("**`%s`** · 그룹 `%s` · 물러난 이유: %s" % (folder, gid, why))
    A("")
    A("```")
    A(action)
    A("```")
    A("")
A("---")
A("")
A("## 4. 벌별 표")
A("")
A("**벌 md5 는 프레임을 이름 순으로 이어 붙여 한 번에 해싱한 값이다.** 첫 프레임 md5 만으로는 벌을 못 가른다 —")
A("`keep_first_frame=true` 인 벌은 `frame_000` 이 회전 참조본 그대로라 같은 대상의 대기·사망·태그가")
A("첫 프레임 md5 를 공유한다.")
A("")
A("| 대상 | 상태 | 방향 | 차수 | 도구 | `frame_count` | `keep_first_frame` | 프레임 | 캔버스 | 그룹 id | 수신 | 경로 | **벌 md5** | 첫 프레임 md5 |")
A("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")

total_clips = 0
total_frames = 0
missing = []
for folder, gid, dirs, fc, keep, cost, gen, action in GROUPS:
    robot, state = folder.rsplit("_", 1)
    for d in dirs:
        path = os.path.join(ROOT, folder, d)
        files = frames_of(path)
        if not files:
            missing.append("%s/%s" % (folder, d))
            continue
        w, h = canvas(path, files)
        total_clips += 1
        total_frames += len(files)
        A("| `%s` | %s | %s | %s | `animate_character` v3 | %d | %s | **%d** | %d×%d | `%s` | %s | `%s` | `%s` | `%s` |"
          % (robot, state, d, gen, fc, "true" if keep else "false", len(files), w, h,
             gid, mtime(path, files), path.replace("\\", "/"),
             clip_md5(path, files), first_md5(path, files)))

A("")
A("**%d벌 · 프레임 %d장.** 규격은 대기 5 · 이동 6 · 사망·태그 상한 9 이고 캔버스는 256 이다 (15「동작의 크기」)."
  % (total_clips, total_frames))
A("")
A("`robot_b_Idle/west` 는 **일부러 없다** — 동면을 코드가 뒤집어 쓴다 (3장).")
if missing:
    A("")
    A("⚠️ **빠진 벌: %s**" % ", ".join(missing))
A("")
A("### 4-1. 동작 묘사 전문 (그룹마다)")
A("")
A("한 그룹의 방향들은 **같은 동작 묘사**를 쓴다. 그래서 벌마다 옮겨 적지 않고 그룹마다 한 번 적는다 —")
A("**전문 그대로이며 줄이지 않았다.**")
A("")
for folder, gid, dirs, fc, keep, cost, gen, action in GROUPS:
    A("**`%s`** · 방향 %s · %s · `frame_count=%d` · `keep_first_frame=%s` · %d 생성 · 그룹 `%s`"
      % (folder, "·".join(dirs), gen.replace("**", ""), fc, "true" if keep else "false", cost, gid))
    A("")
    A("```")
    A(action)
    A("```")
    A("")
A("---")
A("")
A("## 5. 비용")
A("")
A("| 무엇 | 생성 |")
A("|---|---|")
A("| 합체 256 스틸 (1차 + 2차) | 40 |")
A("| 8방향 회전 3종 | 24 |")
first_pass = 16 + 24 + 8 + 8 + 16 + 24 + 8 + 8 + 12 + 18 + 8
A("| 애니메이션 1차 (27벌) | %d |" % first_pass)
A("| 애니메이션 2차 재생성 (`robot_a_Move` 4 · `fusion_Idle` 3 · `robot_b_Idle` 동면 1) | 40 |")
A("| **합계** | **%d** |" % (40 + 24 + first_pass + 40))
A("")
A("---")
A("")
A("## 6. 09-04 초안을 지웠다")
A("")
A("`robot_a_*` · `robot_b_*` · `fusion_*` 의 09-04 일괄 생성분(`96dafd5`)을 지우고 그 자리에 넣었다.")
A("캔버스가 96·128·136·140·152·160·168·236 으로 제각각이었고 09-05 동작 규격(진폭 4~6% · 착지마다")
A("상체가 내려앉음) 이전에 뽑은 것이라 **초안이지 승인본이 아니었다.** 프레임 수도 몬스터 규격(대기 7)을")
A("따르고 있었다.")
A("")
A("**덮어쓰지 않고 지운 뒤 넣었다** — 프레임 수가 7 에서 5 로 줄어 덮어쓰기만 하면 옛 `frame_005`·")
A("`frame_006` 이 남는다. 재생성분을 받아 넣는 `Docs/art_log/fetch_anim_frames.py` 도 같은 이유로")
A("**넣기 전에 대상 폴더를 비운다.**")
A("")
A("`boss_*` · `mob_armor_*` · `mob_cannon_*` · `mob_infantry_*` 는 **손대지 않았다** — 지시 범위 밖이다.")
A("")
A("---")
A("")
A("## 7. 보는 도구 둘")
A("")
A("| 무엇 | 자리 | 만드는 것 |")
A("|---|---|---|")
A("| 정지 시트 | `Assets/_Project/Art/Anim/_anim_sheet.png` | `Docs/art_log/make_anim_sheet.py` |")
A("| 움직이는 판정본 | `Docs/art_log/preview/*.gif` | `Docs/art_log/make_anim_gif.py` |")
A("| 재생성분 받기 | — | `Docs/art_log/fetch_anim_frames.py` |")
A("")
A("**정지 시트로는 대기 진폭이 안 잡힌다.** 256 캔버스 안에서 10픽셀쯤 오르내리는 것은 프레임을 나란히")
A("놓아도 눈이 못 따라간다 — 사용자가 「gif로 움직여봐야 확실하게 알 수 있겠다」고 했고 그것이 맞았다.")
A("GIF 로 보고 나서야 판정 넷이 나왔다.")
A("")
A("**대기 GIF 는 되감기(8프레임)로 낸다** — 3-1 의 확정을 그대로 보인다. 이동·사망·태그는 그냥 반복이다.")
A("")
A("**대기에는 빨간 가로 기준선을 긋는다.** `frame_000` 실루엣의 윗변 자리이며 프레임이 바뀌어도 움직이지")
A("않는다 — 어깨가 그 선에서 떨어졌다 붙었다 하는 폭이 진폭이다. **이동·사망·태그에는 긋지 않았다**")
A("(진폭 규격이 없다). GIF 번호는 시트의 줄 순서와 같다.")
A("")
A("⚠️ **재생 속도는 구현 가정이다** — 대기 6fps(167ms) · 이동·사망·태그 8fps(125ms).")
A("어느 기획 문서도 정한 적이 없고 `260907_V01` ❓2-2 로 올라가 있다. **설계가 값을 주면 GIF 도 다시 만든다.**")
A("")
A("⚠️ **시트와 GIF 는 눈으로 보는 것이고 숫자는 `MBI.Editor.AnimReport` 가 낸다.** 여기서 잰 값을 판정")
A("근거로 쓰지 않는다 (지침 §10 — 도구가 낸 숫자만 근거).")
A("")
A("> 사망은 게임에서 1회 재생 뒤 마지막 프레임에 멈춘다(`SpriteFrameAnimator`). GIF 반복은 보기 편하라고")
A("> 둔 것이며 재생 규칙이 바뀐 것이 아니다.")
A("")
A("---")
A("")
A("## 8. 이 세션이 쓰지 않은 것")
A("")
A("플랜 §20-1 소유 표 · HANDOFF §1-1 에 따라 아래는 **읽기만** 했다.")
A("")
A("- `Docs/HANDOFF.md` · `Docs/art_manifest.md` · `Docs/measure/**`")
A("- `Assets/_Project/Scripts/**`")
A("- Notion 전부")
A("")
A("**HANDOFF §1-1 에 소유 표가 들어왔다** — 구현-문서·코드 세션이 커밋 `036318b` 으로 적었다.")
A("이 세션이 착수할 때는 아직 없어서 소유 경로를 플랜 §20-1 에서 직접 읽어 따랐고, 대조해 보니")
A("§1-1 의 아트 쪽 경로 셋이 플랜과 같다.")
A("")
A("⚠️ **커밋 충돌이 한 번 났다.** 두 세션이 체크아웃 하나를 쓰는데 git 인덱스도 하나라, 이 세션이")
A("`git add` 해 둔 GIF 가 문서·코드 세션의 커밋 `0be3755` 에 딸려 들어갔다. 잃은 것은 없다(내용 온전 ·")
A("서로의 파일을 고치지 않았다). 이후 이 세션은 **`git commit -- <경로>` 로 인덱스를 우회**해 자기 파일만")
A("담는다. 플랜 §20-2 1번이 막으려던 자리이며, `git add` 만으로는 부족하다는 것이 실측으로 나왔다.")

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("wrote", OUT, "clips", total_clips, "frames", total_frames, "missing", missing)
