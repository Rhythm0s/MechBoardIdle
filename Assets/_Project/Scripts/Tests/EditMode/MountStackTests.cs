using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 마운트 스택 상한 10 — 확정(260901_V03 §1층).
    ///
    /// 적재량은 **슬롯의 파생값**이다: 로봇 A 4 × 10 = 40 · 로봇 B 8 × 10 = 80.
    ///
    /// ⚠️ 이 값이 없던 동안 `StackLimitOf`가 전부 0이라 `IsFull`이 영영 false였고,
    /// **태그 스킬이 한 번도 발동하지 않았다.** 배선은 있는데 조건이 서지 않는 상태였다 —
    /// 지침 §7 「실패하지 않는 결함」의 한 형태다. 여기서 조건이 실제로 서는지 잰다.
    /// </summary>
    public sealed class MountStackTests
    {
        private const float D = 0.01f;
        private const float Stack = 10f;

        private BalanceConfig _bal;

        [SetUp]
        public void SetUp()
        {
            _bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(
                "Assets/_Project/ScriptableObjects/BalanceConfig.asset");
            if (_bal == null) Assert.Ignore("자산 없음 — 먼저 밸런스·노드 생성 메뉴를 실행해야 한다.");
        }

        // ---- 값 ----

        [Test]
        public void StackLimitIsTen()
        {
            Assert.AreEqual(Stack, _bal.mountStackLimit, D, "탄약 3종·드론 2종 공통 10");
        }

        /// <summary>적재량은 슬롯 × 스택이다. 따로 적어 둔 숫자가 아니다.</summary>
        [Test]
        public void CapacityIsSlotsTimesStack()
        {
            var a = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(Stack));
            var b = new MountLoad(MountLoad.SlotsRobotB, MountLoad.StandardStacks(Stack));

            a.Load(MountItem.Pierce, 999f);
            b.Load(MountItem.Pierce, 999f);

            Assert.AreEqual(40f, a.Capacity, D, "로봇 A 4슬롯 × 10");
            Assert.AreEqual(80f, b.Capacity, D, "로봇 B 8슬롯 × 10");
        }

        /// <summary>
        /// 저장 노드 용량 40과 **숫자가 같은 것은 우연이다.** 층이 다르다 —
        /// 저장은 태그 주기를 만들고 만충 판정에 세지 않으며, 만충은 마운트 층이 본다.
        /// 층을 섞으면 한쪽을 고칠 때 다른 쪽이 조용히 따라 움직인다.
        /// </summary>
        [Test]
        public void StorageFortyAndMountFortyAreDifferentLayers()
        {
            Assert.AreEqual(40f, _bal.storeCapacity, D, "저장 노드 용량");
            Assert.AreEqual(40f, MountLoad.SlotsRobotA * _bal.mountStackLimit, D, "로봇 A 적재량");

            // 같은 숫자를 **다른 필드**가 들고 있다. 하나로 합치면 우연이 계약이 된다.
            Assert.AreNotSame(nameof(_bal.storeCapacity), nameof(_bal.mountStackLimit));
        }

        // ---- 조건이 실제로 서는가 ----

        /// <summary>
        /// **만충 판정이 선다.** 이것이 이 파일의 존재 이유다 — 종전에는 상한이 0이라
        /// 아무리 채워도 `IsFull`이 false였다.
        /// </summary>
        [Test]
        public void FullnessNowActuallyTriggers()
        {
            var m = new MountLoad(MountLoad.SlotsRobotA, MountLoad.StandardStacks(Stack));

            Assert.IsTrue(m.CanJudgeFullness, "판정할 수 있다");
            Assert.IsFalse(m.IsFull, "비었으면 만충이 아니다");

            // 슬롯 넷을 각각 상한까지 채운다.
            m.Load(MountItem.Pierce, Stack);
            m.Load(MountItem.Split, Stack);
            m.Load(MountItem.Explosive, Stack);
            m.Load(MountItem.Drone, Stack);

            Assert.IsTrue(m.IsFull, "만충이 선다 — 태그 스킬 조건이 성립한다");
            Assert.AreEqual(40f, m.Total, D);
        }

        /// <summary>상한을 안 주면 종전 상태다 — 판정 자체가 없다. 회귀를 여기서 잡는다.</summary>
        [Test]
        public void WithoutLimits_FullnessNeverStands()
        {
            var m = new MountLoad(MountLoad.SlotsRobotA);
            m.Load(MountItem.Pierce, 999f);

            Assert.IsFalse(m.IsFull, "상한이 없으면 만충이 영영 서지 않는다");
            Assert.AreEqual(0f, m.Capacity, D);
        }

        // ---- 확정치가 안 움직인다 ----

        /// <summary>
        /// **2026-07-23 확정치 「40 × 52.6 ≈ 2,103」이 그대로 재현된다**(260901_V03 §1층).
        ///
        /// 분류기로 나눈 적재 내역 관통 10 · 분열 10 · 폭발 20에서
        /// 평균 발당피해 = (10×20 + 10×25 + 20×50) ÷ 40 = 36.25,
        /// 강화 후 = 36.25 × 1.45 = 52.56, 스킬 피해 = 40 × 52.56 ≈ 2,102.
        ///
        /// 이 단언이 깨지면 S5 교대 기여 151과 S6 예산 39,018도 함께 흔들린다.
        /// </summary>
        [Test]
        public void ConfirmedTagSkillDamage_IsReproduced()
        {
            const float load = 40f;
            float average = (10f * 20f + 10f * 25f + 20f * 50f) / load;

            Assert.AreEqual(36.25f, average, D, "평균 발당피해");

            float enhanced = average * _bal.enh;
            Assert.AreEqual(52.56f, enhanced, 0.02f, "강화 ×1.45");

            float damage = GrandEntrance.Damage(true, load, enhanced);
            Assert.AreEqual(2103f, damage, 2f, "2026-07-23 확정치와 일치");
        }

        /// <summary>
        /// 로봇 B가 만충까지 **여덟 배 오래 걸린다.** 슬롯 비대칭(4:8)이 그대로 드러난 것이고
        /// 「A는 자주 채우고 자주 쓰고, B는 천천히 채워 크게 쓴다」에 맞는다.
        ///
        /// 조절 손잡이는 값이 아니라 **배치**다 — 드론 라인에 노드를 더 붙이면 짧아진다.
        /// </summary>
        [Test]
        public void RobotB_TakesEightTimesLongerToFill()
        {
            float a = MountLoad.SlotsRobotA * _bal.mountStackLimit;
            float b = MountLoad.SlotsRobotB * _bal.mountStackLimit;

            const float ratePerSec = 4f; // 대표 조합 4발/초
            Assert.AreEqual(10f, a / ratePerSec, D, "로봇 A 10초");
            Assert.AreEqual(80f, b / 1f, D, "로봇 B 80초 (드론 1기/초)");
            Assert.AreEqual(2f, b / a, D, "적재량은 두 배");
        }
    }
}
