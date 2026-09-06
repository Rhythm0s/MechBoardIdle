# 승인본인데 `Assets/`에 없는 아트 — 보관 (2026-09-06)

## 왜 이 폴더가 생겼나

**2026-09-06 재생성 승인본 전량이 세션 임시 폴더에만 있었다.** `Assets/_Project/Art/` 아래 파일은 전부 **09-04판**이다.

측정 도구(`MBI.Editor.OverlapReport`)를 만들어 처음 돌렸을 때 드러났다 — 도구는 리포의 `Assets/` 를 읽는데, 나온 숫자가 회신문에 적은 값과 달랐다.

| | 리포 `Assets/` (09-04판) | 회신문에 적은 값 (임시 폴더 승인본) |
|---|---|---|
| 노드 21쌍 최대 | **0.894** (`node_booster` × `node_storage`) | 0.860 (`node_core` × `node_muni_basic`) |
| `node_energy` 여백 | **0** (네 변 중 최소) | — |

**같은 자산 이름이 두 가지 그림을 가리키고 있었다.** 회신문은 승인본을 보고 썼고 리포는 구판을 들고 있었다.

## 왜 `Assets/`에 안 넣고 여기 두는가

「Unity 임포트 금지」가 아직 살아 있다. `260906_W04` 2-4가 그 금지의 실질을 **「임포트 설정을 확정하지 말 것 · `.meta`는 커밋하지 말 것」**으로 확정했지만, **파일을 `Assets/` 아래로 옮기는 것 자체가 그 범위인지는 답이 없다.**

그래서 **판정 전까지 잃지 않는 것**만 한다. `Docs/` 아래는 Unity가 임포트하지 않으므로 설정이 굳지 않는다.

⚠️ **이 폴더는 임시다.** 어디로 갈지가 정해지면 옮기고 이 폴더는 지운다 — 같은 그림이 두 곳에 있는 상태를 오래 두지 않는다.

## 예외 하나 — `battery`

`Assets/_Project/Art/Items/battery.png`는 **09-06판으로 갈아 끼웠다.** `260906_W04` 6장이 재생성을 지시한 유일한 자산이고, 이미 추적 중인 파일의 내용을 갱신하는 것이라 새 임포트가 아니다. 여기 사본도 같은 그림이다.

## 목록

| 파일 | 출처 | job |
|---|---|---|
| `Board/node_core.png` | `board_v2/node_core.png` | `419617d5` |
| `Board/node_muni_basic.png` | `board_v2/node_muni_basic_try2.png` | `1efb8e61` |
| `Board/node_muni_complex.png` | `gen3/node_muni_complex.png` | `4bbbae4a` |
| `Board/node_processing.png` | `gen3/node_processing.png` | `a10d8962` |
| `Board/node_storage.png` | `gen3/node_storage.png` | `4837a128` |
| `Board/node_booster.png` | `gen5/node_booster_try3.png` | `56c541e4` — 3차에 삼각과 여백을 둘 다 얻은 것 |
| `Board/port_input.png` · `port_output.png` · `port_power.png` | `gen4/` | `97b051e5` · `7bc000d4` · `400bb0a0` — `260906_W04` 2-1이 셋 다 통과로 확정 |
| `Items/*.png` (10종) | `items_v3/` · `battery`만 `gen6` | 품목 전량 재생성분 |
| `VFX/vfx_specialstrike_a.png` · `_b.png` | `vfx_v2/` | `895e4c86` · `9f1ff31d` |
| `Units/boss_512.png` | `gen3/boss_512.png` | `d136d116` |

**`node_energy`는 재생성 대상이 아니었다** — 리포의 09-04판이 그대로 승인본이다. **다만 2026-09-06에 이 폴더로 복사해 넣었다** — 이 폴더가 「승인본 세트」로서 온전해야 측정 도구가 노드 21쌍을 온전히 잴 수 있기 때문이다. 여섯만 두면 15쌍밖에 안 나온다. 리포본과 내용이 같으며 보고서의 md5가 그것을 보인다.

## 이 폴더가 없어질 때 확인할 것

옮긴 뒤 `MBI.Editor.OverlapReport.Run`을 다시 돌려 `Docs/measure/`의 숫자가 회신문 값과 맞는지 본다. 안 맞으면 아직 옮겨지지 않은 자산이 남은 것이다.
