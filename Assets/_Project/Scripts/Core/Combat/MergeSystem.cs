using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 합체·버스트(전투 시스템 문서 4·5장 · 밸런스 5장). 순수 상태 기계 — 씬 비의존.
    ///
    /// 확정치: 게이지 만충 **90초** · 합체 지속 **20초** · 합체 배율 **×1.8** · 버스트 **300%**.
    /// 게이지는 **공통 1개**이고 **스테이지당 1회**이며, 만충 시 합체와 버스트가 **동시 발동**한다.
    ///
    /// 두 축이 분리돼 있다(밸런스 1장「순간/지속 경계」):
    ///   합체 = **지속** 화력, (A + B) × 1.8 — 요구치 **예산 안**
    ///   버스트 = **순간** 1회, 발동 시점 스냅샷 × 300% — 요구치 **예산 밖 마진 항**
    /// 그래서 버스트는 지속 DPS 계산에 섞이지 않는다 — 섞으면 예산이 무너진다.
    ///
    /// ⚠️ **합체에는 태그 스킬이 붙지 않는다.** 구 「과부하 특공」(합체 발동 시 마운트 만충으로
    /// 특공)은 2026-08-27 폐지됐고, 되살아난 것은 이름 그대로 **태그** 경로 하나뿐이다.
    /// </summary>
    public sealed class MergeSystem
    {
        /// <summary>게이지 만충까지 걸리는 시간(초). params gaugeFull = 90 확정.</summary>
        public const float GaugeFullSeconds = 90f;

        /// <summary>합체 지속(초). params bd = 20 확정.</summary>
        public const float DurationSeconds = 20f;

        /// <summary>합체 배율. params mergeMult = 1.8 확정.</summary>
        public const float MergeMultiplier = 1.8f;

        /// <summary>버스트 계수. params bc = 300% 확정.</summary>
        public const float BurstPercent = 300f;

        private float _charge;   // 초 단위 누적
        private float _remaining;

        /// <summary>게이지 충전율 0~1(HUD용).</summary>
        public float ChargeRatio => Mathf.Clamp01(_charge / GaugeFullSeconds);

        /// <summary>합체 중인가. 이 동안 태그가 잠긴다(전투 문서 4장 상위 잠금).</summary>
        public bool IsActive => _remaining > 0f;

        /// <summary>남은 합체 시간(초).</summary>
        public float RemainingSeconds => Mathf.Max(0f, _remaining);

        /// <summary>이 스테이지에서 이미 썼는가. **스테이지당 1회**가 확정이다.</summary>
        public bool UsedThisStage { get; private set; }

        /// <summary>지금 발동할 수 있는가 — 만충이고, 아직 안 썼고, 합체 중이 아니다.</summary>
        public bool IsReady => !UsedThisStage && !IsActive && _charge >= GaugeFullSeconds;

        /// <summary>
        /// 게이지는 **전투 수행 중에만** 찬다. 조립 화면에서 시간만 보내도 차면
        /// 「전투를 수행해 기세를 쌓는다」는 신문법이 성립하지 않는다.
        /// 합체 중에는 충전하지 않는다 — 스테이지당 1회라 쌓아 둘 이유가 없다.
        /// </summary>
        public void Tick(float dt, bool inCombat)
        {
            if (dt <= 0f) return;

            if (IsActive)
            {
                _remaining -= dt;
                if (_remaining < 0f) _remaining = 0f;
                return;
            }

            if (inCombat && !UsedThisStage) _charge += dt;
        }

        /// <summary>
        /// 합체 발동. 실패하면 아무 일도 없다.
        /// 성공하면 20초 지속이 시작되고 이 스테이지에서는 다시 못 쓴다.
        /// </summary>
        public bool TryActivate()
        {
            if (!IsReady) return false;

            _remaining = DurationSeconds;
            _charge = 0f;
            UsedThisStage = true;
            return true;
        }

        /// <summary>스테이지 시작 시 초기화. 게이지도 사용 이력도 스테이지 단위다.</summary>
        /// <summary>
        /// 게이지를 만충 직전까지 채운다 — **심사자용 바로가기 전용**(260901_W04 §3층).
        ///
        /// 만충까지 90초라 촬영과 리허설에서 그 90초를 매번 기다리게 된다.
        /// 이미 썼거나 진행 중이면 아무 일도 하지 않는다 — 스테이지당 1회 규칙은 그대로다.
        ///
        /// ⚠️ **만충이 아니라 직전이다.** 꽉 채우면 자동 발동이 걸려 「발동하는 순간」을
        /// 촬영자가 못 고른다. 남겨 둔 1초가 그 조작 여유다.
        /// </summary>
        public void FillGaugeAlmost()
        {
            if (UsedThisStage || IsActive) return;
            _charge = Mathf.Max(0f, GaugeFullSeconds - 1f);
        }

        public void Reset()
        {
            _charge = 0f;
            _remaining = 0f;
            UsedThisStage = false;
        }

        // ---- 화력 ----

        /// <summary>
        /// 합체 중 지속 화력 = **(A 화력 + B 화력) × 1.8**.
        /// 합체 중에는 태그가 없으므로 **교대 공백을 적용하지 않는다** — 원 화력 기준이다
        /// (밸런스 5-2: 계수 0.96은 교대가 있을 때의 값이다).
        /// </summary>
        public static float MergedOutput(float outputA, float outputB) =>
            (Mathf.Max(0f, outputA) + Mathf.Max(0f, outputB)) * MergeMultiplier;

        /// <summary>
        /// 버스트 피해 = **발동 순간 스냅샷 × 300%**, 순간 1회.
        /// 지속 DPS가 아니라 한 번 터지는 값이라 예산 밖 마진 항이다 —
        /// 이것을 DPS에 섞으면 요구치 예산이 무너진다.
        /// </summary>
        public static float BurstDamage(float snapshotOutput) =>
            Mathf.Max(0f, snapshotOutput) * (BurstPercent / 100f);
    }
}
