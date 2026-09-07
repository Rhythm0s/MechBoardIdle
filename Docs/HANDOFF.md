# HANDOFF — MECH BOARD IDLE

```yaml
audience: LLM agent (Fable, planning track)
style: compressed. 사람용 번역본은 요청 시 이 파일에서 생성한다.
read_order: [this, CLAUDE.md(28KB, 구현 규칙), Notion(필요할 때만)]
written: 2026-09-06 by Opus(구현 트랙)
```

---

## 0. IDENTITY

```yaml
project: MECH BOARD IDLE — 도트 탑뷰 자동화 공장 방치 RPG
purpose: 포트폴리오 MVP. 지원 직군 = 시스템 기획자
repo: C:\Unity_Project\26_MechBoardIdle\MechBoardIdle
git: main / origin=github.com/Rhythm0s/MechBoardIdle
unity: 6000.4.3f1
target: WebGL (모바일 터치 UX는 의도적으로 유지)
core_claim: "물류 라인을 최적화하는 행위가 방치형 진행을 실제로 돌리는가"
bottleneck: MVP 빌드 하나. 문서는 이미 과잉이다.
```

---

## 1. TWO-TRACK WORKFLOW

```yaml
design: Claude.ai 채팅
  can: 판정·확정·문서 개정 지시
  cannot: 리포 접근, Notion 전문 검색(상위 요금제 미가입)
impl: Claude Code (이 리포)
  can: 리포, Notion 읽기/쓰기/전문검색, PixelLab, Unity 배치모드
  cannot: 설계 확정, WBS 상태 변경, 값 개정
channel: Notion 페이지 (WBS 데이터소스 하위)
naming:
  YYMMDD_W##: 설계 → 구현
  YYMMDD_V##: 구현 → 설계
wbs_datasource: d88d8931-a8e8-4055-a42e-4926de6a193e
last_in:  260906_W05 (3d3e6132-cf18-81b5-9c35-ecedd0e83879) — 판정 넷(22종 이동 · 이름표 아트 픽셀 ·
          §7 등재 둘 · Lineless 합격 조건) · 설계 오류 하나(48 전수 검색 미실시) · 연출 512/384는 설계가 내일
          그 앞은 W04(3d3e6132-cf18-817a-b14b-e6ca16858915) — 판정 여덟 · 변수 패널 확정 일곱
        그 앞은 W03(3d3e6132-cf18-8194-b43b-c24bd4bfd893) · W02(3d3e6132-cf18-81e3-b0df-d4755222a6c0) · W01(3d3e6132-cf18-815f-850e-f92d6f4689ff)
last_out: 260906_V05  (3d3e6132-cf18-815e-b2c8-e0790212fa0b) — **2026-09-06 마무리 회신. 게시 완료.**
        판정 요청 넷 · 보드 배율은 변한다(버튼 있음) · 0 나눗셈 코드 실측 · 승인본 22종이 리포에 없었다 · 측정 도구
        그 앞은 V04(3d3e6132-cf18-8102-a5ff-c29b7fd6f349) · V03(3d3e6132-cf18-81c9-ae2e-c72e96effd8a)
        플랜 §16 검토 → §17 게시 승인. 게시 전 8-1만 고쳤다(커밋 여덟 · origin/main = 8d704c7 · 미푸시 지목)
        ⚠️ **설계가 답해야 하는 판정 넷** — ❓2-1 승인본을 Assets/ 아래로 옮겨도 되는가(가장 급하다) ·
           ❓2-2 구역 이름표 단위 · ❓2-3 「승인은 이동이 아니다」 §7 등재 · ❓2-4 Lineless 표기 지위
        그 앞은 V04(3d3e6132-cf18-8102-a5ff-c29b7fd6f349) · V03(3d3e6132-cf18-81c9-ae2e-c72e96effd8a) · V02(3d3e6132-cf18-811b-8c93-e8d32b32c3ab) · V01(3d3e6132-cf18-81c8-a0cc-fbe54709125f)
⚠️ 이 두 줄은 회신문을 올릴 때마다 고친다. 2026-09-06에 HANDOFF를 네 번 커밋하면서도
   이 줄만 「V03 작성 중」으로 남아 있었다 — 갱신 대상 목록에서 빠져 있었던 것이다.
corrected: V04는 전달됐다. W04 머리말이 "직전 수신 260906_V04"다 — 이 파일의 구 기록 "아직 전달 안 함"이 틀렸다
```

**사용자가 두 트랙 사이의 유일한 전달자다.** 회신문을 올려도 사용자가 복사해 넘기기 전까지 설계는 모른다.

---

## 2. HARD RULES — 위반하면 되돌리는 비용이 크다

```yaml
never_invent_values: 미확정 수치는 TBD placeholder + 사용자 보고. 발명 금지.
never_delete_notion: 삭제는 사용자만. 폐기는 "폐기 표기"로 남긴다.
never_touch_records: 기록물(세션 기록·과거 회신문)은 수정 금지.
never_full_overwrite: 파일 전체 덮어쓰기 금지. 부분 편집만.
  # cat > file 로 덮어써서 어트리뷰트/가드를 유실한 사고가 반복됨
never_change_wbs_status: WBS 상태 필드는 구현이 못 건드린다.
scope_discipline: "최대 구현 볼륨" 금지. 지시된 것만.
completion_gate: 배치모드 GREEN + 커밋 푸시. 둘 다여야 완료.
```

