using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 구성별 출력 실측(260824_V02 §5 · 260825_V01 §0에서 밸런스 문서 11-4로 등재).
    ///
    /// ⚠️ **물류 천장 ×1.6은 폐기됐다**(260901_V05 §2층). 이 파일의 원래 물음은
    /// 「6노드 최대 조합이 천장 160을 넘는가」였고, 답은 「넘는다(200)」였다.
    /// 천장이 사라졌으므로 그 물음은 닫혔고, **남은 것은 구성별 출력이라는 사실**이다.
    ///
    /// 그 사실이 여전히 값진 이유: 조합 축이 살아 있는지를 여기서 볼 수 있다.
    /// 섞은 쪽이 몰아넣은 쪽보다 세다는 것이 라인 스펙 상한의 존재 이유다.
    ///
    /// ⚠️ **2026-09-09 표준탄 좌표 확정으로 값이 전부 내려갔다**(`260909_W01` 2-1·2-4).
    /// 구 분열탄 25×4가 표준탄 **10×6**이 되면서 최대 조합이 **200 → 180**이고,
    /// 최대를 내는 조합도 「표준 4 + 폭발 2」가 아니라 **「관통 4 + 폭발 2」**로 바뀌었다.
    /// **구조는 그대로 산다** — 단일 탄종은 여전히 잘리고 최대는 여전히 섞어야 나온다.
    ///
    /// ⚠️ 노드별 전력은 260901_V02에서 확정됐다 — 종전의 「수치 TBD」 표기는 해소됐다.
    /// </summary>
    public sealed class CeilingRecheckTests
    {
        private const float D = 0.001f;
        private const float PerNode = 1f;   // muniPerNode 확정치
        private const float Origin = 100f;  // params origin

        private static List<MunitionsLine> Mix(int pierce, int standard, int explosive) => new List<MunitionsLine>
        {
            new MunitionsLine(AmmoKind.Pierce, 5f, 20f, pierce),
            new MunitionsLine(AmmoKind.Standard, 6f, 10f, standard),
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
        /// **`260909_W01` 2-4 표 그대로.** 소비 상한 6발/초에서 조합 넷이 무엇을 내는가.
        ///
        /// 표에 있는 네 줄을 여기 옮겨 적는다 — 값이 흔들리면 이 시험이 먼저 빨개진다.
        /// </summary>
        [Test]
        public void ConsumptionCapCombinations_MatchW01Table()
        {
            // 표준 6 — 단일 탄종으로 상한을 다 채운 자리. 초당 출력 60.
            Assert.AreEqual(6f, Rate(Mix(0, 6, 0)), D, "표준 6발/초 = 소비 상한을 채운다");
            Assert.AreEqual(60f, AmmoLineProduction.TotalOutput(Mix(0, 6, 0), PerNode), D,
                "표준 6 = 60 (W01 2-4)");

            // 표준 4 + 폭발 2 — 구 최대 조합이 있던 자리. 이제 140이다.
            Assert.AreEqual(140f, AmmoLineProduction.TotalOutput(Mix(0, 4, 2), PerNode), D,
                "표준 4 + 폭발 2 = 40 + 100 = 140 (W01 2-4)");

            // 관통 5 + 폭발 1.
            Assert.AreEqual(150f, AmmoLineProduction.TotalOutput(Mix(5, 0, 1), PerNode), D,
                "관통 5 + 폭발 1 = 100 + 50 = 150 (W01 2-4)");

            // 관통 4 + 폭발 2 — **새 최대.**
            Assert.AreEqual(180f, AmmoLineProduction.TotalOutput(Mix(4, 0, 2), PerNode), D,
                "관통 4 + 폭발 2 = 80 + 100 = 180 (W01 2-4)");
        }

        /// <summary>
        /// 소비 상한 6발/초 안에서 **가장 높은 출력이 관통 4 + 폭발 2**라는 확인.
        ///
        /// ⚠️ **구 등가선 「단일 탄종의 최대는 언제나 100」이 깨졌다.** 표준탄만 60에서 잘린다 —
        /// 축이 초당 출력에서 노드당 출력으로 옮겨졌기 때문이며(W01 2-2), 표준탄은 체인이
        /// 짧은 것으로 그 차이를 돌려받는다. **결함이 아니다.**
        /// </summary>
        [Test]
        public void MaxOutputWithinConsumptionCap_IsPierceFourPlusExplosiveTwo()
        {
            float best = AmmoLineProduction.TotalOutput(Mix(4, 0, 2), PerNode);
            Assert.AreEqual(180f, best, D);

            // 한 탄종에 몰면 그 라인 스펙에서 잘린다.
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(Mix(0, 0, 6), PerNode), D,
                "폭발만 6노드 → 스펙 2에서 잘려 2발/초 × 50 = 100");
            Assert.AreEqual(100f, AmmoLineProduction.TotalOutput(Mix(6, 0, 0), PerNode), D,
                "관통만 6노드 → 스펙 5에서 잘려 5발/초 × 20 = 100");
            Assert.AreEqual(60f, AmmoLineProduction.TotalOutput(Mix(0, 6, 0), PerNode), D,
                "표준만 6노드 → 스펙 6이라 안 잘리는데도 60 — 구 등가선이 깨진 자리");

            // **섞은 쪽이 몰아넣은 쪽보다 세다** — 라인 스펙 상한의 존재 이유이며 여기는 살아 있다.
            Assert.Greater(best, AmmoLineProduction.TotalOutput(Mix(6, 0, 0), PerNode),
                "관통 4 + 폭발 2 > 관통 6");
            Assert.Greater(best, AmmoLineProduction.TotalOutput(Mix(0, 6, 0), PerNode),
                "관통 4 + 폭발 2 > 표준 6");
            Assert.Greater(best, AmmoLineProduction.TotalOutput(Mix(0, 4, 2), PerNode),
                "140에서 180으로 갈아탈 자리가 있다 — 실측 3번이 겨누는 것");

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

        // ⚠️ `ThrottleNeededToRespectCeiling_Is0Point8`을 지웠다(260901_V05 §2층).
        // 「천장 안에 들어오려면 0.8로 깎여야 한다」는 **막을 천장이 있을 때만** 뜻이 있는 값이다.
        // 천장이 폐기됐으므로 그 물음 자체가 닫혔다 — 억제는 경제가 한다.
    }
}
