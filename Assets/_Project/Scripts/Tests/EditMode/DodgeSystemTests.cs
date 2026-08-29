using MBI.Core;
using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 회피(전투 시스템 문서 11-9장 · 07「생존 체계」, 2026-08-29 신설).
    /// 확정치: 무적 0.167초 · 부스터 1대 = 스택 2칸 · 추진제 1개 = 회피 1회.
    ///
    /// 구 「상한 3」은 2026-08-29 폐기됐다(260829_V02 §①) — 상수로 두면 부스터 1대와 3대가
    /// 같아져 「더 놓으면 강해진다」가 수치로 무너지기 때문이다.
    /// </summary>
    public sealed class DodgeSystemTests
    {
        private const float D = 0.0001f;

        /// <summary>부스터 2대(= 4칸)를 놓고 스택을 채워 둔 상태.</summary>
        private static DodgeSystem Ready(int stacks = 4, int boosters = 2)
        {
            var d = new DodgeSystem { BoosterCount = boosters };
            d.AddStacks(stacks);
            return d;
        }

        // ---- 확정치 ----

        [Test]
        public void ConfirmedConstants()
        {
            Assert.AreEqual(0.167f, DodgeSystem.InvincibleSeconds, D, "무적 0.167초");
            Assert.AreEqual(2, DodgeSystem.StacksPerBooster, "부스터 1대 = 2칸");
        }

        /// <summary>
        /// **무적은 시간으로 센다.** 프레임을 세면 30fps 기기에서 무적이 두 배가 된다 —
        /// Android가 타깃이라 기기마다 값이 달라진다.
        /// 같은 0.167초를 프레임 수를 달리해 소모해도 결과가 같아야 한다.
        /// </summary>
        [Test]
        public void Invincibility_IsMeasuredInTime_NotFrames()
        {
            DodgeSystem sixty = Ready();
            DodgeSystem thirty = Ready();

            sixty.TryDodge(true, Vector2.right, false, Vector2.zero);
            thirty.TryDodge(true, Vector2.right, false, Vector2.zero);

            // 같은 0.15초를 9프레임과 1프레임으로 나눠 쓴다. 프레임을 세는 구현이면
            // 여기서 벌써 갈린다 — 9프레임 쪽이 먼저 끝나거나 1프레임 쪽이 안 끝난다.
            for (int i = 0; i < 9; i++) sixty.Tick(1f / 60f);
            thirty.Tick(0.15f);

            Assert.IsTrue(sixty.IsInvincible, "0.15초에는 아직 남아 있다");
            Assert.IsTrue(thirty.IsInvincible, "프레임을 적게 썼다고 빨리 끝나지 않는다");

            sixty.Tick(0.02f);
            thirty.Tick(0.02f);

            Assert.IsFalse(sixty.IsInvincible, "0.17초면 끝났다");
            Assert.IsFalse(thirty.IsInvincible, "프레임 수와 무관하게 같은 시점에 끝난다");
        }

        // ---- 스택 ----

        /// <summary>
        /// **상한은 부스터 대수의 파생값이다.** 한 대가 드는 것은 2칸이고,
        /// 회피를 늘리는 방법은 부스터를 더 놓는 것뿐이다.
        /// </summary>
        [Test]
        public void Capacity_ComesFromBoosterCount()
        {
            var d = new DodgeSystem();

            Assert.AreEqual(0, d.Capacity, "부스터가 없으면 회피 자체가 없다");

            d.BoosterCount = 1;
            Assert.AreEqual(2, d.Capacity);

            d.BoosterCount = 3;
            Assert.AreEqual(6, d.Capacity, "대수에 비례한다 — 상수가 아니다");
        }

        /// <summary>상한을 넘겨 쌓이지 않는다. 넘치는 분은 버려진다.</summary>
        [Test]
        public void Stacks_CapAtCapacity_OverflowIsDropped()
        {
            var d = new DodgeSystem { BoosterCount = 2 };

            Assert.AreEqual(4, d.AddStacks(10), "4개만 들어간다");
            Assert.AreEqual(4, d.Stacks);
            Assert.AreEqual(0, d.AddStacks(5), "가득 차면 한 개도 안 들어간다");
        }

        /// <summary>
        /// 부스터를 뽑으면 넘치는 스택이 **그 자리에서 잘린다.**
        /// 남겨 두면 노드를 빼도 결과가 안 바뀌어 보드가 생존을 못 만든다.
        /// </summary>
        [Test]
        public void RemovingBoosters_TrimsStacksImmediately()
        {
            var d = new DodgeSystem { BoosterCount = 3 };
            d.AddStacks(6);

            d.BoosterCount = 1;

            Assert.AreEqual(2, d.Capacity);
            Assert.AreEqual(2, d.Stacks, "상한 위로 넘친 분은 잘린다");
        }

        /// <summary>
        /// **그릇과 채우는 속도는 다른 축이다.** 부스터를 늘려 여섯 칸을 만들어도
        /// 채우는 것은 군수 노드(15초에 1개)라 90초가 든다 — 그릇만 키우면 빈 그릇이 는다.
        /// </summary>
        [Test]
        public void BiggerCapacity_DoesNotFillFaster()
        {
            var d = new DodgeSystem { BoosterCount = 3 };

            Assert.AreEqual(1, d.AddStacks(1), "한 번에 들어오는 것은 추진제 하나뿐이다");
            Assert.AreEqual(1, d.Stacks);
            Assert.AreEqual(6, d.Capacity, "칸은 여섯이지만 다섯 칸이 비어 있다");
        }

        [Test]
        public void Dodge_SpendsOneStack()
        {
            DodgeSystem d = Ready(2);

            Assert.IsTrue(d.TryDodge(true, Vector2.right, false, Vector2.zero));
            Assert.AreEqual(1, d.Stacks);
        }

        /// <summary>추진제가 없으면 회피가 안 나간다 — 재고로만 갈린다(운도 조작 미숙도 아니다).</summary>
        [Test]
        public void NoStacks_NoDodge()
        {
            var d = new DodgeSystem { BoosterCount = 2 }; // 부스터는 있는데 추진제가 아직 안 왔다

            Assert.IsFalse(d.CanDodge);
            Assert.IsFalse(d.TryDodge(true, Vector2.right, true, Vector2.up));
            Assert.IsFalse(d.IsInvincible);
        }

        // ---- 자동 · 수동 ----

        /// <summary>
        /// **수동이 자동을 이긴다.** 겹쳐도 회피는 한 번이고 **추진제도 1개만** 나간다 —
        /// 두 경로가 각각 소비하면 플릭 한 번에 재고가 두 개 빠진다.
        /// </summary>
        [Test]
        public void ManualBeatsAuto_AndSpendsOnlyOneStack()
        {
            DodgeSystem d = Ready();

            bool fired = d.TryDodge(autoTriggered: true, autoDirection: Vector2.right,
                                    manualFlick: true, flickDirection: Vector2.up);

            Assert.IsTrue(fired);
            Assert.AreEqual(DodgeTrigger.Manual, d.LastTrigger, "수동이 이긴다");
            Assert.AreEqual(Vector2.up, d.LastDirection, "플릭 방향으로 피한다");
            Assert.AreEqual(3, d.Stacks, "추진제는 하나만 나간다");
        }

        [Test]
        public void AutoFires_WhenNoManualInput()
        {
            DodgeSystem d = Ready();

            d.TryDodge(true, Vector2.left, false, Vector2.zero);

            Assert.AreEqual(DodgeTrigger.Auto, d.LastTrigger);
            Assert.AreEqual(Vector2.left, d.LastDirection);
        }

        [Test]
        public void NoTrigger_NoDodge()
        {
            DodgeSystem d = Ready();

            Assert.IsFalse(d.TryDodge(false, Vector2.zero, false, Vector2.zero));
            Assert.AreEqual(4, d.Stacks, "헛발질이 재고를 먹지 않는다");
        }

        // ---- 재발동 ----

        /// <summary>회피가 진행 중이면 재발동하지 않는다 — 종료 모션이 끝나야 다음이 나간다.</summary>
        [Test]
        public void CannotRetrigger_WhileDodging()
        {
            DodgeSystem d = Ready();
            d.TryDodge(true, Vector2.right, false, Vector2.zero);

            Assert.IsTrue(d.IsDodging);
            Assert.IsFalse(d.TryDodge(true, Vector2.right, true, Vector2.up), "진행 중엔 안 나간다");
            Assert.AreEqual(3, d.Stacks, "재고도 안 빠진다");
        }

        [Test]
        public void CanDodgeAgain_AfterItEnds()
        {
            DodgeSystem d = Ready();
            d.TryDodge(true, Vector2.right, false, Vector2.zero);

            d.Tick(DodgeSystem.InvincibleSeconds + DodgeSystem.RecoveryDelayTbd + 0.01f);

            Assert.IsFalse(d.IsDodging);
            Assert.IsTrue(d.TryDodge(true, Vector2.right, false, Vector2.zero));
            Assert.AreEqual(2, d.Stacks);
        }

        /// <summary>
        /// 종료 모션 딜레이는 **무적과 별개 값**이다 — 합치면 무적을 늘리려다 연사 속도까지 바뀐다.
        /// 아직 미확정이라 0 센티넬이고, 확정되면 이 테스트가 실패해 SO 승격을 알린다.
        /// </summary>
        [Test]
        public void RecoveryDelay_IsSeparateFromInvincibility_AndUnmeasured()
        {
            Assert.AreEqual(0f, DodgeSystem.RecoveryDelayTbd, D, "미측정 센티넬");
            Assert.AreNotEqual(DodgeSystem.InvincibleSeconds, DodgeSystem.RecoveryDelayTbd,
                "두 값이 같아지면 안 된다 — 별개 축이다");
        }

        // ---- 판정식과의 관계 ----

        /// <summary>
        /// **회피는 판정식의 항이 아니다.** 무적 구간에서는 피해 계산에 진입하지 않는다 —
        /// 판정식이 max(1, …) 구조라 「방어력 무한대」로 표현하면 여전히 1이 꽂힌다.
        /// 그 사실을 여기서 못 박는다: 아무리 큰 방어를 넣어도 최소 1이 나온다.
        /// </summary>
        [Test]
        public void InvincibilityCannotBeExpressedAsHugeDefence()
        {
            float withHugeDefence = DamageFormula.PerHit(100f, 1f, 1f, def: 999999f);

            Assert.AreEqual(1f, withHugeDefence, D, "방어를 아무리 올려도 1은 들어간다");
            // → 그래서 무적은 값이 아니라 **분기**여야 한다: 계산 자체를 건너뛴다.
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            DodgeSystem d = Ready();
            d.TryDodge(true, Vector2.right, false, Vector2.zero);

            d.Reset();

            Assert.AreEqual(0, d.Stacks);
            Assert.IsFalse(d.IsDodging);
            Assert.AreEqual(0, d.TotalDodges);
            Assert.AreEqual(DodgeTrigger.None, d.LastTrigger);
            Assert.AreEqual(2, d.BoosterCount, "부스터 대수는 보드의 것이라 초기화가 지우지 않는다");
        }

        // ---- 부스터 노드 자산 ----

        /// <summary>노드 7종 — 부스터가 자산으로 존재하고 추진제를 받는다.</summary>
        [Test]
        public void BoosterNode_ExistsAndTakesPropellant()
        {
            var boost = AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                "Assets/_Project/ScriptableObjects/Nodes/Node_boost.asset");

            Assert.NotNull(boost, "부스터 자산");
            Assert.AreEqual(NodeType.Booster, boost.type);
            Assert.IsTrue(boost.implemented);

            bool takesPropellant = false;
            foreach (NodePort p in boost.ports)
                if (p.io == PortIO.Input && p.kind == FlowKind.Propellant) takesPropellant = true;

            Assert.IsTrue(takesPropellant, "추진제 입력 포트");
        }

        /// <summary>
        /// 추진제 조합표가 가동된다 — 착수 금지가 풀렸다.
        /// 주기 15초/1개는 **선언치**이고 시뮬 실측 후 확정된다.
        /// </summary>
        [Test]
        public void PropellantRecipe_IsNowRunnable()
        {
            var muni = AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                "Assets/_Project/ScriptableObjects/Nodes/Node_muni.asset");

            NodeRecipe propellant = default;
            foreach (NodeRecipe r in muni.recipes)
                if (r.kind == RecipeKind.Propellant) propellant = r;

            Assert.IsTrue(propellant.IsRunnable, "추진제를 돌릴 수 있다");
            Assert.AreEqual(1f / 15f, propellant.outputPerSec, D, "15초에 1개");
            // ⚠️ 회피 스택 상한(부스터 대수 × 2)과 **다른 축**이다 — 이쪽은 마운트 한 칸에
            // 몇 개가 쌓이는가이고, 저쪽은 부스터가 채우는 게이지의 칸 수다.
            Assert.AreEqual(3f, propellant.stackLimitTbd, D, "추진제 아이템 최대 스택 3");
        }

        /// <summary>
        /// 군수 노드 한 대는 추진제를 15초에 하나 만든다 — 아이템 스택 3을 채우는 데 45초.
        /// **채우는 속도는 부스터가 아니라 여기서 정해진다.**
        /// </summary>
        [Test]
        public void PropellantStack_TakesFortyFiveSeconds_AtDeclaredRate()
        {
            var recipe = new NodeRecipe
            {
                kind = RecipeKind.Propellant, output = FlowKind.Propellant,
                outputPerSec = 1f / 15f, stackLimitTbd = 3f, implemented = true,
            };

            Assert.AreEqual(3f, NodeProduction.Produce(recipe, 0f, 45f), D);
            Assert.AreEqual(0f, NodeProduction.Produce(recipe, 3f, 45f), D, "버퍼가 차면 멈춘다");
        }
    }
}