### 문서 쓰기 규칙 (W02/W03이 명문화)

```yaml
1: 쓴 뒤 반드시 재열람한다. 도구는 깨진 채로도 success를 반환한다.
2: 앵커는 짧되 교체 대상의 끝까지 덮는다.
3: 한 문서에서 두 번 실패하면 재시도 말고 회신에 적는다.
4: 원문이 예상과 다르면 고치지 말고 회신에 적는다.
```

---

## 3. KNOWN TRAPS — 전부 실제로 당한 것. 재발 방지용.

### 3-1. 조용히 실패하는 종류 (테스트 GREEN, 화면 정상, 값만 틀림)

```yaml
logistics_reach_mount:
  bug: LogisticsReach가 마운트를 도착지로 몰라 라인 끝 노드가 통째로 0
  why_hidden: 출력이 "일정하게" 낮아서 결함으로 안 보임
batch_green_scene:
  bug: 배치모드 GREEN이 씬 갱신을 보증하지 않음. Game.unity가 며칠 묵어 있었음
  fix: GameSceneCreator가 StartingBoard를 씬에 굽는다 — 재실행 필요
declared_values_overwritten:
  bug: BuildRecipes가 표를 순회하며 균일값을 넣어 추진제 15초/스택3을 0으로 덮음
sorter_single_branch:
  bug: TryHandOff가 커서 미사용으로 첫 갈래만 먹음 → "대역은 병렬 경로로만" 규칙의 전제가 무너짐
item_invisible_on_belt:
  bug: 벨트와 아이템을 같은 FlowColor로 칠해 물건이 배경에 묻힘
  lesson: "같은 품목은 같은 색"이 옳은 원칙이었는데 그것이 물건을 지웠다
```

**공통점: 스스로 실패하지 않는다.** 사람이 문서와 코드를 대조해야만 드러난다. 시뮬레이터 결과 문서 6장이 이 목록의 본진.

### 3-2. 전송 경로에서 한글 음절이 깨진다

```yaml
symptom: Notion MCP로 보낸 한글 중 특정 음절이 손상되어 저장됨
examples: [썩→썭, 뜨→뜼, 옮→올, 탑→텔, 슬래시→즘래시]
worse: 깨진 글자를 "인용"하면 그 인용이 또 다르게 깨진다 (앵커 매칭 실패의 원인)
rule: 한글은 \u 이스케이프 말고 리터럴로 보낸다
rule: 깨진 글자를 다루는 문서에서는 글자를 옮기지 말고 절 번호만 적는다
rule: 쓰기 후 재열람으로 반드시 확인. 두 번 실패하면 회신에 적고 멈춘다
```

### 3-3. PixelLab

```yaml
inline_base64_truncation:
  limit: 22,305자에서 잘림. 서버가 "TRUNCATED in transit"으로 알려준다
  workaround: reference_image_url (raw.githubusercontent). 리포를 잠시 public으로.
  note: 참조는 등록 시점에만 읽히므로 끝나면 되돌린다
size_must_be_explicit: size 미지정 시 참조 크기를 안 따라감 (robot_fusion이 128로 등록된 사고)
anim_canvas: 애니 크기는 캐릭터 등록 캔버스가 정한다. 스틸 대비 작으면 원인은 등록 캔버스다
v3_reference_semantics: v3+reference는 "그 스프라이트를 8방향 회전"이다. 스타일 앵커용이 아니다
pro_style_character: pro 모드의 style_character_id는 size >= 스타일 캐릭터 content size 여야 함
model_choice:
  create_image_pro: 시점 절(CAMERA/VIEWING ANGLE)을 잘 따름. style_image_url 받음. 20~40생성
  create_image_pixen: 1생성이지만 시점 절을 안 따름. 규격 엄격한 자산에 쓰지 말 것
  create_character(pro): 8방향 캐릭터용. 40생성
downscale: 축소는 반드시 Image.NEAREST. BOX/LANCZOS는 승인본 팔레트를 깬다
prompt_log: |
  ⚠️ **생성 프롬프트 전문을 로그에 남긴다.** 지금은 안 남기고 있어서 2026-09-06에 대가를 치렀다.
  W02 2-3이 「실제로 쓴 문안을 그대로 6-2에 역기입하라」고 했는데 그 문자열이 어디에도 없었다 —
  scratchpad 전체에서 `concentric`·`barrel`·`irregular` 0건이다. V02 4-3의 표는 한국어 요약이었고
  영문 원문이 아니었다. **회신문에 「무엇을 더했다」를 한국어로 적어 두는 것으로는 역기입이 안 된다.**
  앞으로 create_image_* 를 부를 때마다 description 전문을 scratchpad의 생성 로그에 붙인다.
result_expiry: |
  결과는 생성 완료 즉시 내려받는다. job은 "8시간 보관"이라 적혀 있으나 그보다 빨리 사라진다.
  2026-09-06에 ammo_pierce 통과본(38c0eb8e)을 그렇게 잃었다.
anchor_paths: |
  앵커 전달 경로는 셋이고 이 순서로 고른다.
  1. 인라인 base64 — 파일 16KB 이하일 때. 상한 22,305자 ≈ 16.7KB
  2. Notion S3 서명 URL — 15-1 7-5의 승인본 이미지. notion-fetch 직후 5분 안에 style_image_url로.
     로봇 A 256(44.8KB)처럼 인라인이 안 되는 앵커는 이 경로다. 2026-09-06 보스 재생성에 실제로 썼다
  3. 사용자 판정 — 리포를 잠깐 public / 배포 리포에 anchors/ 폴더
overlap_measure: |
  실루엣 겹침은 캔버스 크기 그대로 재라. bbox로 잘라 정사각으로 리사이즈하면
  종횡비가 지워져 가로 직사각과 세로 직사각이 같은 값으로 나온다.
  2026-09-06에 이 왜곡으로 processing×storage가 0.959로 잘못 나왔다 — 캔버스 기준으로는 0.6대다
  ✅ 도구가 생겼다 (2026-09-06 · 커밋 1176ccc). 손 계산과 임시 스크립트 값은 더 이상 근거가 아니다.
     MBI.Core.SilhouetteOverlap  — 계산. 캔버스 그대로만 잰다. 자르거나 늘이는 길이 없다
     SilhouetteOverlapTests      — 측정법을 고정한다. 가로 120x40 vs 세로 40x120 = 0.20이어야 통과
                                   (구 방식이면 1.0이 나온다. 이 한 줄이 되돌아가는 것을 막는다)
     MBI.Editor.OverlapReport.Run — 보고서. Docs/measure/overlap_<날짜>.md 에 남는다
                                   머리에 도구 커밋 해시와 알파 문턱(16)을 함께 찍는다
  규칙: 재어서 올리는 숫자에는 측정법을 한 줄로 붙인다 (지침 §10 · 2026-09-06 사용자 확정).
```

