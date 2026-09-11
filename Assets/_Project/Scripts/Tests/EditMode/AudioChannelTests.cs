using MBI.Core.Audio;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 볼륨 채널 셋 (2026-09-11 신설 · 사운드 문서 7장 · 플랜 §71-16 ③).
    ///
    /// **무엇을 지키는가.** 셋이 **서로 안 섞이는가**와, 사람이 줄여도
    /// 「경고 &gt; 효과음 &gt; 조작음」 **관계가 보존되는가**다.
    /// </summary>
    public sealed class AudioChannelTests
    {
        [SetUp]
        [TearDown]
        public void Clear()
        {
            AudioChannels.Reset();
            MusicVolume.Reset();
        }

        [Test]
        public void 갈래가_채널로_접힌다()
        {
            Assert.AreEqual(AudioChannels.Channel.Ui, AudioChannels.ChannelOf(SoundKind.Ui));
            Assert.AreEqual(AudioChannels.Channel.Effect, AudioChannels.ChannelOf(SoundKind.Effect));

            // ⚠️ **경고는 효과음 채널이다.** 채널이 셋뿐이라 넷째를 만들 수 없다.
            Assert.AreEqual(AudioChannels.Channel.Effect, AudioChannels.ChannelOf(SoundKind.Warning));
        }

        [Test]
        public void 셋이_서로_안_섞인다()
        {
            AudioChannels.Set(AudioChannels.Channel.Effect, 0.5f);

            Assert.AreEqual(0.5f, AudioChannels.Value(AudioChannels.Channel.Effect), 0.0001f);
            Assert.AreEqual(AudioChannels.UiDefault,
                AudioChannels.Value(AudioChannels.Channel.Ui), 0.0001f,
                "효과음을 줄였다고 조작음이 따라 줄면 슬라이더를 셋으로 둔 뜻이 없다");
        }

        [Test]
        public void 배경은_MusicVolume_이_든다()
        {
            // 값을 두 곳에 두면 한쪽만 바뀌는 날이 온다 — 채널은 저장과 읽기만 거든다.
            AudioChannels.Set(AudioChannels.Channel.Music, 0.42f);
            Assert.AreEqual(0.42f, MusicVolume.Value, 0.0001f);
            Assert.AreEqual(MusicVolume.Value, AudioChannels.Value(AudioChannels.Channel.Music), 0.0001f);
        }

        [Test]
        public void 사람이_줄여도_갈래_관계가_보존된다()
        {
            // **이것이 AudioMix 와 AudioChannels 를 가른 이유다.** 사람이 효과음을 줄이면
            // 경고도 같은 비율로 줄어야 「경고가 가장 크다」가 유지된다.
            AudioChannels.Set(AudioChannels.Channel.Effect, 0.3f);

            float warn = AudioChannels.Value(SoundKind.Warning);
            float fx = AudioChannels.Value(SoundKind.Effect);
            Assert.AreEqual(fx, warn, 0.0001f, "경고와 효과음은 같은 채널을 탄다");
        }

        [Test]
        public void 기본값은_가정_100_이다()
        {
            // ⚠️ 사운드 문서에 사람 기본값의 절이 없다 — 배경 30% 만 확정됐다.
            // 설계가 역기입하면 이 단언이 바뀐다.
            Assert.AreEqual(1f, AudioChannels.EffectDefault, 0.0001f);
            Assert.AreEqual(1f, AudioChannels.UiDefault, 0.0001f);
            Assert.AreEqual(0.30f, MusicVolume.Default, 0.0001f, "배경만 확정값이다");
        }

        [Test]
        public void 값은_0에서_1로_잘린다()
        {
            AudioChannels.Set(AudioChannels.Channel.Ui, 3f);
            Assert.AreEqual(1f, AudioChannels.Value(AudioChannels.Channel.Ui), 0.0001f);

            AudioChannels.Set(AudioChannels.Channel.Ui, -1f);
            Assert.AreEqual(0f, AudioChannels.Value(AudioChannels.Channel.Ui), 0.0001f);
        }

        [Test]
        public void 저장_키가_채널마다_다르다()
        {
            // 한 덩어리로 묶으면 하나만 고쳐도 셋이 다 쓰인다.
            string m = AudioChannels.KeyOf(AudioChannels.Channel.Music);
            string e = AudioChannels.KeyOf(AudioChannels.Channel.Effect);
            string u = AudioChannels.KeyOf(AudioChannels.Channel.Ui);

            Assert.AreNotEqual(m, e);
            Assert.AreNotEqual(e, u);
            Assert.AreNotEqual(m, u);
        }

        [Test]
        public void 이름표가_셋_다_있다()
        {
            foreach (AudioChannels.Channel c in System.Enum.GetValues(typeof(AudioChannels.Channel)))
                Assert.IsNotEmpty(AudioChannels.Label(c), c.ToString());
        }
    }
}
