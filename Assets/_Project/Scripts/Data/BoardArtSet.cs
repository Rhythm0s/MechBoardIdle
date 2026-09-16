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

            /// <summary>
            /// **그림이 캔버스에서 실제로 차지하는 비율** (2026-09-15 · 육안 4차 ⑥ · 실측).
            ///
            /// ⚠️ **이것이 없어서 탄약이 점만 하게 그려졌다.** 크기를 맞출 때
            /// `Sprite.bounds` 를 썼는데 그건 **캔버스**(64×64)이지 **그림**이 아니다.
            /// `ammo_standard` 는 64 안에 40×20 만 그려져 있어서, 캔버스를 한 칸의 0.26 에
            /// 맞추면 **보이는 것은 0.16×0.08 칸**이 된다 — 한 칸이 80픽셀이면 13×6픽셀,
            /// 즉 **주황 얼룩**이다. `core_energy` 는 54×52 로 캔버스를 거의 채워서
            /// 같은 셈에도 제 크기로 나왔다 — **그래서 한 품목만 이상해 보였다.**
            ///
            /// 여기 든 값은 긴 변 기준(= max(그림폭, 그림높이) / 캔버스변)이고,
            /// <see cref="MBI.Editor.ItemArtSpanGenerator"/> 가 PNG 알파에서 재서 넣는다.
            /// **0 이면 「안 쟀다」**는 뜻이고 부르는 쪽이 1 로 본다(구 동작 그대로).
            /// </summary>
            [Range(0f, 1f)] public float contentSpan;

            /// <summary>
            /// **그림이 캔버스 안에서 차지하는 네모**(0~1 정규화 · 2026-09-15 · 육안 5차 ③).
            ///
            /// ⚠️ **`contentSpan` 하나로는 모자랐다.** 스프라이트로 그리는 쪽(벨트 위 품목)은
            /// **긴 변만** 알면 되지만, `GUI.DrawTextureWithTexCoords` 로 그리는 쪽
            /// (노드 출력 아이콘 · 조합표 칩)은 **어디를 잘라 쓸지**를 알아야 한다.
            /// 그 둘을 한 값으로 묶으려다 **한 경로만 고치고 다른 경로를 놓쳤다** —
            /// 09-15 에 「주황 정사각」이 두 번 올라온 까닭이다.
            ///
            /// 비어 있으면(폭·높이 0) 부르는 쪽이 **캔버스 전체**로 본다(구 동작).
            /// </summary>
            public Rect contentRect;
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
        /// <summary>
        /// 면 화살표 — **아직 다 안 이어진 노드**의 입출력 안내 (2026-09-16 · §74-3 #34).
        ///
        /// ⚠️ **오른쪽을 보는 한 장이다** — 네 면은 코드가 돌린다(자산을 넷으로 늘리지 않는다).
        /// ⚠️ 비면 안 그린다 — 자리표시 사각으로 대신하지 않는다(무엇인지가 그림에만 있다).
        /// </summary>
        public Sprite portArrow;

        public Sprite portInput;
        public Sprite portOutput;
        public Sprite portPower;
        [Tooltip("마운트 포트 — 보드의 산출이 전투로 넘어가는 자리. **결합부**이며 마운트 본체가 아니다.")]
        public Sprite mountPort;

        [Header("마운트 본체 (2026-09-14 · 플랜 §71-41 · 실루엣 바깥에 선다)")]
        [Tooltip("로봇 A — 팔 총열 마운트. 192×192. 없으면 색 사각 폴백.")]
        public Sprite mountGunA;
        [Tooltip("로봇 B — 어깨 드론 베이. 192×192. 없으면 색 사각 폴백.")]
        public Sprite mountDronebayB;

        /// <summary>
        /// 그 로봇의 마운트 본체 그림. 없으면 <c>null</c> — 부르는 쪽이 **색 사각으로 폴백한다**.
        ///
        /// ⚠️ 포트 마커와 달리 **폴백을 둔다.** 결합부는 없어도 격자가 말이 되지만,
        /// 마운트 본체가 없으면 **적재 그리드가 허공에 뜬다** — 무엇에 딸린 표시인지가 사라진다.
        /// </summary>
        public Sprite MountBody(MountOwner owner) =>
            owner == MountOwner.RobotB ? mountDronebayB : mountGunA;

        [Header("모듈 기호 (2026-09-09 · 260909_W01 5장)")]
        [Tooltip("생산량 모듈 — 바깥으로 벌어지는 겹꺾쇠 둘. 64 × 64.")]
        public Sprite moduleOutput;
        [Tooltip("생산속도 모듈 — 한 방향으로 누운 빗금 셋. 64 × 64.")]
        public Sprite moduleRate;

        /// <summary>
        /// 그 모듈의 기호. 없으면 <c>null</c>이고 **부르는 쪽이 안 그린다** —
        /// 색 사각으로 대신하면 노드 안에서 무엇인지 알 수 없는 얼룩이 된다.
        /// </summary>
        public Sprite ModuleSprite(ModuleKind kind) =>
            kind == ModuleKind.Output ? moduleOutput : moduleRate;

        [Header("배경")]
        [Tooltip("보드 바닥. 캔버스 192라 한 장이 정확히 한 칸이다 — 칸마다 한 장씩 깐다.")]
        public Sprite boardBackground;

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
        /// 그 품목 그림이 캔버스에서 차지하는 비율. 안 쟀으면 <b>1</b>(= 캔버스를 다 쓴다)로 본다.
        ///
        /// ⚠️ **1 로 떨어지는 것이 구 동작이다** — 못 잰 그림 때문에 크기가 갑자기
        /// 달라지지 않는다. 대신 시험이 「전부 쟀는가」를 지킨다.
        /// </summary>
        /// <summary>
        /// 그 품목 그림이 실제로 그려진 네모(정규화). 안 쟀으면 **캔버스 전체**를 돌려준다.
        ///
        /// `GUI.DrawTextureWithTexCoords` 의 UV 로 그대로 쓴다 — 그러면 **빈 여백을 빼고**
        /// 그림만 아이콘 상자에 채워진다.
        /// </summary>
        public Rect ItemContentRect(FlowKind kind)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].kind == kind)
                {
                    Rect r = items[i].contentRect;
                    if (r.width > 0.0001f && r.height > 0.0001f) return r;
                    break;
                }
            return new Rect(0f, 0f, 1f, 1f);
        }

        public float ItemContentSpan(FlowKind kind)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].kind == kind)
                    return items[i].contentSpan > 0.0001f ? items[i].contentSpan : 1f;
            return 1f;
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