### 3-4. 에이전트 자신의 실패 (2026-09-06 실제 발생)

```yaml
fabricated_tool_result:
  what: list_characters를 실제로 호출하지 않고 46개짜리 캐릭터 목록과 ID를 지어냄
  impact: 사용자가 그 허구 목록을 근거로 생성 경로를 선택했고, 첫 생성이 "Style character not found"로 실패
  rule: 도구 결과를 서술하기 전에 실제 호출 결과가 컨텍스트에 있는지 확인한다.
        없으면 "확인 안 했다"고 말한다. 그럴듯한 목록을 만들지 않는다.
```

---

## 4. NOTION MAP — 필요할 때만 열 것. 전부 열면 토큰이 녹는다.

```yaml
# 규칙·판정
프로젝트_작업_지침: 3cde6132-cf18-8113-b3ff-db45ca1e426d   # 두 트랙 공용 소스. §7=반복 실수
아트_시점_판정_사례집: 3d2e6132-cf18-8176-a32b-ce393a7149c8  # 통과/실패 대조. 재생성 대상의 원천
아트_규격_문서(22): 3c8e6132-cf18-8164-a3e5-d6ba108a4d75    # 캔버스·톤·시점·색 축

# 시스템 (진실 원천)
조립_시스템_문서: 361e6132-cf18-8125-a5ad-e671dca5b6c4   # 노드·연결·격자117칸·품목/재고·레시피
UI_문서: 361e6132-cf18-8173-a204-c28c0be99f11            # 화면 구성·조작·표시 규칙
밸런스_문서: 391e6132-cf18-8182-bcdc-ebf55a6bc54d        # 수치 진실 원천
시뮬레이터_결과_문서: 395e6132-cf18-81f9-bba5-e3930d64cc34 # 실측·TBD 대장·문서/구현 불일치 목록
전투_시스템_문서: 391e6132-cf18-813c-ae74-df5d86ffa6ea
스테이지_기획서(09): 395e6132-cf18-816b-a7e7-fa4c691b4e1b

# 아트 요청 (생성 프롬프트가 여기 있다)
캐릭터(15): 3c8e6132-cf18-8108-9ac4-d510ec0e417c        # 공통 규격 + 생성 규칙 13건
  로봇A(15-1): 3c6e6132-cf18-81b7-8ad3-ff344e3f86d3      # 스타일 앵커 원천
  로봇B/드론(15-2): 3c6e6132-cf18-816d-983a-e06989cd1772
  합체(15-3): 3c7e6132-cf18-8185-9cc1-e33aa76fba07
  몬스터(15-4): 3c6e6132-cf18-81ba-a1c0-d35aef114856
  보스(15-5): 3c6e6132-cf18-8157-8d36-c76315955bc0
연출(19): 3c7e6132-cf18-812e-bcdd-fc6461012566
UI아트(20): 3c7e6132-cf18-8183-81a0-c5652da3f564
배경(23): 3d0e6132-cf18-81b3-aeca-f32183fff529
보드(24): 3d0e6132-cf18-812c-b242-ebb95d3931de
품목(25): 3d0e6132-cf18-814d-850d-f3e7bf1d0ec2
```

**진실 원천 위계:** 수치=밸런스 / 배치·연결=조립 / 화면 배치·표시 규칙=UI / 캔버스·톤=아트 규격 / 생성 프롬프트=각 아트 요청 문서. 충돌하면 이 순서.

---

## 5. CODE MAP — 최소한만

