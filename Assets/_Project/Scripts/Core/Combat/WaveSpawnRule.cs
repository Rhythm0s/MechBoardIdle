namespace MBI.Core.Combat
{
    /// <summary>
    /// 적이 **묶음으로** 나온다 — 웨이브 스폰 (2026-09-18 · 화면에 「리젠까지 남은 시간」을
    /// 놓기 위해 들인 모델 · ⚠️ **가정 · 설계 판정 자리**).
    ///
    /// ⚠️⚠️ **왜 모델을 바꾸는가.** 종전 스폰은 <c>index × spawnCadence</c> 로 **한 마리씩
    /// 끊임없이** 나왔다. 거기에는 「다음 리젠까지 남은 시간」이라는 것이 **없다** —
    /// 늘 0.8초 뒤에 하나가 나오므로 타이머를 걸면 같은 수만 되풀이해 찍힌다.
    /// 화면에 리젠 타이머를 놓으려면 **묶음과 묶음 사이에 빈 시간**이 있어야 한다.
    ///
    /// 📌 **평균 속도는 그대로 둔다.** 묶음 크기 W, 간격 T 일 때 평균은 W/T 마리/초이므로
    /// <c>T = W × spawnCadence</c> 로 잡으면 종전과 **같은 평균**이다 — 값을 지어내지 않고
    /// 이미 있는 값에서 끌어온다. 달라지는 것은 **고르게 오던 것이 뭉쳐서 온다**는 것뿐이고,
    /// 그것이 밸런스에 닿는지는 하네스가 재서 답한다(S1 Win 유지 확인).
    ///
    /// ⚠️ **묶음 크기는 가정이다**(<c>waveSizeTbd</c>) — 문서에 웨이브 절이 없다.
    /// </summary>
    public static class WaveSpawnRule
    {
        /// <summary>
        /// 이 차례의 적이 나올 시각. 같은 묶음에 든 적은 **같은 시각**을 받는다.
        ///
        /// ⚠️ 묶음 크기가 1 이면 <c>index × interval</c> 이라 **종전 모델과 같은 식**이 된다 —
        /// 되돌릴 자리를 코드가 스스로 갖고 있는 셈이다.
        /// </summary>
        public static float SpawnTime(int index, int waveSize, float waveInterval)
        {
            if (index <= 0) return 0f;
            if (waveInterval <= 0f) return 0f;
            if (waveSize <= 0) waveSize = 1;
            return (index / waveSize) * waveInterval;
        }

        /// <summary>묶음 간격 — 0 이면 <c>묶음 크기 × 종전 cadence</c> 로 끌어온다(위 주석).</summary>
        public static float Interval(float intervalTbd, int waveSize, float spawnCadence)
        {
            if (intervalTbd > 0f) return intervalTbd;
            if (waveSize <= 0) waveSize = 1;
            return waveSize * (spawnCadence > 0f ? spawnCadence : 0f);
        }

        /// <summary>
        /// 다음 묶음까지 남은 초. **더 나올 적이 없으면 음수**다 — 화면은 그때 타이머를 안 그린다.
        ///
        /// ⚠️ 「남은 시간 0」과 「더 없음」을 같은 수로 내지 않는다. 0 은 지금 나온다는 뜻이고,
        /// 더 없음은 이 판에서 다시는 안 나온다는 뜻이다 — 화면에서 둘은 다른 글자여야 한다.
        /// </summary>
        public static float SecondsToNextWave(float elapsed, int spawnedCount, int totalCount,
                                              int waveSize, float waveInterval)
        {
            if (spawnedCount >= totalCount) return -1f;
            if (waveInterval <= 0f) return 0f;
            if (waveSize <= 0) waveSize = 1;

            float due = SpawnTime(spawnedCount, waveSize, waveInterval);
            float left = due - elapsed;
            return left > 0f ? left : 0f;
        }

        /// <summary>mm:ss. 화면 한 곳이 쓰고, 시험이 여기를 잰다.</summary>
        public static string Clock(float seconds)
        {
            if (seconds < 0f) return "--:--";
            int total = UnityEngine.Mathf.CeilToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return m.ToString("00") + ":" + s.ToString("00");
        }
    }
}
