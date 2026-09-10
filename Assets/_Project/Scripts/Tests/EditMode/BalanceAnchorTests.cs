using System;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// balance_v4.json 앵커 검증(CLAUDE.md §4 검증 방법).
    /// BalanceConfig.asset 값이 원천 json을 미러함을 assert.
    ///
    /// ⚠️ **요구치를 정하는 규칙이 바뀌었다** (2026-09-10 · `260910_W02` 2-1·2-2).
    /// 요구치는 이제 **도달치 × 0.9**(역산)이고, 여기 있는 셋은 그 관계를 잰다 —
    /// 대표 조합 **140** · S3 **126** · S4 **Fixed 183**.
    /// **돌파 타깃(`s3Break`)과 S4 밴드(`s4Band`)는 폐기됐다** — 미러할 값 자체가 없어졌다.
    /// 턱걸이를 막는 장치는 **상수 0.9 하나**이며, 돌파 체감이 부족하면 그 상수를 올린다.
    /// 재현/참고값은 TestContext.WriteLine 으로 로그에 인쇄.
    ///
    /// 실행 전 메뉴 MBI/Generate Balance + Nodes 로 자산 생성 필요.
    /// </summary>
    public sealed class BalanceAnchorTests
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/BalanceConfig.asset";
        private const float Delta = 0.001f;

        /// <summary>
        /// 요구치 역산 상수 — **요구치 = 도달치 × 0.9** (`260910_W02` 2-2).
        /// ⚠️ 구 `S3BreakMargin`(돌파 +10%)은 **폐기**됐다. 둘을 겹쳐 걸면 타깃이 도달치의
        /// 99% 가 되어 남는 여유가 반올림 잔여와 구분되지 않았다.
        /// </summary>
        private const double ReqRatio = 0.9;

        private BalanceConfig _config;
        private BalanceJson _json;

        [SetUp]
        public void SetUp()
        {
            _json = BalanceJsonLoader.Load();
            _config = AssetDatabase.LoadAssetAtPath<BalanceConfig>(ConfigPath);
            if (_config == null)
                Assert.Ignore($"BalanceConfig 자산 없음: {ConfigPath} — 먼저 메뉴 'MBI/Generate Balance + Nodes' 실행.");
        }

        private static long RoundAway(double v) =>
            (long)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>대표 조합 = **표준 4 + 폭발 2** = 140. 원천 좌표에서 그때그때 낸다 — 새 상수를 만들지 않는다.</summary>
        private float Representative() => 4f * _json.Param("dA1") + 2f * _json.Param("dA2");

        // ---- 0. 드리프트 감시: SO가 원천 json을 미러하는가 ----
        [Test]
        public void Config_MirrorsSource_NoDrift()
        {
            Assert.AreEqual(_json.meta.schemaVersion, _config.schemaVersion, "schemaVersion 드리프트(§7)");
            Assert.AreEqual(_json.Param("origin"), _config.origin, Delta);
            Assert.AreEqual(_json.Param("enh"), _config.enh, Delta);
            Assert.AreEqual(_json.enhance.enhBand[0], _config.enhBand.x, Delta);
            Assert.AreEqual(_json.enhance.enhBand[1], _config.enhBand.y, Delta);
            Assert.AreEqual(_json.enhance.snapBand, _config.snapBand, Delta);
            Assert.AreEqual(_json.enhance.s4Cost, _config.s4Cost, Delta);
        }

        /// <summary>
        /// 드론(로봇 B) 확정치 미러. 이 넷이 어긋나면 등가선이 깨진다 —
        /// pB × dB = 1.0 × 100 = 100 이고, 그게 관통 20×5와 같은 DPS라는 것이 로봇 B의 밸런스 근거다.
        /// </summary>
        [Test]
        public void Config_MirrorsDroneParams()
        {
            Assert.AreEqual(_json.Param("slot"), _config.droneSlots, Delta, "슬롯 수");
            Assert.AreEqual(_json.Param("r"), _config.droneReleaseRate, Delta, "슬롯당 방출률");
            Assert.AreEqual(_json.Param("dB"), _config.droneCharge, Delta, "1기 충전량");
            Assert.AreEqual(_json.Param("pB"), _config.droneInflow, Delta, "몸체 유입");

            Assert.AreEqual(100f, _config.droneInflow * _config.droneCharge, Delta,
                "등가선 — 드론 DPS 100");
        }

        // ---- 1. 원점 = 100 ----
        [Test]
        public void Anchor1_Origin_Is100()
        {
            TestContext.WriteLine($"[재현] origin = {_config.origin}");
            Assert.AreEqual(100f, _config.origin, Delta);
        }

        // ---- 2. S4는 강화-only 벽이다 (물류 천장은 폐기됐다) ----

        /// <summary>
        /// ⚠️ **물류 천장 ×1.6은 폐기됐다**(260901_V05 §2층). 종전 이 자리는
        /// 「S3req &lt; 천장 160 &lt; S4밴드.lo」를 못 박고 있었다.
        ///
        /// 폐기 근거는 셋이었다 — 측정된 적이 없고, 문서가 이미 세 곳에서 부정하고 있으며,
        /// 억제는 경제가 이미 하고 있다(요구치 초과분은 시간만 줄고 보상은 그대로다).
        ///
        /// **지켜야 할 사실은 남는다**: S4는 물류만으로 못 넘고 강화가 있어야 넘는다.
        /// 천장이라는 이름 없이 그것만 잰다.
        /// </summary>
        [Test]
        public void Anchor2_S4_IsAnEnhancementOnlyWall()
        {
            // ⚠️ **S4 는 밴드가 아니라 고정치다** (2026-09-10 · `260910_W02` 2-4) —
            // 종전에 여기서 읽던 `reqBand[0]`(149)은 **폐기**됐고 `req` 183 이 정본이다.
            float representative = Representative();                                   // 140
            float s4Req = _json.Stage("S4").req;                                       // 183
            float enhanced = representative * _config.enh;                             // 203

            TestContext.WriteLine(
                $"[재현] 대표 {representative} < S4 요구치 {s4Req} ≤ 강화후 {enhanced:F1}");

            Assert.Less(_json.Stage("S3").req, representative, "S3는 물류만으로 통과 가능");
            Assert.Less(representative, s4Req, "대표 조합으로는 S4를 못 넘는다");
            Assert.GreaterOrEqual(enhanced, s4Req, "강화하면 넘는다");
        }

        // ---- 3. 요구치는 도달치 × 0.9 다 (260910_W02 2-1·2-2 정본) ----

        /// <summary>
        /// ⚠️ **구 `Anchor3_S3Break_Is114_AndNowMatchesItsFormula` 는 폐기**됐다 —
        /// 재던 값(`s3Break` 114)과 규칙(돌파 +10%)이 **둘 다 없어졌다.**
        ///
        /// 이 자리가 재는 것은 새 규칙 하나다 — **요구치 = 도달치 × 0.9.**
        /// S3 는 도달 140 × 0.9 = **126**, S4 는 강화 후 도달 203 × 0.9 = **183**.
        /// **S1 과 S2 가 같은 54 인 것은 오타가 아니다**(`260910_W02` 2-3).
        /// </summary>
        [Test]
        public void Anchor3_Requirements_AreReachTimesNinetyPercent()
        {
            float representative = Representative();                 // 140
            float enhanced = representative * _config.enh;           // 203

            long s3Derived = RoundAway(representative * ReqRatio);   // 126
            long s4Derived = RoundAway(enhanced * ReqRatio);         // 183

            TestContext.WriteLine(
                $"[재현] S3 = round({representative}x{ReqRatio}) = {s3Derived} · " +
                $"S4 = round({enhanced:F1}x{ReqRatio}) = {s4Derived}");

            Assert.AreEqual(126f, _json.Stage("S3").req, Delta, "S3 정본 126");
            Assert.AreEqual(s3Derived, (long)_json.Stage("S3").req, "S3 도출식이 원천과 맞는다");

            Assert.AreEqual(183f, _json.Stage("S4").req, Delta, "S4 정본 183");
            Assert.AreEqual(s4Derived, (long)_json.Stage("S4").req, "S4 도출식이 원천과 맞는다");

            // ⚠️ **S1·S2 는 54 에서 36 으로 내려갔다**(2026-09-11 · `260911_W01` 값 4).
            // 54 는 도달 60 전제였고, **60 의 출처는 「표준탄 라인 스펙 6발/초」** 였다 —
            // 그것은 **마운트 소비 상한**이지 시작 보드가 채워야 할 값이 아니다(W01 2-2).
            // 네 줄 보드의 도달은 40 이고 40 × 0.9 = 36 이다.
            Assert.AreEqual(36f, _json.Stage("S1").req, Delta, "S1 정본 36");
            Assert.AreEqual(RoundAway(40f * ReqRatio), (long)_json.Stage("S1").req,
                "S1 도 같은 역산이다 — 도달 40 × 0.9");
            Assert.AreEqual(_json.Stage("S1").req, _json.Stage("S2").req, Delta,
                "S2 는 S1 과 같은 값이 맞다 — 오타가 아니다(260910_W02 2-3)");
        }

        // ---- 4. 돌파 타깃과 S4 밴드는 원천에서 사라졌다 ----

        /// <summary>
        /// ⚠️ **구 `Anchor4_S4Band_Is186To215_MirrorsSource` 는 폐기**됐다.
        ///
        /// 폐기를 **표기로만 두면 값이 조용히 돌아온다.** 이 자리는 그것을 막는다 —
        /// S4 는 밴드가 아니라 고정치여야 하고, `reqBand` 가 남아 있으면 두 벌이 다시 생긴다.
        /// </summary>
        [Test]
        public void Anchor4_RetiredAnchors_DoNotComeBack()
        {
            Assert.AreEqual("fixed", _json.Stage("S4").reqType,
                "S4 는 Fixed 다 — 밴드로 되돌아가지 않는다");
            Assert.IsTrue(_json.Stage("S4").reqBand == null || _json.Stage("S4").reqBand.Length == 0,
                "S4 에 reqBand 가 남아 있으면 요구치가 두 벌이 된다");

            Assert.AreEqual(183f, _json.Stage("S4").req, Delta,
                "S4 요구치는 stages 에 있다 — enhance 블록이 아니다");

            // ⚠️ 도달치 203 은 구 밴드 상한 215 아래였다. 그 상한이 없어졌으므로
            // 「밴드 안에 든다」는 판정도 함께 사라진다 — 남는 것은 요구치를 넘느냐 하나다.
            Assert.Greater(Representative() * _config.enh, _json.Stage("S4").req,
                "강화 후 도달치가 요구치를 넘는다");
        }

        // ---- 5. 강화 위치: 대표 조합×enh 가 S4 요구치를 넘고 enh ∈ enhBand ----
        [Test]
        public void Anchor5_Enh_Is145_AndClearsTheS4Requirement()
        {
            // ⚠️ **기준을 대표 조합으로 옮겼다** (`260910_W01` 3-2) — 종전에는 `s3Break`에 곱했다.
            float representative = Representative();                                   // 140
            float enhanced = representative * _config.enh;                             // 203
            float s4Req = _json.Stage("S4").req;                                       // 183

            TestContext.WriteLine(
                $"[재현] 대표×enh = {representative}×{_config.enh} = {enhanced:F1} · S4 요구치 {s4Req}");

            Assert.AreEqual(1.45f, _config.enh, Delta, "enh 앵커");
            Assert.GreaterOrEqual(_config.enh, _config.enhBand.x, "enh ≥ enhBand.lo");
            Assert.LessOrEqual(_config.enh, _config.enhBand.y, "enh ≤ enhBand.hi");
            Assert.GreaterOrEqual(enhanced, s4Req, "강화 결과가 S4 요구치를 넘는다");
            // ⚠️ 구 「강화 결과 ≤ S4 밴드 상한」은 폐기 — 상한 자체가 없어졌다(260910_W02 2-1).
            // 대신 역산 관계를 잰다: 요구치는 도달치보다 낮고, 그 차가 0.9 에서 나온다.
            Assert.Less(s4Req, enhanced, "요구치 < 도달치 — 역산이므로 항상 여유가 있다");
        }

        // ---- 6. 오프라인 상한 = 36시간 (경제 항목 중 유일한 확정치) ----
        [Test]
        public void Anchor6_OfflineCapHours_Is36_MirrorsSource()
        {
            float src = _json.economy != null && _json.economy.offline != null ? _json.economy.offline.capHours : 0f;
            TestContext.WriteLine($"[재현] economy.offline.capHours = {src} → BalanceConfig {_config.offlineCapHours}");

            Assert.AreEqual(36f, src, Delta, "원천 계약값");
            Assert.AreEqual(36f, _config.offlineCapHours, Delta, "SO 미러");
        }

        // ---- 드리프트 트립와이어: 경제 TBD가 확정되면 여기서 실패해 승격 판단을 강제한다 ----
        [Test]
        public void EconomyTbdParams_StillUnconfirmed()
        {
            foreach (string key in new[] { "scrapPerKill", "offlineCoef", "offlineBaseRate" })
            {
                bool confirmed = ParamConfirmed(key);
                TestContext.WriteLine($"[재현] {key}.confirmed = {confirmed}");
                Assert.IsFalse(confirmed,
                    $"{key}가 확정됐다 — EconomyConfig의 TBD 필드를 계약 미러(BalanceConfig)로 승격할지 판단할 것");
            }
        }

        // ---- 상주 파밍 정원·간격도 아직 미확정 ----
        [Test]
        public void StageSpawnParams_StillUnconfirmed()
        {
            foreach (string id in new[] { "S1", "S2", "S3", "S4", "S5", "S6" })
            {
                StageEntry s = _json.Stage(id);
                TestContext.WriteLine($"[재현] {id} spawnCap={s.spawnCap} spawnInterval={s.spawnInterval} confirmed={s.spawnConfirmed}");
                Assert.IsFalse(s.spawnConfirmed, $"{id} 정원·간격이 확정됐다 — 값을 검증 대장에서 SO로 반영할 것");
            }
        }

        private bool ParamConfirmed(string key)
        {
            if (_json.paramList == null) return false;
            foreach (ParamEntry p in _json.paramList)
                if (p.key == key) return p.confirmed;
            return false; // 없는 키는 확정된 적이 없다
        }
    }
}
