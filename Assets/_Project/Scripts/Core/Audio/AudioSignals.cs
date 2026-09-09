using System.Collections.Generic;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 효과음 사건을 넣는 중립 채널 (2026-09-09 신설).
    ///
    /// <see cref="LogisticsOutputBridge"/>·<see cref="GameViewSignals"/>·<see cref="TutorialSignals"/>와
    /// 같은 패턴이다. **전투와 물류가 서로를 참조하지 않으므로** 재생기를 직접 부를 수 없고,
    /// 재생기를 양쪽이 다 보는 자리에 두면 이번에는 그 자리가 두 어셈블리를 다 알아야 한다.
    ///
    /// **그래서 넣는 쪽은 이름만 안다.** `AudioSource`도 `AudioClip`도 안 보고, 소리가
    /// 실제로 나는지도 모른다 — 자산이 없으면 재생기가 **조용히 건너뛴다**(사운드 문서 7장
    /// 「모든 소리가 빠져도 게임이 정상 동작해야 한다」).
    ///
    /// ⚠️ **소리가 판정을 바꾸지 않는다**(1장). 이 큐가 넘치든 비든 피해 수치는 같다.
    /// </summary>
    public static class AudioSignals
    {
        /// <summary>넣어 둔 사건 하나.</summary>
        public readonly struct Cue
        {
            /// <summary>자산 이름 그대로(`sfx_nodesnap` 등). **번역하지 않는다**(지침 §8).</summary>
            public readonly string id;
            public readonly SoundKind kind;

            public Cue(string id, SoundKind kind)
            {
                this.id = id;
                this.kind = kind;
            }
        }

        // 한 프레임에 쌓였다가 재생기가 통째로 비운다. 재생기가 없으면 아래 상한에서 멈춘다.
        private static readonly List<Cue> Pending = new List<Cue>();

        /// <summary>
        /// 쌓아 둘 수 있는 사건 수의 상한.
        ///
        /// ⚠️ **재생기가 없는 씬에서도 넣는 쪽은 돈다** — 격리 전투 씬처럼 재생기를 안 붙인
        /// 자리에서 이 목록이 영원히 자란다. 상한을 두고 **오래된 것부터 버린다** —
        /// 소리는 지금 일어난 일을 알리는 것이라 늦은 것은 값이 없다.
        /// </summary>
        public const int MaxPending = 64;

        /// <summary>지금 쌓여 있는 수.</summary>
        public static int PendingCount => Pending.Count;

        /// <summary>사건 하나를 넣는다. 이름이 비었으면 아무 일도 안 한다.</summary>
        public static void Play(string id, SoundKind kind)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Pending.Count >= MaxPending) Pending.RemoveAt(0);
            Pending.Add(new Cue(id, kind));
        }

        /// <summary>쌓인 것을 <paramref name="into"/>로 옮기고 비운다. 재생기가 프레임마다 부른다.</summary>
        public static void Drain(List<Cue> into)
        {
            if (into != null) into.AddRange(Pending);
            Pending.Clear();
        }

        /// <summary>
        /// 지금 어느 국면인가 — **러너가 쓰고 재생기가 읽는다.**
        ///
        /// 효과음처럼 큐에 안 넣는 이유: 국면은 **사건이 아니라 상태**다. 큐에 넣으면
        /// 재생기가 없는 동안 쌓였다가 한꺼번에 터지고, 마지막 하나만 뜻이 있는데
        /// 앞의 것들이 곡을 몇 번 갈게 된다.
        ///
        /// ⚠️ 러너가 `MusicPhaseRule.Of(stage.reqType)`로 정한다 — **판정은 한 자리에만** 산다.
        /// </summary>
        public static MusicPhase Phase;

        /// <summary>도메인 리로드를 껐을 때 이전 Play의 사건이 남는 것을 막는다.</summary>
        public static void Reset()
        {
            Pending.Clear();
            Phase = MusicPhase.Battle;
        }
    }
}