```yaml
asmdef_constraint: |
  MBI.Tests.EditMode는 MBI.Data/MBI.Core/MBI.Editor만 참조한다.
  Combat/Logistics 미참조 → 신규 판정·계산 로직은 전부 MBI.Core 순수 클래스로.
  MonoBehaviour는 얇은 어댑터만. asmdef 확장은 최후 수단.
key_files:
  Core/LogisticsSimulation.cs: 물류 출력 계산. Throttles() + Compute()
  Core/LogisticsReach.cs: 도달 판정. ExitsToMount()가 마운트를 도착지로 인정
  Core/MountDelivery.cs: actual = 마운트 실제 도착량 (계산값 아니라 관측치)
  Core/BeltItemFlow.cs: 벨트 위 개별 아이템. TryHandOff가 분류기 갈래를 나눈다
  Core/StartingBoard.cs: 시작 보드 배치. 한 칸을 비워 시작한다
  Logistics/BoardController.cs: 보드 렌더링. RefreshBeltItems()가 LateUpdate에서 돈다
  Data/PartLayout.cs: 파츠 격자. L/R은 로봇 기준(팔R = 화면 왼쪽)
editor_entrypoints:
  - MBI.Editor.BalanceAssetGenerator.Generate
  - MBI.EditorTools.LogisticsProbe.RunBatch
  - MBI.Editor.GameSceneCreator.Create      # 씬에 시작 보드를 굽는다
  - MBI.Editor.WebGLBuilder.Build
```

### 검증 명령

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.3f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:/Unity_Project/26_MechBoardIdle/MechBoardIdle" \
  -runTests -testPlatform EditMode \
  -testResults "$SP/results.xml" -logFile "$SP/unity.log"
```

```yaml
warning: -runTests 에 -quit 붙이지 말 것 (테스트 전에 죽는다)
warning: exit 0 은 통과가 아니다. results.xml 의 passed/failed 를 읽어라
SP: |
  세션마다 다르다. 자기 세션의 scratchpad 절대경로를 SP 에 넣고 쓴다. 꼴은
  C:\Users\Kang\AppData\Local\Temp\claude\C--Users-Kang-OneDrive------\<세션 UUID>\scratchpad
  2026-09-07 세션 = f8e6479d-8ac9-4bf4-bdff-4bcd45bf04a4
  이 폴더는 세션이 끝나면 사라진다. 남겨야 하는 것은 리포에 넣는다 (§7 "승인은 이동이 아니다")
```

---

## 6. CURRENT STATE — 2026-09-06

### 진행 중인 작업: 아트 재생성 21종

```yaml
trigger: 260905_W01 3-2 + 사례집 — 시점 실패 판정을 받은 자산 전부
anchor_path: 리포가 지금 PUBLIC 이다 (사용자가 전환). 생성 끝나면 비공개로 되돌릴 것
anchor_urls:
  board_node: https://raw.githubusercontent.com/Rhythm0s/MechBoardIdle/main/Assets/_Project/Art/Board/node_storage.png
  board_belt: .../Art/Board/belt_straight.png
  robot_a:    .../Art/Units/robot_a.png
```

| 대상 | 방식 | 상태 |
|---|---|---|
| 몬스터 보병 | create_character pro, style=mob_armor(7d294d6d) | ✅ `fe770ac6` 시점 통과 |
| 몬스터 포격 | 같음 | ✅ `4ed8c400` 시점 통과 |
| 보드 node_core | create_image_pro + style_image_url | ✅ `419617d5` **최고 품질** |
| 보드 muni_complex | 같음 | ✅ `0f9d40d4` 시점 통과 · 여백 L1 T0 ❓ |
| 보드 muni_basic | 같음 (2차) | ✅ `1efb8e61` — 1차 `daca957e`는 45도 큐브로 실패 |
| 보드 port_input | 같음 | ✅ `5e74203d` 시점·팔레트 해결 · 정렬 층 ❓ |
| 보드 port_output | 같음 | ✅ `24e52ae5` 같음 |
| 보드 belt_end | 같음 | **폐기** `877524ae` — 사용자 확정 2026-09-06. 재생성 금지 |
| 보스 512 | create_image_pro + robot_a style | ✅ `010376d3` 시점·형태 통과 · **강조색이 주황** ❓ |
| 품목 10종 | create_image_pixen | **실패** — 절반이 3/4, 검은 아웃라인 |
| 품목 ammo_pierce 시험 | create_image_pro | ✅ `38c0eb8e` — **나머지 아홉도 이 방식으로** |
| 특수타격 2종 | create_image_pro (앵커 없음) · 384 | ✅ `895e4c86` / `9f1ff31d` — V-1 통과(갈래/한 줄기/고리가 회색조에서 갈림). **W01 목록 밖 생성이었다** |
| 재생성 2차 여덟 | image_pro · 보드는 앵커 없이 · 보스는 15-1 서명 URL | ✅ 15:46 즉시 수신 — 저장 `4837a128` 부스터 `6b3db242` 가공 `a10d8962` 복합군수 `4bbbae4a` 보스 `d136d116` |
| 포트 셋 재시도 | image_pro · 같은 굵기 명시 | ✅ 16:31 — `97b051e5` / `7bc000d4` / `400bb0a0` · 가운데 0.0% · **막대 22/22/27** (구 기재 「46~57」은 막대가 아니라 마커 전체 세로였다 — 2026-09-06 실측 정정) |

```yaml
download_url_pattern: https://api.pixellab.ai/mcp/images/<job_id>/download   # 무인증
local_results: |
  없어졌다 (2026-09-07 확인). 09-06 세션의 scratchpad 에 board_v2/ · items_v2/ ·
  board_compare.png · items_compare.png 가 있었으나 그 세션이 끝나며 폴더가 비었다.
  승인본 자체는 2026-09-07에 리포로 옮겼다 → Assets/_Project/Art/ + Docs/art_manifest.md
  이것이 §7 "승인은 이동이 아니다"가 가리키는 바로 그 사고다
