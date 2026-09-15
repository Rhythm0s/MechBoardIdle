using MBI.Core;
using MBI.Core.Audio;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 튜토리얼 목표 달성음 — **한 번만 나는지를 지킨다** (2026-09-15 사용자 확정).
    ///
    /// 걸쇠는 한 번 서면 안 내려간다. 그래서 「참인가」로 소리를 넣으면
    /// **그 뒤 모든 프레임이 참이라 초당 60번 울린다.** 여기서 그것을 막는다.
    /// </summary>
    public sealed class Stage0GoalSoundTests
    {
        [SetUp]
        public void 큐를_비운다() => AudioSignals.Drain(null);

        private static int Cues()
        {
            var got = new System.Collections.Generic.List<AudioSignals.Cue>();
            AudioSignals.Drain(got);
            int n = 0;
            foreach (AudioSignals.Cue c in got) if (c.id == SoundIds.GoalClear) n++;
            return n;
        }

        [Test]
        public void 목표_두_줄이_각각_한_번씩_운다()
        {
            var goal = new Stage0Goal();

            goal.Observe(true, false);           // 첫 줄이 선다
            Assert.That(Cues(), Is.EqualTo(1), "빈 칸을 채운 순간 한 번");

            for (int i = 0; i < 60; i++) goal.Observe(true, false);
            Assert.That(Cues(), Is.EqualTo(0),
                "걸쇠가 이미 서 있다 — 참인 프레임마다 울면 초당 60번이다");

            goal.Observe(true, true);            // 둘째 줄이 선다
            Assert.That(Cues(), Is.EqualTo(1), "마운트가 찬 순간 한 번");

            for (int i = 0; i < 60; i++) goal.Observe(true, true);
            Assert.That(Cues(), Is.EqualTo(0), "둘 다 선 뒤로는 조용하다");
        }

        [Test]
        public void 순서를_안_지키면_둘째_줄은_안_운다()
        {
            var goal = new Stage0Goal();

            // 빈 칸을 안 채운 채로 마운트만 차는 경우 — 목표 규칙상 둘째 줄이 안 선다.
            for (int i = 0; i < 10; i++) goal.Observe(false, true);

            Assert.That(Cues(), Is.EqualTo(0), "선 걸쇠가 없으면 소리도 없다");
            Assert.That(goal.MountFilled, Is.False);
        }

        [Test]
        public void 다시_시작하면_또_운다()
        {
            var goal = new Stage0Goal();
            goal.Observe(true, true);
            goal.Observe(true, true);
            Cues();

            goal.Reset();
            goal.Observe(true, false);
            Assert.That(Cues(), Is.EqualTo(1), "걸쇠를 풀었으니 다음 판에서 다시 난다");
        }

        [Test]
        public void 채널은_효과음이다()
        {
            Assert.That(SoundIds.KindOf(SoundIds.GoalClear), Is.EqualTo(SoundKind.Effect),
                "경고 채널은 화면을 안 봐도 닿아야 하는 것의 자리다 — 병목보다 크게 울리면 안 된다");
            Assert.That(SoundIds.All, Contains.Item(SoundIds.GoalClear));
        }
    }
}
