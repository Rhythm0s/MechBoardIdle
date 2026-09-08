# 아트 자산 매니페스트 — 승인본 출처 (2026-09-07)

> `Docs/art_pending/README.md`를 대신한다. **`260906_W05` 2-1이 「`README.md` 내용을 먼저 옮긴 뒤에 폴더를 지운다」고 지시했고 그 옮긴 자리가 여기다.**
> **md5 열은 사람이 적지 않는다** — 이 파일을 만들 때 스크립트가 `Assets/_Project/Art/` 실물에서 찍는다. 자산 이름은 세대가 바뀌어도 같으므로 이름만으로는 세대를 가를 수 없다.

## 이 파일이 왜 있나

**2026-09-06 재생성 승인본 전량이 세션 임시 폴더에만 있었고, 리포 `Assets/` 아래는 전부 09-04판이었다.** 측정 도구(`MBI.Editor.OverlapReport`)를 만들어 처음 돌렸을 때 드러났다 — 도구는 리포를 읽는데 나온 숫자가 회신문에 적은 값과 달랐다.

| | 리포 `Assets/` (09-04판) | 회신문에 적은 값 (임시 폴더 승인본) |
|---|---|---|
| 노드 21쌍 최대 | **0.894** (`node_booster` × `node_storage`) | 0.860 (`node_core` × `node_muni_basic`) |

**같은 자산 이름이 두 그림을 가리키고 있었다.** 회신문은 승인본을 보고 썼고 리포는 구판을 들고 있었다.

`260906_W05` 2-1이 **「옮긴다」**로 판정했다 — 옮기는 것은 임포트 설정을 확정하는 일이 아니고, 그대로 두어 얻는 것이 없기 때문이다. **2026-09-07에 23개 파일을 `Assets/_Project/Art/` 아래로 옮기고 `Docs/art_pending/`을 지웠다.**

## 목록 — 24개 파일

**재생성 승인본 22종 + `node_energy` + 합체 256 스틸(09-07)**(재생성 대상이 아니어서 09-04판이 그대로 승인본이나, 측정 세트를 온전하게 하려고 함께 둔 것). 그래서 파일은 24개고 「승인본 22종」과 어긋나지 않는다.

| 파일 (`Assets/_Project/Art/` 기준) | md5 | 출처 | job | 비고 |
|---|---|---|---|---|
| `Board/node_core.png` | `8513ac4b` | `board_v2/node_core.png` | `419617d5` |  |
| `Board/node_muni_basic.png` | `8a305963` | `board_v2/node_muni_basic_try2.png` | `1efb8e61` |  |
| `Board/node_muni_complex.png` | `52de3a98` | `gen3/node_muni_complex.png` | `4bbbae4a` |  |
| `Board/node_processing.png` | `bbaf730d` | `gen3/node_processing.png` | `a10d8962` |  |
| `Board/node_storage.png` | `e0d71a9f` | `gen3/node_storage.png` | `4837a128` |  |
| `Board/node_booster.png` | `e689eee8` | `gen5/node_booster_try3.png` | `56c541e4` | 3차에 삼각과 여백을 둘 다 얻은 것 |
| `Board/port_input.png` | `988a22bb` | `gen4/` | `97b051e5` | `260906_W04` 2-1이 셋 다 통과로 확정 |
| `Board/port_output.png` | `d1b84504` | `gen4/` | `7bc000d4` | 같은 판정 |
| `Board/port_power.png` | `299b2630` | `gen4/` | `400bb0a0` | 같은 판정 |
| `Board/node_energy.png` | `c84f9828` | — (재생성 대상 아님) | — | **09-04판이 그대로 승인본이다.** 측정 세트를 온전하게 하려고 2026-09-06에 함께 두었다 |
| `Items/ammo_explosive.png` | `a57ccf07` | `items_v3/` | — |  |
| `Items/ammo_pierce.png` | `3c0cb423` | `items_v3/` | — |  |
| `Items/ammo_standard.png` | `e3a47549` | `items_v3/` | — |  |
| `Items/basic_parts.png` | `2ec59118` | `items_v3/` | — |  |
| `Items/battery.png` | `16eb652c` | `gen6/` | `b540b804` | `260906_W04` 6장 지시로 09-06 재생성 · 후보 c1 |
| `Items/core_energy.png` | `b13d155e` | `items_v3/` | — |  |
| `Items/defense_material.png` | `0394fce7` | `items_v3/` | — |  |
| `Items/drone_body_parts.png` | `9911db42` | `items_v3/` | — |  |
| `Items/power_material.png` | `bd55def4` | `items_v3/` | — |  |
| `Items/propellant.png` | `14988acf` | `items_v3/` | — |  |
| `VFX/vfx_specialstrike_a.png` | `a5cbe83a` | `vfx_v2/` | `895e4c86` | ⚠️ **폐기 — 사용자 확정 2026-09-08.** 태그 스킬 연출을 갈아탄다 (아래 절) |
| `VFX/vfx_specialstrike_b.png` | `11d74516` | `vfx_v2/` | `9f1ff31d` | ⚠️ **폐기 — 사용자 확정 2026-09-08.** 같은 자리 (아래 절) |
| `Units/boss.png` | `0f536866` | `gen3/boss_512.png` | `d136d116` | **`boss_512.png`에서 이름을 바꿔 놓았다** — `CombatAssetGenerator.LoadArt`가 `Art/Units/<이름>.png`로 읽기 때문. 캔버스는 512 그대로 |
| `Units/robot_fusion_256.png` | `b3ef28a8` | `create_image_pro` 2차 | `47cdb935` | **2026-09-07 생성·승인.** 그전까지 이 자리는 512 승인본의 NEAREST 축소본(`29290d8` 「참조본」)이었다 — **256 전투 스틸은 생성된 적이 없었다.** 앵커 = 로봇 A 승인본 |