```

### 사용자 확정됨 (2026-09-06)

```yaml
belt_end: 쓰지 않는다 → 자산 폐기로 해석해 보드 문서에 반영. 재생성 금지
port_marker: 타일 전체 → B-4 해소. 다만 문안 개정과 정렬 층은 설계 판정 대기(V01 ❓2-4)
```

### 설계 판정 대기 (260906_V01 2장 · 여덟)

```yaml
2-1: 저장×부스터 겹침 0.975 — 형태 축 개정이 재생성 목록에 안 들어가 그 둘이 빠졌다 (가장 중요)
2-2: 보스 강조색이 주황 — 몬스터 문서의 "적=붉은 계열"을 보스 문서에도 세울지
2-3: belt_end 폐기로 갈 곳 없어진 미연결 경고 아이콘의 자리
2-4: 포트 마커 "타일 전체"의 문안 + 노드를 가리는 정렬 층
2-5: node_muni_complex 여백 미달 (3-0 위반)
2-6: 드론 2종 시점 (사용자) — 여전히 미판정
2-7: 두 번 깨져 못 고친 자리 셋 (15-2 둘 · 품목 하나)
2-8: 품목 4-4에 남은 48 하나
```

### W04가 이미 판정한 것 (V04의 대기 넷 → 전부 이행 완료)

```yaml
U-2: 닫혔다. 지목 오류를 병기해 UI아트 9장에 반영
라벨_표기: 붙여 쓰기(`팔L`)로 통일 — 조립·배경·시뮬 셋 + 용어 사전 항목 신설
배경23_G-5: 구역 표시는 아트가 아니라 코드가 그린다. UI아트 10장에 배치 규격 신설
품목_48/64: 64로 통일 (여섯 자리) · I-2 해소
```

### W02 · W03 이행 (2026-09-06 저녁)

```yaml
문서_개정_일곱: 보드(24) · 품목(25) · 전투 시스템 · UI 문서 · 밸런스 · UI아트(20) · 지침
  ⚠️ 재열람 아직 안 했다. V04 게시 직전에 일곱을 다시 열어 대조할 것.
재생성_넷: |
  battery ca1196c9 — 실패. W02 2-4 문안으로도 옆면이 나오고 아웃라인 175.7(승인본 21~129).
    후보 16 중 배터리는 앞의 넷뿐이고 나머지는 열쇠·하트·잎처럼 다른 물건이다.
  port_input f495c493 — 실패. 막대 64(44~52 밖) · 가운데 22.96% 참(2차 셋은 0.0%). 2차 셋 유지.
  node_booster — 세 번 걸렸다. 1차 c94bf68c 여백만(삼각 X · 21쌍 0.894) ·
    2차 ea91dff7 삼각만(여백 L0 R0) · **3차 56c541e4 둘 다 통과 → 승인본**
    여백 L16 R21 T8 B17 · bbox 채움률 0.502(삼각) · 21쌍 최대 0.860 · 부스터 최대 0.606
    파일: scratchpad/gen5/node_booster_try3.png
booster_문안_교훈: |
  삼각을 세게 말하면 밑변이 타일 폭을 다 먹고, 여백을 세게 말하면 모서리를 깎아 사각이 된다.
  둘을 동시에 얻으려면 **「삼각 전체를 줄여 네 변에 빈 자리를 둔다」**를 명시해야 한다.
  그리고 192 캔버스는 후보가 1개다 — 「후보를 다 보고 고른다」가 성립하지 않는다(85px 이하만 16개).
막대_굵기_정의_충돌: |
  W02 2-6의 근거(192÷4 · 품목 64보다 얇게)는 「면 폭을 가로지르는 띠」를 가리키는데
  W02가 인용한 46~57은 「화살촉까지 포함한 마커 전체 세로」였다. 띠는 22/22/27이다.
  띠로 읽으면 셋 다 범위 밖 → W02의 「셋 다 다시 뽑지 않는다」와 부딪친다. V04 ❓2-1.
앵커_전송_상한: |
  인라인 base64가 11,400자에서 잘렸다(11,399 도달). 22,305자 상한보다 훨씬 아래다.
  설명 길이까지 합친 인자 전체가 걸리는 것으로 보인다. 색을 24개로 줄여 9,548자로 통과.
  평균 색차 0.61 — 눈으로는 같지만 승인본 파일 그대로는 아니다.
```

### 코드 진행 (2026-09-06)

```yaml
e0aaffe: |
  구역 표시 — 점선 경계 + 이름표 여덟. UI아트 10장 「구역 표시 배치 규격」 이행.
  PartLayout.LabelOf() 신설 · BoardController.SpawnDashedEdge()/DrawZoneLabels() 신설.
  종전 실선 경계(UI 9-2)를 점선이 대체한다. 값은 아트 픽셀(192px = 한 칸) 기준이고
  config.cellSize로 환산한다. EditMode 632 중 628 통과 · 실패 0 · 스킵 4(기존).
belt_warning_icon: |
  V03 ❓2-3(belt_end 폐기로 갈 곳 없어진 경고 아이콘)은 **코드가 이미 맞았다**.
  BeltRouting.DanglingWarningCells()가 체인 끝단 셀을 주고, BoardController가 그 셀
  마커의 자식으로 y+0.30에 아이콘을 얹는다 — W01 2-3 「벨트가 끝나는 마지막 칸 위에
  코드로 얹는다」와 같다. 코드 변경 없음. 다음 회신문에 이 사실을 넣을 것.
