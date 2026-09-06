# 실루엣 측정 보고서

- 생성 시각: 2026-09-06 22:57
- 도구 커밋: `f46fce0`
- 측정법: **두 스프라이트를 각자의 캔버스 그대로 겹쳐, 알파가 함께 차 있는 넓이를 둘을 합친 넓이로 나눈다.** 잘라내지 않고 늘이지도 않는다
- 알파 문턱: 16 초과를 「있다」로 본다
- 계산: `MBI.Core.SilhouetteOverlap` · 정답 케이스는 `SilhouetteOverlapTests`가 고정한다

## 노드 — 7종

| 자산 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |
|---|---|---|---|---|
| `node_booster` | 192×192 | L13 R16 T14 B15 | **13** | 1.00 |
| `node_core` | 192×192 | L18 R9 T14 B19 | **9** | 1.04 |
| `node_energy` | 192×192 | L1 R1 T0 B2 | **0** | 1.00 |
| `node_muni_basic` | 192×192 | L27 R25 T11 B19 | **11** | 0.86 |
| `node_muni_complex` | 192×192 | L4 R16 T15 B8 | **4** | 1.02 |
| `node_processing` | 192×192 | L12 R18 T30 B20 | **12** | 1.14 |
| `node_storage` | 192×192 | L17 R19 T19 B20 | **17** | 1.02 |

**21쌍 · 상한 0.90 초과 0쌍**

| 쌍 | 겹침 | |
|---|---|---|
| `node_booster` × `node_storage` | 0.894 |  |
| `node_core` × `node_muni_basic` | 0.853 |  |
| `node_booster` × `node_energy` | 0.849 |  |
| `node_processing` × `node_storage` | 0.842 |  |
| `node_energy` × `node_storage` | 0.825 |  |
| `node_booster` × `node_muni_complex` | 0.816 |  |
| `node_muni_complex` × `node_storage` | 0.815 |  |
| `node_muni_basic` × `node_processing` | 0.810 |  |
| `node_core` × `node_muni_complex` | 0.804 |  |
| `node_core` × `node_processing` | 0.796 |  |
| `node_core` × `node_storage` | 0.793 |  |
| `node_muni_basic` × `node_storage` | 0.782 |  |
| `node_energy` × `node_muni_complex` | 0.776 |  |
| `node_muni_basic` × `node_muni_complex` | 0.775 |  |
| `node_muni_complex` × `node_processing` | 0.771 |  |
| `node_booster` × `node_processing` | 0.770 |  |
| `node_booster` × `node_core` | 0.738 |  |
| `node_energy` × `node_processing` | 0.714 |  |
| `node_booster` × `node_muni_basic` | 0.709 |  |
| `node_core` × `node_energy` | 0.688 |  |
| `node_energy` × `node_muni_basic` | 0.655 |  |

## 품목 — 10종

| 자산 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |
|---|---|---|---|---|
| `ammo_explosive` | 64×64 | L7 R7 T7 B7 | **7** | 1.00 |
| `ammo_pierce` | 64×64 | L23 R23 T7 B8 | **7** | 0.37 |
| `ammo_standard` | 64×64 | L15 R15 T17 B17 | **15** | 1.13 |
| `basic_parts` | 64×64 | L9 R9 T14 B14 | **9** | 1.28 |
| `battery` | 64×64 | L16 R16 T3 B3 | **3** | 0.55 |
| `core_energy` | 64×64 | L10 R9 T13 B13 | **9** | 1.18 |
| `defense_material` | 64×64 | L11 R11 T13 B13 | **11** | 1.11 |
| `drone_body_parts` | 64×64 | L17 R14 T14 B14 | **14** | 0.92 |
| `power_material` | 64×64 | L14 R14 T8 B8 | **8** | 0.75 |
| `propellant` | 64×64 | L16 R16 T20 B20 | **16** | 1.33 |

**45쌍 · 상한 0.90 초과 0쌍**

| 쌍 | 겹침 | |
|---|---|---|
| `core_energy` × `defense_material` | 0.854 |  |
| `ammo_explosive` × `power_material` | 0.774 |  |
| `basic_parts` × `core_energy` | 0.760 |  |
| `basic_parts` × `defense_material` | 0.757 |  |
| `battery` × `power_material` | 0.755 |  |
| `defense_material` × `power_material` | 0.718 |  |
| `ammo_standard` × `basic_parts` | 0.716 |  |
| `ammo_standard` × `drone_body_parts` | 0.713 |  |
| `ammo_standard` × `propellant` | 0.669 |  |
| `core_energy` × `power_material` | 0.669 |  |
| `drone_body_parts` × `propellant` | 0.656 |  |
| `ammo_explosive` × `core_energy` | 0.650 |  |
| `ammo_explosive` × `battery` | 0.649 |  |
| `ammo_explosive` × `defense_material` | 0.644 |  |
| `ammo_standard` × `defense_material` | 0.609 |  |
| `ammo_standard` × `core_energy` | 0.603 |  |
| `battery` × `defense_material` | 0.586 |  |
| `basic_parts` × `drone_body_parts` | 0.566 |  |
| `defense_material` × `drone_body_parts` | 0.566 |  |
| `basic_parts` × `power_material` | 0.560 |  |
| `ammo_explosive` × `basic_parts` | 0.546 |  |
| `core_energy` × `drone_body_parts` | 0.540 |  |
| `battery` × `core_energy` | 0.535 |  |
| `ammo_pierce` × `ammo_standard` | 0.516 |  |
| `basic_parts` × `propellant` | 0.495 |  |
| `ammo_pierce` × `drone_body_parts` | 0.495 |  |
| `ammo_standard` × `power_material` | 0.475 |  |
| `ammo_pierce` × `defense_material` | 0.469 |  |
| `ammo_pierce` × `core_energy` | 0.452 |  |
| `basic_parts` × `battery` | 0.452 |  |
| `drone_body_parts` × `power_material` | 0.452 |  |
| `ammo_pierce` × `basic_parts` | 0.431 |  |
| `ammo_pierce` × `power_material` | 0.428 |  |
| `defense_material` × `propellant` | 0.420 |  |
| `core_energy` × `propellant` | 0.416 |  |
| `ammo_standard` × `battery` | 0.413 |  |
| `battery` × `drone_body_parts` | 0.394 |  |
| `ammo_explosive` × `ammo_standard` | 0.392 |  |
| `ammo_pierce` × `battery` | 0.385 |  |
| `ammo_pierce` × `propellant` | 0.374 |  |
| `ammo_explosive` × `drone_body_parts` | 0.373 |  |
| `ammo_explosive` × `ammo_pierce` | 0.356 |  |
| `power_material` × `propellant` | 0.327 |  |
| `battery` × `propellant` | 0.292 |  |
| `ammo_explosive` × `propellant` | 0.270 |  |

