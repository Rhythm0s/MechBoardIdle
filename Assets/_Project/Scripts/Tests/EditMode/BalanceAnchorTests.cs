using System;
using MBI.Data;
using MBI.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// balance_v4.json 앵커 검증(CLAUDE.md §4 검증 방법).
    /// BalanceConfig.asset 값이 원천 json을 미러함을 assert. 재도출 체인은 sanity-only —
    /// v4는 s3Break을 2차 실측으로 145로 개정(v3.1의 round(130×1.1)=143에서 상향)했고,
    /// S4밴드[186,215]는 원천 확정치(145×enhBand 재도출[189,218]과 불일치)라 재도출이 아닌 미러로 검증.
    /// 재현/참고값은 TestContext.WriteLine 으로 로그에 인쇄.
    ///
    /// 실행 전 메뉴 MBI/Generate Balance + Nodes 로 자산 생성 필요.
    /// </summary>
    public sealed class BalanceAnchorTests
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/BalanceConfig.asset";
        private const float Delta = 0.001f;

        // S3 돌파 마진(+10%): CLAUDE.md §9 "S3 돌파 = req + 10% 마진". 재현 규칙 상수.
        private const double S3BreakMargin = 1.1;

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

        // ---- 0. 드리프트 감시: SO가 원천 json을 미러하는가 ----
        [Test]
        public void Config_MirrorsSource_NoDrift()
        {
            Assert.AreEqual(_json.meta.schemaVersion, _config.schemaVersion, "schemaVersion 드리프트(§7)");
            Assert.AreEqual(_json.Param("origin"), _config.origin, Delta);
            Assert.AreEqual(_json.Param("ceil"), _config.ceil, Delta);
            Assert.AreEqual(_json.Param("enh"), _config.enh, Delta);
            Assert.AreEqual(_json.enhance.s3Break, _config.s3Break, Delta);
            Assert.AreEqual(_json.enhance.s4Band[0], _config.s4Band.x, Delta);
            Assert.AreEqual(_json.enhance.s4Band[1], _config.s4Band.y, Delta);
        }

        // ---- 1. 원점 = 100 ----
        [Test]
        public void Anchor1_Origin_Is100()
        {
            TestContext.WriteLine($"[재현] origin = {_config.origin}");
            Assert.AreEqual(100f, _config.origin, Delta);
        }

        // ---- 2. 물류 천장 = origin*ceil = 160, S3req < 160 < S4밴드.lo ----
        [Test]
        public void Anchor2_LogisticsCeiling_Is160_AndS4IsAboveIt()
        {
            float ceiling = _config.LogisticsCeiling;
            float s3Req = _json.Stage("S3").req;
            TestContext.WriteLine($"[재현] 물류천장 = {_config.origin} × {_config.ceil} = {ceiling}");
            TestContext.WriteLine($"[재현] S3req {s3Req} < 천장 {ceiling} < S4밴드.lo {_config.s4Band.x}");

            Assert.AreEqual(160f, ceiling, Delta, "물류 천장");
            Assert.Less(s3Req, ceiling, "S3는 물류만으로 통과 가능");
            Assert.Less(ceiling, _config.s4Band.x, "S4는 물류 천장 초과 = 강화-only 벽");
        }

        // ---- 3. S3 돌파 = 145 (v4 2차 실측 개정치, 원천 미러) ----
        [Test]
        public void Anchor3_S3Break_Is145_MirrorsSource()
        {
            // v4: s3Break = 145 (v3.1의 round(S3req 130 ×1.1)=143에서 재측정으로 상향).
            // 재도출이 아니라 원천 미러 — 재도출식은 sanity-only(§5-3 뱅커스 라운딩 함정 회피).
            float s3ReqLegacy = _json.Stage("S3").req;                    // 130 (sanity)
            long v31Reproduced = RoundAway(s3ReqLegacy * S3BreakMargin);  // 143 (구 v3.1 도출·참고)
            TestContext.WriteLine(
                $"[미러] s3Break(v4) = {_config.s3Break} (원천 {_json.enhance.s3Break}); " +
                $"참고 v3.1 도출 round({s3ReqLegacy}×{S3BreakMargin})={v31Reproduced}");

            Assert.AreEqual(145f, _json.enhance.s3Break, Delta, "원천 json s3Break = 145(v4)");
            Assert.AreEqual(_json.enhance.s3Break, _config.s3Break, Delta, "SO가 원천 미러");
            Assert.Greater(_config.s3Break, s3ReqLegacy, "s3Break > S3req(돌파 마진 존재)");
        }

        // ---- 4. S4 밴드 = [186,215] (v4 원천 확정치, 미러) ----
        [Test]
        public void Anchor4_S4Band_Is186To215_MirrorsSource()
        {
            // v4: S4밴드[186,215]는 원천 확정치. s3Break(145)×enhBand로 재도출하면 [189,218]가 되어
            // 원천과 불일치 → 재도출 금지, 미러만(재도출은 sanity-only).
            float[] band = _json.enhance.s4Band;
            TestContext.WriteLine($"[미러] S4밴드 = [{band[0]}, {band[1]}] (원천 확정)");

            Assert.AreEqual(186f, band[0], Delta, "원천 S4밴드 하한");
            Assert.AreEqual(215f, band[1], Delta, "원천 S4밴드 상한");
            Assert.AreEqual(band[0], _config.s4Band.x, Delta, "SO 밴드 하한 미러");
            Assert.AreEqual(band[1], _config.s4Band.y, Delta, "SO 밴드 상한 미러");
            Assert.Less(_config.s3Break, _config.s4Band.x, "s3Break < S4밴드.lo (S4 강화-only 벽)");
        }

        // ---- 5. 강화 위치: s3Break×enh ∈ [186,215], enh ∈ enhBand ----
        [Test]
        public void Anchor5_Enh_Is145_LandsInsideBand()
        {
            float enhanced = _config.s3Break * _config.enh;
            TestContext.WriteLine(
                $"[재현] s3Break×enh = {_config.s3Break}×{_config.enh} = {enhanced:F2} ∈ [{_config.s4Band.x}, {_config.s4Band.y}]");

            Assert.AreEqual(1.45f, _config.enh, Delta, "enh 앵커");
            Assert.GreaterOrEqual(_config.enh, _config.enhBand.x, "enh ≥ enhBand.lo");
            Assert.LessOrEqual(_config.enh, _config.enhBand.y, "enh ≤ enhBand.hi");
            Assert.GreaterOrEqual(enhanced, _config.s4Band.x, "강화 결과 ≥ S4 밴드 하한");
            Assert.LessOrEqual(enhanced, _config.s4Band.y, "강화 결과 ≤ S4 밴드 상한");
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
