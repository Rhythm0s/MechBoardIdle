using MBI.Core;
using MBI.Editor;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// **파밍이 읽는 열이 표에 살아 있는가** (2026-10-13 · 실제로 한 번 사라졌다).
    ///
    /// ⚠️⚠️ **무엇이 있었나.** `Tables/xlsx_to_csv.py` 를 돌렸더니
    /// `ENEMY_DATA.ScrapPerKill` 열과 `STAGE_DATA` 의 Spawn 세 열 값이 **지워졌다** —
    /// 그 열들은 CSV 에만 있었고 **xlsx 에는 없었기** 때문이다. 변환기는 xlsx 를
    /// 원천으로 보므로 xlsx 에 없는 열은 「지운다」가 아니라 「처음부터 없다」로 다룬다.
    ///
    /// 📌 **그런데 아무도 안 터졌다.** 생성기가 `Has`/`Exists` 로 막아 두어
    /// 열이 없으면 **조용히 건너뛴다**(표가 덜 채워진 동안에도 돌게 하려던 보호다).
    /// 그 보호가 이 경우에는 **고장을 감췄다** — 정원 0 · 단가 0 으로 구워지고
    /// 파밍 층이 안 서는데 빨간 것은 아무 데도 없다.
    ///
    /// 그래서 **읽는 쪽이 아니라 표를 직접 본다.** 이 시험이 빨개지면
    /// 「xlsx 가 다시 와야 한다」는 뜻이지 코드가 틀린 것이 아니다.
    /// </summary>
    public sealed class FarmTableColumnsTests
    {
        [Test]
        public void 적_표가_종류별_고철_단가를_든다()
        {
            CsvTable t = GameDataTables.Load("ENEMY_DATA");
            Assert.IsTrue(t.Has("ScrapPerKill"),
                "ENEMY_DATA 에 ScrapPerKill 열이 없다 — xlsx 에 그 열이 빠졌는지 보라");

            // 적어도 한 줄은 0 이 아니어야 한다 — 전부 0 이면 파밍이 한 푼도 못 번다.
            bool any = false;
            foreach (CsvTable.Row r in t.Rows)
                if (r.Num("ScrapPerKill") > 0f) { any = true; break; }
            Assert.IsTrue(any, "ScrapPerKill 이 전부 0 이다 — 파밍 수입이 0 이 된다");
        }

        [Test]
        public void 스테이지_표가_정원과_간격을_든다()
        {
            CsvTable t = GameDataTables.Load("STAGE_DATA");
            t.Require("StageID", "SpawnCap", "SpawnInterval", "SpawnConfirmed");

            int standing = 0;
            foreach (CsvTable.Row r in t.Rows)
            {
                // 확정으로 적힌 줄은 **실제로 설 수 있어야** 한다 —
                // 확정인데 정원 0 이면 그 판은 파밍 층이 안 선다(화면에 「리젠 —」).
                if (r.Int("SpawnConfirmed") == 0) continue;
                standing++;
                Assert.Greater(r.Int("SpawnCap"), 0,
                    r.Text("StageID") + " 는 확정인데 정원이 0 이다");
                Assert.Greater(r.Num("SpawnInterval"), 0f,
                    r.Text("StageID") + " 는 확정인데 간격이 0 이다");
            }

            Assert.Greater(standing, 0, "정원·간격이 확정된 판이 하나도 없다");
        }

        [Test]
        public void 파밍_구성_표가_있고_정원이_선다()
        {
            Assert.IsTrue(GameDataTables.Exists("FARM_COMP_DATA"),
                "FARM_COMP_DATA 가 없다 — 파밍 층에 세울 것이 없어진다");

            CsvTable t = GameDataTables.Load("FARM_COMP_DATA");
            t.Require("StageID", "EnemyKey", "Weight", "Hp", "Def");

            bool any = false;
            foreach (CsvTable.Row r in t.Rows)
                if (r.Int("Weight") > 0) { any = true; break; }
            Assert.IsTrue(any, "Weight 가 전부 0 이다 — 정원 합이 0 이 된다");
        }
    }
}
