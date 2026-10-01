using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **한 축짜리 걸음 둘을 모아 바라볼 쪽을 정한다** (2026-10-01 사용자 육안 ⑭ ② —
    /// 「약 45도 방향으로 대각선 이동 시 떨림 현상이 발생」).
    ///
    /// ⚠️⚠️ **왜 관성이 안 걸렸나.** <see cref="GridMovement.Step"/> 은 남은 거리가 큰 축
    /// **하나만** 민다(4방향 규칙 · 전투 사양). 45° 로 가면 <c>ax ≈ ay</c> 라 이긴 축이
    /// **매 틱 뒤바뀌고**, 뷰가 받는 위치 변화는 「가로만 → 세로만 → 가로만」이 된다.
    ///
    /// 그 한 축짜리 값을 <see cref="DirectionHysteresis.Resolve"/> 에 넣으면
    /// <c>ay &gt; ax * h</c> 에서 **<c>ax</c> 가 정확히 0** 이다 — 어떤 배수를 곱해도 0 이라
    /// **관성이 한 톨도 안 걸리고 매 프레임 면이 뒤집힌다.** 떨린 것은 관성 값(120°)이
    /// 모자랐던 탓이 아니라, 관성에 **대각선을 한 번도 안 보여준** 탓이다.
    ///
    /// 📌 **고치는 자리는 걸음이 아니라 보는 자리다.** 걸음을 대각선으로 바꾸면
    /// 4방향 규칙과 장갑형 길막이 같이 흔들린다 — 판정은 그대로 두고 **눈만** 모아 본다.
    ///
    /// 📌 **새 값이 없다.** 둘을 모으는 것은 시간이 아니라 **축의 개수**다 — 격자가
    /// 두 축을 번갈아 쓰므로 연속한 걸음 **둘**에 두 축이 다 들어온다. 관성 배수도
    /// 그대로 <see cref="DirectionHysteresis"/> 것을 쓴다(지침 §7 · 한 값은 한 곳에).
    ///
    /// ⚠️ **로봇만의 일이 아니다.** 로봇(<c>AutoPilotPolicy</c>)과 모든 적
    /// (<c>StepOrSide</c>)이 같은 <see cref="GridMovement"/> 를 지나 같은
    /// <c>CombatEntityView</c> 로 들어온다 — 보스가 유난히 보인 것은 **몸이 커서**다.
    /// </summary>
    public static class StepFacing
    {
        /// <summary>
        /// 이번 걸음 <paramref name="step"/> 과 **직전 걸음**을 합쳐 면을 고른다.
        ///
        /// <paramref name="previousStep"/> 은 부르는 쪽이 들고 있는 기억이고, 여기서
        /// **이번 걸음으로 갈아 준다.**
        ///
        /// ⚠️ **안 움직인 프레임은 기억을 안 지운다** — 지우면 다음 걸음이 다시 한 축짜리가
        /// 되어 떨림이 돌아온다. 걸음이 0 이면 면도 그대로 둔다(고를 근거가 없다).
        ///
        /// ⚠️ 축이 **진짜로** 바뀔 때는 연속한 두 걸음이 같은 축으로 나므로 합도 그 축이
        /// 되어 **넘어간다** — 붙들기가 영영 붙드는 것이 아니다(걸음 둘, 곧 두 틱 안에 돈다).
        /// </summary>
        public static UnitAnimDirection Resolve(Vector2 step, ref Vector2 previousStep,
            UnitAnimDirection last, float hysteresis = 0f)
        {
            if (step.sqrMagnitude <= 0f) return last;

            UnitAnimDirection dir =
                DirectionHysteresis.Resolve(step + previousStep, last, hysteresis);
            previousStep = step;
            return dir;
        }
    }
}