**품목 열 종의 job 번호는 `items_v3/` 일괄 생성분이라 자산별로 나뉘어 있지 않다** — `battery`만 09-06에 따로 다시 뽑았다. 없는 번호를 지어 적지 않는다.

## 연출 자산의 상태 — 폐기 셋 · 반려 넷 (2026-09-08 사용자 육안)

**연출 열한 종은 2026-09-04 초안이며 승인된 적이 없다.** 2026-09-08 육안에서 일곱이 갈렸다. **파일은 지우지 않는다** — 삭제는 사용자 몫이고, 여기는 상태만 적는다.

| 식별자 | 상태 | 사유 · 다음 |
|---|---|---|
| `cutin_fusion` | **폐기 (사용자 확정)** | 초안의 모델이 **폐기된 세대**(구 로봇 A 앵커)다. 컷인 연출을 재설정한다 — 총이 있는 로봇 팔(A)과 드론 베이(B)가 합쳐지는 장면 · 참조는 현행 승인본 셋. 문안은 `260908_W04` |
| `vfx_specialstrike_a` | **폐기 (사용자 확정)** | 새 A 태그 인 = **120도 부채꼴 탄환비가 내리며 360도 회전** |
| `vfx_specialstrike_b` | **폐기 (사용자 확정)** | 새 B 태그 인 = **밝은 레이저 한 줄기가 맵을 360도 회전** |
| `vfx_dronelaunch` | **초안 · 사용자 반려** | 미사일로 보인다. 문안은 `260908_W04` |
| `vfx_booster` | **초안 · 사용자 반려** | 폭발로 보인다 — 애프터버너처럼 **한 방향 연소**여야 한다 |
| `vfx_droneexpire` | **초안 · 사용자 반려** | 배터리가 **펑** 하고 작게 터지는 느낌이어야 한다 |
| `vfx_ammoout` | **초안 · 사용자 반려** | 폭발 같고 **경고 느낌이 없다** — 문안은 「벨트 위 상자 + 굵은 사선」인데 초안에 사선이 없다 |

**남은 넷**(`vfx_muzzleflash` · `vfx_shelleject` · `vfx_death` · `vfx_burst`)**은 이번 육안에서 갈리지 않았다** — 승인도 반려도 아니고 초안 그대로다.

## 필수 자산이 일곱에서 여섯이 됐다 (2026-09-08 · `260908_W04` 3-4)

`vfx_specialstrike_a` · `_b` 둘이 빠지고 **`vfx_tagbullet` 하나가 들어왔다.**

| 식별자 | 캔버스 | 상태 | 무엇 |
|---|---|---|---|
| **`vfx_tagbullet`** | **64 × 64** (실루엣 약 38) | **미생성 — 문안은 나왔다** | 내려꽂히는 탄환 한 발. **탄환비는 이 한 발을 코드가 부채꼴로 뿌리고 돌려** 만든다 |

⚠️ **레이저(B 태그 인)는 자산이 없다** — 전부 코드 드로잉이라 이 목록에 들어오지 않는다.

⚠️ **캔버스를 64로 둔 근거.** 도구 하한은 **16**이라 38도 받는다(아트 로그 3-15 · `2716797`). W04 3-2가 「64가 되면 64」로 정했고, 38은 도구가 캔버스를 채우는 성질 때문에 탄환이 테두리에 닿기 쉽다.

⚠️ **위 일곱은 이 파일의 「목록 — 24개 파일」에 들어 있지 않다** — 그 목록은 재생성 승인본만 담기 때문이다. `vfx_specialstrike_a/b` 둘만 예외로 거기 있고, 두 줄에 폐기 표기를 붙였다.

## 임포트에 대한 금지는 그대로 살아 있다

`260906_W04` 2-4가 확정한 금지의 실질 둘은 **옮긴 뒤에도 그대로다.**

- **`.meta`는 커밋하지 않는다** — Unity 에디터가 만들지만 리포에 넣지 않는다
- **`.gitignore`에도 넣지 않는다** — 금지를 규칙으로 굳히는 것이 아니라 지금은 넣지 않기로 한 것이다

## 옮긴 것을 무엇으로 확인했나

`MBI.Editor.OverlapReport.Run`을 `-artRoot Assets/_Project/Art`로 다시 돌려 **자산별 md5가 `Docs/measure/overlap_260906_pending.md`(승인본 세트를 잰 것)와 같은지** 본다. 다르면 아직 안 옮겨진 자산이 남은 것이다.

