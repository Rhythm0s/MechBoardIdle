using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 회피할 때 기체가 **짧게 밀려나는 것** (2026-09-17 · `260917_W07` 3장 ·
    /// 사용자 「부스터 발생 시 이펙트 및 플레이어 로봇의 이동이 필요함」).
    ///
    /// 🗑️ 전투 시스템 문서의 구 문구 「회피는 이동이 아니다 · 자리 이동 없음」 폐기.
    ///
    /// ✅ **값 넷은 2026-09-17 사용자 확정이다**(`260917_W08` 2장) —
    /// 방향 규칙 · 거리 **0.5칸** · 시간 **0.167초** · 곡선 **감속형**.
    /// 🗑️ 구 「설계 가정 · `confirmed: false`」 폐기.
    /// ⚠️ **넷 중 둘만 `balance_v4.json` 에 산다** — 방향 규칙과 곡선은 **수가 아니라 규칙**이라
    /// 값 칸에 넣을 수 없어 이 파일이 든다. 여기 상수는 **자산을 못 읽는 판의 기본값**이다.
    ///
    /// ⚠️⚠️ **무적 판정은 이 이동과 무관하다.** 막혀서 한 칸도 못 갔어도 회피는 성립한다
    /// (전투 시스템 문서「회피」 「무적을 준 이유」). 그래서 이 클래스는
    /// **무적을 모른다** — 재는 것도 거는 것도 `DodgeSystem` 이다.
    ///
    /// 📌 순수 로직 — 씬 비의존이라 EditMode 로 검증된다(`DodgeSystem` 과 같은 결).
    /// </summary>
    public sealed class DodgeMotion
    {
        /// <summary>
        /// ✅ **1.25 칸 — 사용자 확정 2026-09-18 리허설**(구 0.5 의 2.5 배).
        /// 🗑️ 구 0.5(격자 반 칸) 폐기 — 화면에서 **밀린 것이 안 보였다.**
        /// ⚠️ 시간(<see cref="DefaultSeconds"/>)은 안 건드렸으므로 **같은 시간에 2.5 배**를 간다.
        /// </summary>
        public const float DefaultDistance = 1.25f;

        /// <summary>무적과 **같이 시작하고 같이 끝난다**. ✅ 사용자 확정 2026-09-17.</summary>
        public const float DefaultSeconds = 0.167f;

        /// <summary>한 번에 밀려나는 거리(칸).</summary>
        public float Distance { get; set; } = DefaultDistance;

        /// <summary>미는 데 걸리는 시간(초).</summary>
        public float Seconds { get; set; } = DefaultSeconds;

        private Vector2 _dir;
        private float _elapsed;
        private float _done;      // 이미 나아간 몫(0~1 곡선값)

        /// <summary>지금 밀려나는 중인가.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>이번 회피가 실제로 나아간 거리(칸) — 막히면 0.5칸에 못 미친다(진단용).</summary>
        public float MovedDistance { get; private set; }

        /// <summary>마지막으로 민 방향(연출용 · 4방향에 맞춘 값).</summary>
        public Vector2 Direction => _dir;

        /// <summary>
        /// **네 방향에 맞춘다** — 보드도 전투도 4방향 문법이라 대각으로 밀면 그 문법이 깨진다.
        /// 0 벡터가 들어오면 0 을 돌려준다(부를 쪽이 「밀지 않는다」로 읽는다).
        /// </summary>
        public static Vector2 SnapToFour(Vector2 v)
        {
            if (v.sqrMagnitude <= 0.000001f) return Vector2.zero;

            return Mathf.Abs(v.x) >= Mathf.Abs(v.y)
                ? new Vector2(Mathf.Sign(v.x), 0f)
                : new Vector2(0f, Mathf.Sign(v.y));
        }

        /// <summary>
        /// **감속형** — 처음에 빠르고 끝에서 멈춘다. `1 - (1-u)²`.
        /// ⚠️ 등속으로 두면 끝에서 뚝 끊겨 「튕겼다」가 아니라 「순간이동했다」로 보인다.
        /// </summary>
        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            float left = 1f - u;
            return 1f - left * left;
        }

        /// <summary>
        /// 밀기 시작. 방향은 **부르는 쪽이 준 것**을 네 방향에 맞춰 쓴다
        /// (자동 = 가장 가까운 적의 반대쪽 · 수동 = 드래그 방향).
        ///
        /// ⚠️ **이미 밀리는 중이면 새로 시작한다** — 회피가 다시 났다는 뜻이고,
        /// 남은 몫을 이어 미는 쪽으로 두면 두 번째 회피가 더 짧게 보인다.
        /// </summary>
        public void Begin(Vector2 direction)
        {
            _dir = SnapToFour(direction);
            _elapsed = 0f;
            _done = 0f;
            MovedDistance = 0f;
            IsMoving = _dir != Vector2.zero && Distance > 0f && Seconds > 0f;
        }

        /// <summary>
        /// 시간을 흘려 **이번 틱에 움직일 벡터**를 돌려준다. 안 밀리는 중이면 0 이다.
        ///
        /// ⚠️ **프레임이 아니라 시간으로 센다**(무적과 같은 까닭) — 30fps 기기에서
        /// 반 칸이 한 칸이 되면 안 된다.
        /// ⚠️ 부르는 쪽이 **막힘을 판정한다** — 그쪽이 덜 갔다고 알려 주면
        /// <see cref="ReportMoved"/> 로 실제 간 거리를 적는다.
        /// </summary>
        public Vector2 Step(float dt)
        {
            if (!IsMoving || dt <= 0f) return Vector2.zero;

            _elapsed += dt;
            float u = Mathf.Clamp01(_elapsed / Seconds);
            float now = Ease(u);
            float delta = (now - _done) * Distance;
            _done = now;

            if (u >= 1f) IsMoving = false;
            return _dir * delta;
        }

        /// <summary>실제로 간 거리를 더한다(막힘 진단용) — 부르는 쪽이 옮긴 뒤에 적는다.</summary>
        public void ReportMoved(float distance) => MovedDistance += Mathf.Max(0f, distance);

        /// <summary>스테이지 시작·재시작.</summary>
        public void Reset()
        {
            _dir = Vector2.zero;
            _elapsed = 0f;
            _done = 0f;
            MovedDistance = 0f;
            IsMoving = false;
        }
    }
}
