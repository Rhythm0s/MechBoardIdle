# ENHANCE · META 표 — 채울 칸 명세 (2026-10-13)

사용자가 xlsx 시트를 채우면 내가 걸고 `balance_v4.json` 읽기 경로를 건는다(09-18 분업).
**지금 json 에 있는 값을 그대로 옮겨 적는다 — 값을 바꾸는 자리가 아니다.**

내보내는 곳: `Assets/_Project/GameData/ENHANCE_DATA.csv` · `META_DATA.csv` (UTF-8).

---

## 공통 — 열 꼴

이미 있는 `ECONOMY_DATA` · `LOGISTICS_DATA` 와 **같은 꼴**로 맞춘다. 그래야 내가 쓴
`GameDataTables.ApplyFieldTable` 하나가 그대로 읽는다(새 로더를 안 만든다).

| 열 | 뜻 |
|---|---|
| `ID` | 1 부터 차례. 뜻은 없고 줄을 가리키는 번호다 |
| `FieldName` | **자산의 칸 이름 그대로**. 틀리면 내 로더가 「모르는 칸」 경고를 내고 넘긴다 |
| `Value` | 값 하나. **형은 안 적는다** — 자산의 칸이 형을 안다 |
| `Confirmed` | 확정 1 · 미확정 0 |
| `Basis` | 그 수의 근거(문서·회신 번호). 비워도 로더는 돈다 |

⚠️ **`FieldName` 은 자산 칸 이름이지 json 키가 아니다.** 아래 표에 둘을 나란히 적었다.

---

## 1. `ENHANCE_DATA` — 강화 앵커

지금 `BalanceConfig` 가 `json.enhance` 에서 받는 셋이다.

| ID | `FieldName` | `Value` | `Confirmed` | 지금 json 자리 | 비고 |
|---|---|---|---|---|---|
| 1 | `enhBandX` | `1.3` | 1 | `enhance.enhBand[0]` | ⚠️ 아래 ※1 |
| 2 | `enhBandY` | `1.5` | 1 | `enhance.enhBand[1]` | ⚠️ 아래 ※1 |
| 3 | `snapBand` | `0.08` | 1 | `enhance.snapBand` | |
| 4 | `s4Cost` | `100` | 1 | `enhance.s4Cost` | |

※1 **`enhBand` 는 자산에서 `Vector2` 한 칸**(`enhBand: {x: 1.3, y: 1.5}`)이다.
이 꼴의 표는 **칸 하나에 값 하나**만 담으므로 벡터를 통째로 못 넣는다. 둘 중 하나다 —

- **ⓐ 두 줄로 나눈다**(위 표대로 `enhBandX` · `enhBandY`). 그러면 **내가 `BalanceConfig`
  에 그 두 칸을 만들고 `enhBand` 는 둘을 합쳐 내는 읽기 전용으로 바꾼다.** 코드 변경이 있다.
- **ⓑ 한 줄에 `1.3,1.5` 로 적는다.** 그러면 **내가 로더에 벡터 갈래를 더한다.**
  코드 변경은 더 작지만, 표에 「쉼표로 둘」이라는 **적지 않은 약속**이 생긴다.

📌 **나는 ⓐ 를 권한다** — 표를 보는 사람이 약속을 몰라도 읽히고, 「한 칸 한 값」이
이 표 꼴의 뜻이기 때문이다. **어느 쪽인지 정해 주셔야 내가 짓는다.**

🗑️ `enhance` 의 나머지 넷(`s3BreakRetired` · `s4BandRetired` · `enhPoint` · `check7`)은
**안 옮긴다** — `BalanceConfig` 가 안 읽는다. `s3Break`·`s4Band` 미러는 `260910_W02` 에서
이미 폐기됐다. 시트에 넣고 싶으면 넣어도 되지만 로더가 「모르는 칸」으로 넘긴다.

---

## 2. `META_DATA` — 원천 표식

지금 `BalanceConfig` 가 `json.meta` 에서 받는 둘이다.

| ID | `FieldName` | `Value` | `Confirmed` | 지금 json 자리 |
|---|---|---|---|---|
| 1 | `schemaVersion` | `4.0` | 1 | `meta.schemaVersion` |
| 2 | `exportedAt` | `TBD_STAMP_ON_EXPORT` | **0** | `meta.exportedAt` |