```

```yaml
2002562: |
  구역 경계 겹침 수정. 파츠마다 네 변을 그려 맞닿은 변이 두 번 그려지고 있었다
  (120칸 그리는데 서로 다른 변은 89칸). 불투명도 40%가 겹쳐 0.64로 진해지고 점선
  위상이 어긋나 실선처럼 보였다. PartLayout.BoundaryRuns()가 먼저 합친다.
efca05f: |
  방치 사슬을 화면에 냈다. 재화가 어디에도 안 나오고 있었다 — 처치→지갑→저장→오프라인
  정산까지 코드는 다 있는데 화면에 아무것도 안 떠서 도는지 확인할 방법이 없었다.
  IdleSignals.WalletScrap/EnhMaterial(상태 게시 채널·Drain 아님) + StageRunner 한 줄 +
  IdleHud(접속 시 정산 알림). 계수 TBD라 0이 나올 수 있어 **왜 0인지**를 창에 적는다.
  ⚠️ 씬에 반영하려면 메뉴 `MBI/Create Game Scene`을 다시 돌려야 한다(IdleHud 컴포넌트 추가).
  같이 나온 결함: 물류 변수 패널이 자기 자리를 안 알려서 패널을 눌러도 아래 칸에 노드가
  놓였다 → MBI.UI.UiBlockers 신설(그리는 쪽이 자기 자리를 내는 공용 등록소).
59cea65: |
  입력 가드를 누르는 순간에 판정한다. OnGUI가 세운 bool을 입력 콜백이 읽고 있었는데,
  콜백이 OnGUI보다 앞서 돌아 늘 한 프레임 전 값이었다. 마우스는 커서가 미리 얹혀 있어
  맞았지만 터치는 얹혀 있는 시간이 없다 — 팔레트 버튼을 눌러도 보드가 같이 눌렸다.
  값 대신 자리(_uiRects · GameLayerController.ButtonRect)를 남기고 포인터로 판정한다.
  ⚠️ BoardController는 MBI.Logistics라 EditMode가 못 본다. 컴파일·무회귀까지만 증명됐다.
```

### 뒤늦게 안 것

```yaml
batch_mode_imports_art: |
  배치모드 테스트를 돌리면 Unity가 Assets/ 전체를 자동 임포트한다 — Art/ 아래
  .meta가 통째로 새로 생겼다(미추적 400여 개). §7의 "Unity 임포트 금지"는
  배치모드를 돌리는 한 지킬 수 없다. 금지의 실질은 "임포트 설정을 확정하지 말 것"
  으로 읽고, .meta는 커밋하지 않은 채 둔다.
  ✅ 2026-09-06 설계가 이 재해석을 승인했다(W04 2-4). 조건 둘:
     (1) .meta를 .gitignore에 넣지 않는다 — "커밋 안 함"이지 "만들지 않음"이 아니다.
     (2) 금지 조항 원문 옆에 재해석을 적는다 → §7 "현재 금지"에 적었다.
  ❓ 남은 것: 파일을 Assets/ 아래로 "옮기는 것" 자체가 금지 범위인지는 답이 없다.
     그래서 09-06 재생성 승인본 22종은 Docs/art_pending/ 에 보관 중이다(V05 판정 요청).
```

### 측정된 결함

```yaml
대기_진폭_규격_미달: |
  2026-09-07 · Docs/measure/anim_260907.md · 도구 MBI.Editor.AnimReport
  규격은 실루엣 높이의 4~6% (캐릭터 아트 요청 문서(15)「동작의 크기」).
  대기 11벌 중 잴 수 있는 것이 5벌뿐이고, 그 다섯도 1.3~3.5%로 전부 미달이다.
  ⚠️ 도구 자체의 결함을 먼저 잡았다 — 진폭을 알파 bbox 윗변으로 재는데
     실루엣이 캔버스에 닿아 있으면 윗변이 더 못 올라가 몸이 움직여도 0으로 나온다.
     robot_a_Idle/west 는 다섯 프레임 전부 top=0·bottom=255 였다.
     "여백 T/B" 열과 "잴 수 있나" 열을 넣어 그 행을 가른다. 잘린 행의 진폭은 하한값이다.
  뿌리: robot_a 승인본의 여백이 T7 B0 이다. 4~6%(10~15px)가 캔버스에 아예 안 들어간다.
  260907_V01 판정 요청으로 올린다 — 규격을 낮출지 · 스틸에 여백을 만들지 ·
  코드가 스프라이트를 흔들지(15-1 A-2가 발사 반동에 이미 쓰는 방식) 중 어느 것인지.
```


```yaml
ammo_silhouette_overlap:  # 보드 3-1-1 판정선 = 0.90 초과면 실패
  ammo_standard vs ammo_explosive: 0.971   # 사실상 같은 원
  ammo_pierce vs 나머지: 0.65              # 통과
  fix_per_doc: 품목 문서 I-3 — "폭발탄을 크기로 더 벌린다"
