using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 쉴드 — 생존 세 층의 **가운데**(플레이어블 로봇 기획서「생존 체계」 ·
    /// 2026-09-17 사용자 결정으로 되살림 · `260917_W05` 4장).
    ///
    /// 피격 차례는 **회피(무적이면 무효) → 쉴드 → HP** 다. 쉴드가 남아 있으면 쉴드가 먼저
    /// 줄고, 0 이 된 뒤에 HP 가 준다.
    ///
    /// ⚠️⚠️ **한 방이 남은 쉴드보다 크면 넘친 만큼 HP 로 간다**(구현 판단 · 설계 역기입 자리).
    /// 반대로 「남은 쉴드가 1 이라도 있으면 그 한 방을 통째로 막는다」로 두면
    /// **쉴드 1 = 무적 1회**가 되어 **회피와 같은 일을 하는 층이 둘** 생긴다.
    /// 층을 셋으로 둔 까닭이 사라지므로 넘치는 쪽을 골랐다.
    ///
    /// ⚠️ **부스터와 같은 문법이되 그릇의 임자가 다르다.** 회피 스택 상한은 **부스터 대수**가
    /// 정하는데, 쉴드 최대치는 **로봇의 것**으로 두고 노드 수는 **채우는 속도**만 정한다
    /// (`260917_W05` 5-1 이 최대치와 충전률을 따로 센 대로다).
    /// 📌 **이것은 구현 판단이다** — 노드를 더 놓으면 그릇도 커지는 쪽이 옳다면 설계가 뒤집어 달라.
    ///
    /// ⚠️ **기본 최대치는 0 이다** — 그러면 쉴드가 없는 것과 같아 **배포 거동이 지금 그대로**다
    /// (`260917_W05` 4-2). 값이 정해지면 그때 켠다.
    ///
    /// 📌 순수 로직 — 씬 비의존이라 EditMode 로 검증된다(`DodgeSystem` 과 같은 결).
    /// </summary>
    public sealed class ShieldSystem
    {
        private float _max;
        private float _value;

        /// <summary>
        /// 게이지 그릇. **0 이면 쉴드가 없는 것**이다.
        /// ⚠️ 줄이면 넘치는 분은 잘린다 — 노드를 뽑았는데 게이지가 그대로면 보드가 결과를 못 바꾼다.
        /// </summary>
        public float Max
        {
            get => _max;
            set
            {
                _max = Mathf.Max(0f, value);
                if (_value > _max) _value = _max;
            }
        }

        /// <summary>지금 남은 쉴드.</summary>
        public float Value => _value;

        /// <summary>1초에 채워지는 쉴드 = 재료 소비 × 재료 1개당 충전량.</summary>
        public float ChargeRate { get; set; }

        /// <summary>
        /// 보드 집계 → **최대치**로 바꾸는 하나뿐인 문 (2026-09-17 사용자 확정 · 설계 뒤집기).
        ///
        /// > 최대치 = **쉴드 발생 노드 수 × 대당 최대치**
        ///
        /// 📌 **회피의 「부스터 대수 × 4」와 같은 규칙**이다 — 생존 두 층이 한 문법으로 읽힌다.
        /// 🗑️ 구 규칙 「최대치는 로봇의 것 · 노드는 속도만 정한다」 폐기(`260917_V04` 구현 판단 4).
        ///    그쪽은 **노드를 더 놓아도 그릇이 안 커져서** 보드가 생존의 절반밖에 못 바꿨다.
        ///
        /// ⚠️ **노드가 0 이면 0 이다** — 시작 보드에 쉴드 줄이 없으므로 배포 거동이 지금 그대로다.
        /// </summary>
        public static float MaxFrom(int nodeCount, float perNode)
            => nodeCount <= 0 || perNode <= 0f ? 0f : nodeCount * perNode;

        /// <summary>
        /// 보드 집계 → 충전률(쉴드/초)로 바꾸는 **하나뿐인 문**
        /// (지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」 — 러너와 하네스가 같이 쓴다).
        ///
        /// > 충전률 = **최대치 × 초당 비율** × (먹은 재료 ÷ 먹고 싶은 재료)
        ///
        /// ✅ **2026-09-17 사용자 확정 — 비율은 고정(0.025/초)이고 노드 수와 무관하다.**
        /// 그래서 **빈 게이지가 차는 데 늘 40초**다. 노드를 더 놓으면 그릇이 커지고
        /// 충전률도 같이 커지므로 **채우는 시간은 그대로**다 — 두 손잡이가 안 겹친다.
        /// 🗑️ 구 규칙 「충전률 = 먹은 재료 × 개당 충전량」 폐기. 개당 충전량은 이제 **파생값**이다
        ///    (= 최대치 × 비율 ÷ 노드 대당 소비).
        ///
        /// ⚠️ **재료 게이트는 그대로다** — 먹고 싶은 만큼 재료가 안 오면 **온 만큼만** 찬다.
        /// 재료가 하나도 없으면 0 이고, 그것이 「벨트가 생존에 닿는 자리」다.
        ///
        /// ⚠️⚠️ **발생 노드가 0 이면 0 이다** — 재료를 아무리 만들어도 먹을 입이 없다.
        /// 이 고리가 없으면 쉴드는 보드와 무관한 공짜 HP 가 되고,
        /// 노드를 뽑아도 생존이 안 변해서 「보드가 결과를 바꾼다」가 무너진다.
        /// </summary>
        public static float ChargeFrom(float max, float ratioPerSec, float materialProduce,
                                       int nodeCount, float materialPerSec)
        {
            if (nodeCount <= 0 || max <= 0f || ratioPerSec <= 0f) return 0f;

            float want = nodeCount * Mathf.Max(0f, materialPerSec);
            if (want <= 0f) return 0f;   // 먹는 양이 0 이면 게이트가 뜻을 잃는다

            float eaten = Mathf.Min(Mathf.Max(0f, materialProduce), want);
            return max * ratioPerSec * (eaten / want);
        }

        /// <summary>쉴드가 있기는 한가 — 최대치가 0 이면 이 층 자체가 없다.</summary>
        public bool Exists => _max > 0f;

        /// <summary>가득 찼는가. **가득이면 재료를 안 먹어 공급 벨트가 정체된다**(부스터와 같은 문법).</summary>
        public bool IsFull => _max > 0f && _value >= _max;

        /// <summary>이 전투에서 쉴드가 막은 피해 합(진단용).</summary>
        public float Absorbed { get; private set; }

        /// <summary>
        /// 시간 경과 — **최대치 미만일 때만** 채운다.
        /// ⚠️ **프레임이 아니라 시간으로 센다**(무적과 같은 까닭 — 30fps 기기에서 두 배가 된다).
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || ChargeRate <= 0f || !Exists) return;
            if (_value >= _max) return;   // 만충 — 여기서 멈추는 것이 「정체」의 원천이다

            _value = Mathf.Min(_max, _value + ChargeRate * dt);
        }

        /// <summary>
        /// 피해를 먹는다. **먹고 남은 피해**를 돌려준다(그것이 HP 로 간다).
        /// 쉴드가 없거나 비었으면 받은 값을 그대로 돌려준다.
        /// </summary>
        public float Absorb(float damage)
        {
            if (damage <= 0f || _value <= 0f) return Mathf.Max(0f, damage);

            float eaten = Mathf.Min(_value, damage);
            _value -= eaten;
            Absorbed += eaten;
            return damage - eaten;
        }

        /// <summary>
        /// 채워 넣는다(진단·시험용 · 합체 종료 뒤 되돌리기). 실제로 들어간 양을 돌려준다.
        /// </summary>
        public float Add(float amount)
        {
            if (amount <= 0f || !Exists) return 0f;

            float room = _max - _value;
            float taken = Mathf.Min(room, amount);
            _value += taken;
            return taken;
        }

        /// <summary>
        /// 스테이지 시작·재시작. ⚠️ **빈 채로 시작한다** — 채우는 것은 보드의 일이다.
        /// 최대치는 보드·자산이 정하는 것이라 여기서 안 지운다(`DodgeSystem.Reset` 과 같은 결).
        /// </summary>
        public void Reset()
        {
            _value = 0f;
            Absorbed = 0f;
        }
    }
}
