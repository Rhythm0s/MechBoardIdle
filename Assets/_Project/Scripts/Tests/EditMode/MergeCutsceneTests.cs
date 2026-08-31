using MBI.Core;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 합체 3초 연출의 시간표(260831_V07 「3초 최소본이면 충분하다」).
    ///
    /// 최소본의 조건 둘을 여기서 고정한다 — **화면이 바뀐다**(암전이 올랐다 내린다)와
    /// **수치가 바뀐다**(화력이 before에서 after로 올라간다).
    /// </summary>
    public sealed class MergeCutsceneTests
    {
        private const float D = 0.01f;

        private static void Run(MergeCutscene c, float seconds, float dt = 1f / 60f)
        {
            int steps = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < steps; i++) c.Tick(dt);
        }

        // ---- 화면이 바뀐다 ----

        [Test]
        public void BeforePlaying_NothingIsDrawn()
        {
            var c = new MergeCutscene();

            Assert.IsFalse(c.IsPlaying);
            Assert.AreEqual(0f, c.Dim, D, "안 틀고 있으면 화면을 덮지 않는다");
        }

        /// <summary>암전이 **올랐다 내린다.** 올라간 채로 끝나면 화면이 검게 남는다.</summary>
        [Test]
        public void DimRises_ThenFalls_BackToZero()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            Assert.AreEqual(0f, c.Dim, D, "시작 순간은 아직 투명");

            Run(c, MergeCutscene.FadeInSeconds);
            Assert.AreEqual(MergeCutscene.MaxDim, c.Dim, 0.05f, "차오른 뒤 최대");

            Run(c, MergeCutscene.TotalSeconds);
            Assert.IsFalse(c.IsPlaying);
            Assert.AreEqual(0f, c.Dim, D, "끝나면 화면이 완전히 걷힌다");
        }

        /// <summary>
        /// **화면을 다 덮지 않는다.** 뒤에서 합체 화력으로 적이 녹는 것이 이 연출의 내용물인데
        /// 알파 1.0으로 덮으면 보여 줄 것이 사라진다.
        /// </summary>
        [Test]
        public void NeverFullyOpaque()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            for (int i = 0; i < 200; i++)
            {
                Assert.Less(c.Dim, 1f, $"{c.Elapsed:F2}s");
                c.Tick(MergeCutscene.TotalSeconds / 200f);
            }
        }

        /// <summary>3초짜리다. 길어지면 영상에서 한 컷으로 안 지나간다.</summary>
        [Test]
        public void RunsForThreeSeconds()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            Run(c, MergeCutscene.TotalSeconds - 0.1f);
            Assert.IsTrue(c.IsPlaying, "3초 전에는 아직 튼다");

            Run(c, 0.2f);
            Assert.IsFalse(c.IsPlaying);
        }

        /// <summary>넘긴 dt만큼만 간다 — 한 프레임이 길어도 3초를 넘겨 세지 않는다.</summary>
        [Test]
        public void OneHugeFrame_EndsCleanly()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            c.Tick(10f);

            Assert.IsFalse(c.IsPlaying);
            Assert.AreEqual(MergeCutscene.TotalSeconds, c.Elapsed, D, "경과가 넘치지 않는다");
            Assert.AreEqual(0f, c.Dim, D);
        }

        // ---- 수치가 바뀐다 ----

        /// <summary>합체 후 화력 = 합체 배율. 이 곱을 연출이 따로 만들지 않는다.</summary>
        [Test]
        public void AfterOutput_UsesTheMergeMultiplier()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            Assert.AreEqual(50f, c.OutputBefore, D);
            Assert.AreEqual(50f * MergeSystem.MergeMultiplier, c.OutputAfter, D, "50 × 1.8 = 90");
        }

        /// <summary>
        /// **숫자가 올라간다.** 처음부터 90이 떠 있으면 「합체로 올랐다」가 안 읽히고
        /// 그냥 큰 숫자가 하나 뜬 것이 된다.
        /// </summary>
        [Test]
        public void OutputCountsUp_FromBeforeToAfter()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            Assert.AreEqual(50f, c.OutputNow, D, "시작은 합체 전 값");

            Run(c, MergeCutscene.FadeInSeconds + MergeCutscene.CountUpSeconds * 0.5f);
            float mid = c.OutputNow;
            Assert.Greater(mid, 50f, "올라가는 중");
            Assert.Less(mid, 90f, "아직 안 닿았다");

            Run(c, MergeCutscene.CountUpSeconds);
            Assert.AreEqual(90f, c.OutputNow, 0.1f, "카운트업이 끝나면 합체 화력");
        }

        /// <summary>
        /// 값은 <c>Play</c> 시점에 **스냅샷**한다. 재생 중에도 전투가 돌아 화력이 변하는데
        /// 그걸 그대로 비추면 숫자가 흔들려 「이만큼 올랐다」가 안 읽힌다.
        /// </summary>
        [Test]
        public void ValuesAreSnapshotAtPlay()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);

            Run(c, 1f);

            Assert.AreEqual(50f, c.OutputBefore, D, "재생 중에 안 변한다");
            Assert.AreEqual(150f, c.BurstDamage, D);
        }

        /// <summary>
        /// 표적이 없어 버스트가 안 터졌으면 **0으로 남는다** — 화면은 이 값이 0일 때 줄을 안 그린다.
        /// 「버스트 0」이라고 띄우면 터졌는데 0이었다는 거짓말이 된다.
        /// </summary>
        [Test]
        public void NoBurst_StaysZero_SoTheLineCanBeSkipped()
        {
            var c = new MergeCutscene();
            c.Play(50f, 0f);

            Assert.AreEqual(0f, c.BurstDamage, D);
            Assert.AreEqual(90f, c.OutputAfter, D, "버스트가 없어도 합체 화력은 오른다");
        }

        /// <summary>음수가 들어와도 화면에 음수를 띄우지 않는다.</summary>
        [Test]
        public void NegativeInput_ClampsToZero()
        {
            var c = new MergeCutscene();
            c.Play(-10f, -5f);

            Assert.AreEqual(0f, c.OutputBefore, D);
            Assert.AreEqual(0f, c.BurstDamage, D);
        }

        // ---- 다시 시작 ----

        /// <summary>스테이지를 다시 시작하면 **지난 판의 숫자가 남지 않는다.**</summary>
        [Test]
        public void Reset_ClearsTheNumbersToo()
        {
            var c = new MergeCutscene();
            c.Play(50f, 150f);
            Run(c, 1f);

            c.Reset();

            Assert.IsFalse(c.IsPlaying);
            Assert.AreEqual(0f, c.Dim, D);
            Assert.AreEqual(0f, c.OutputBefore, D);
            Assert.AreEqual(0f, c.OutputAfter, D);
            Assert.AreEqual(0f, c.BurstDamage, D);
        }

        [Test]
        public void Deterministic_SameInputSameTimeline()
        {
            var a = new MergeCutscene();
            var b = new MergeCutscene();
            a.Play(50f, 150f); b.Play(50f, 150f);

            for (int i = 0; i < 90; i++)
            {
                a.Tick(1f / 30f); b.Tick(1f / 30f);
                Assert.AreEqual(a.Dim, b.Dim, 0.0001f, $"frame {i}");
                Assert.AreEqual(a.OutputNow, b.OutputNow, 0.0001f, $"frame {i}");
            }
        }
    }
}
