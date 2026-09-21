using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 처치한 자리에 떨어진 재화가 **로봇에게 빨려 들어온다** (2026-09-18 설계 지시 ① ·
    /// 쿠키런 크럼블 문법).
    ///
    /// ⚠️⚠️ **판정에 손대지 않는다.** 재화는 이미 적립됐다 — 고철은 `KillRewardRule`,
    /// 골드는 `GoldRewardRule` 이 방치 런타임 안에서 처리하고, 이 파일은 **그 일이 일어났다는
    /// 것을 눈에 보이게** 할 뿐이다. 여기서 지갑을 만지면 적립 규칙이 두 곳으로 흩어진다
    /// (`IdleSignals` 주석이 막고 있는 자리 그대로).
    ///
    /// ⚠️ **먼저 쉬고 그 다음 빨린다.** 곧바로 날아가면 「떨어졌다」가 화면에서 안 읽히고
    /// 처치와 수입이 한 동작으로 뭉개진다 — 떨어지는 것이 보여야 무엇 때문에 늘었는지가 읽힌다.
    ///
    /// ⚠️ **표적이 사라지면 제자리에서 걷힌다** — 로봇이 교대·합체로 바뀌면 트랜스폼이
    /// 갈릴 수 있다. 그때 `null` 을 향해 날아가면 원점으로 빨려 들어간다.
    /// </summary>
    public sealed class DropMagnet : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _from;
        private float _rest;
        private float _fly;
        private float _elapsed;
        private SpriteRenderer _sr;

        /// <summary>
        /// 하나를 띄운다. <paramref name="target"/> 이 <c>null</c> 이면 쉬었다가 그대로 사라진다.
        /// </summary>
        public static DropMagnet Play(Transform parent, Vector3 worldPos, Transform target,
                                      Sprite sprite, Color color, float sizeUnits,
                                      float restSeconds, float flySeconds, int sortingOrder)
        {
            var go = new GameObject("Drop");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(sizeUnits, sizeUnits, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            var d = go.AddComponent<DropMagnet>();
            d._sr = sr;
            d._target = target;
            d._from = worldPos;
            d._rest = RestOf(restSeconds);
            d._fly = FlyOf(flySeconds);
            return d;
        }

        /// <summary>쉬는 시간 · 나는 시간의 **하한**. 값이 두 곳에 살지 않게 뺀다.</summary>
        public static float RestOf(float seconds) => Mathf.Max(0f, seconds);

        public static float FlyOf(float seconds) => Mathf.Max(0.01f, seconds);

        /// <summary>
        /// 떨어진 자리에서 **로봇에 닿기까지** 걸리는 시간(초).
        ///
        /// ⚠️ **글자 쪽이 이것을 읽는다**(`StageRunner.Hud` 의 팝). 흡수 시점을 저쪽에서
        /// 따로 계산하면 연출값을 만질 때마다 **둘이 어긋난다** — 지침 §7 의 그 자리다.
        /// </summary>
        public static float FlightSeconds(float restSeconds, float flySeconds) =>
            RestOf(restSeconds) + FlyOf(flySeconds);

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_elapsed < _rest) return;                  // 아직 바닥에 있다

            if (_target == null) { Destroy(gameObject); return; }

            float u = Mathf.Clamp01((_elapsed - _rest) / _fly);
            // 뒤로 갈수록 빨라진다 — 자석은 가까울수록 세다.
            float eased = u * u;
            transform.position = Vector3.Lerp(_from, _target.position, eased);

            // 닿기 직전에 사라진다. 로봇 위에 한 프레임 얹히면 「안 먹혔다」로 보인다.
            if (u >= 1f) Destroy(gameObject);
            else if (_sr != null)
            {
                Color c = _sr.color;
                _sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0.2f, eased));
            }
        }
    }
}
