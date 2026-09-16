using System;
using System.Collections.Generic;

namespace MBI.Core
{
    /// <summary>
    /// 보드 한 판을 저장에 싣는 꼴 (2026-09-15 설계 · `Docs/board_save_design.md` 승인본).
    ///
    /// ⚠️⚠️ **`JsonUtility` 가 삼키는 모양만 쓴다** — `public` 필드 + `[Serializable]` 이다.
    /// 프로퍼티 · `Dictionary` · 중첩 배열은 **에러 없이 빈 값이 된다.** 그래서 면도
    /// 배열이 아니라 **비트마스크 정수 둘**이고, 좌표도 `Vector2Int` 가 아니라 `x`·`y` 다.
    ///
    /// 📌 **무엇을 안 싣는지가 규칙이다**(설계 3-3) — 진행 중인 한 회분(버퍼 셋) ·
    /// 벨트의 표시용 `Kind` · 벨트 위를 흐르는 아이템은 **일부러 뺀다.**
    /// 되살리면 「멈춰 있던 상태」까지 되살아나고, 조합표와 어긋난 버퍼가 되돌아온다.
    /// </summary>
    [Serializable]
    public class BoardNodeEntry
    {
        /// <summary>자산이 아니라 id 로 싣는다 — 자산 참조는 GUID 라 저장에 못 넣는다.</summary>
        public string nodeId;
        public int x;
        public int y;

        /// <summary>0~3. **이번 묶음이 생긴 이유다** — 회전이 안 실리면 판이 돌아가 버린다.</summary>
        public int rotation;

        /// <summary>`RecipeKind`. 안 실으면 재입장에 기본값으로 돌아가 폭발탄 줄이 통째로 죽는다.</summary>
        public int recipe;

        /// <summary>`AmmoKind`. 탄종은 노드별이라 안 실으면 관통으로 돌아간다.</summary>
        public int ammo;

        /// <summary>모듈 칸 둘. **순서가 뜻을 갖는다** — 빈 칸은 빈 문자열이다.</summary>
        public string module0;
        public string module1;
    }

    /// <summary>벨트 한 칸.</summary>
    [Serializable]
    public class BoardBeltEntry
    {
        public int x;
        public int y;

        /// <summary>`BeltElementKind` — 직선 · 코너 · 병합기 · 분류기.</summary>
        public int element;

        /// <summary>받는 면 · 내보내는 면. N=1 E=2 S=4 W=8 (<see cref="BoardStateCodec"/>).</summary>
        public int inMask;
        public int outMask;
    }

    /// <summary>보드 한 판.</summary>
    [Serializable]
    public class BoardStateV1
    {
        /// <summary>
        /// 격자 크기를 **같이 싣는다.**
        ///
        /// ⚠️ 09-14 에 `Rows` 가 13 → 14 로 늘었다. 좌표만 싣고 크기를 안 실으면
        /// **옛 저장이 조용히 어긋난 자리에 놓인다** — 에러 없이 판이 틀어진다.
        /// 크기가 다르면 통째로 버리고 시작 보드로 간다.
        /// </summary>
        /// <summary>
        /// 이 판이 **어느 시작 보드 세대**에서 나왔는가 (2026-09-16 사용자 결정 §74-9).
        ///
        /// ⚠️⚠️ 격자 크기만으로는 못 거른다 — 09-15 에 시작 보드가 네 줄 → 두 줄로
        /// 바뀌었는데 **크기는 그대로 12x14** 였다. 옛 배치가 새 규격 위에 조용히 올라와
        /// 「벨트를 이었는데 마운트 적재 0」이 됐다(육안 9차 ⑦).
        ///
        /// ⚠️ **다르면 저장을 버린다** — 플레이어가 늘린 배치도 같이 사라진다.
        /// 사용자가 그것을 알고 고른 쪽이다(조용히 틀린 판보다 낫다).
        ///
        /// ⚠️ 비어 있으면 **세대 표식이 없던 시절의 저장**이다 — 그것도 버린다.
        /// </summary>
        public string generation;

        /// <summary>
        /// **이 판은 누구의 것인가** — `MountOwner` 값 (2026-09-16 · 보드 로봇별 분리).
        ///
        /// ⚠️ **자기가 누구 것인지를 판이 들고 있어야 한다.** 목록의 차례로만 가리면
        /// 한 칸이 밀렸을 때 **A 판이 B 자리에 조용히 들어앉는다** — 에러 없이
        /// 마운트가 엉뚱한 로봇에게 붙는 꼴이다.
        ///
        /// ⚠️ 0 은 `MountOwner.RobotA` 이므로 **세대 표식이 있던 구 저장(필드 없음)은
        /// A 로 읽힌다** — 그 시절 판이 실제로 A 의 것이라 맞다.
        /// </summary>
        public int owner;

        public int columns;
        public int rows;

        public List<BoardNodeEntry> nodes = new List<BoardNodeEntry>();
        public List<BoardBeltEntry> belts = new List<BoardBeltEntry>();
    }
}
