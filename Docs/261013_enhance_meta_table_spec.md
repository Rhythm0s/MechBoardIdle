# ENHANCE · META 표 — 사용자가 채울 최종 칸 (2026-10-13 · 답 넷 반영)

사용자가 `Tables\ENHANCE_DATA.xlsx` · `META_DATA.xlsx` 와 `ECONOMY` 한 줄을 넣으면
내가 변환 → 배선 → `balance_v4.json` 읽기 경로 제거 → 회귀 → 빌드 한다(09-18 분업).

**여기 적힌 값은 지금 쓰고 있는 값 그대로다 — 값을 바꾸는 자리가 아니다.**

내보내는 곳: `Assets/_Project/GameData/ENHANCE_DATA.csv` · `META_DATA.csv` (UTF-8).

---

## 공통 — 열 꼴

이미 있는 `ECONOMY_DATA` · `LOGISTICS_DATA` 와 **같은 꼴**이다. 그래야 이미 쓴
`GameDataTables.ApplyFieldTable` 하나가 그대로 읽는다(새 로더를 안 만든다).

| 열 | 뜻 |
|---|---|
| `ID` | 1 부터 차례. 뜻은 없고 줄을 가리키는 번호다 |
| `FieldName` | **자산의 칸 이름 그대로**. 틀리면 로더가 「모르는 칸」 경고를 내고 넘긴다 |
| `Value` | 값 하나. **형은 안 적는다** — 자산의 칸이 형을 안다 |
| `Confirmed` | 확정 1 · 미확정 0 |
| `Basis` | 그 수의 근거(문서·회신 번호). 비워도 로더는 돈다 |

---

## 1. `ENHANCE_DATA` — 강화 앵커 (칸 넷)

✅ **코드는 이미 바꿔 두었다**(사용자 확정 ① ⓐ). `BalanceConfig` 가 `enhBandX`·`enhBandY`
두 칸을 들고, `enhBand` 는 **둘을 합쳐 내는 읽기 전용**이 됐다 — 읽는 코드와 시험은
종전처럼 `enhBand.x`·`enhBand.y` 를 쓴다. 표는 **그대로 적기만** 하면 된다.

| ID | `FieldName` | `Value` | `Confirmed` | `Basis` | 지금 json 자리 |
|---|---|---|---|---|---|
| 1 | `enhBandX` | `1.3` | `1` | enhance 블록 | `enhance.enhBand[0]` |
| 2 | `enhBandY` | `1.5` | `1` | enhance 블록 | `enhance.enhBand[1]` |
| 3 | `snapBand` | `0.08` | `1` | enhance 블록 | `enhance.snapBand` |
| 4 | `s4Cost` | `100` | `1` | enhance 블록 | `enhance.s4Cost` |

🗑️ `enhance` 의 나머지 넷(`s3BreakRetired` · `s4BandRetired` · `enhPoint` · `check7`)은
**안 옮긴다** — `BalanceConfig` 가 안 읽는다. `s3Break`·`s4Band` 미러는 `260910_W02` 에서
이미 폐기됐다. 시트에 넣어도 로더가 「모르는 칸」으로 넘기므로 탈은 없다.

---

## 2. `META_DATA` — 원천 표식 (칸 둘)

| ID | `FieldName` | `Value` | `Confirmed` | `Basis` |
|---|---|---|---|---|
| 1 | `schemaVersion` | `4.0` | `1` | meta 블록 |
| 2 | `exportedAt` | **비워 둔다** | `1` | 변환기가 채운다 |

⚠️ **이 둘은 수가 아니라 글자다.** `BalanceConfig` 의 두 칸이 `string` 이므로 로더가
글자로 넣는다 — 따옴표 없이 적는다.

### `exportedAt` — **시트에는 비워 두시고, 변환기가 찍는다** (사용자 확정 ②)

🗑️ 구 값 `TBD_STAMP_ON_EXPORT` 는 **폐기**다. 내보낼 때 날짜를 찍기로 한 자리인데
한 번도 안 찍혔다 — 자리표시가 그대로 자산까지 들어와 있었다.

**꼴은 내가 정한다**(명세에 적으라 하셨다):

    ISO 8601 · UTC · 초까지 · 예: 2026-10-13T08:41:07Z

까닭 셋 — ① 사람이 읽어도 기계가 읽어도 같은 뜻이다 ② 지역 시각은 **같은 순간을
다른 글자로** 적어 어느 판이 최신인지 못 가린다 ③ 글자 순서가 곧 시간 순서라
표를 정렬하면 판이 정렬된다.

