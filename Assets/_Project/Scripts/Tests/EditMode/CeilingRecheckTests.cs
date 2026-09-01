using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 밸런스 재검증 실측(260824_V02 §5 · 260825_V01 §0에서 밸런스 문서 11-4로 등재).
    ///
    /// 물음: 소비 상한 6발/초를 채우는 최대 조합이 물류 천장 160을 넘는가, 넘으면 무엇이 막는가.
    /// V02 §5의 표적은 「군수 6노드 구성이 전력에서 최소 40 이상 깎이는지」다.
    ///
    /// ⚠️ 이 파일은 **현재 데이터로 무엇이 나오는가**를 기록한다. 노드별 전력 카탈로그가
    /// 미확정(밸런스 11-4 미결)이므로 결과는 「엔진 정합 PASS / 수치 TBD」로 읽어야 한다.
    /// </summary>
    public sealed class CeilingRecheckTests
    {
        private const float D = 0.001f;
        private const float PerNode = 1f;   // muniPerNode 확정치
        private const float Origin = 100f;  // params origin
        private const float Ceil = 1.6f;    // params ceil → 천장 160

        private static List<MunitionsLine> Mix(int pierce, int split, int explosive) => new List<MunitionsLine>
        {
            new MunitionsLine(AmmoKind.Pierce, 5f, 20f, pierce),
            new MunitionsLine(AmmoKind.Split, 4f, 25f, split),
            new MunitionsLine(AmmoKind.Explosive, 2f, 50f, explosive),
        };

        private static float Rate(IReadOnlyList<MunitionsLine> lines)
        {
            float sum = 0f;
            foreach (MunitionsLine m in lines)
                sum += AmmoLineProduction.LineOutput(m.specShotsPerSec, m.nodeCount, PerNode);
            return sum;
        }

        /// <summary>
        /// V02 §5가 지목한 조합: 분열 4 + 폭발 2 = 6노드 = 소비 상한을 정확히 채운다.
        /// 출력 200, 천장 160 → **40 초과.**
        /// </summary>
        [Test]
        public void SixNodes_SplitFourPlusExplosiveTwo_Yields200_Over160Ceiling()
        {
            List<MunitionsLine> mix = Mix(0, 4, 2);

            Assert.AreEqual(6f, Rate(mix), D, "4 + 2 = 6발/초 = 소비 상한 capA와 같다");
            Assert.AreEqual(200f, AmmoLineProduction.TotalOutput(mix, PerNode), D, "100 + 100");
            Assert.AreEqual(160f, Origin * Ceil, D, "물류 천장");
            Assert.Greater(AmmoLineProduction.TotalOutput(mix, PerNode), Origin * Ceil,
                "생산만으로 천장을 넘는다 — 무엇이 막는지가 재검증의 물음");
        }

        /// <summary>
        /// 소비 상한 6발/초 안에서 **가장 높은 출력이 이 조합**이라는 확인.
        /// 폭발만 6노드로 몰면 스펙 2에서 잘려 100밖에 안 나온다 — 상한이 조합을 강제한다.
        /// </summary>
        [Test]
        public void MaxOutputWithinConsumptionCap_IsSplitFourPlusExplosiveTwo()
        {
            Assert.AreEqual(200f, AmmoLineProduction.TotalOutput(Mix(0, 4, 2), PerNode), D);

            // 한 탄종에 몰면 그 라인 스펙에서 잘려 등가선 100을 못 넘는다.
            // 등가선이 「스펙 × 발당피해 = 100」이므로 단일 탄종의 최대는 언제나 100이다.
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(Mix(0, 0, 6), PerNode), D,
                "폭발만 6노드 → 스펙 2에서 잘려 2발/초 × 50 = 100");
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(Mix(6, 0, 0), PerNode), D,
                "관통만 6노드 → 스펙 5에서 잘려 5발/초 × 20 = 100");

            // 남는 노드는 버려진다 — 관통 5노드로 이미 100이고 6번째는 아무것도 안 한다.
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(Mix(5, 0, 0), PerNode), D);
        }

        /// <summary>
        /// **소비 쪽은 살아났다.** 군수 노드 대당 2/초가 확정되면서(260829_V03) 6노드를 깔면
        /// 전력 소비가 12/초로 실제로 늘어난다 — 종전에는 0이었다(전 노드 고정비 66이 코어에 lumped).
        ///
        /// ⚠️ **그런데도 감쇠는 0이다.** 공급 쪽 대당 값이 아직 없어 에너지 노드 **한 대**가
        /// 발전 용량 합 80을 통째로 낸다. 12 &lt; 80이므로 효율은 1이다.
        /// V02 §5가 물은 「최소 40 이상 깎이는지」의 답은 여전히 **0 깎임**이고,
        /// 남은 미확정은 **에너지 대당 발전량** 하나다(260829_V03 미확정 5건 #1).
        ///
        /// 그 값이 오면 아래 Tbd 단언이 실패해 재산정을 알린다.
        /// </summary>
        [Test]
        public void PowerStillDoesNotThrottle_ButNowBecauseGenerationPerNodeIsUnset()
        {
            NodeDefinition muni = Node("muni");
            NodeDefinition ener = Node("ener");

            Assert.AreEqual(2f, muni.resources.powerDraw, D, "군수 대당 2/초(확정)");

            float draw6 = 6f * muni.resources.powerDraw;
            Assert.AreEqual(12f, draw6, D, "6노드 = 12/초 — 노드를 늘리면 실제로 늘어난다");

            // ⚠️ **해소됐다**(260901_V02 판정 4). 종전에는 `pwc`(발전 용량 **합** 80)가 노드 한 대에
            // 얹혀 있어 한 대가 6노드를 다 먹여 살렸고, 그래서 **전력 축이 한 번도 작동한 적이 없었다.**
            Assert.AreEqual(10f, ener.resources.powerSupply, D, "에너지 대당 발전량 10/초(확정)");
            Assert.Less(ener.resources.powerSupply, draw6,
                "한 대로는 6노드를 못 먹인다 — 전력이 실제 제약이 된다");

            Assert.AreEqual(ConfirmState.Confirmed, ener.resources.confirm,
                "대당 발전량이 확정됐다");
        }

        /// <summary>
        /// 천장을 전력으로 막으려면 **6노드 구성에서 공급이 12 미만**이어야 한다.
        /// 필요한 감쇠 0.8(아래 테스트)을 전력만으로 내려면 공급 ÷ 소비 = 0.8, 즉 공급 9.6이다.
        /// 지금은 그 숫자를 만들 대당 발전량이 없어 **계산만 해 두고 값은 비운다.**
        /// </summary>
        [Test]
        public void SupplyNeededToThrottleByPower_Is9Point6()
        {
            float draw6 = 6f * Node("muni").resources.powerDraw;

            Assert.AreEqual(9.6f, draw6 * 0.8f, D, "12 × 0.8 — 이 아래로 공급돼야 전력이 막는다");
        }

        private static NodeDefinition Node(string id)
        {
            var n = UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                $"Assets/_Project/ScriptableObjects/Nodes/Node_{id}.asset");
            Assert.NotNull(n, $"{id} 노드 자산");
            return n;
        }

        /// <summary>
        /// 천장을 무엇으로 막을지의 선택지 확인용 실측:
        /// 6노드가 천장 160 안에 들어오려면 전체 배율이 0.8 이하로 깎여야 한다.
        /// </summary>
        [Test]
        public void ThrottleNeededToRespectCeiling_Is0Point8()
        {
            float output = AmmoLineProduction.TotalOutput(Mix(0, 4, 2), PerNode);
            float needed = (Origin * Ceil) / output;

            Assert.AreEqual(0.8f, needed, D, "160 ÷ 200 — 20% 이상 깎여야 천장 안");
        }
    }
}
