using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 세이브 저장소(§5-7 태스크 B). 실제 PlayerPrefs를 쓰되 전용 키를 쓰고 끝나면 지운다
    /// (테스트 더블은 2026-08-18 컷 — 실물 경로를 그대로 검증하는 편이 웹빌드 동작에 가깝다).
    ///
    /// 핵심은 마지막 테스트다: 잘린 세이브가 남아도 게임이 켜져야 한다.
    /// </summary>
    public sealed class SaveStoreTests
    {
        private const string TestKey = "MBI_SAVE_TEST";
        private PlayerPrefsSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(TestKey);
            _store = new PlayerPrefsSaveStore(TestKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(TestKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FirstRun_NoSave_IsNotAnError()
        {
            // 파일 없음은 정상 경로 — 예외가 아니라 false.
            Assert.IsFalse(_store.TryLoad(out SaveDataV1 data));
            Assert.IsNull(data);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var src = new SaveDataV1 { scrap = 500d, lastFarmStageId = "S3", totalKills = 12 };
            src.TryRecordFarmRate("S3", 480f);
            _store.Save(src);

            Assert.IsTrue(_store.TryLoad(out SaveDataV1 back));
            Assert.AreEqual(500d, back.scrap, 0.001d);
            Assert.AreEqual("S3", back.lastFarmStageId);
            Assert.AreEqual(12, back.totalKills);
            Assert.AreEqual(480f, back.BestFarmRate("S3"), 0.001f);
        }

        [Test]
        public void Delete_RemovesSave()
        {
            _store.Save(new SaveDataV1 { scrap = 10d });
            _store.Delete();

            Assert.IsFalse(_store.TryLoad(out _));
        }

        /// <summary>
        /// 잘린 JSON이 남아도 예외로 게임을 죽이지 않는다(견고성 1건, 2026-08-19 복원).
        /// 웹빌드엔 원자적 쓰기가 없어 저장 중 탭을 닫으면 실제로 이 상태가 만들어진다.
        /// </summary>
        [Test]
        public void CorruptSave_FallsBackToDefaults_DoesNotThrow()
        {
            PlayerPrefs.SetString(TestKey, "{\"scrap\":123, \"bestFarmRa");  // 쓰다 만 JSON
            PlayerPrefs.Save();

            bool loaded = true;
            Assert.DoesNotThrow(() => loaded = _store.TryLoad(out _), "손상 세이브가 예외를 올리면 게임이 안 켜진다");
            Assert.IsFalse(loaded, "손상이면 기록 없음으로 취급 → 기본값 시작");
        }
    }
}
