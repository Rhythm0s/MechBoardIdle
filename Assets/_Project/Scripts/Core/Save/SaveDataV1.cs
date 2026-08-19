using System;
using System.Collections.Generic;

namespace MBI.Core
{
    /// <summary>스테이지별 최고 파밍 시급 1건. JsonUtility가 Dictionary를 못 다뤄 리스트로 둔다.</summary>
    [Serializable]
    public class StageRateEntry
    {
        public string stageId;
        public float scrapPerHour;
    }

    /// <summary>
    /// 세이브 스키마 v1(§5-7). 오프라인 보상이 없으면 방치형이 성립하지 않으므로 최소한 이만큼은 남는다.
    ///
    /// 담는 것(스테이지 기획서「오프라인 보상」):
    ///   - 스테이지별 최고 파밍 시급 → 오프라인 정산의 기준값
    ///   - 마지막 상주 스테이지 → 어느 기록을 쓸지 정하는 조회 키(여러 기록 중 최댓값을 고르지 않는다)
    ///   - 마지막 접속 시각 → 꺼둔 시간 계산
    ///
    /// 순수 DTO + 조회/기록 규칙만. 파일 입출력은 <see cref="ISaveStore"/> 구현이 맡는다(§3 한 파일=한 책임).
    /// JsonUtility 호환을 위해 전부 public 필드 + [Serializable] 이어야 한다(프로퍼티는 직렬화되지 않는다).
    /// </summary>
    [Serializable]
    public class SaveDataV1
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;
        public long lastSeenUtcTicks;              // DateTimeOffset.UtcNow.Ticks
        public double scrap;                       // 고철
        public double enhMaterial;                 // 강화재료 — 오프라인으로는 절대 늘지 않는다(닫힌 곡선)
        public string lastFarmStageId = "";        // 끈 시점의 상주 스테이지
        public int totalKills;

        public List<StageRateEntry> bestFarmRates = new List<StageRateEntry>();
        public List<string> clearedStageIds = new List<string>(); // 최초 클리어 1회 보상 판정용

        /// <summary>그 스테이지의 최고 파밍 시급. 기록이 없으면 0(= 미측정 → 호출자가 기본 시급으로 대체).</summary>
        public float BestFarmRate(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return 0f;
            for (int i = 0; i < bestFarmRates.Count; i++)
                if (bestFarmRates[i].stageId == stageId) return bestFarmRates[i].scrapPerHour;
            return 0f;
        }

        /// <summary>이번 바퀴 시급이 기존 기록보다 클 때만 교체. 교체했으면 true.</summary>
        public bool TryRecordFarmRate(string stageId, float scrapPerHour)
        {
            if (string.IsNullOrEmpty(stageId) || scrapPerHour <= 0f) return false;

            for (int i = 0; i < bestFarmRates.Count; i++)
            {
                if (bestFarmRates[i].stageId != stageId) continue;
                if (scrapPerHour <= bestFarmRates[i].scrapPerHour) return false;
                bestFarmRates[i].scrapPerHour = scrapPerHour;
                return true;
            }

            bestFarmRates.Add(new StageRateEntry { stageId = stageId, scrapPerHour = scrapPerHour });
            return true;
        }

        public bool HasCleared(string stageId) =>
            !string.IsNullOrEmpty(stageId) && clearedStageIds.Contains(stageId);

        /// <summary>최초 클리어면 true(= 강화재료 지급). 두 번째부터는 false — 재지급하면 닫힌 곡선이 무너진다.</summary>
        public bool MarkCleared(string stageId)
        {
            if (string.IsNullOrEmpty(stageId) || clearedStageIds.Contains(stageId)) return false;
            clearedStageIds.Add(stageId);
            return true;
        }
    }
}
