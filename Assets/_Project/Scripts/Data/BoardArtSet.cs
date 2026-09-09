using System;
using System.Collections.Generic;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 보드 아트 묶음(2026-09-09 신설) — 노드·벨트 부속·포트·품목 스프라이트의 **단일 참조처**.
    ///
    /// **왜 <see cref="BoardConfig"/>에 안 넣는가.** 그쪽은 격자 치수·셀 크기라는
    /// 레이아웃 한 책임이고, 아트가 섞이면 생성기가 재실행될 때 서로를 덮는다
    /// (§3 「한 파일 = 한 책임」 · BoardConfig 주석이 BalanceConfig와 갈랐던 것과 같은 이유).
    ///
    /// ⚠️ **경로는 여기 없다.** 파일 이름과 폴더는 **에디터 생성기 한 곳**에만 산다
    /// (`BoardConfigGenerator` · §8 명명 규칙 — 런타임은 참조만 든다).
    /// 그래서 아트를 옮겨도 런타임 코드는 한 줄도 안 바뀐다.
    ///
    /// ⚠️ **비어 있는 자리는 오류가 아니다.** 아직 안 온 그림이 있으면 그 칸만 null이고,
    /// 보드는 종전의 색 사각으로 그린다 — **한 종류가 없다고 보드 전체가 빈 화면이 되지 않는다.**
    /// </summary>
    [CreateAssetMenu(fileName = "BoardArtSet", menuName = "MBI/Board Art Set", order = 3)]
    public sealed class BoardArtSet : ScriptableObject
    {
        [Serializable]
        public struct NodeArt
        {
            public NodeType type;
            public Sprite sprite;
        }

        [Serializable]
        public struct ItemArt
        {
            public FlowKind kind;
            public Sprite sprite;
        }

        [Header("노드 — 종류마다 한 장")]
        [Tooltip("쉴드는 스텁이라 그림이 없다(NodeDefinition.implemented=false).")]
        public List<NodeArt> nodes = new List<NodeArt>();

        [Header("벨트 부속")]
        public Sprite beltStraight;
        public Sprite beltCorner;
        [Tooltip("끝단 — 이어지지 않은 벨트의 마지막 칸.")]
        public Sprite beltEnd;
        public Sprite merger;
        public Sprite sorter;

        [Header("포트")]
        public Sprite portInput;
        public Sprite portOutput;
        public Sprite portPower;
        [Tooltip("마운트 포트 — 보드의 산출이 전투로 넘어가는 자리.")]
        public Sprite mountPort;

        [Header("품목 — 벨트 위를 흐르는 것")]
        [Tooltip("아직 안 온 품목은 목록에 없다. 없으면 색 점으로 그린다.")]
        public List<ItemArt> items = new List<ItemArt>();

        /// <summary>노드 종류의 그림. 없으면 null — 부르는 쪽이 폴백한다.</summary>
        public Sprite NodeSprite(NodeType type)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].type == type) return nodes[i].sprite;
            return null;
        }

        /// <summary>품목의 그림. 없으면 null.</summary>
        public Sprite ItemSprite(FlowKind kind)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].kind == kind) return items[i].sprite;
            return null;
        }

        /// <summary>
        /// 벨트 요소의 그림. 직선·코너는 배향을 <b>회전</b>으로 주고 그림은 한 장씩만 둔다
        /// — 방향마다 그리면 자산이 네 배가 되고, 각도가 어긋나면 그 방향만 틀어진다.
        /// </summary>
        public Sprite BeltSprite(BeltElementKind kind)
        {
            switch (kind)
            {
                case BeltElementKind.Corner: return beltCorner;
                case BeltElementKind.Merger: return merger;
                case BeltElementKind.Sorter: return sorter;
                default: return beltStraight;
            }
        }

        /// <summary>채워진 노드 칸 수. 배선 확인용 — 「몇 종이 붙었는가」를 세는 자리다.</summary>
        public int FilledNodeCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < nodes.Count; i++) if (nodes[i].sprite != null) n++;
                return n;
            }
        }

        /// <summary>채워진 품목 칸 수.</summary>
        public int FilledItemCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < items.Count; i++) if (items[i].sprite != null) n++;
                return n;
            }
        }

        /// <summary>채워진 부속(벨트 다섯 · 포트 넷) 수.</summary>
        public int FilledPartCount
        {
            get
            {
                Sprite[] parts =
                {
                    beltStraight, beltCorner, beltEnd, merger, sorter,
                    portInput, portOutput, portPower, mountPort,
                };
                int n = 0;
                for (int i = 0; i < parts.Length; i++) if (parts[i] != null) n++;
                return n;
            }
        }
    }
}
