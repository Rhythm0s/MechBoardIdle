# 보드 저장 — 자료형 설계 (착수 전 · §72-43)

**목표** 재입장에 보드가 살아남고, **프리셋(③)이 같은 자료형**을 쓴다.
**제약** `SaveDataV1` 은 `JsonUtility` 로 직렬화된다 — **public 필드 + `[Serializable]`만** 된다
(프로퍼티·Dictionary·Vector2Int 중첩 배열은 안 된다).

---

## 1. 무엇을 싣는가 — 실측으로 센 것

### 노드 (`NodeInstance`)

| 상태 | 싣는가 | 왜 |
|---|---|---|
| `Definition` | ✅ `nodeId` 문자열로 | 자산 참조는 GUID 라 저장에 못 넣는다 |
| `Cell` | ✅ `x`·`y` 두 int | `Vector2Int` 는 JsonUtility 가 되지만 **평평하게 두는 편**이 판을 읽기 쉽다 |
| **`Rotation`** | ✅ int 0~3 | 이번 묶음의 이유 |
| `SelectedRecipe` | ✅ int(enum) | 조합표를 안 실으면 재입장에 **기본값으로 돌아간다** — 폭발탄 줄이 통째로 죽는다 |
| `AmmoKind` | ✅ int(enum) | 탄종이 노드별이라 안 실으면 관통으로 돌아간다 |
| 모듈 둘 | ✅ `moduleId` 문자열 둘 | 슬롯 **순서가 뜻을 갖는다**(빈 칸은 빈 문자열) |
| `OutputBuffer`·`BufferKind`·`InputBuffer` | ❌ | **진행 중인 한 회분**이다. 안 실으면 재입장에 한 사이클 늦을 뿐이고, **실으면 저장이 커지고 버그가 는다**(버퍼가 조합표와 어긋난 상태까지 되살린다) |

### 벨트 (`BeltInstance`)

| 상태 | 싣는가 | 왜 |
|---|---|---|
| `Cell` | ✅ | |
| `Element` | ✅ int(직선·코너·병합기·분류기) | |
| `InFaces`·`OutFaces` | ✅ **면 비트마스크 두 int** | 배열 둘을 중첩하면 JsonUtility 가 싫어한다. N/E/S/W = 비트 0~3 |
| `Kind` | ❌ | **표시용**이다(§72-29). 불러온 뒤 `BeltFlow.Resolve` 가 다시 정한다 |

⚠️ **벨트 위 아이템(`BeltItemFlow`)은 안 싣는다.** 흐르는 중인 물건이고, 되살리면
**정지 상태까지 되살아난다.** 재입장은 빈 벨트로 시작하는 편이 읽기 쉽다.

---

## 2. 자료형 (JsonUtility 가 삼키는 꼴)

```csharp
[Serializable] public class BoardNodeEntry {
    public string nodeId; public int x, y;
    public int rotation;          // 0~3
    public int recipe;            // RecipeKind
    public int ammo;              // AmmoKind
    public string module0, module1;
}
[Serializable] public class BoardBeltEntry {
    public int x, y;
    public int element;           // BeltElementKind
    public int inMask, outMask;   // N=1 E=2 S=4 W=8
}
[Serializable] public class BoardStateV1 {
    public int columns, rows;     // 격자가 바뀌면 못 읽는다(오늘 12→14 가 그랬다)
    public List<BoardNodeEntry> nodes = new();
    public List<BoardBeltEntry> belts = new();
}
```

`SaveDataV1` 에 `public BoardStateV1 board;` 한 칸.

---

## 3. 규칙 넷

1. **없거나 깨지면 `StartingBoard`.** 빈 보드로 떨어뜨리면 **못 고치는 상태**가 된다
   (노드가 없으면 아무것도 못 만들고, 만들 것이 없으니 못 놓는다).
2. **격자 크기를 같이 싣고 다르면 버린다.** 오늘 `Rows 13 → 14` 가 있었다 —
   좌표만 싣고 크기를 안 실으면 **옛 저장이 조용히 어긋난 자리에 놓인다.**
3. **불러온 뒤 반드시 `BeltAutoOrient.Resolve` → `BeltFlow.Resolve`.**
   면과 품목은 배치에서 다시 나온다 — 저장이 그것까지 들고 있으면 두 진실이 생긴다.
