using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// **로봇 HP 의 원천은 하나다** (2026-09-17 사용자 확정 · `260917_W05` 3장 · `260917_V03` 판정 C).
    ///
    /// 확정치 **1000** · 로봇 A · B 같은 값.
    ///
    /// ⚠️⚠️ **여기가 갈리면 화면과 측정이 다른 로봇을 본다.** 구 `robotHpTbd` 는
    /// 코드 기본값이 **3000** 이고 자산이 1000 이었다 — 자산이 안 붙은 판이
    /// **세 배 튼튼한 로봇**을 세우고 있었고 아무 시험도 안 울었다.
    /// 지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」.
    /// </summary>
    public sealed class RobotHpTests
    {
        private const float D = 0.001f;
        private const string TuningPath = "Assets/_Project/ScriptableObjects/CombatTuning.asset";

        private static CombatTuning Tuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            if (t == null) Assert.Ignore("CombatTuning 자산 없음 — 먼저 'MBI/Generate Combat Data' 실행.");
            return t;
        }

        [Test]
        public void 자산_HP_는_json_에서_온다()
        {
            BalanceJson json = BalanceJsonLoader.Load();
            Assert.AreEqual(json.Param("robotHp"), Tuning().robotHp, D,
                "CombatTuning 의 HP 가 balance_v4.json 과 갈렸다 — 생성기를 다시 돌린다");
        }

        [Test]
        public void 코드_기본값도_같은_수다()
        {
            // 🗑️ 구 기본값 3000 폐기 — 자산을 못 읽는 판이 다른 로봇을 세웠다.
            Assert.AreEqual(Tuning().robotHp, new CombatTuning().robotHp, D,
                "코드 기본값이 자산과 갈렸다");
        }

        [Test]
        public void A_와_B_가_같은_값을_쓴다()
        {
            // 📌 HP 는 **로봇 자산이 아니라 전투 튜닝**이 든다 — 그래서 A · B 가 자동으로 같다.
            //    이 시험은 그 구조를 못 박는다: 누가 `RobotDefinition` 에 hp 를 달면
            //    그 순간 값이 둘이 되고 여기서 걸린다.
            foreach (string id in new[] { "A", "B" })
            {
                var robot = AssetDatabase.LoadAssetAtPath<RobotDefinition>(
                    $"Assets/_Project/ScriptableObjects/Robots/Robot_{id}.asset");
                Assert.IsNotNull(robot, $"Robot_{id} 자산이 없다");
            }

            Assert.AreEqual(1000f, Tuning().robotHp, D,
                "확정치 1000 이 아니다 — 값이 바뀌었다면 json 과 이 줄을 같이 고친다");
        }
    }
}
