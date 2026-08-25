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
        /// **전력이 상한 역할을 하지 못한다.** 군수 노드의 powerDraw가 0이라 6노드를 깔아도
        /// 전력 소비가 늘지 않는다(전 노드 고정비 66이 코어에 lumped). V02 §5가 물은
        /// 「최소 40 이상 깎이는지」의 답은 현재 데이터에서 **0 깎임**이다.
        ///
        /// 이는 밸런스가 틀렸다는 뜻이 아니라 **노드별 전력 카탈로그가 아직 없다**는 뜻이다
        /// (밸런스 11-4 미결). 카탈로그가 들어오면 이 테스트가 실패해 재산정을 알린다.
        /// </summary>
        [Test]
        public void PowerDoesNotThrottle_BecauseMunitionsDrawIsUnset()
        {
            NodeDefinition muni = UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDefinition>(
                "Assets/_Project/ScriptableObjects/Nodes/Node_muni.asset");
            Assert.NotNull(muni, "군수 노드 자산");

            Assert.AreEqual(0f, muni.resources.powerDraw, D,
                "군수 노드 전력 소비가 0 — 6노드를 깔아도 전력이 늘지 않아 천장을 막지 못한다");
            Assert.AreEqual(ConfirmState.Confirmed, muni.resources.confirm,
                "생산은 확정됐다 — 미확정인 것은 전력 쪽(밸런스 11-4)");
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