시트의 그 칸이 **비어 있으면** 변환기가 찍고, **채워져 있으면 그대로 둔다** —
손으로 적은 판을 변환기가 덮으면 안 된다.

---

## 3. `ECONOMY_DATA` — 한 줄 더한다

✅ **코드는 이미 바꿔 두었다**(사용자 확정 ③). `offlineCapHours` 칸이
`BalanceConfig` → **`EconomyConfig`** 로 옮겨 갔다. 오프라인 값 셋(상한 · 계수 ·
기본 시급)이 **한 자산 · 한 표**에 산다. `Target` 열은 안 만들었다.

지금 네 줄 아래에 한 줄을 더한다:

| ID | `FieldName` | `Value` | `Confirmed` | `Basis` |
|---|---|---|---|---|
| 5 | `offlineCapHours` | `36` | `1` | economy.offline |

✅ **확정 1 이다**(2026-10-13 재판정 · 닫힘). 원천은 `balance_v4.json` 의
`economy.offline.capHours = 36` 이고, `BalanceAnchorTests.Anchor6` 이 그 원천을 재고 있다.
**설계 역기입 자리가 아니다** — 옮기는 것은 칸뿐이고 값도 확정 여부도 안 바뀐다.

### ⚠️ 기록 — 이 줄은 내가 틀린 보고 위에서 한 번 잘못 정해졌다

앞선 보고(`eaadbdf` · `261013_table_wiring_block.log`)에서 저는

> `balance_v4.json` 의 `economy.offline` 에는 `capHours` 라는 수가 없다 —
> 그래서 36 은 C# 기본값이다

라고 적었습니다. **거짓입니다.** 실제 키는 아홉이고 그 안에 `capHours = 36` 이 있습니다:

    formula · hourlyRate · lap · record · payoutKey · capHours · currency · note · docRef

앞 200 자만 찍어 보고 거기 보인 셋만으로 「글뿐」이라 단정했습니다 — 잘린 출력을
전부로 읽었습니다. 📌 **시험이 이미 알고 있었습니다** —
`BalanceAnchorTests.Anchor6_OfflineCapHours_Is36_MirrorsSource` 가 `원천 = 36` 을 재며
초록이었는데, 제 보고와 어긋나는 그것을 안 보고 보고부터 했습니다.

답 ④ 는 처음에 「`Confirmed 0` · 미확정 · 설계 역기입」으로 정해졌는데, 그것은
**제가 준 틀린 전제 위에서 내려진 판단**이었습니다. 정정을 올린 뒤 **재판정되어
`Confirmed 1` 로 닫혔습니다**(2026-10-13 · 위 표가 그 값입니다).

(답 ③ 칸 이동은 이 오류와 무관하게 그대로 옳습니다. 값을 모으는 일이지
확정 여부와 상관이 없습니다.)

### 📌 이 일에서 남길 것 둘

- **출력이 잘리면 전부를 다시 연다.** 앞 200 자는 전부가 아니다.
- **보고가 초록 시험과 어긋나면, 틀린 쪽은 보고라고 먼저 의심한다.**
  시험은 이미 「원천 = 36」을 재고 있었다.

---

## 이미 끝난 코드 쪽

| | 커밋 |
|---|---|
| 미확정치 표 셋 배선 · 구 규칙 폐기 · `robotHp` 건너뜀 | `0f1d71a` |
| `enhBand` 두 칸 + 읽기 전용 · `offlineCapHours` 이동 | 이 커밋 |

회귀 — 하네스 S0~S5 **글자까지 같다**(170 · 5.7 · 80.3 · 98.8 · 73.6 · 83.0 · 47.4) ·
시험 **1400 · 통과 1394 · 실패 0 · 건너뜀 6**.

자산 diff는 **꼴과 자리만** 바뀌었고 값은 그대로다:

    BalanceConfig:  enhBand {x:1.3,y:1.5}  →  enhBandX 1.3 · enhBandY 1.5
    BalanceConfig:  offlineCapHours 36     →  (빠짐)
    EconomyConfig:  (없었음)               →  offlineCapHours 36

## 시트가 들어오면 내가 할 일

1. xlsx → CSV 변환(`exportedAt` 이 비어 있으면 그때 찍는다)
2. `ENHANCE_DATA` · `META_DATA` 를 생성기에 걸기
3. `balance_v4.json` 읽기 경로 제거(그때 비로소 표가 전부 덮는다)
4. 회귀 — 하네스 S0~S5 같은 수 · SO diff · 시험 → 빌드
