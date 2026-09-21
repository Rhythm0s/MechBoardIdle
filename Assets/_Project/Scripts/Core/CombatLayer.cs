using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 전투 층 — **조립 화면 보드 위에 몬스터가 그려지던 것**(2026-09-21 사용자 육안 ③).
    ///
    /// <see cref="BoardLayer"/> 의 **반대 방향**이다. 09-16 에 「전투 화면에 보드가 비친다」를
    /// 층으로 갈랐는데, 그때 판 쪽만 갈랐다 — 전투 쪽은 여전히 기본 층이라 **조립 화면
    /// 주 카메라가 적을 그대로 그렸다.** 사용자 스크린샷에서 머리 구역 오른쪽 위에
    /// 몬스터가 **HP 바째** 떠 있던 자리다.
    ///
    /// 📌 **거리로는 못 막는다** — 전장이 무한하고 카메라가 로봇을 따라가므로
    /// 어떤 거리도 언젠가 닿는다(09-16 에 보드 쪽에서 이미 배운 것이다).
    ///
    /// ⚠️ **인셋은 전투를 비춘다** — 조립 화면에서도 인셋 카메라에는 이 층이 켜져 있어야
    /// 한다. 주 카메라에서만 끈다.
    /// </summary>
    public static class CombatLayer
    {
        /// <summary>
        /// 전투 층 번호. `ProjectSettings/TagManager.asset` 의 9번 칸에 「Combat」으로 적혀 있다.
        ///
        /// ⚠️ 이름이 아니라 **번호로 쓴다** — `LayerMask.NameToLayer` 는 이름이 안 맞으면
        /// 조용히 <c>-1</c> 을 주고, 그 마스크는 **전부 끄거나 전부 켠다**
        /// (<see cref="BoardLayer"/> 와 같은 까닭 · 지침 §7).
        /// </summary>
        public const int Index = 9;

        /// <summary>전투 층 하나만 켠 마스크.</summary>
        public const int Mask = 1 << Index;

        /// <summary>
        /// 이 카메라가 써야 할 컬링 마스크 — <paramref name="combatVisible"/> 면 전투 층을
        /// 켜고 아니면 끈다. 나머지 층은 <paramref name="baseMask"/> 그대로 둔다.
        /// </summary>
        public static int MaskFor(int baseMask, bool combatVisible)
            => combatVisible ? (baseMask | Mask) : (baseMask & ~Mask);

        /// <summary>
        /// 뿌리와 그 아래 전부를 전투 층으로 옮긴다.
        ///
        /// ⚠️⚠️ **문을 하나로 둔다.** 전투 그림은 배경·적·탄·드론·연출·드롭까지
        /// 전부 `StageRunner` 아래에 달린다 — 낳는 자리마다 층을 적으면 **새로 낳는 것마다
        /// 또 빠뜨린다**(09-15 에 같은 대가를 치렀다: 「새로 그리는 것마다 같은 처리를
        /// 또 해야 했다」). 뿌리에서 한 번 훑는다.
        ///
        /// ⚠️⚠️ **맞는 가지라고 건너뛰지 않는다.** 처음에 「이미 층이 맞으면 그 아래는
        /// 안 본다」로 썼는데 틀렸다 — 뒤늦게 자식이 붙는 것들이 있다(쉴드 막대처럼
        /// 필요할 때 생기는 것). 부모가 맞다고 건너뛰면 **그 자식만 기본 층에 남아**
        /// 보드 위에 뜬다. 고치려던 것과 같은 병이다. 값만 안 쓰고 **훑기는 다 한다.**
        /// </summary>
        public static void Apply(Transform root)
        {
            if (root == null) return;
            if (root.gameObject.layer != Index) root.gameObject.layer = Index;

            for (int i = 0; i < root.childCount; i++) Apply(root.GetChild(i));
        }
    }
}
