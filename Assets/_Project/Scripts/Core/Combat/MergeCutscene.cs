using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 합체 연출의 **시간표**(260831_V07 「3초 최소본이면 충분하다」).
    ///
    /// 최소본이 보여야 하는 것은 둘이다 — **화면이 바뀐다**(암전)와 **수치가 바뀐다**(화력이
    /// 올라간다). 셋째로 버스트 피해를 같이 띄운다: 합체 순간에 실제로 무슨 일이 일어났는지가
    /// 숫자로 남지 않으면 「멋있는 화면이 잠깐 떴다」로 끝난다.
    ///
    /// **판정은 없다.** 여기는 시간 → 화면값 사상(寫像)만 한다. 얼마를 때렸는지는 시뮬이
    /// 정하고(<see cref="CombatSimulation.LastBurstDamage"/>), 이 클래스는 받아 적을 뿐이다.
    ///
    /// ⚠️ 값은 <see cref="Play"/> 시점에 **스냅샷**한다. 재생 도중 전투가 계속 돌아 화력이
    /// 변하는데 그걸 그대로 비추면 숫자가 흔들려 「합체로 이만큼 올랐다」가 안 읽힌다.
    /// </summary>
    public sealed class MergeCutscene
    {
        /// <summary>전체 길이. 3초 — 영상에서 한 컷으로 지나갈 길이다.</summary>
        public const float TotalSeconds = 3f;

        /// <summary>암전이 차오르는 시간.</summary>
        public const float FadeInSeconds = 0.35f;

        /// <summary>암전이 걷히는 시간. 들어올 때보다 길게 — 나가는 쪽이 급하면 뚝 끊긴 느낌이 난다.</summary>
        public const float FadeOutSeconds = 0.8f;

        /// <summary>화력 숫자가 before에서 after로 올라가는 시간.</summary>
        public const float CountUpSeconds = 0.6f;

        /// <summary>암전 최대 알파. 1.0으로 덮으면 전투가 안 보여 무슨 일이 났는지 사라진다.</summary>
        public const float MaxDim = 0.75f;

        private float _elapsed;
        private bool _playing;

        public bool IsPlaying => _playing;

        public float Elapsed => _elapsed;

        /// <summary>합체 직전 두 로봇이 합쳐 내던 초당 실피해.</summary>
        public float OutputBefore { get; private set; }

        /// <summary>합체 후 화력 = before × 합체 배율. 이 곱을 여기서 다시 만들지 않는다.</summary>
        public float OutputAfter { get; private set; }

        /// <summary>버스트가 낸 피해. 표적이 없어 안 터졌으면 0이고, 그때는 줄을 안 그린다.</summary>
        public float BurstDamage { get; private set; }

        /// <summary>지금 화면에 띄울 화력 — before에서 after로 올라가는 중간값.</summary>
        public float OutputNow
        {
            get
            {
                if (!_playing) return OutputAfter;
                float t = (_elapsed - FadeInSeconds) / CountUpSeconds;
                return Mathf.Lerp(OutputBefore, OutputAfter, Mathf.Clamp01(t));
            }
        }

        /// <summary>화면 암전 알파 0~<see cref="MaxDim"/>. 안 틀고 있으면 0이다.</summary>
        public float Dim
        {
            get
            {
                if (!_playing) return 0f;
                if (_elapsed < FadeInSeconds) return MaxDim * (_elapsed / FadeInSeconds);

                float fadeOutStart = TotalSeconds - FadeOutSeconds;
                if (_elapsed > fadeOutStart)
                    return MaxDim * Mathf.Clamp01((TotalSeconds - _elapsed) / FadeOutSeconds);

                return MaxDim;
            }
        }

        /// <summary>
        /// 재생 시작. <paramref name="outputBefore"/>는 합체 직전 두 로봇 합산 초당 실피해다.
        /// </summary>
        public void Play(float outputBefore, float burstDamage)
        {
            OutputBefore = Mathf.Max(0f, outputBefore);
            OutputAfter = OutputBefore * MergeSystem.MergeMultiplier;
            BurstDamage = Mathf.Max(0f, burstDamage);

            _elapsed = 0f;
            _playing = true;
        }

        public void Tick(float dt)
        {
            if (!_playing) return;

            _elapsed += dt;
            if (_elapsed >= TotalSeconds)
            {
                _elapsed = TotalSeconds;
                _playing = false;
            }
        }

        /// <summary>스테이지를 다시 시작할 때. 값까지 지운다 — 지난 판의 숫자가 남으면 안 된다.</summary>
        public void Reset()
        {
            _elapsed = 0f;
            _playing = false;
            OutputBefore = OutputAfter = BurstDamage = 0f;
        }
    }
}
