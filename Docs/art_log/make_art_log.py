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
    ("robot_a_Move", "074c016e-01c8-4ec5-940c-151a12a056b7", ["south"], 6, False, 4,
     "**4차 · 규칙 15 · v3 보간 · 5장 · 칸 목록 0 1 2 3 4 2** (통과)",
     "포즈 경로 — 승인본의 다리 구간만 `inpaint` 로 다시 그려 걷는 자세 두 장(`pose_L2` · 대칭축으로 되접은 "
     "`pose_R_mirror`)을 만들고, 그 둘을 `custom_start_frame_url`·`end_frame_url` 로 준 v3 보간이다. "
     "보간이 낸 그림은 **다섯 장**이고 재생은 **여섯 칸**이다 — 칸 목록 `0 1 2 3 4 2` 가 가운데 자세(발이 모이는 칸)를 "
     "돌아오는 길에 한 번 더 가리킨다. 사본 파일 `frame_005` 는 `342f9b1` 의 `cellOrder` 가 들어온 뒤 지웠다(`5f76bbe`) — "
     "**화면은 안 바뀐다.** 「이동 6」은 상한이라 5장이어도 규격 위반이 아니다(`260907_W03` 2-3). 보간에 준 동작 묘사: "
     "walking forward, the two legs swinging past each other so the leg that was forward goes back and the "
     "leg that was back comes forward, the body staying upright"),
    ("robot_a_Move", "bcc4aac1-466b-49c2-82e9-432b9ceb3da2", ["north"], 6, False, 4,
     "**4차 · 규칙 15 · v3 보간 · 5장 · 칸 목록 0 1 2 3 4 2** (통과)",
     "남면과 같은 경로다 — 북면 회전본의 다리 구간을 `inpaint` 한 `north_L` 과 그것을 x=127 축으로 되접은 "
     "`north_R` 을 보간의 두 끝으로 준다. 칸 목록도 같다(`0 1 2 3 4 2`) — 사본 `frame_005` 는 함께 지웠다. "
     "walking, the two legs swinging past each other so the leg that was forward goes back and the leg "
     "that was back comes forward, the body staying upright"),
    ("robot_a_Move", "2dce895b-6fb9-4b38-ac22-2421ede1012c", ["east"], 6, False, 6,
     "**5차 · v3 문안** (통과 · 되접기 판 반려 뒤)",
     "walking in profile: the near leg and the far leg swing past each other, one foot planted while the "
     "other swings through, AND BOTH ARMS SWING TOO - the arm on the same side as the forward leg swings "
     "back while the other arm swings forward, the gun arm clearly moving with each step, the body staying upright"),
    ("robot_a_Move", "2972bbe4-55b0-4979-bd1d-12b6e6d34bd2", ["west"], 6, False, 6,
     "**5차 · v3 문안** (통과 · 되접기 판 반려 뒤)",
     "동면과 같은 문안이며 **미러링을 쓰지 않고 서면을 따로 뽑았다** (사용자 확정 — 로봇 A 는 좌우 비대칭이다). "
     "walking in profile: the near leg and the far leg swing past each other, one foot planted while the "
     "other swings through, AND BOTH ARMS SWING TOO - the arm on the same side as the forward leg swings "
     "back while the other arm swings forward, the gun arm clearly moving with each step, the body staying upright"),
    ("robot_a_Death", "fa3c68ee-61bb-46d0-bae5-47baba8ff945", ["south"], 8, True, 8, "1차",
     "power draining away, the machine sagging and settling down onto the ground, its joints buckling "
     "as it comes to a stop, no explosion"),
    ("robot_a_TagIn", "9287a72c-a939-4eff-b5ed-0e51c9483abc", ["south"], 8, True, 8,
     "**2차 · `260907_W02` 2-4 재생성본** (통과)",
     "W02 2-4 표의 네 구간을 그대로 넣었다 — 첫 프레임부터 발사 자세 · 왼쪽으로 기움 · 오른쪽으로 되밀림 · 중립 수렴. "
     "the machine is ALREADY IN ITS FIRING STANCE in the very first frame, gun barrel levelled and firing, "
     "never neutral at the start; as it arrives it leans to the LEFT, the direction it is entering from the "
     "right; then the recoil shoves the whole body back to the RIGHT so the lean swings the other way; in the "
     "last frames it settles upright into a neutral ready pose"),
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
    ("robot_b_Death", "ec73fed0-dc3f-48f2-9f19-c29fe8f73faa", ["south"], 8, True, 8,
     "**3차 · v3 문안 · 통과** (세로 44px · 1차는 반려)",
     "1차(`5a0d217a`)가 「풍선이 바람 빠져 찌그러지는 느낌」으로 반려됐다 — 세로·가로가 함께 줄었다. "
     "3-14 참조. power draining away and the machine dropping: the knees BUCKLE OUTWARD so the stance gets "
     "WIDER as the body falls between them, the legs folding at the joints while every armour panel stays "
     "hard, flat and the same size, then it settles onto the ground. It bends and folds at its joints - it "
     "never shrinks, squashes or deflates. The hatches over its back launch tubes are left standing open. "
     "No explosion"),
    ("robot_b_TagIn", "7f473271-115e-4413-9493-6e266e57ba0b", ["south"], 8, True, 8,
     "**2차 · `260907_W02` 2-4 재생성본** (통과)",
     "A 와 같은 네 구간이되 반동이 아래로 눌린다(15-2 5장). "
     "the machine ARRIVES ALREADY LAUNCHING in the very first frame, its vertical launch tubes open and "
     "drones leaving them, never neutral at the start; as it arrives it leans to the LEFT, the direction it "
     "is entering from the right; then the recoil presses it DOWN and back to the RIGHT, the whole body "
     "squatting as the lean swings the other way; in the last frames it rises and settles upright into a "
     "neutral ready pose"),
    ("fusion_Idle", "693f3dcd-c368-4140-9edf-c61f52af19fc", ["south"], 4, True, 4,
     "**4차 · 끝 자세 보간** (통과)",
     "끝 자세를 그림으로 주었다 — 승인본을 세로 94%로 눌러 만든 `sink_target.png` 를 `end_frame_url` 로, "
     "승인본을 `custom_start_frame_url` 로 준 v3 보간이다. 다섯 칸이 아래로 가라앉고 **왕복 재생이 위로 되돌린다**. "
     "김 절은 3차에서 이미 빠졌다(`260907_W02` 2-1). "
     "idling in place, the machine settling down onto its knees a little and then holding, its shoulders and "
     "head dropping with the body, everything else steady"),
    ("fusion_Idle", "8f45c770-fb89-4dc9-9bc8-07d487e97f24", ["north"], 4, True, 4,
     "**4차 · 끝 자세 보간** (통과)",
     "남면과 같은 방식이며 목표는 북면 회전본을 눌러 만든 `sink_north.png` 다."),
    ("fusion_Idle", "823164a8-2f4f-4c2b-afc0-30581d57142a", ["east"], 4, True, 4,
     "**4차 · 끝 자세 보간** (통과)",
     "남면과 같은 방식이며 목표는 동면 회전본을 눌러 만든 `sink_east.png` 다."),
    ("fusion_Move", "a8a67ecf-54df-428a-8a10-e744aad58883", ["south"], 4, True, 4,
     "**3차 · 규칙 15 · v3 보간 · 5장 · 칸 목록 0 1 2 3 4 2** (합격 — 좌우 아랫변 차 −10 → +12)",
     "규칙 15(자세 두 장을 손으로 만들어 보간에 준다 · `260907_W03` 2-1 · 번호는 15)로 다시 걸었다. "
     "다리 구간 마스크 `x68 y150 122×102`(양팔 총열은 밖) · 되접기 축 **x=127.0 · 대칭도 98.9%** · "
     "자세는 `candidates/fusion_pose/stride2_{L,R}.png` · 승인본은 건드리지 않았다. "
     "사본 `frame_005` 는 `1e7bee7`(`CellOrder()` 에 합체 남·북 줄 · EditMode 666/662 GREEN) 뒤에 지웠다(`3d3e754`) — "
     "폴더는 다섯 장이고 여섯째 칸은 목록이 가운데 자세를 한 번 더 가리킨다. **화면은 안 바뀐다.** "
     "a heavy walk cycle: the forward leg swings back and the trailing leg swings forward past it, the feet "
     "trading places, the arms swinging with them, the torso upright and dropping slightly on each footfall"),
    ("fusion_Move", "bc9c684a-743b-43f1-8f07-77dc82c821cd", ["north"], 4, True, 4,
     "**3차 · 규칙 15 · v3 보간 · 5장 · 칸 목록 0 1 2 3 4 2** (합격 — 좌우 아랫변 차 −4 → +10)",
     "남면과 같은 방식이며 마스크는 `x72 y140 116×100` · 되접기 축 **x=127.5 · 대칭도 99.3%** · "
     "자세는 `candidates/fusion_pose/stride2_north_{L,R}.png` 다. 사본 `frame_005` 도 남면과 같이 `3d3e754` 에서 지웠다."),
    ("fusion_Move", "08eb1760-9197-4ecb-8c62-d26bb9cbd54c", ["east"], 6, False, 6,
     "**3차 · v3 문안 · 통과** (발 구간 가로폭 여닫힘 28px → 65px · 옆모습이라 좌우 아랫변 판정은 성립하지 않는다)",
     "walking forward with heavy clanking steps, the legs alternating so one foot stays planted while the "
     "other lifts and swings through, the whole frame pressing down on every footfall and the joints "
     "following a beat late, AND BOTH ARMS SWING with the steps, the gun barrels moving forward and back"),
    ("fusion_Death", "e6ed76fb-8e63-49ac-b325-029cda61bdd8", ["south"], 8, True, 8,
     "**4차 · 규칙 15(옮김·축소) · 통과** (세로 54px = 24.0% 승인본 분모 / 27.3% 벌 평균 분모)",
     "끝 자세를 **그리지 않고 승인본 `648b864e` 의 픽셀을 옮기고 줄여** 만들었다 — 새 획 0. "
     "다리(`y≥150` · `x68~190`)만 세로 0.45 배로 축소하고 발끝은 제자리에 두어 엉덩이가 52px 내려앉는다 · "
     "몸통·어깨(발사관 포함)와 총열 팔은 **옮기기만**(총열은 발밑을 안 뚫도록 2px) · 이음선은 축 `x=127` 에서 "
     "좌우를 ±3px 벌려 접합부가 풀린 것으로 읽힌다. 끝 자세는 `candidates/fusion_pose/collapse_target.png` "
     "(세로 225→173 · 가로 234→238)이며 그것을 `end_frame_url`, 승인본을 `custom_start_frame_url` 로 준 v3 보간이다. "
     "칸마다 계속 내려앉고(되올라오는 칸 없음) 끝에서 가로가 넓어진다. **물러난 차수 셋이 나란히 있다**(§22-3) — "
     "`candidates/fusion_Death_v2/`(2차 `fd9d157a` · 11px = 4.9% · `5d556c3` 에서 꺼냈다) · "
     "`candidates/fusion_Death_v3/`(문안 강화 · 21px) · `candidates/fusion_Death_pose/`(눌린 실루엣 보간 `260c66ff` · 109px · 폐기). "
     "the machine dies and drops: its knees fold under it so the hips sink toward the ground, the torso settles "
     "straight down, the seam down its chest pulls apart as the locking clamps let go, and the gun arms hang "
     "slack. Each frame sits lower than the one before. No explosion"),
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
A("### 3-2. 문서·코드 세션에 넘기는 것 셋")
A("")
A("**하나 — `SpriteFrameAnimator` 에 되감기 재생을 넣어야 한다.** 지금은 `Play(clip, loop)` 가")
A("0→4 를 반복하고 4 다음에 0 으로 돌아간다. 대기는 0·1·2·3·4·3·2·1 순서로 돌아야 한다.")
A("프레임 파일은 그대로 두고 재생 순서만 바꾸는 일이다.")
A("")
A("**둘 — `UnitAnimWiringTests` 가 로봇 B 대기를 4방향으로 기대한다** (`fourWay`). 서면이 없어졌으므로")
A("`AllClipFolders_Exist` 가 「아직 생성 전인 벌 1/27」로 건너뛴다. **27벌이 아니라 26벌 + 미러 하나**로")
A("고쳐야 한다.")
A("")
A("**셋 — `robot_a_TagIn` 재생 순서를 뒤집는 것을 제안한다.** 클립이 중립으로 시작해 발사 자세로")
A("끝나서 15-1 5-1 의 순서와 반대다(3-6 확인 2). 뒤집으면 발사로 시작해 중립으로 끝나 5-1 의")
A("「대기 자세로 수렴」과도 이어지고 **새로 뽑지 않아도 된다.** 다만 재생 순서는 코드다.")
A("`robot_b_TagIn` 은 가운데가 정점이라 뒤집어도 같다 — 그쪽은 손댈 것이 없다.")
A("")
A("셋 다 `Assets/_Project/Scripts/**` 라 이 세션 소유가 아니다 — 손대지 않았다.")
A("")
A("### 3-3. `robot_a_Move` 차수 이력 — 개정 §20-3 이 「차수마다 한 줄」을 요구한다")
A("")
A("| 차 | 방식 | 그룹 | 결과 |")
A("|---|---|---|---|")
A("| 1차 | `animate_character` v3 · 문안 | `7d282139` | **불합격** — 다리가 움직이지 않았다 (사용자) |")
A("| 2차 | v3 · 문안을 「좌우 다리 교차 + 팔 반대 스윙 + 프레임마다 위치가 다르다」로 강화 | `5414e959` | **불합격** — 두 다리가 같이 움직였다 (사용자) |")
A("| 3차 | **`template_animation_id=\"walking-6-frames\"`** — 방식을 바꿨다 | `88751c0f` | **폐기** — 로봇이 아니다. 아래 ⚠️ |")
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
A("⚠️ **3차 폐기 — 템플릿 모드는 참조 스프라이트를 지킨다는 보장이 없다.**")
A("네 방향 24프레임이 규격(6프레임 · 256)은 맞았으나 **그림이 로봇이 아니었다.**")
A("남면은 빈 캔버스에 주황색 갈매기 하나, 동면은 관절 점이 찍힌 회색 판 두 장 — **골격 리그로 보인다.**")
A("`Docs/art_log/candidates/robot_a_Move_v3/` 에 증거로 남긴다(지우지 않는다).")
A("")
A("**규칙으로 적어 둔다 — 캐릭터 애니메이션에 `template_animation_id` 를 쓰지 않는다.**")
A("도구 문서가 template 모드를 「skeleton from template_animation_id」로 적고 있다. 골격에서 새로")
A("그리는 방식이라 **`mode=\"v3\"` + 참조 이미지처럼 그 개체를 이어받지 않는다.**")
A("탑뷰 기체처럼 사람 골격과 안 맞는 대상에서는 결과가 통째로 다른 물건이 된다.")
A("15 §4 「애니메이션은 그 개체 자신의 승인본을 참조로 건다」가 지키려던 것이 이 자리다.")
A("")
A("**규격만 보고 통과시키면 못 잡는다.** 프레임 수도 캔버스도 맞았고 `UnitAnimWiringTests` 가 보는")
A("조건은 전부 충족한다. **눈으로 봐야 잡힌다** — 사용자가 GIF 를 보고 「로봇이 아닌데?」로 잡았다.")
A("")
A("**총 소요 47분 · 4 생성.** v3 문안 넷이 6분 · 24 생성이었다. **템플릿은 싸지만 느리고, 무엇보다")
A("결과가 대상을 안 지킨다** — 비용이 아니라 이 이유로 쓰지 않는다.")
A("")
A("**2차를 덮지 않았다.** 개정 §20-3 대로 3차는 `Docs/art_log/candidates/robot_a_Move_v3/` 에 두었고")
A("`Art/Anim/robot_a_Move/` 에는 2차가 그대로 있다. **사용자가 고른 뒤에야** 들어간다.")
A("")
A("나란히 움직이는 비교본: `Docs/art_log/preview/compare/<방향>.gif` (왼쪽 2차 · 오른쪽 3차 · 8fps).")
A("**그 비교본이 3차 폐기를 잡았다** — 정지로 봤으면 규격이 맞아 통과했을 것이다.")
A("만드는 것은 `Docs/art_log/make_compare_gif.py` 다 — 차수 비교는 앞으로도 반복되므로 도구로 두었다.")
A("")
A("**남은 것: 2차가 가장 낫지만 아직 불합격이다** — 「반대편 다리의 애니메이션이 없다」(사용자 2026-09-07).")
A("한쪽 다리만 움직인다. 4차의 방식은 아직 정하지 않았다 — 템플릿은 위 이유로 막혔고,")
A("`mode=\"pro\"` 는 256 에서 프레임이 4로 고정돼 「이동 6」 규격을 어기므로 설계 판정이 필요하다.")
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
A("### 3-5. 재생 속도 비교본 (사용자 요청 2026-09-07)")
A("")
A("`260907_V01` ❓2-2 — **프레임 재생 속도를 정한 기획 문서가 없다.** 지금 코드 값(대기 6fps ·")
A("나머지 8fps)은 구현 가정이다. 속도는 숫자로 못 고르고 움직이는 것을 봐야 고른다.")
A("")
A("`robot_a_Idle/south` 를 **5 · 6 · 7 · 9 · 12 · 15 · 24 fps** 일곱으로 냈다. `Docs/art_log/preview/fps/`.")
A("")
A("| fps | 프레임당 | 한 바퀴(되감기 8프레임) |")
A("|---|---|---|")
A("| 5 | 200ms | 1,600ms |")
A("| **6 (지금 코드)** | 167ms | 1,336ms |")
A("| 7 | 143ms | 1,144ms |")
A("| 9 | 111ms | 888ms |")
A("| 12 | 83ms | 664ms |")
A("| 15 | 67ms | 536ms |")
A("| 24 | 42ms | 336ms |")
A("")
A("느린 쪽 셋(5·6·7)은 사용자가 뒤이어 요청했다 — 빠른 쪽만으로는 지금 값이 느린 것인지")
A("빠른 것인지 가늠이 안 되기 때문이다. **지금 값을 가운데 두고 양쪽을 봐야 고를 수 있다.**")
A("")
A("**넷 다 되감기 적용본**이라 실제 재생 방식과 같다 — 속도만 다르고 나머지는 같게 두어야 비교가 된다.")
A("만드는 것은 `Docs/art_log/make_fps_sweep.py` 다.")
A("")
A("⚠️ **고른 속도는 코드에 들어가야 확정이다.** 자리는 `CombatAssetGenerator.DefaultFps` 이고")
A("`Assets/_Project/Scripts/**` 는 아트 세션 소유가 아니다 — 고른 값을 「→ 플랜」 문서로 올린다.")
A("")
A("### 3-6. W01 7장 확인 2·3·4 — 후보 (플랜 §23-3 B)")
A("")
A("**후보이고 판정이 아니다.** W01 4-3 이 「도구가 낸 프레임이므로 구현이 화면을 보고 골라")
A("회신문에 번호를 적는다」로 정했다. 재는 것은 `Docs/art_log/measure_poses.py` 다 —")
A("알파 문턱 16 초과를 「있다」로 보고 프레임마다 bbox 네 변과 채워진 픽셀의 가로 무게중심을")
A("캔버스 좌표 그대로 잰다. 자르거나 늘이지 않는다.")
A("")
A("#### 확인 2 — 태그 클립의 발사 프레임 번호")
A("")
A("| 대상 | 읽은 것 | 발사 후보 |")
A("|---|---|---|")
A("| `robot_a_TagIn/south` | 1칸에서 무기를 늘어뜨리고 서 있다가 칸이 갈수록 팔을 들어 올려 **9칸에서 겨눈 자세**가 된다 | **9** (끝) |")
A("| `robot_b_TagIn/south` | 등 발사관이 3~6칸에서 가장 들리고 9칸에 가라앉는다. 세로로 17px 눌렸다 돌아오며 **6칸이 가장 낮다** | **5~6** (가운데) |")
A("")
A("⚠️ **로봇 A 는 15-1 5-1 의 순서와 반대다.** 5-1 은 「진입 → **진입이 끝나기 전에 이미 발사 자세**")
A("→ 반동 → 대기 자세로 수렴」인데, 이 클립은 중립에 가까운 자세로 **시작해** 발사 자세로 **끝난다.**")
A("W01 확인 4 가 적어 둔 「발사 자세가 도착 뒤에 나오면 『쏟아부으며 들어온다』가 『들어와서 쏜다』가")
A("된다」가 지금 상태다 — 5-1 이 (나) 착륙형을 기각한 바로 그 이유다.")
A("")
A("**재생 순서를 뒤집으면 양끝이 다 맞는다** — 9(발사)로 시작해 1(중립)로 끝나면 5-1 의")
A("「종료: 대기 자세로 수렴」과도 이어진다. **새로 뽑지 않아도 된다.**")
A("재생 순서는 `SpriteFrameAnimator` 소관이라 문서·코드 세션 몫이다 — 3-2 에 함께 넘긴다.")
A("")
A("#### 확인 3 — 기울기 방향과 반동 방향")
A("")
A("가로 무게중심이 1칸 기준으로 얼마나 움직이는가 (+ 오른쪽 · − 왼쪽).")
A("")
A("| 대상 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 폭 |")
A("|---|---|---|---|---|---|---|---|---|---|---|")
A("| `robot_a_TagIn/south` | 0.0 | +0.6 | +1.3 | +2.6 | +4.9 | +4.3 | +4.3 | +4.9 | +8.4 | **8.4px** |")
A("| `robot_b_TagIn/south` | 0.0 | −0.0 | −0.1 | −0.1 | −1.2 | −1.2 | −1.2 | −0.6 | −0.0 | **1.2px** |")
A("")
A("⚠️ **어느 쪽도 5-1 이 요구한 쌍을 갖고 있지 않다.** 5-1 은 둘을 함께 적었다 —")
A("「이동 방향으로 약간 기움」(우→좌 진입이면 **왼쪽**)과 「반동으로 몸이 뒤로 밀림」(그러면 **오른쪽**).")
A("**기울었다가 되밀리는 왕복**이어야 하는데,")
A("")
A("- **로봇 A** 는 한 방향(오른쪽)으로 8.4px 흐르고 **되돌아오지 않는다.** 뒤집으면 왼쪽으로만 흐른다 —")
A("  어느 순서로 재생해도 쌍이 안 된다")
A("- **로봇 B** 는 좌우가 1.2px 로 **사실상 없다.** 대신 세로로 17px 눌렸다 돌아온다 — 기울기가 아니라 웅크림이다")
A("")
A("**설계 판정이 필요하다** — 기울기·반동을 스프라이트가 갖게 할지(재생성), 코드가 위치로 낼지.")
A("후자면 15-1 9장 A-2 가 발사 반동에 이미 쓰는 방식과 같아진다. 구현이 정하지 않았다.")
A("")
A("#### 확인 4 — 이동 벌의 착지 칸 후보")
A("")
A("몸이 가장 내려앉은 칸 둘. W01 4-5 가 이동에 `r` 넷을 열어 두었고 그 자리에 줄 후보다.")
A("")
A("| 벌 | 착지 후보 | 내려앉음 폭 | 믿을 수 있나 |")
A("|---|---|---|---|")
A("| `fusion_Move/north` | **4 · 5** | 8px | 예 |")
A("| `robot_b_Move/east` | **3 · 2** | 7px | 예 |")
A("| `robot_b_Move/north` | **1 · 5** | 5px | 예 |")
A("| `robot_b_Move/south` | **4 · 5** | 8px | 예 |")
A("| `robot_b_Move/west` | **2 · 3** | 5px | 예 |")
A("| `fusion_Move/east` | 1 · 2 | 0px | **아니다 — 잘림** |")
A("| `fusion_Move/south` | 4 · 5 | 7px | **아니다 — 잘림** |")
A("| `robot_a_Move/east` | 2 · 3 | 4px | **아니다 — 잘림** |")
A("| `robot_a_Move/north` | 4 · 6 | 12px | **아니다 — 잘림** |")
A("| `robot_a_Move/south` | 4 · 3 | 9px | **아니다 — 잘림** |")
A("| `robot_a_Move/west` | 3 · 1 | **1px** | **아니다 — 잘림** |")
A("")
A("⚠️ **잘린 벌은 세로 값을 믿지 않는다.** 실루엣이 캔버스 위나 아래에 닿으면 bbox 가 더 못 움직여")
A("「안 내려앉았다」로 나온다 — `AnimReport` 가 겪은 것과 같은 자리다. **로봇 A 이동 넷이 전부 잘렸다.**")
A("")
A("**다만 잘린 값이 사용자 판정과 같은 곳을 가리킨다.** `robot_a_Move/west` 는 내려앉음 폭이 **1px** 로")
A("사실상 없고, 나머지 셋도 4~12px 로 들쭉날쭉하다. 사용자가 「다리가 움직이지 않는다」·「반대편 다리의")
A("애니메이션이 없다」로 두 번 판정한 그 벌이다 — **도구가 같은 것을 숫자로 보인다.**")
A("")
A("### 3-7. 보류 — 프롬프트 전문을 아직 빼지 않는다 (플랜 §23-3 D)")
A("")
A("플랜 §23-3 D 는 4-1 「동작 묘사 전문」을 `Docs/art_prompts_260907.md` 포인터로 바꾸라 한다")
A("(W01 3-3 이 두 파일의 책임을 갈랐고 겹치는 열은 실물에서 찍는 쪽을 남긴다).")
A("")
A("⚠️ **지금 하면 프롬프트 전문이 어디에도 없게 된다.** `art_prompts_260907.md` 는 플랜 §22-2 3 으로")
A("**포인터화된 상태**이며(애니 절이 「이 파일이 갖지 않는다」로 비어 있다), W01 3-3 에 따른 복원은")
A("문서·코드 세션의 §23-2 5번 마지막 항목이고 **아직 안 들어왔다**(2026-09-07 확인).")
A("")
A("**복원이 리포에 들어온 것을 보고 뺀다.** 순서를 뒤집으면 09-06 에 프롬프트 문자열을 통째로 잃은")
A("그 사고를 모양만 바꿔 다시 하는 것이 된다(지침 §7).")
A("")
A("### 3-8. W01 6장 테스트 결과 — 사용자 판정 (2026-09-07)")
A("")
A("| 물음 | 답 |")
A("|---|---|")
A("| 대기 1.00초 대 1.50초 | **1.00초로 진행** |")
A("| 이동 1.00초가 걸음으로 읽히는가 | **기존과 차이점이 없음** — 불합격 |")
A("")
A("**대기 1.00초는 W01 4-5 가 이미 적어 둔 목표 초와 같다** — 값이 바뀐 것이 아니라 확인된 것이다.")
A("")
A("⚠️ **진폭 새 대역은 여기서 정하지 않는다.** W01 2-1 이 「6장 테스트를 보고 정하고 그때 「동작의")
A("크기」 표에 역기입한다」로 설계 몫으로 두었고, 플랜 §23-5 도 「진폭 새 대역을 구현이 정하지 않는다」다.")
A("**설계에 넘기는 재료 하나** — 사용자가 고른 그 벌(`robot_a_Idle/south`)은 실루엣이 캔버스 위아래에")
A("닿아 **`AnimReport` 가 진폭을 못 잰다.** 대역을 숫자로 세우려면 잴 수 있는 벌에서 세워야 한다.")
A("")
A("#### 이동이 걸음으로 안 읽히는 뿌리 — 동작 묘사가 아니라 여백이다")
A("")
A("네 번째로 문안을 고쳐 걸기 전에 재 봤다. **로봇 A 는 다리가 내려갈 자리가 없다.**")
A("")
A("| 대상 | 아래 여백 (최소~최대) | 바닥에 닿은 프레임 |")
A("|---|---|---|")
A("| `robot_a.png` 승인본 | **0** | — |")
A("| `robot_a_Move/east` | 0 ~ 0 | **6/6** |")
A("| `robot_a_Move/west` | 0 ~ 5 | 5/6 |")
A("| `robot_a_Move/south` | 0 ~ 22 | 1/6 |")
A("| `robot_a_Move/north` | 4 ~ 26 | 0/6 |")
A("| `robot_b.png` 승인본 | **12** | — |")
A("| `robot_b_Move` 네 방향 | 4 이상 | **0/6** |")
A("| `fusion_Move/east` | 0 ~ 0 | **6/6** |")
A("")
A("**불평이 없었던 로봇 B 는 여백이 있고, 두 번 불합격한 로봇 A 는 0이다.**")
A("걸음은 한 다리가 땅에서 떨어졌다 닿는 것인데, 발이 이미 캔버스 아래변에 붙어 있으면")
A("내려디딜 자리가 없다. 여섯 장 눈으로 본 것도 같다 — 몸이 위아래로 흔들릴 뿐 두 다리가")
A("나란히 붙은 채다.")
A("")
A("⚠️ **이것은 대기 진폭이 못 재지는 것과 같은 뿌리다.** `robot_a.png` 의 여백이 위 7 · 아래 0 이라")
A("4~6% 진폭도, 다리 교차도 캔버스에 들어갈 자리가 없다. 하나의 원인이 두 판정을 막고 있다.")
A("")
A("**그래서 4차를 걸지 않았다.** 문안을 또 고쳐도 같은 벽에 부딪힌다 — 1차·2차가 문안만 달랐고")
A("둘 다 여기서 막혔다. **여백을 만들지 않고는 방식을 바꿔도 안 된다**는 것이 이번 측정의 결론이다.")
A("길은 셋이며 전부 이 세션 밖이다 (아래 3-9).")
A("")
A("### 3-9. 이동 4차 — 길 셋 · 전부 이 세션 밖이다")
A("")
A("| 길 | 무엇 | 걸리는 것 |")
A("|---|---|---|")
A("| (가) 승인본을 여백 있게 다시 뽑는다 | 근본 해결. 진폭 문제도 같이 풀린다 | **앵커가 바뀐다** — 15-1 이 스타일 앵커라 15-2~15-5 와 27벌 전부가 영향. 가장 비싸다 |")
A("| (나) 승인본을 캔버스 안에서 위로 밀어 아래 여백을 만든다 | 새로 안 뽑는다. 스프라이트 내용은 그대로 | **수작업 보정 금지**(HANDOFF · 사용자 판정)에 걸린다. 피벗·정합도 흔들린다 |")
A("| (다) 로봇 A 이동은 지금 것으로 간다 | 비용 0 | 걸음으로 안 읽힌다는 판정이 남는다. 화면에서 50px 라 넘어갈 수도 있다 |")
A("")
A("**구현이 고르지 않는다.** (가)는 앵커 변경이라 설계 판정이고, (나)는 금지 조항 해제라 사용자 판정이며,")
A("(다)는 규격 미달을 받아들이는 것이라 역시 설계 판정이다.")
A("")
A("### 3-10. (가) 착수 — 승인본을 여백 있게 다시 뽑는다 (사용자 결정 2026-09-07)")
A("")
A("3-9 의 세 길 중 **(가)** 로 간다. 승인본을 여백 있게 다시 뽑아 진폭과 다리 교차를 함께 푼다.")
A("")
A("#### 범위 — 스틸 셋을 재고 갈랐다")
A("")
A("| 스틸 | 여백 (L R T B) | 실루엣 세로 | 다시 뽑나 |")
A("|---|---|---|---|")
A("| `robot_a.png` | 0 · 11 · **7 · 0** | **97.3%** | **그렇다 — 앵커라 먼저** |")
A("| `robot_fusion_256.png` | 6 · 6 · **3 · 0** | **98.8%** | **그렇다 — 로봇 A 보다 더 꽉 찼다** |")
A("| `robot_b.png` | 23 · 15 · 9 · 12 | 91.8% | **아니다 — 여백이 있다** |")
A("")
A("**로봇 B 가 대조군이다.** 여백 12 를 갖고 있고 이동에 불평이 없었으며 `AnimReport` 로 진폭도 잰다.")
A("**91.8% 가 되는 자리가 목표**이고, 그래서 프롬프트에 「세로의 6분의 5」를 넣었다 —")
A("4~6% 진폭(10~15px)과 다리를 들 자리가 위아래에 각각 20px 안팎 남는다.")
A("")
A("⚠️ **합체 스틸은 오늘 오전에 사용자 승인을 받은 것이다**(`b3ef28a8` · job `47cdb935`).")
A("다시 뽑으면 그 승인이 무효가 되고 재승인이 필요하다. **로봇 A 앵커가 먼저 확정된 뒤에 간다** —")
A("합체 스틸은 로봇 A 를 참조로 뽑으므로 순서를 뒤집으면 두 번 뽑게 된다.")
A("")
A("#### 순서")
A("")
A("| # | 할 것 | 비용(예상) |")
A("|---|---|---|")
A("| 1 | **로봇 A 스틸 재생성 → 사용자 승인** (앵커) | 20/차 |")
A("| 2 | 로봇 A 8방향 회전 + 남면 검증 | 8 |")
A("| 3 | 로봇 A 10벌 재생성 | 56 |")
A("| 4 | 합체 스틸 재생성(새 앵커 + 여백) → **재승인** | 20/차 |")
A("| 5 | 합체 회전 + 7벌 | 46 |")
A("| 6 | 로봇 B 는 그대로 두되 새 앵커와 화풍이 어긋나는지 대조 | 0 |")
A("")
A("**→ 2026-09-07 이 순서는 다 지나갔다.** 4번 합체 스틸 **재승인은 났고**(사용자 「합체 256 승인」)")
A("`Units/robot_fusion_256.png` 가 `648b864e` 로 교체됐다. 5번 회전(`4e01d2a6`)과 7벌도 끝났다 —")
A("결과와 그 뒤의 반려·재작업은 3-13 에 있다. **어디에도 「재승인 대기」로 남은 자리는 없다.**")
A("")
A("**1차 시도** — job `8c3c1b3c` · `create_image_pro` · 256 · 배경 제거 ON ·")
A("참조 = 기존 승인본 `ecffb0dec30f`(화풍을 잇기 위해).")
A("프롬프트는 15-1 7-1 승인 문안에 **`FRAMING` 절 하나만 더한 것**이다 —")
A("「세로의 6분의 5 를 차지하고 머리 위와 발 아래에 빈 띠를 남긴다」 · `SILHOUETTE` 절에도")
A("「두 발이 아래변보다 충분히 위에서 끝난다」를 붙였다.")
A("")
A("#### 결과 — 두 번 걸었고 **프롬프트로는 여백을 못 만든다**")
A("")
A("| 무엇 | 레버 | md5 | 여백 L R T B | 실루엣 | 세로 |")
A("|---|---|---|---|---|---|")
A("| 기존 승인본 | — | `ecffb0de` | 0 · 11 · 7 · 0 | 245×249 | 97.3% |")
A("| 1차 job `8c3c1b3c` | `reference_images` = 기존 승인본 + `FRAMING` 절 | `f44d7abd` | 0 · 11 · 7 · 0 | 245×249 | 97.3% |")
A("| 2차 job `4c585cc2` | **`style_image_url`** (팔레트·아웃라인·디테일·명암만) + 강화한 `FRAMING` | `7b9bcca6` | 0 · 11 · 7 · 0 | 245×249 | 97.3% |")
A("| **3차 job `a81f2930`** | **`reference_images` = 로봇 B 승인본** (`usage` = art style and surface finish) · 구 A 는 어느 슬롯에도 안 넣음 | `6f2b7329` | **25 · 11 · 34 · 12** | 220×210 | **82.0%** |")
A("| (대조) `robot_b.png` | — | `067ec6d1` | 23 · 15 · 9 · 12 | 218×235 | 91.8% |")
A("")
A("⚠️ **셋의 테두리가 픽셀 단위로 완전히 같다.** md5 는 셋 다 다르고, 2차는 눈으로 봐도 다른 기체다")
A("(총열에 붉은 강조가 붙고 어깨 판이 달라졌다). **그런데 L·R·T·B 가 하나도 안 움직였다.**")
A("")
A("**해석 — 도구가 결과를 캔버스에 맞춰 다시 앉힌다.** 배경 제거를 켜면 잘라내고 캔버스에 채우는")
A("것으로 보인다. 그래서 `FRAMING` 절도, 참조를 떼고 `style_image_url` 로 바꾼 것도 소용이 없었다.")
A("2차에서 레버를 바꾼 것은 규칙 11 「참조 이미지는 캔버스를 고정한다」를 피하려던 것인데,")
A("**참조가 아니라 도구의 후처리가 원인이었다.**")
A("")
A("⚠️ **「도구가 언제나 꽉 채운다」로 단정하지 않는다.** `robot_b.png` 는 91.8% 로 여백을 갖고 있다.")
A("다만 그것은 2026-08-25 생성분이라 어떤 도구·설정으로 뽑았는지 이 세션이 모른다.")
A("**지금 설정(`create_image_pro` · 256 · 배경 제거 ON)으로는 안 된다**까지가 실측이다.")
A("")
A("**40 생성을 썼고 세 번째 프롬프트는 걸지 않는다** — 두 번이 같은 벽에 부딪혔고 레버를 이미 바꿔 봤다.")
A("")
A("#### 3차 — **로봇 B 를 앵커로 걸자 발자국이 움직였다** (사용자 확정 2026-09-07 14:15 · 플랜 §24-1)")
A("")
A("사용자가 (가)를 승인하되 **앵커를 로봇 B 로** 바꿨다. 플랜 §24-1 이 이것을 실험으로 세웠다 —")
A("1·2차의 해석(「도구가 캔버스에 맞춰 다시 앉힌다」)이 맞으면 발자국이 또 `0 · 11 · 7 · 0` 으로 나오고,")
A("**다른 해석**(「발자국은 참조 이미지의 것을 물려받는다」)이 맞으면 B 쪽 값으로 온다.")
A("")
A("**둘째가 맞았다.** 여백이 `25 · 11 · 34 · 12` 로 왔고 세로가 97.3% 에서 **82.0%** 로 떨어졌다.")
A("1·2차가 안 움직인 것은 도구의 후처리가 아니라 **참조가 구 A 였기 때문**이다.")
A("규칙 11 「참조 이미지는 캔버스를 고정한다」가 여백까지 고정한다는 뜻이었다.")
A("")
A("**게이트 (플랜 §24-1 · 도구 판정)**")
A("")
A("| 게이트 | 상한·하한 | 실측 | |")
A("|---|---|---|---|")
A("| ① 아래 여백 | ≥ 10 | **12** | 통과 |")
A("| ① 위 여백 | ≥ 8 | **34** | 통과 |")
A("| ① 좌우 여백 | ≥ 8 | **11** (L25 · R11) | 통과 |")
A("| ② 실루엣 세로 | ≤ 93% | **82.0%** | 통과 |")
A("| ③ `robot_b` 와의 겹침 | ≤ 0.90 | **0.800** | 통과 |")
A("")
A("측정법 — 알파 문턱 16 초과를 「있다」로 보고 캔버스 네 변까지의 빈 픽셀을 세었다. 겹침은")
A("두 그림을 각자의 캔버스 그대로 겹쳐 알파가 함께 찬 넓이를 합친 넓이로 나눈 값이다")
A("(`Docs/measure/overlap_260907_rotcheck_a.md` · `MBI.Core.SilhouetteOverlap` 과 같은 식).")
A("도구는 `Docs/art_log/measure_still.py`.")
A("")
A("**겹침 셋을 나란히 둔다** — 새 A 가 B 를 얼마나 닮았는지가 B 앵커의 대가다.")
A("")
A("| 쌍 | 겹침 |")
A("|---|---|")
A("| 구 A × B | 0.683 |")
A("| **새 A × B** | **0.800** |")
A("| 새 A × 구 A | 0.645 |")
A("")
A("상한 0.90 은 넘지 않았지만 **구 A 보다 B 에 가까워졌다.** 참조를 B 로 걸었으니 예상된 방향이다.")
A("")
A("⚠️ **함께 딸려 온 것 둘 — 사용자 판정거리다.**")
A("")
A("| 무엇 | 실측·관찰 | 왜 판정거리인가 |")
A("|---|---|---|")
A("| 강조색이 **주황** | 구 A 는 적갈색, B 는 주황. 새 A 는 B 의 주황을 물려받았다 | 15-1 7-5 승인본 표가 「적갈색 강조색」으로 적혀 있다 — 앵커가 바뀌면 그 줄도 바뀐다 (§24-1 귀결 a) |")
A("| 로봇이 **약 16% 작아진다** | 실루엣 세로 249 → 210 | 목표는 B 수준(235 · 91.8%)이었는데 82.0% 로 더 작다. 15-1 1장 「캔버스 256 = 화면 폭 17.8%」는 캔버스 값이라 안 바뀌지만, 화면에서 보이는 크기는 바뀐다 |")
A("")
A("**여백은 목표를 넘겼다.** 위 34 · 아래 12 면 진폭 4~6%(실루엣 210 기준 8~13px)와 다리 들 자리가 함께 선다.")
A("다만 위로 치우쳐 있다 — 대기 진폭은 위로 뜨는 쪽이라 아래 12 가 실제 제약이 된다.")
A("")
A("**후보는 `Docs/art_log/candidates/robot_a_still_v3/` 에 있다** — `try3_b_anchor.png` (`6f2b7329`) ·")
A("구 A · 새 A · B 를 나란히 2배로 놓은 `_compare.png`. **사용자 육안 판정(게이트 ④) 전에는 설치하지 않는다.**")
A("")
A("#### ✅ 사용자 승인 — 2026-09-07 (게이트 ④ 통과)")
A("")
A("「로봇 A 수정본 승인 하겠음.」 **`6f2b7329` 가 로봇 A 의 새 승인본이다.**")
A("이 그림이 이후 회전 · 10벌 · 합체 스틸의 앵커가 된다 (15-1 3-1 「앵커가 되는 순서」).")
A("")
A("⚠️ **`Assets/_Project/Art/Units/robot_a.png` 는 이 세션 소유가 아니다** (플랜 §20-1 — 아트가 쓰는 Units 경로는")
A("`robot_fusion_256.png` 하나뿐이다). 승인본 파일 교체와 `Robot_A.asset` 주입은 **문서·코드 세션 몫**이라")
A("`Docs/art_log/260907_art_to_plan.md` 로 올린다. 이 세션은 후보 경로의 파일을 참조로 써서 회전·애니를 뽑는다.")
A("")
A("#### 회전 → 10벌 재생성 (같은 날 · 새 앵커로)")
A("")
A("| 단계 | 도구 | id | 결과 |")
A("|---|---|---|---|")
A("| 8방향 회전 | `create_character` `mode=v3` · 256 · `view=high top-down` · `reference_image_url` = 승인본 raw URL | `153eb088` | **남면 겹침 1.000** — 회전이 승인본을 그대로 지켰다 |")
A("| 대기 4방향 | `animate_character` `mode=v3` · `frame_count=4` · `keep_first=true` | 그룹 `cd9509f2` | 5프레임 × 4 |")
A("| 이동 4방향 | 같은 도구 · `frame_count=6` · `keep_first=false` | 그룹 `2c21fdc3` | 6프레임 × 4 |")
A("| 사망 남면 | 같은 도구 · `frame_count=8` · `keep_first=true` | 그룹 `ef9091b2` | 9프레임 |")
A("| 태그 남면 | 같은 도구 · `frame_count=8` · `keep_first=true` | 그룹 `2e354fdf` | 9프레임 |")
A("")
A("**2차 10벌(62프레임)은 `Docs/art_log/candidates/robot_a_anim_v2/` 에 그대로 두었다** — 덮지 않는다(§22-3).")
A("회전 남면·동면·북면·서면은 `candidates/robot_a_rot_v2/` 에 있다.")
A("")
A("##### 실측 — **이제 전 벌을 잴 수 있다**")
A("")
A("구 A 는 모든 벌이 캔버스 위·아래에 닿아 `AnimReport` 가 진폭을 0.0% 로 냈다(잴 수 없다는 뜻이었다).")
A("새 앵커의 10벌은 **한 프레임도 잘리지 않았다.**")
A("")
A("| 벌 | 방향 | 윗변 진폭 | 실루엣 대비 | 아랫변 변동 | 여백 위·아래 |")
A("|---|---|---|---|---|---|")
A("| 대기 | 남 | 18 px | **8.2%** | 1 px | 16 · 11 |")
A("| 대기 | 북 | 9 px | **4.3%** | 1 px | 15 · 26 |")
A("| 대기 | 서 | 10 px | **4.2%** | 0 px | 6 · 8 |")
A("| 대기 | 동 | 6 px | **2.6%** | 18 px | 6 · 8 |")
A("| 이동 | 남 | 18 px | 8.6% | **27 px** | 14 · 13 |")
A("| 이동 | 북 | 10 px | 4.8% | **28 px** | 13 · 16 |")
A("| 이동 | 동 | 6 px | 2.6% | 13 px | 7 · 7 |")
A("| 이동 | 서 | 5 px | 2.2% | 16 px | 7 · 8 |")
A("| 태그 | 남 | 19 px | 8.6% | 0 px | 15 · 12 |")
A("| 사망 | 남 | 48 px | 26.0% | 1 px | 34 · 11 |")
A("")
A("측정법 — 알파 문턱 16 초과를 「있다」로 보고 프레임마다 윗변·아랫변을 캔버스 좌표로 재어")
A("벌 안의 최대−최소를 진폭으로 삼았다. 실루엣 대비는 벌 평균 세로로 나눈 값이다.")
A("")
A("**이동의 아랫변이 움직인다** — 남면 27 px · 북면 28 px. 구 A 는 아랫변이 캔버스 끝(255)에")
A("붙박여 있어 다리를 들 자리가 없었다. 이것이 4-4 에서 뿌리로 지목한 자리이며 **여백이 그것을 풀었다.**")
A("")
A("⚠️ **진폭이 방향마다 다르다** — 대기가 남 8.2% · 북 4.3% · 서 4.2% · 동 2.6%.")
A("규격 4~6% 안에 드는 것은 북·서 둘뿐이다. **이제 잴 수 있으므로** ❓2-6 진폭 대역 판정의 재료가 생겼다 —")
A("규격을 방향별로 둘지, 대역을 넓힐지는 설계 판정이다. 구현이 정하지 않는다.")
A("")
A("#### 3-11. 이동 — **포즈를 그려 넣어 다리를 교차시켰다** (사용자 확정 2026-09-07 「이 걸음으로 간다」)")
A("")
A("##### 먼저 — 앞선 보고 하나를 정정한다")
A("")
A("「이동 아랫변이 27px 움직인다 = 다리를 든다」로 적었던 것은 **좌우를 갈라 재지 않은 값**이었다.")
A("갈라 재니 두 다리 아랫변이 함께 241→235→224→215→225→242 로 움직였다(좌우 차 최대 6px).")
A("걸음이 아니라 **몸통 전체가 쪼그렸다 펴지는 것**이었다. 사용자 지적이 맞았다.")
A("")
A("##### 왜 문안으로는 안 되는가")
A("")
A("`animate_character` v3 에는 **골격이 없다.** 스프라이트 한 장을 받아 실루엣 전체를 변형할 뿐")
A("어느 픽셀이 왼다리인지 모른다. 그래서 「왼다리가 들리는 동안 오른다리는 붙어 있다」로 못 박아도")
A("반영될 자리가 없다. **문안 네 번이 모두 상하 진동을 냈다.**")
A("")
A("**템플릿(골격) 모드는 정체성을 잃는다** — 09-07 3차에서 이미 봤고, 새 앵커로 다시 걸어 확인했다")
A("(`bb286e23` · 진단용 1 생성). 이번엔 다리조차 안 움직였다 — 좌우 차가 6칸 중 4칸에서 정확히 0.")
A("**골격을 얻으면 정체성을 잃고, 정체성을 지키면 골격이 없다**가 이 도구의 실제 맞바꿈이다.")
A("")
A("##### 방식 — 포즈를 말로 시키지 않고 그림으로 준다")
A("")
A("| 단계 | 무엇 | 비용 |")
A("|---|---|---|")
A("| 1 | 승인본·회전본의 **다리 구간만** `inpaint_image` 로 다시 그린다 (마스크 밖은 얼어 있다) | 20/장 |")
A("| 2 | 반대쪽 포즈는 **다리 구간의 대칭축으로 되접어** 만든다 (남·북) | 0 |")
A("| 3 | 두 포즈를 `custom_start_frame_url` · `end_frame_url` 로 주고 v3 보간 | 4/방향 |")
A("")
A("**되접기의 근거는 실측이다** — 다리 구간이 남면 x=127 축에서 **98.7%**, 북면 x=127 에서 **98.0%** 좌우 대칭이다.")
A("비대칭인 총열 팔(15-1 3-2)은 마스크 밖이라 건드리지 않는다. 되접기는 공짜이고 정확한데,")
A("오른발 앞 포즈를 도구로 뽑은 두 번은 모두 깨졌다(주황 덩어리 · 조각난 다리).")
A("")
A("##### 실측 — 좌우 아랫변의 차 (부호가 뒤집히면 교차한 것이다)")
A("")
A("| 방향 | 칸별 좌우 차 | 판정 |")
A("|---|---|---|")
A("| 남 | −29 · −13 · +10 · +25 · +29 · +10 | **교차** |")
A("| 북 | −20 · −11 · +2 · +22 · +20 · +2 | **교차** |")
A("| (구 벌 · 남) | 0 · −1 · −2 · −6 · −1 · 0 | 교차 없음 |")
A("")
A("**옆모습(동·서)은 다른 잣대로 잰다** — 두 다리가 겹쳐 좌우로 못 가른다. 발 구간의 가로폭으로 재면")
A("동 92→53→92 (39px 여닫힘) · 서 98→37→94 (61px). 다리가 벌어졌다 모인다.")
A("")
A("⚠️ **옆모습은 한쪽만 앞으로 나간다.** 동·서는 「앞뒤가 바뀐 반대 포즈」를 두 번 뽑아 두 번 다 깨져서")
A("(다리가 얇은 칼날로 변함) **선 자세 ↔ 벌린 자세** 보간으로 갔다. 12.2mm 에서 어느 다리가 앞인지는")
A("거의 안 읽히지만, 남·북과 달리 발이 서로 바뀌지는 않는다 — **아는 한계로 적어 둔다.**")
A("")
A("⚠️ **서면 마스크는 정강이·발만 덮는다.** 서면은 총열이 허벅지 위를 가로질러(y165~200) 있어")
A("그 위를 다시 그리면 총을 잃는다. 그래서 y202 아래만 열었다.")
A("")
A("##### 규격과 어긋나는 자리 — **6 그림**")
A("")
A("포즈 방식은 반 바퀴 5장을 낸다. 자연스러운 한 바퀴는 **왕복 8칸**(대기와 같은 꼴)인데 규격은 이동 6 그림이다.")
A("코드를 안 건드리고 오늘 서는 쪽으로 **`f0·f1·f2·f3·f4·f2` 여섯 칸**으로 설치했다 — 가운데(발이 모이는 칸)를")
A("돌아오는 길에 한 번 더 쓴다. 원본 5장은 `Docs/art_log/candidates/robot_a_Move_pose_all/` 에 있다.")
A("")
A("**더 나은 길은 왕복 재생이다** — `PINGPONG_STATES` 에 `Move` 를 더하면 5장이 8칸으로 정확히 돈다.")
A("`Assets/_Project/Scripts/**` 는 이 세션 소유가 아니라 「→ 플랜」 문서로 올린다.")
A("")
A("##### 동·서 다시 — 사용자 판정 (2026-09-07)")
A("")
A("「남·북은 이상 없음 · 동은 라이플 손이 안 보이고 라이플 없는 손이 이상하다 · 서는 발목이 꺾인다 ·")
A("**서면을 고쳐 좌우 대칭으로 쓸 것**」")
A("")
A("동면의 흠은 내가 만든 것이 아니라 **회전본 자체의 것**이다(총열이 몸에 가려 손이 안 읽힌다).")
A("그래서 서면 하나만 제대로 만들면 두 방향이 함께 풀린다.")
A("")
A("**생성으로는 다섯 번 다 깨졌다** — 마스크를 넓혀도(x72 w92) 좁혀도(발만 x100 w100 h46), 문안에")
A("「발목은 곧은 관절 · 굽지 않는다」를 못 박아도 다리가 얇은 칼날이 되거나 발이 사라졌고, 마지막에는")
A("투명 배경 자리에 흙바닥을 그려 넣었다(「empty ground」를 글자 그대로 받았다).")
A("")
A("**그래서 그리지 않고 옮겼다.**")
A("")
A("| | |")
A("|---|---|")
A("| 방식 | 회전본에서 **y202 아래(다리 전부)**를 떼어 두 벌 복제하고, 한 벌은 앞으로 한 벌은 뒤로 민다. 뒤쪽 벌은 66~70%로 어둡게 해 뒤에 깔고 앞쪽 벌을 위에 얹는다 |")
A("| 이동량 | 최대 ±16px. y202 에서 0, y228 에서 최대가 되도록 늘려 **엉덩이에서 떨어지지 않게** 한다. 발(y228 아래)은 통째로 평행 이동한다 |")
A("| 왜 발목이 안 꺾이나 | **발과 발목이 앵커의 픽셀 그대로**이기 때문이다. 새로 그린 획이 하나도 없다 |")
A("| 비용 | 0 생성 |")
A("| y202 인 이유 | 총열이 허벅지 위 y165~200 을 가로지른다. 그 아래만 만진다 |")
A("")
A("**동면은 서면의 좌우 대칭이다** (사용자 확정). 실측 — 발 구간 가로폭이 두 방향 모두")
A("86 · 70 · 54 · 70 · 86 · 54 로 벌어졌다 모인다. 캔버스 아래변에 닿는 프레임은 없다.")
A("")
A("⚠️ **되접으면 총열이 반대 팔로 간다.** 15-1 3-2 는 「마운트가 붙은 쪽 팔이 명확히 두껍다」로")
A("로봇 A 를 좌우 비대칭으로 규정한다 — 동·서를 대칭으로 쓰면 두 방향에서 총이 다른 팔에 보인다.")
A("**사용자가 그 대가를 알고 고른 것**이며(동면 회전본은 총열이 아예 안 읽혔다), 기록으로 남긴다.")
A("")
A("##### 그 되접기 판은 **반려됐다** (2026-09-07)")
A("")
A("「팔이 아예 안 움직임 · 미러링이 안 되는가? 좌우 대칭이 아니라면 둘 다 뽑아라(좌측면 우측면)」")
A("")
A("맞는 지적이다 — 픽셀 옮기기는 **다리만** 옮긴 것이라 팔이 움직일 수 없다. 방식의 한계였다.")
A("동·서를 **각각 따로** `animate_character` v3 로 다시 뽑았고 미러링은 쓰지 않았다.")
A("동작 묘사에 「두 팔도 함께 흔들리고 총 든 팔이 걸음마다 움직인다」를 넣었다.")
A("`robot_a_Move_arms_east` · `robot_a_Move_arms_west` · 각 6 생성 · `keep_first_frame=false` 라 6칸 그대로 규격에 맞는다.")
A("**사용자 통과 — 2026-09-07** (「태그와 무브 모두 통과」).")
A("")
A("---")
A("")
A("### 3-12. 포즈 경로 기록 (플랜 §26-3 C)")
A("")
A("| | |")
A("|---|---|")
A("| 무엇 | 승인본·회전본의 **다리 구간만** `inpaint_image` 로 다시 그려 걷는 자세 두 장을 만들고, 그것을 `animate_character` v3 의 시작·끝 프레임으로 주어 사이를 보간했다. 반대쪽 자세는 다리 구간의 대칭축으로 **되접어** 만들었다 |")
A("| 왜 | 이동 벌이 네 번(문안 셋 · 템플릿 하나) 모두 다리 교차를 못 냈다. v3 에 골격이 없어 「어느 픽셀이 왼다리인가」를 모르고, 골격이 있는 템플릿 모드는 로봇 형태를 잃는다. **자세를 말로 시키는 대신 그림으로 준 것**이다 |")
A("| 어느 규칙에 걸리는가 | 생성 결과에 **사람 손이 들어간 자리**가 둘이다 — ① `inpaint` 로 다리를 다시 그린 것 ② 픽셀을 되접은 것. 15 규칙 13(승인본을 직접 편집)은 **「같아야 할 두 그림」(상태 교체)에만** 열어 둔 문이고, 애니메이션 프레임을 만드는 공정으로는 문서 어디에도 없다 |")
A("| 지금 어디에 쓰였나 | **이동 남·북 두 벌**(`robot_a_Move` south · north). 동·서는 이 경로가 아니라 v3 재생성분이다 |")
A("")
A("⚠️ **플랜 §26-3 C 의 전제가 실제와 어긋난다.** C 는 「판정 전에는 벌 생성에 쓰지 마라」로 적었으나,")
A("**그 지시가 닿기 전에 이미 벌이 만들어졌고 사용자 판정까지 끝났다** — 남·북은 「이상 없음」(2026-09-07),")
A("이어서 「태그와 무브 모두 통과」. 되돌리려면 남·북을 문안 판으로 되돌려야 하는데 그것은 사용자가")
A("이미 물린 제자리 굽힘이다. **그래서 되돌리지 않고 공정 허용 여부를 판정으로 올린다.**")
A("")
A("**판정이 필요한 것 하나** — 「생성 결과의 일부를 손으로 고쳐 다음 생성의 입력으로 쓰는 공정」을")
A("규격에 넣을 것인가, 이번 한 번의 예외로 둘 것인가. 넣는다면 15 규칙 13 옆이 자리로 보인다.")
A("")
A("---")
A("")
A("### 3-13. 합체 — ⓑ(끝 자세 보간) 결과 · **대기 통과 · 사망 폐기 · 이동 미달** (2026-09-07)")
A("")
A("사용자가 ⓑ를 골랐고, 리포를 다시 공개로 돌려 주어 목표 그림을 URL 로 넘길 수 있었다(비공개면 이 길이 막힌다).")
A("")
A("**방법** — 끝 자세를 도구에 그리게 하지 않고 **실루엣만 만들어 목표로 준다.**")
A("승인본·회전본을 세로로 눌러 만든 그림을 `end_frame_url` 로, 원본을 `custom_start_frame_url` 로 주고 v3 가 사이를 그린다.")
A("`create_image_pro` 로 「쓰러진 기체」를 그리게 한 시도(`bcafdb68`)는 **실패**했다 — 세로 78.1% 로 여전히 서 있었다.")
A("")
A("| 벌 | 결과 | 세로 변화 | 판정 |")
A("|---|---|---|---|")
A("| 대기 남·북·동 | 다섯 칸이 가라앉고 왕복 재생이 되돌린다 | 14 · 13 · 15px (2차는 10 · 6 · 6) | **사용자 통과** |")
A("| 사망 남 | 여덟 칸이 무너진다 (목표 칸은 버려 화면에 안 나간다) | **109px** (2차 11 · 로봇 A 48) | **사용자 폐기** |")
A("| 이동 남·북·동 | 걷는 자세 보간이 다리를 못 벌렸다 | 20 · 24 · 5px (2차 유지) | 미달 · 미해결 |")
A("")
A("**사망은 되돌렸다** — 설치본은 2차(`fd9d157a` · 9칸 · 세로 11px)이고 **여전히 안 무너진다.**")
A("폐기된 보간판은 `Docs/art_log/candidates/fusion_Death_pose/` 에 남긴다. **왜 폐기됐는지는 사용자 판단이라 적지 않는다** —")
A("다만 실측으로 말할 수 있는 것은 목표 실루엣이 **세로를 48%로 누른 것**이라 중간 칸들이 「무릎이 꺾인다」보다")
A("**「납작해진다」에 가깝게 나왔다**는 것이다. 다음 시도는 목표를 누르는 대신 **주저앉은 자세를 그림으로 그려** 주어야 한다.")
A("")
A("**이동이 안 된 이유도 실측으로 남긴다** — 합체의 걷는 자세(`stride_L`·`stride_R`)가 좌우 아랫변 차 −1 / +1 이다.")
A("같은 방법에서 로봇 A 는 −29 / +29 였다. **무릎만 굽고 발이 제자리라** 보간해도 다리가 안 갈린다.")
A("")
A("⚠️ **다음 시도는 걸지 않는다** — 합체 사망·이동의 다음 차수는 `260907_V03` ❓3-1(포즈 공정 등재) 답이 온 뒤다.")
A("사용자가 직접 지시하면 그때 위 후보 둘(사망 = 주저앉은 자세 그림 · 이동 = 다리 `inpaint`)로 간다.")
A("")
A("---")
A("")
A("### 3-14. 로봇 B 사망 반려와 판정 재료 (2026-09-07 · 커밋 `700d2e8` · `9160b2f`)")
A("")
A("#### 반려 — 「풍선이 바람 빠져 찌그러지는 느낌」")
A("")
A("사용자가 로봇 A 사망을 통과시키고 **로봇 B 사망을 반려**했다. 「로봇 A와 같이 가는 것이 best」.")
A("")
A("**말로 온 인상을 숫자로 갈랐다.** 두 벌은 세로가 줄어드는 정도가 아니라 **가로가 어떻게 움직이는가**가 다르다.")
A("")
A("| 벌 | 세로 변화 | 칸별 가로 |")
A("|---|---|---|")
A("| 로봇 B 1차 (반려) | 35px | 218 → **203** → 211 — 계속 줄어든다 |")
A("| 로봇 B 2차 | 18px | 218 → 227 → 214 — 덜 무너진다 |")
A("| **로봇 B 3차** | **44px** | 218 → **227 … 228** — 넓어지고 유지된다 |")
A("| 로봇 A (통과본) | 48px | 220 → **230** → 197 |")
A("")
A("**로봇 A 는 떨어지면서 폭이 먼저 넓어진다** — 무릎이 바깥으로 꺾이고 몸이 그 사이로 내려앉기 때문이다.")
A("1차는 세로·가로가 함께 줄기만 해서 「부피가 빠지는」 것으로 읽혔다. 그것이 반려 사유의 실측 대응물이다.")
A("")
A("#### 방법 — **규칙 15 가 아니다. `animate_character` v3 문안이다**")
A("")
A("2차·3차 모두 **마스크도 되접기도 쓰지 않았다** — 캐릭터 `888deba4` 에 동작 묘사만 바꿔 걸었다.")
A("손댄 자리가 없으므로 규칙 15 의 조건 넷(대칭도 · 마스크 · 재현 · 승인본 불변)이 걸리지 않는다.")
A("")
A("| 차수 | 그룹 | 문안에서 바뀐 것 |")
A("|---|---|---|")
A("| 2차 | `b3b3ca0a` | 로봇 A 사망 문안 + 「장갑판이 단단하고 곧게 남는다 · 접히는 것이지 눌리는 것이 아니다」 |")
A("| **3차** | `ec73fed0` | 위에 더해 **「무릎이 바깥으로 꺾여 자세가 넓어지고 몸이 그 사이로 떨어진다 · 장갑판은 크기가 그대로다」** |")
A("")
A("**갈린 것은 「넓어진다」 한 마디다.** 2차는 「눌리지 마라」로 막기만 해서 아예 덜 움직였고,")
A("3차는 **무엇을 하라**로 적어 무너지는 방향이 생겼다(규칙 9 「금지문은 오히려 불러온다」와 같은 자리다).")
A("2차 후보는 `Docs/art_log/candidates/robot_b_Death_v2/` 에 남긴다.")
A("")
A("#### 마스크 — 판정 재료 넷 (`700d2e8`)")
A("")
A("❓4-5(합체 사망)와 ❓4-6(옆모습 잣대)을 사용자가 보게 만든 것들이다.")
A("")
A("| 파일 | 무엇 |")
A("|---|---|")
A("| `preview/_judge45_death.gif` | 합체 사망 세 판 — 2차(11px) · 폐기된 보간판(109px) · 로봇 A(48px) |")
A("| `preview/_judge45_mask.png` | 마스크 두 안을 **다시 그려질 자리를 실제로 지워서** 보인 것 |")
A("| `preview/_judge46_profile.gif` · `_judge46_profile_50px.gif` | 옆모습 넷 — A 동·서(5차) · 합체 동면 2차·3차. 256 과 실제 화면 50px |")
A("")
A("⚠️ **첫 마스크 그림은 빨간 네모만 얹은 것이라 「무슨 차이인지 모르겠다」로 되돌아왔다.**")
A("두 안의 차이는 **얼마나 넓은 자리가 새로 그려지는가**인데 네모는 그것을 안 보여 준다.")
A("**지워서 보이자 한 번에 갈렸다** — 현행은 다리만, 제안은 다리·몸통·가슴 이음선·발사관 아래턱까지 사라진다.")
A("**보여 주려는 것이 「없어지는 것」이면 없애서 보인다**를 기록으로 남긴다.")
A("")
A("---")
A("")
A("### 3-15. `create_image_pro` 의 캔버스 하한 — **16 이다** (`260907_W04` 탄환비 판정 · 2026-09-08)")
A("")
A("탄환비를 **탄환 한 발 스프라이트(`vfx_tagbullet` · 실루엣 약 38px)** 로 뽑기로 판정됐다.")
A("생성 전에 도구가 받아 주는 캔버스의 하한부터 읽었다.")
A("")
A("| 인자 | 스키마가 적은 것 |")
A("|---|---|")
A("| `width` · `height` | **`minimum: 16`** · 기본 128 · 최대는 가로세로비에 달렸다(정사각 512 · 16:9 는 688×384) |")
A("")
A("**38 은 하한 아래가 아니다** — 16 이 하한이므로 `38 × 38` 캔버스도 도구가 받는다.")
A("그래서 캔버스는 **38 또는 64** 둘 다 열려 있고, **어느 쪽으로 갈지는 설계 확정 뒤다.** 아트가 정하지 않는다.")
A("")
A("**함께 읽은 것 — 후보 수가 캔버스로 갈린다.** 한 번 호출에 도구가 내는 후보는")
A("42px 이하 **64장** · 85px 이하 **16장** · 170px 이하 **4장** · 그 위 **1장**이고 **비용은 후보 수와 무관하다.**")
A("")
A("| 캔버스 | 후보 | 딸려 오는 것 |")
A("|---|---|---|")
A("| **38** | **64장** | 실루엣이 캔버스를 꽉 채운다 — 로봇 A 승인본에서 겪은 여백 0 문제와 같은 자리 |")
A("| **64** | 16장 | 사방에 여백이 남는다 · 드론(64)과 같은 칸 체계 · 15-2 8-3 「최종 크기에서 생성」과 어긋나지 않는다 |")
A("")
A("**아트의 권고는 64 다** — 후보가 4분의 1로 줄지만 열여섯도 고르기에 충분하고, 38 은 도구가 캔버스를 채우는")
A("성질(3-10) 때문에 탄환이 테두리에 닿은 채로 나올 공산이 크다. **다만 이것은 권고이고 값은 설계가 정한다.**")
A("")
A("⚠️ **아직 아무것도 생성하지 않았다.** 순서는 **문서·코드 세션이 연출 문서 프롬프트를 옮긴 뒤**(사용자 한 줄) —")
A("`vfx_tagbullet` 1종 → 반려 넷 재생성(드론 발사 · 부스터 · 드론 소멸 · 탄약 없음 · 이전 초안은 `candidates/`) →")
A("컷인(15-3 F-1 2단계 · 참조는 현행 승인본 셋 · 사용자 승인). 결과는 **사용자 육안 뒤** `Assets/_Project/Art/VFX/` 로 간다.")
A("")
A("---")
A("")
A("### 3-16. `vfx_tagbullet` 1차 — 후보 열여섯 (2026-09-08 · job `865c756f`)")
A("")
A("연출 아트 요청 문서(19) 9-2 의 `vfx_tagbullet` 문안을 **틀 B(바닥에 눕는다)** 에 그대로 넣었다 ·")
A("`create_image_pro` · **64 × 64** · 배경 제거 ON · **참조 없음**(9-0 — 로봇 승인본을 걸면 금속 질감이 딸려온다) · 20 생성.")
A("**64 라 후보가 열여섯 장 나온다**(3-15). 비용은 후보 수와 무관하다.")
A("")
A("| 후보 | 실루엣 | 여백 L R T B | | 후보 | 실루엣 | 여백 L R T B |")
A("|---|---|---|---|---|---|---|")
A("| c00 | 18×43 | 23 23 8 13 | | c08 | 18×45 | 23 23 10 9 |")
A("| c01 | 18×48 | 23 23 3 13 | | c09 | 24×46 | 20 20 10 8 |")
A("| c02 | 18×48 | 23 23 3 13 | | c10 | 24×46 | 20 20 10 8 |")
A("| **c03** | **18×42** | 23 23 7 15 | | c11 | 26×48 | 19 19 10 6 |")
A("| c04 | 18×57 | 23 23 **0** 7 | | c12 | 28×55 | 18 18 3 6 |")
A("| c05 | 18×57 | 23 23 **0** 7 | | c13 | 18×48 | 23 23 9 7 |")
A("| c06 | 20×57 | 22 22 **0** 7 | | c14 | 28×48 | 18 18 9 7 |")
A("| c07 | 20×48 | 22 22 9 7 | | c15 | 28×49 | 18 18 9 6 |")
A("")
A("**규격이 적은 것은 「그림은 약 38」인데 열여섯 중 38 인 것이 없다** — 가장 작은 것이 c03 의 42 이고 넷(c04·c05·c06·c12)은")
A("50 을 넘어 위 여백이 0 이다. **꼬리(모션 스트릭)를 문안이 요구했으므로 그만큼 길어진다** — 몸통만 재면 30 안팎이다.")
A("38 이 몸통을 가리키는지 꼬리까지인지는 문서에 없다. **줄여서 맞추지 않는다**(15-2 8-3) — 값의 뜻을 설계가 정할 자리다.")
A("")
A("⚠️ **아웃라인이 붙어 나왔다.** 9-5 설정값이 `Outline: Lineless` 이고 틀 B 의 `EDGES` 절도 「투명 배경과 바로 맞닿는다」인데,")
A("실측하니 실루엣 가장자리 평균 휘도가 **c00 1.7 · c13 2.0** 으로 사실상 검은 테두리다. **c03 만 61.7 로 밝다.**")
A("`create_image_pro` 에는 `outline` 인자가 없어(3-10 에서 이미 본 자리) 문안으로만 막아야 하는데 이번엔 안 먹었다.")
A("**사용자가 c03 이 아닌 것을 고르면 테두리를 지우는 후처리가 필요하고, 그것은 규칙 15 밖의 새 공정이라 판정거리다.**")
A("")
A("후보 열여섯은 `Docs/art_log/candidates/vfx_tagbullet_v1/` · 한 장에 모은 그림은 `preview/_vfx_tagbullet_v1.png`.")
A("**설치하지 않았다** — 사용자 육안 뒤 `Assets/_Project/Art/VFX/` 로 간다.")
A("컷인(15-3 F-1 2단계 · 참조는 현행 승인본 셋 · 사용자 승인). 결과는 **사용자 육안 뒤** `Assets/_Project/Art/VFX/` 로 간다.")
A("")
A("#### 남은 길 — 작게 뽑아 투명 여백을 덧대는 것")
A("")
A("도구가 캔버스를 채운다면 **캔버스를 작게 주고 그 결과를 256 안에 가운데로 놓으면 된다.**")
A("예를 들어 224 로 뽑아 256 에 앉히면 사방에 16px 씩 남는다.")
A("")
A("| | |")
A("|---|---|")
A("| 픽셀 손실 | **없다** — 224 로 뽑은 것을 그대로 두고 빈 자리만 덧댄다. 축소가 아니다 |")
A("| 규격 | 캔버스 256 · 피벗 가운데 · PPU 192 가 그대로 선다 |")
A("| 「최종 크기에서 생성」 원칙 | 지킨다 — 품목 6-3 이 금지한 것은 **뽑아 놓고 줄이는 것**이다 |")
A("")
A("⚠️ **대가 하나 — 화면에서 로봇이 약 12% 작아진다.** 실루엣이 249 에서 224 로 줄기 때문이다.")
A("15-1 1장이 「캔버스 256 = 화면 폭의 17.8%」로 적어 둔 그 크기가 바뀐다.")
A("**여백을 얻는 값이다** — 진폭 4~6% 와 다리 교차가 그 자리에서 나온다.")
A("")
A("⚠️ **구현이 하지 않았다.** 덧대기는 문서 어디에도 없는 새 공정이고, 로봇이 작아지는 것은")
A("15-1 1장의 값을 건드린다. **사용자·설계 판정이 먼저다.**")
A("")
A("#### 넘길 것 — HANDOFF 금지 조항 하나가 풀린다")
A("")
A("HANDOFF 「현재 금지」의 **「로봇 3종 스틸·`cutin_fusion` 512 재생성 금지 (앵커 유지)」**가")
A("이 결정으로 **로봇 A 와 합체 256 에 한해 풀린다.** `cutin_fusion` 512 와 로봇 B 는 그대로 금지다.")
A("`Docs/HANDOFF.md` 는 이 세션 소유가 아니라 손대지 않았다 — 문서·코드 세션 몫이다.")
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
