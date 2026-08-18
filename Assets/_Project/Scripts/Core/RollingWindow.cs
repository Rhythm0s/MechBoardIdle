using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 다채널 시간창 롤링 평균(순수 — EditMode 검증 가능). "움직이는 거울"(밸런스 10장, 실측 60초 롤링).
    ///
    /// 왜 다채널인가: 예상·실제·갭 분해 3항을 **같은 샘플 집합**으로 굴려야
    /// `roll(gapPower)+roll(gapHeat)+roll(gapBelt) == roll(expected)−roll(actual)` 가 성립한다.
    /// 채널마다 큐를 따로 두면 샘플 시각이 어긋나 변수패널의 분해 합이 총갭과 안 맞는다(§3 중복 금지).
    ///
    /// 고정 간격 샘플링: 매 프레임 넣으면 60초 창에 수천 샘플이 쌓인다. 평균값은 실질 동일하므로
    /// sampleInterval(기본 0.1초)로 솎는다 — 밸런스 무영향, 표시값만 미세 변동.
    /// 링버퍼라 생성 후 추가 할당이 없다(매 프레임 호출 경로).
    /// </summary>
    public sealed class RollingWindow
    {
        private readonly int _channels;
        private readonly float _window;
        private readonly float _interval;

        private readonly float[] _times;
        private readonly float[] _data;   // [slot * channels + ch]
        private readonly double[] _sums;  // 누적 오차 억제용 double
        private readonly int _capacity;

        private int _head;   // 가장 오래된 샘플 슬롯
        private int _count;
        private float _lastSample;
        private bool _primed;

        public RollingWindow(int channels, float windowSeconds, float sampleInterval = 0.1f)
        {
            _channels = Mathf.Max(1, channels);
            _window = Mathf.Max(0f, windowSeconds);
            _interval = Mathf.Max(0f, sampleInterval);

            _capacity = Mathf.Max(2, Mathf.CeilToInt(_window / Mathf.Max(0.0001f, _interval)) + 2);
            _times = new float[_capacity];
            _data = new float[_capacity * _channels];
            _sums = new double[_channels];
        }

        public int SampleCount => _count;

        /// <summary>
        /// 샘플 1건 투입. 직전 샘플과 간격이 sampleInterval 미만이면 버린다(솎기).
        /// 값 배열 길이는 채널 수와 같아야 한다. 반환값 = 실제로 담겼는가.
        /// </summary>
        public bool TrySample(float now, float[] values)
        {
            if (values == null || values.Length != _channels) return false;
            if (_primed && now - _lastSample < _interval) { Expire(now); return false; }

            if (_count == _capacity) PopOldest(); // 링이 꽉 차면 가장 오래된 것부터

            int slot = (_head + _count) % _capacity;
            _times[slot] = now;
            for (int c = 0; c < _channels; c++)
            {
                _data[slot * _channels + c] = values[c];
                _sums[c] += values[c];
            }
            _count++;
            _lastSample = now;
            _primed = true;

            Expire(now);
            return true;
        }

        /// <summary>채널 평균. 샘플이 없으면 0.</summary>
        public float Average(int channel)
        {
            if (channel < 0 || channel >= _channels || _count == 0) return 0f;
            return (float)(_sums[channel] / _count);
        }

        public void Reset()
        {
            _head = 0;
            _count = 0;
            _primed = false;
            for (int c = 0; c < _channels; c++) _sums[c] = 0d;
        }

        // 창을 벗어난 오래된 샘플 제거. 마지막 1건은 남긴다(평균이 0으로 튀지 않도록).
        private void Expire(float now)
        {
            while (_count > 1 && now - _times[_head] > _window) PopOldest();
        }

        private void PopOldest()
        {
            for (int c = 0; c < _channels; c++)
                _sums[c] -= _data[_head * _channels + c];
            _head = (_head + 1) % _capacity;
            _count--;
        }
    }
}
