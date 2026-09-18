using System;
using System.Collections.Generic;
using MBI.Data;

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
        public double gold;                        // 골드 — 처치 20마리마다 5(2026-09-18 사용자 확정)

        /// <summary>
        /// 아직 골드가 안 된 처치 수 (2026-09-18 · <see cref="GoldRewardRule"/>).
        ///
        /// ⚠️⚠️ **이 칸이 없으면 19 마리가 매번 사라진다.** 골드는 스무 마리를 모아야 나오므로
        /// 남은 수를 어딘가 들고 있어야 하고, 그 자리는 **저장**이다 — 끄고 켜면 0 으로
        /// 돌아가는 카운터는 「스무 마리마다」라는 약속을 못 지킨다.
        /// </summary>
        public int killsTowardGold;
        public string lastFarmStageId = "";        // 끈 시점의 상주 스테이지
        public int totalKills;

        public List<StageRateEntry> bestFarmRates = new List<StageRateEntry>();
        public List<string> clearedStageIds = new List<string>(); // 최초 클리어 1회 보상 판정용

        /// <summary>
        /// 보드 한 판 — **없으면 시작 보드로 간다**(2026-09-16 · 설계 규칙 1).
        ///
        /// ⚠️ 빈 보드로 떨어뜨리지 않는다 — 노드가 없으면 아무것도 못 만들고,
        /// 만들 것이 없으니 못 놓는다. **스스로 못 빠져나오는 상태**가 된다.
        /// </summary>
        /// <remarks>
        /// 🗑️ **폐기 — 읽기 전용 이주 통로다**(2026-09-16 · 보드가 로봇별 둘로 갈렸다).
        /// 안 지우는 까닭은 **이미 나간 저장이 이 칸에 A 판을 담고 있기** 때문이다.
        /// 불러올 때 <see cref="MigrateLegacyBoard"/> 가 <see cref="boards"/> 로 옮기고,
        /// **쓰는 쪽은 아무도 없다** — 새로 저장하면 이 칸은 비어 나간다.
        /// </remarks>
        public BoardStateV1 board;

        /// <summary>
        /// 보드 **둘** — 로봇마다 한 판 (2026-09-16 · 사용자 확정 · 플랜 §74-16 ①).
        ///
        /// ⚠️ **차례가 아니라 판이 든 <see cref="BoardStateV1.owner"/> 로 찾는다.**
        /// 차례로 가리면 한 칸 밀렸을 때 A 판이 B 자리에 조용히 들어앉는다.
        ///
        /// ⚠️ 한 판만 있어도 된다 — **없는 쪽은 시작 보드로 간다**(위 `board` 와 같은 규칙).
        /// B 를 한 번도 안 건드린 저장이 정확히 그 꼴이다.
        /// </summary>
        public List<BoardStateV1> boards = new List<BoardStateV1>();

        /// <summary>그 로봇의 저장된 판. 없으면 <c>null</c>(= 시작 보드로 간다).</summary>
        public BoardStateV1 BoardOf(MountOwner owner)
        {
            for (int i = 0; i < boards.Count; i++)
                if (boards[i] != null && boards[i].owner == (int)owner) return boards[i];
            return null;
        }

        /// <summary>그 로봇의 판을 넣는다 — 같은 주인의 옛 판은 갈아 끼운다.</summary>
        public void SetBoard(MountOwner owner, BoardStateV1 state)
        {
            for (int i = 0; i < boards.Count; i++)
            {
                if (boards[i] == null || boards[i].owner != (int)owner) continue;
                if (state == null) boards.RemoveAt(i); else boards[i] = state;
                return;
            }
            if (state != null) boards.Add(state);
        }

        /// <summary>
        /// 구 저장의 한 판을 목록으로 옮긴다 — **불러온 직후 한 번** 부른다.
        ///
        /// ⚠️ 목록에 이미 A 판이 있으면 **아무것도 안 한다.** 새 저장이 이기고,
        /// 구 칸이 그것을 덮으면 최신 판이 옛 판으로 되돌아간다.
        /// </summary>
        public void MigrateLegacyBoard()
        {
            if (board == null) return;
            if (BoardOf((MountOwner)board.owner) == null) boards.Add(board);
            board = null;
        }

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
