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
        public int columns;
        public int rows;

        public List<BoardNodeEntry> nodes = new List<BoardNodeEntry>();
        public List<BoardBeltEntry> belts = new List<BoardBeltEntry>();
    }
}