⚠️ **이 둘은 수가 아니라 글자다.** `BalanceConfig` 의 두 칸이 `string` 이므로 로더가
글자로 넣는다 — 따옴표 없이 적는다.

⚠️⚠️ **`exportedAt` 은 한 번도 안 찍혔다.** json 의 값이 글자 그대로
`TBD_STAMP_ON_EXPORT` 다 — 내보낼 때 날짜를 찍기로 한 자리인데 **찍은 적이 없다.**
그래서  를 0 으로 적었다.

⚠️ **`exportedAt` 은 「표를 언제 내보냈나」로 뜻이 바뀐다.** 지금은 json 을 내보낸
때인데, 원천이 표로 옮겨 가면 **표를 내보낸 때**가 맞다. 값을 그대로 옮기든 새로
적든 **사용자가 정한다** — 나는 안 정한다.

---

## 3. `offlineCapHours` 36 의 임자 — **`ECONOMY_DATA`** 로 가자

판단해 달라고 하신 자리다. **`ECONOMY_DATA` 에 한 줄 더하는 쪽**을 권한다.

| ID | `FieldName` | `Value` | `Confirmed` | 비고 |
|---|---|---|---|---|
| 5 | `offlineCapHours` | `36` | 0 | `ECONOMY_DATA` 에 **더한다** |

까닭 셋 —

1. **뜻이 경제다.** 방치 보상 상한이고, `ECONOMY_DATA` 에 이미 `offlineCoefTbd` ·
   `offlineBaseRateTbd` 가 있다. 같은 묶음의 셋째 값이 딴 표에 살 까닭이 없다.
2. **`META` 는 「표가 언제·어느 판인가」를 적는 자리**다. 게임 수를 담으면 뜻이 흐려진다.
3. 자산 임자가 같다 — 셋 다 `EconomyConfig` 가 아니라… ⚠️ **여기 함정이 하나 있다**(아래).

### ⚠️ `offlineCapHours` 는 자산 임자가 다르다

`offlineCoefTbd` · `offlineBaseRateTbd` 는 **`EconomyConfig`** 에 살고,
`offlineCapHours` 는 **`BalanceConfig`** 에 산다. 한 표가 **두 자산**에 부으려면
내 `ApplyFieldTable` 이 「이 줄은 어느 자산인가」를 알아야 한다.

그래서 **표에 열 하나를 더 두는 쪽**을 권한다 —

| 열 | 뜻 |
|---|---|
| `Target` | 값을 받을 자산 이름(`EconomyConfig` · `BalanceConfig` …). **비우면 그 표의 기본 자산** |

그러면 `ECONOMY_DATA` 의 넷은 `Target` 을 비워 두고, `offlineCapHours` 줄만
`Target = BalanceConfig` 로 적으면 된다. **열을 더하는 것은 내가 로더를 고치면 되는
일이고, 지금 있는 표 넷은 `Target` 이 비어 있어도 그대로 돈다.**

⚠️ 이것도 **정해 주셔야 짓는다** — 열을 더할지, 아니면 `offlineCapHours` 를
`EconomyConfig` 로 옮길지(그러면 코드가 그 칸을 옮겨 읽어야 한다).

### ⚠️ 그리고 36 은 지금 **어디서도 안 온다**

`BalanceAssetGenerator` 는 `json.economy.offline.capHours > 0f` 일 때만 쓰는데,
json 의 `economy.offline` 은 **글(formula · hourlyRate · lap)뿐이고 `capHours` 라는 수가
없다.** 조건이 참이 되는 일이 없으므로 **36 은 C# 기본값**이다.
표에 36 을 적으면 **그때 처음으로 원천이 생긴다.** 36 이 맞는 수인지는 내가 모른다.

---

## 내가 기다리는 답 넷

1. `enhBand` — **ⓐ 두 줄**(`enhBandX`/`enhBandY`) 인가 **ⓑ 한 줄 `1.3,1.5`** 인가
2. `exportedAt` — json 값 그대로인가, 표 내보낸 때로 새로 적는가
3. `offlineCapHours` — `Target` 열을 더하는가, 칸을 `EconomyConfig` 로 옮기는가
4. **36 이 맞는 수인가** (지금은 C# 기본값일 뿐 원천이 없다)

시트가 들어오면 걸고, `balance_v4.json` 읽기 경로를 건고, 회귀(하네스 S0~S5 · SO diff ·
시험)를 돌려 보고한다.
