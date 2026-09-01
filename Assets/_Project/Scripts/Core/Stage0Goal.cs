namespace MBI.Core
{
    /// <summary>
    /// 스테이지 0의 종료 판정 — **전투가 없는 첫 스테이지**(260901_V05 §3층 확정).
    ///
    /// 목표는 하나다: **벨트를 이으면 물건이 만들어진다는 것을 안다.**
    ///
    /// <code>
    /// 종료 = 빈 칸에 노드를 놓았다  AND  마운트가 가득 찼다
    /// </code>
    ///
    /// ⚠️ **조건이 둘인 이유.** 마운트 만충 하나만 걸면 아무것도 안 하고 10초 기다려도 통과한다 —
    /// 관통 4노드가 이미 정상 작동 중이기 때문이다. 그러면 배우는 것이 없다.
    /// 앞 조건은 플레이어의 **행동**이고 뒤 조건은 그 **결과**이며, 둘이 다 있어야
    /// 「놓으면 이어진다」와 「이어지면 쌓인다」를 순서대로 보게 된다.
    ///
    /// ⚠️ **두 조건 모두 걸쇠(latch)다.** 놓은 뒤 마운트가 차기까지 약 8초가 걸리는데,
    /// 그 사이 「놓았다」가 풀리면 영영 성립하지 않는다. 반대로 만충은 한 프레임만에
    /// 소비로 다시 내려갈 수 있어, 그 순간을 놓치면 통과가 운에 걸린다.
    /// </summary>
    public sealed class Stage0Goal
    {
        /// <summary>빈 칸에 노드를 놓았는가. 한 번 서면 내려가지 않는다.</summary>
        public bool NodePlaced { get; private set; }

        /// <summary>마운트가 가득 찬 적이 있는가. 한 번 서면 내려가지 않는다.</summary>
        public bool MountFilled { get; private set; }

        /// <summary>둘 다 섰는가. 이것이 스테이지 0의 종료 조건이다.</summary>
        public bool IsComplete => NodePlaced && MountFilled;

        /// <summary>
        /// 매 프레임 관찰. 값이 참으로 오는 순간을 걸어 잠근다.
        /// </summary>
        /// <param name="emptySlotFilled">비워 둔 칸이 채워졌는가</param>
        /// <param name="mountIsFull">마운트가 만충인가</param>
        public void Observe(bool emptySlotFilled, bool mountIsFull)
        {
            if (emptySlotFilled) NodePlaced = true;
            if (mountIsFull) MountFilled = true;
        }

        /// <summary>다시 시작할 때. 걸쇠를 전부 푼다.</summary>
        public void Reset()
        {
            NodePlaced = false;
            MountFilled = false;
        }
    }
}