4. **프리셋은 같은 `BoardStateV1`** 이다. 심사자 바로가기 S3+ 진입이
   **저장 보드를 덮어쓴다** — 그 사실을 시험 한 줄로 못 박는다.

5. **시작 보드 세대를 같이 싣고 다르면 버린다** (2026-09-16 사용자 결정 · §74-9).
   ⚠️ **크기만으로는 못 거른다** — 09-15 에 시작 보드가 네 줄 → 두 줄로 바뀌었는데
   격자는 그대로 `12×14` 였다. 옛 배치가 새 규격 위에 **조용히** 올라와
   「벨트를 이었는데 마운트 적재 0」이 됐다(육안 9차 ⑦).
   ⚠️⚠️ **표식은 주인별이다** (2026-09-17 사용자 결정 · `260917_V01` 6-1).
   시작 보드가 로봇마다 하나이므로(조립 시스템 문서 11-7) 표식도 판마다 다르다 —
   저장을 뜰 때도 견줄 때도 **그 판 주인의 시작 보드**를 본다(`BoardGeneration.Of(owner)`).
   종전에는 둘 다 A 의 것 하나를 봐서, `StartingBoardB` 를 고쳐도 **B 저장이 안 버려졌다.**
   고치는 대가로 **그때 있던 B 저장이 한 번 버려졌다** — 사용자가 감수하기로 한 몫이다.
   표식은 **그 시작 보드 내용에서 뽑은 해시**다 — 손으로 올리는 버전 번호를 안 쓴다.
   올리는 것을 잊으면 표식이 거짓말을 하고, 그것은 표식이 없는 것보다 나쁘다.
   ⚠️ **플레이어가 늘린 배치도 같이 사라진다** — 사용자가 알고 고른 쪽이다
   (조용히 틀린 판보다 낫다). 버릴 때 **이유를 로그 한 줄로** 남긴다.

---

## 3-2. 이 저장 **밖**인 것 — 경계를 못 박는다

**마운트 적재 · 창고 재고 · 회피 스택은 보드가 아니라 전투 상태다.** 보드 저장에 안 든다.

⚠️ **정정 — 그것들은 `SaveDataV1` 기존 층에도 없다.** 실제로 거기 있는 것은
`scrap` · `enhMaterial` · `lastFarmStageId` · `totalKills` · `bestFarmRates` ·
`clearedStageIds` 뿐이다. 마운트·창고·회피는 **어디에도 저장되지 않고**
`CombatSimulation` 안에서 **전투를 시작할 때마다 새로 선다.**

그것이 맞는가는 **판정거리**다 — 다만 이번 묶음에서 **바꾸지 않는다.**
보드 저장과 전투 상태 저장은 다른 층이고, 한꺼번에 열면 둘 다 흐려진다.

📌 **`260915_V01` 판정 항목으로 올린다** — 「**전투 상태 저장**(마운트 적재 · 창고 재고 ·
회피 스택)이 **재입장에 이어질 것인가**」. 지금은 안 이어진다(전투마다 새로 선다).
이어지게 하면 방치형으로는 자연스럽지만, **꺼서 리셋하는 우회로**가 생긴다 —
그 둘을 저울질하는 것이 판정이다.

## 3-3. 「안 싣는 것」은 규칙이다

버퍼 셋 · 벨트 표시 `Kind` · 흐르는 아이템을 빼는 것은 **구현 편의가 아니라 규칙**이다
(진실을 둘로 만들지 않는다 · 진행 중인 회분은 되살리지 않는다).
**가정 · 설계 사후 역기입**으로 `260915_V01` 에 올린다 — 조립 문서에 **저장 절 신설**이 필요하다.

---

## 4. 시험 (착수 시)

- 시작 보드 왕복 = 동일(노드 수·좌표·벨트 면까지)
- **회전을 넣고 왕복 = 회전이 살아남는다**
- 조합표·탄종·모듈 왕복
- 격자 크기가 다르면 버리고 `StartingBoard`
- 깨진 JSON → `StartingBoard`
- 프리셋 적용이 저장 보드를 덮어쓴다

---

⚠️ **착수 대기 중이다** — 사용자 육안(회전 넷) 결과를 받고 시작한다.
**저장이 회전을 실어 나르므로, 회전이 화면에서 안 되면 「안 되는 것을 저장」하게 된다.**
