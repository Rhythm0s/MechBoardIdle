# 실루엣 측정 보고서

- 생성 시각: 2026-09-06 23:43
- 도구 커밋: `fc34055`
- **잰 자리: `Docs/art_pending`** (`-artRoot`로 바꾼다)
- 측정법: **두 스프라이트를 각자의 캔버스 그대로 겹쳐, 알파가 함께 차 있는 넓이를 둘을 합친 넓이로 나눈다.** 잘라내지 않고 늘이지도 않는다
- 알파 문턱: 16 초과를 「있다」로 본다
- 계산: `MBI.Core.SilhouetteOverlap` · 정답 케이스는 `SilhouetteOverlapTests`가 고정한다
- ⚠️ 도구 커밋은 **실행 시점의 HEAD**다. 도구를 고치고 커밋 전에 돌리면 이 해시가 가리키는 커밋에는 그 도구가 없다 — 2026-09-06에 실제로 그렇게 나왔다. **잰 파일이 무엇인지는 아래 표의 md5가 말한다**

## 노드 — 7종

| 자산 | md5 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |
|---|---|---|---|---|---|
| `node_booster` | `e689eee8` | 192×192 | L16 R21 T8 B17 | **8** | 0.93 |
| `node_core` | `8513ac4b` | 192×192 | L4 R4 T5 B4 | **4** | 1.01 |
| `node_energy` | `c84f9828` | 192×192 | L1 R1 T0 B2 | **0** | 1.00 |
| `node_muni_basic` | `8a305963` | 192×192 | L12 R4 T10 B11 | **4** | 1.03 |
| `node_muni_complex` | `52de3a98` | 192×192 | L12 R12 T14 B13 | **12** | 1.02 |
| `node_processing` | `bbaf730d` | 192×192 | L6 R6 T37 B29 | **6** | 1.43 |
| `node_storage` | `e0d71a9f` | 192×192 | L54 R54 T10 B7 | **7** | 0.48 |

**21쌍 · 상한 0.90 초과 0쌍**

| 쌍 | 겹침 | |
|---|---|---|
| `node_core` × `node_muni_basic` | 0.860 |  |
| `node_energy` × `node_muni_basic` | 0.824 |  |
| `node_core` × `node_energy` | 0.824 |  |
| `node_core` × `node_processing` | 0.760 |  |
| `node_core` × `node_muni_complex` | 0.730 |  |
| `node_muni_basic` × `node_processing` | 0.728 |  |
| `node_energy` × `node_processing` | 0.712 |  |
| `node_muni_complex` × `node_processing` | 0.708 |  |
| `node_muni_basic` × `node_muni_complex` | 0.696 |  |
| `node_energy` × `node_muni_complex` | 0.675 |  |
| `node_booster` × `node_muni_complex` | 0.606 |  |
| `node_muni_complex` × `node_storage` | 0.571 |  |
| `node_core` × `node_storage` | 0.547 |  |
| `node_booster` × `node_processing` | 0.527 |  |
| `node_muni_basic` × `node_storage` | 0.510 |  |
| `node_booster` × `node_storage` | 0.503 |  |
| `node_energy` × `node_storage` | 0.480 |  |
| `node_booster` × `node_muni_basic` | 0.480 |  |
| `node_booster` × `node_core` | 0.476 |  |
| `node_booster` × `node_energy` | 0.471 |  |
| `node_processing` × `node_storage` | 0.403 |  |

## 품목 — 10종

| 자산 | md5 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |
|---|---|---|---|---|---|
| `ammo_explosive` | `a57ccf07` | 64×64 | L0 R0 T2 B2 | **0** | 1.07 |
| `ammo_pierce` | `3c0cb423` | 64×64 | L24 R24 T4 B4 | **4** | 0.29 |
| `ammo_standard` | `e3a47549` | 64×64 | L12 R12 T22 B22 | **12** | 2.00 |
| `basic_parts` | `2ec59118` | 64×64 | L7 R7 T7 B7 | **7** | 1.00 |
| `battery` | `16eb652c` | 64×64 | L16 R16 T3 B3 | **3** | 0.55 |
| `core_energy` | `b13d155e` | 64×64 | L5 R5 T6 B6 | **5** | 1.04 |
| `defense_material` | `0394fce7` | 64×64 | L6 R6 T2 B0 | **0** | 0.84 |
| `drone_body_parts` | `9911db42` | 64×64 | L2 R2 T19 B19 | **2** | 2.31 |
| `power_material` | `bd55def4` | 64×64 | L12 R12 T10 B12 | **10** | 0.95 |
| `propellant` | `14988acf` | 64×64 | L6 R6 T18 B19 | **6** | 1.93 |

**45쌍 · 상한 0.90 초과 0쌍**

| 쌍 | 겹침 | |
|---|---|---|
| `basic_parts` × `defense_material` | 0.868 |  |
| `basic_parts` × `core_energy` | 0.859 |  |
| `core_energy` × `defense_material` | 0.858 |  |
| `ammo_explosive` × `defense_material` | 0.749 |  |
| `ammo_explosive` × `basic_parts` | 0.746 |  |
| `ammo_explosive` × `core_energy` | 0.681 |  |
| `battery` × `defense_material` | 0.645 |  |
| `basic_parts` × `battery` | 0.623 |  |
| `ammo_standard` × `propellant` | 0.620 |  |
| `power_material` × `propellant` | 0.614 |  |
| `battery` × `core_energy` | 0.611 |  |
| `core_energy` × `power_material` | 0.602 |  |
| `battery` × `power_material` | 0.601 |  |
| `ammo_standard` × `drone_body_parts` | 0.597 |  |
| `drone_body_parts` × `propellant` | 0.583 |  |
| `ammo_explosive` × `battery` | 0.562 |  |
| `ammo_standard` × `power_material` | 0.559 |  |
| `basic_parts` × `power_material` | 0.554 |  |
| `defense_material` × `power_material` | 0.548 |  |
| `core_energy` × `propellant` | 0.527 |  |
| `defense_material` × `propellant` | 0.507 |  |
| `basic_parts` × `propellant` | 0.499 |  |
| `drone_body_parts` × `power_material` | 0.422 |  |
| `ammo_pierce` × `battery` | 0.421 |  |
| `ammo_explosive` × `power_material` | 0.413 |  |
| `ammo_pierce` × `power_material` | 0.400 |  |
| `ammo_explosive` × `propellant` | 0.382 |  |
| `battery` × `propellant` | 0.372 |  |
| `core_energy` × `drone_body_parts` | 0.370 |  |
| `ammo_standard` × `core_energy` | 0.348 |  |
| `defense_material` × `drone_body_parts` | 0.331 |  |
| `basic_parts` × `drone_body_parts` | 0.322 |  |
| `ammo_standard` × `battery` | 0.318 |  |
| `ammo_standard` × `basic_parts` | 0.318 |  |
| `ammo_standard` × `defense_material` | 0.314 |  |
| `ammo_pierce` × `defense_material` | 0.313 |  |
| `ammo_pierce` × `core_energy` | 0.310 |  |
| `ammo_pierce` × `basic_parts` | 0.286 |  |
| `ammo_explosive` × `drone_body_parts` | 0.280 |  |
| `battery` × `drone_body_parts` | 0.259 |  |
| `ammo_pierce` × `ammo_standard` | 0.252 |  |
| `ammo_explosive` × `ammo_pierce` | 0.239 |  |
| `ammo_explosive` × `ammo_standard` | 0.237 |  |
| `ammo_pierce` × `propellant` | 0.233 |  |
| `ammo_pierce` × `drone_body_parts` | 0.190 |  |