```

---

## 7. BACKLOG — 재생성 이후

```yaml
1: ✅ **로봇 3종 애니메이션 27벌 완료 (2026-09-07 · 4e332ea)**
   robot_a 10 · robot_b 10 · fusion 7. 전부 256×256 · 대기 5 · 이동 6 · 사망·태그 9.
   파이프라인: create_character mode=v3 + reference_image_url 로 8방향 회전 →
               animate_character mode=v3 로 벌마다 생성 → 캐릭터 zip 으로 내려받아 전개
   캐릭터 id: robot_a 1b1117e3 · robot_b 888deba4 · fusion 8ec7231b
   남면 검증: OverlapReport 로 a_origin × a_south = 1.000 (Docs/measure/overlap_260907_rotcheck_a.md)
   09-04 초안(96dafd5)은 지웠다 — 캔버스 96~236 제각각 · 09-05 동작 규격 이전. boss·mob 은 그대로
   재생 배선도 함께 들어갔다 (315b38d) — SpriteFrameAnimator · LoadAnimClips · CombatEntityView
   ⚠️ **대기 진폭이 규격 미달이다** — 자세한 것은 아래 "측정된 결함"
1b: 합체 256 전투 스틸 ✅ 생성·승인 (2026-09-07 · 365c34e · job 47cdb935 · md5 b3ef28a8)
    그전까지 이 자리는 512 승인본의 NEAREST 축소본이었다
2: ✅ **승인본 23개 파일 이동 완료 (2026-09-07 · df255b8)** — W05 2-1 판정 (가).
   Docs/art_pending/ 삭제 · 기록은 Docs/art_manifest.md 로 옮겼다(md5 열은 도구가 찍는다).
   증빙: overlap_260907_moved.md 의 md5 열이 overlap_260906_pending.md 와 완전 일치.
   boss_512.png → Units/boss.png 로 개명 (LoadArt 규칙). 보스는 아직 미배선.
   **남은 것은 임포트 설정 확정뿐이다** — 아래 "현재 금지" 참조
3: 3-7 정리 (재생성 후)
3b: 구역 이름표 둘 (W04 2-7 · W05 2-2) — **확정값이고 작다**
    (1) 색 40% → 70%. 지금은 ZoneLineColor 하나를 경계선과 이름표가 나눠 쓴다. 상수를 하나 더 만든다
    (2) 단위를 아트 픽셀로 (W05 2-2 판정 (가)). 지금은 글자만 화면 픽셀이라 확대할수록 작아진다.
        조건: ×1.00 에서 읽히는지 보고, 안 읽히면 값을 올려 UI 아트 요청 문서(20) 10장에 역기입
3c: HUD 묶음 — 애니메이션 뒤에 **한 덩어리로** (W04 7장). 나누면 같은 자리를 두 번 그린다
    (a) 회피 스택 눈금 바 + 치수 역기입
    (b) 변수 패널을 사용률 막대로 (지금은 글자만 내고 폐기된 발열 줄이 남아 있다)
    (c) 탄약 줄 — 저장 노드 재고를 탄종 칸으로
    (d) 0 나눗셈 세 경우를 숫자 `—`로 (LogisticsSimulation은 지금 수요 0에서 1.0을 낸다)
4: ✅ 진단 끝 — 버그가 아니었다. 기본 모드가 이동(Pan)이라 탭이 아무 것도 안 한다(T-7).
   대신 진짜 결함을 찾아 고쳤다 → 59cea65 (입력 가드가 한 프레임 늦어 터치에서 UI가 보드까지 눌렀다)
5: §5-7 방치 시스템 — 코드는 사슬 전체가 있고 화면에도 나온다(efca05f).
   남은 것은 **수치뿐**: 오프라인 계수·마리당 고철·기본 시급이 전부 TBD라 지금은 지급이 0이다.
   공식은 확정: 상주 스테이지 최고 파밍 시급 × min(꺼둔 시간, 36h) × 계수
6: Play 육안 확인 대기 — 배치모드가 못 보는 자리 셋
   (a) 터치에서 UI 버튼이 보드까지 눌리지 않는지 (59cea65 · efca05f)
   (b) 구역 점선·이름표가 규격대로 보이는지 — 이름표는 미색 70%가 출발점이다
       (2026-09-06 확정 · W04 2-7. 40%는 경계선에만 걸린다. 코드는 아직 40%이며 역기입 대상)
   (c) 오프라인 정산 창이 뜨는지 — 씬 재생성 후
```

### 사용자에게 넘긴 부탁 (내 할 일 목록이 아니다)

```yaml
밸런스_문서_깨진_글자_여덟: |
  화면에서 직접 고쳐야 한다. 두 번 시도해 두 번 다 앵커가 안 맞았다 —
  고치려는 글자가 전송 중에 또 깨진다 (지침 §8 · 260902_W19).
  자리만 적는다. 글자를 옮겨 적으면 그 인용이 또 깨진다.
  - 「레시피와 탄종 재정의」 레시피 표 · 기초 군수 행 — 품목 이름 "쉴드 재료"의 첫 글자
  - 같은 장 「해소된 공백 하나」 — 같은 이름이 같은 방식으로
  - 같은 장 「값 사슬이 끊긴 자리」 표 머리 — "왜 흔들리나"여야 하는 자리
  - 같은 장 「무엇이 바뀌었나」 — "바뀐다"여야 하는 자리
  - 같은 장 「하나는 그대로 산다」 — "바뀌므로"여야 하는 자리
  - 2장 「요구치 20% 하향」 — "뜬 채로"여야 하는 자리
  - 2장 「튜토리얼 전용 스테이지 신설」 — "몬스터"여야 하는 자리
  - 5-2 변신 화력 행 — "뺀 뒤"여야 하는 자리
  (여덟 자리다. 260906_V05 9-1 이 게시된 목록 — 그것과 개수가 맞아야 한다)
