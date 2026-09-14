using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **발사 라인을 다시 배분할 때인가**를 가른다 (2026-09-15 · 육안 ① 결함).
    ///
    /// ⚠️ **왜 따로 뺐나.** 이 판정이 <c>StageRunner</c> 안에 묻혀 있는 동안
    /// **배분식과 따로 놀았다.** 09-15 에 배분 함수를 탄종별로 바꿨는데 그 함수를
    /// **부를지 말지**는 여전히 구 전역 배율만 봤다 — 출력이 평평하면(만공급이면 늘 평평하다)
    /// 도착률이 0 에서 4 발/초로 올라도 재배분이 **한 번도** 안 일어났고, 전투 시작 시점
    /// 도착률이 0 이라 **라인 0 줄로 굳었다.** 실탄 40 발을 지고 적 96 기에 둘러싸인 채
    /// 78 초 동안 한 발도 안 나갔다.
    ///
    /// 실측 대조(60 초 · 적을 사거리 안에): 구 게이트 **0 발** / 매 틱 재배분 **54 발**.
    ///
    /// 규칙은 하나다 — **배분에 드는 값이 변하면 다시 배분한다.** 배율 · 도착률 · 재고 셋이
    /// 그 값이며, 셋 말고는 배분식에 들어가는 것이 없다.
    ///
    /// ⚠️ **값을 만들지 않는다.** 문턱은 종전 <c>ScaleEpsilon</c> 하나를 셋에 그대로 쓴다.
    ///
    /// 순수 클래스 — <see cref="UnityEngine"/> 타입을 상태로 갖지 않아 EditMode 에서 돈다.
    /// </summary>
    public sealed class FireRateGate
    {
        /// <summary>탄종 셋(관통·표준·폭발). <see cref="AmmoKind"/> 차례 그대로.</summary>
        private const int Kinds = 3;

        private float _lastScale = 1f;
        private readonly float[] _lastArrival = new float[Kinds];
        private readonly bool[] _lastStocked = new bool[Kinds];

        /// <summary>
        /// 다시 배분할 때인가. <c>true</c> 면 **기준값을 새로 새겨 두고** 참을 돌려준다 —
        /// 물어보기만 하고 안 배분하면 다음 물음이 거짓이 되어 변화 하나를 놓친다.
        /// </summary>
        public bool ShouldReallocate(float scale, System.Func<AmmoKind, float> arrivalOf,
            System.Func<AmmoKind, float> stockOf, float epsilon)
        {
            bool changed = Mathf.Abs(scale - _lastScale) >= epsilon;

            for (int i = 0; i < Kinds && !changed; i++)
            {
                var kind = (AmmoKind)i;
                if (arrivalOf != null &&
                    Mathf.Abs(arrivalOf(kind) - _lastArrival[i]) >= epsilon) changed = true;

                // ⚠️ **재고는 「있다/없다」만 규칙에 든다**(발사 규칙 (가)).
                // 40 이 39 가 된 것으로 다시 배분하면 사실상 매 프레임 재배분이라
                // 프레임당 할당 0 이라는 이 경로의 전제가 깨진다. 갈리는 순간만 잡는다.
                else if (stockOf != null && stockOf(kind) > 0f != _lastStocked[i]) changed = true;
            }

            if (!changed) return false;

            _lastScale = scale;
            for (int i = 0; i < Kinds; i++)
            {
                var kind = (AmmoKind)i;
                _lastArrival[i] = arrivalOf != null ? arrivalOf(kind) : 0f;
                _lastStocked[i] = stockOf != null && stockOf(kind) > 0f;
            }
            return true;
        }

        /// <summary>전투가 새로 서면 기준도 새로 선다.</summary>
        public void Reset(float scale)
        {
            _lastScale = scale;
            for (int i = 0; i < Kinds; i++) { _lastArrival[i] = 0f; _lastStocked[i] = false; }
        }
    }
}
