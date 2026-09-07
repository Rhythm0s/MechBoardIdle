# 실루엣 측정 보고서

- 생성 시각: 2026-09-07 09:28
- 도구 커밋: `365c34e`
- **잰 자리: `C:/Users/Kang/AppData/Local/Temp/claude/C--Users-Kang-OneDrive------/f8e6479d-8ac9-4bf4-bdff-4bcd45bf04a4/scratchpad/rot_check`** (`-artRoot`로 바꾼다)
- 측정법: **두 스프라이트를 각자의 캔버스 그대로 겹쳐, 알파가 함께 차 있는 넓이를 둘을 합친 넓이로 나눈다.** 잘라내지 않고 늘이지도 않는다
- 알파 문턱: 16 초과를 「있다」로 본다
- 계산: `MBI.Core.SilhouetteOverlap` · 정답 케이스는 `SilhouetteOverlapTests`가 고정한다
- ⚠️ 도구 커밋은 **실행 시점의 HEAD**다. 도구를 고치고 커밋 전에 돌리면 이 해시가 가리키는 커밋에는 그 도구가 없다 — 2026-09-06에 실제로 그렇게 나왔다. **잰 파일이 무엇인지는 아래 표의 md5가 말한다**

## 품목 — 5종

| 자산 | md5 | 캔버스 | 여백 (L R T B) | 가장 좁은 변 | 가로세로비 |
|---|---|---|---|---|---|
| `a_east` | `f38854df` | 256×256 | L52 R57 T0 B0 | **0** | 0.57 |
| `a_north` | `2cae1622` | 256×256 | L8 R1 T0 B6 | **0** | 0.99 |
| `a_origin` | `ecffb0de` | 256×256 | L0 R11 T7 B0 | **0** | 0.98 |
| `a_south` | `172853d0` | 256×256 | L0 R11 T7 B0 | **0** | 0.98 |
| `a_west` | `158b8ba3` | 256×256 | L28 R59 T0 B0 | **0** | 0.66 |

**10쌍 · 상한 0.90 초과 1쌍**

| 쌍 | 겹침 | |
|---|---|---|
| `a_origin` × `a_south` | 1.000 | **초과** |
| `a_north` × `a_origin` | 0.704 |  |
| `a_north` × `a_south` | 0.704 |  |
| `a_east` × `a_west` | 0.667 |  |
| `a_east` × `a_north` | 0.374 |  |
| `a_east` × `a_origin` | 0.367 |  |
| `a_east` × `a_south` | 0.367 |  |
| `a_north` × `a_west` | 0.347 |  |
| `a_origin` × `a_west` | 0.339 |  |
| `a_south` × `a_west` | 0.339 |  |