```

```yaml
품목_아트_요청_문서_깨진_글자: |
  2026-09-07에 찾았다. 지침 §8이 정한 두 번을 다 썼고 둘 다 전송 중에 또 깨졌다 —
  실패 응답에 실려 온 원문이 내가 보낸 글자가 바뀌어 도착한 것을 보였다.
  1-1 표의 한 자리는 고쳐 넣었고 나머지는 화면에서 고쳐야 한다.
  글자를 옮겨 적지 않고 자리만 적는다 (§8 다섯 중 다섯째).
  - 2장 「드론 2종은 이미 승인본이 있다」 문단 — "줄이면" 다음 낱말
  - 3-1 마지막 문단 첫 낱말, 그리고 같은 문단 "4-3이" 다음 낱말
  - 3-2 표 왼쪽 칸 셋째 줄 — "가로세로 비율" 뒤 두 낱말 중 앞의 것
  - 4-2 표 배터리 행 형태 칸 — "세로로 긴 상자," 다음 낱말
  - 4-3 첫 문장 첫 낱말
  - 4-3 표 관통 행 스펙 칸 끝 — "20 × 5발/초" 뒤 두 낱말 중 앞의 것
  - 4-3 표 표준 행 형태 칸 — "가늘지도" 다음 낱말
  - 4-3 표 폭발 행 형태 칸 첫 낱말
  - 4-3 마지막 문단 — "초당 출력을 100으로" 다음 낱말
  - 5-2 표 첫 행 자리 칸 — "군수 노드" 다음 낱말 (1-1의 같은 낱말은 고쳤다)
  - 5-2 표 둘째 행 소관 칸 — "노드를 탭했을 때" 다음 낱말
  - 6장 머리 주석 마지막 — "형태 한 절·색 한 절로" 다음 낱말
  - 7-1 마지막 문단 — "꽉 찬 상태에도" 다음 낱말
```

### 현재 금지 (해제 전까지 유지)

```yaml
- 로봇 3종 스틸·cutin_fusion 512 재생성 금지 (앵커 유지)
- 드론 2종 임의 재생성 금지 (시점 판정 보류)
- belt_end 재생성 금지 (2026-09-06 폐기 확정 — 파일은 남기되 쓰지 않는다)
- ~~저장·부스터 노드 재생성 보류 (V01 ❓2-1)~~ → **해제 (2026-09-06)**
    저장 `4837a128`(2차 여덟) 승인 · 부스터 3차 `56c541e4` 승인 — 여백·삼각 둘 다 통과, 21쌍 최대 0.860.
    W04 6장이 「재생성은 battery 하나뿐. 포트 셋은 새 범위 안이고 부스터 3차는 통과」로 못 박았다
- 수작업 보정 금지 (사용자 판정)
- Unity 임포트 금지 (재생성 전) — 실질은 둘뿐이다 (2026-09-06 설계 승인 · W04 2-4)
    (1) 임포트 설정(픽셀당 유닛·필터·압축)을 확정하지 않는다
    (2) .meta를 커밋하지 않는다. .gitignore에도 넣지 않는다
    배치모드가 부산물로 만드는 .meta는 이 금지와 무관하다
    ※ **파일을 Assets/ 아래에 두는 것은 이 금지가 아니다** (2026-09-07 · W05 2-1이 판정).
      23개 파일을 옮겼고 위 둘은 그대로 지킨다
- 확정치 재산출·분열탄 제거 금지 (레시피 개정이 코드에 들어간 뒤 한 번만)
- 연출 셋은 크기를 구분자로 쓰지 말 것 — 크기가 아니라 형태를 고친다 (W02 5-4)
  ※ ❓ **아직 답이 없다 (2026-09-06 확인).** 캔버스를 문서(19) 3장·9-5는 512라 하는데 리포 파일은 셋 다 384다.
     V02 2-4로 올렸고 W03·W04 어느 쪽도 이 항목을 다루지 않았다 — 닫힌 것이 아니라 잊힌 것에 가깝다.
     **지어서 닫지 않는다.** 다음 회신문 통보에 「아직 답이 없다」로 한 줄 올린다 (260906_V05 8장).
     "384"라고 적혀 있던 이 줄의 구 표기는 폐기된 구 서술을 옮긴 것이었다
```

---

## 8. USER PREFERENCES

```yaml
language: 한국어. 문서체는 평서형 단정. 과장·이모지 금지
decision_style: 번호 매긴 지시로 답한다 ("1. 진행", "1. 충돌부터 정리")
what_user_wants_reported:
  - 문서별 성공/실패
  - 원문이 예상과 달랐던 자리
  - 재열람으로 확인했는지
  - 값이 반대로 적힌 문서가 있었는지
art_tolerance: "AI로 뽑은 리소스티 나는 건 괜찮다. 어색하지만 않으면 문제 없음"
anim_note: "애니메이션을 더 큼직하게. 너무 세밀해서 거대 로봇이 아장아장 걸어다니는 느낌"
```

---

## 9. 이 파일의 유지

```yaml
update_when: 세션이 끝나거나, 판정이 내려지거나, 재생성 상태가 바뀔 때
do_not: 이 파일에 수치 확정치를 적지 않는다. 수치는 Notion 밸런스 문서가 원천이고
        여기 적으면 두 곳이 갈린다 (§2 never_invent_values 와 같은 사고)
human_translation: 요청 시 이 파일을 산문 한국어로 풀어 쓴다. 내용은 그대로 두고 형식만 바꾼다
```
