using System.Collections.Generic;

namespace MBI.Core.Audio
{
    /// <summary>
    /// 같은 소리가 몇 개까지 겹치는가 (사운드 문서 2장 · 7장 · 2026-09-09 신설).
    ///
    /// **상한이 없으면 사망음이 겹쳐 소음이 된다** — 화면에서 수십 기가 동시에 죽는다.
    /// 넘치면 **가장 오래된 것을 끊고** 새 소리가 그 자리를 쓴다(7장 「동시 재생 상한」).
    ///
    /// **왜 코어에 있는가.** 이것은 `AudioSource`가 없어도 시험할 수 있는 판정이다 —
    /// 「몇 개가 도는가 · 어느 것을 끊는가」는 재생기 밖의 규칙이고, 재생기 안에 두면
    /// **테스트가 못 보는 자리**가 된다(지침 §7).
    ///
    /// ⚠️ **상한의 점값은 미확정이다**(사운드 문서 9장 S-1). 그 문서가 정한 해소 방법이
    /// 「구현 후 실제 화면에서 듣고 정한다」라, 값은 SO에 **잠정으로** 두고 여기서는
    /// 받아 쓰기만 한다 — 이 파일에 숫자를 두지 않는다(§3 수치 하드코딩 금지).
    /// </summary>
    public sealed class SoundOverlap
    {
        /// <summary>지금 도는 소리 하나. 언제 시작했는지가 「가장 오래된 것」을 가른다.</summary>
        private struct Voice
        {
            public string id;
            public float startedAt;
            public int slot;
        }

        private readonly List<Voice> _voices = new List<Voice>();

        /// <summary>같은 소리의 동시 재생 상한. SO가 준다.</summary>
        public int LimitPerSound { get; private set; }

        /// <summary>재생기가 들고 있는 자리 수 — 소리 종류와 무관한 전체 상한.</summary>
        public int SlotCount { get; private set; }

        public SoundOverlap(int limitPerSound, int slotCount)
        {
            LimitPerSound = limitPerSound;
            SlotCount = slotCount;
        }

        /// <summary>상한을 다시 잡는다. 화면에서 듣고 고친 값을 넣는 자리다(S-1).</summary>
        public void SetLimit(int limitPerSound)
        {
            LimitPerSound = limitPerSound;
            // 줄인 상한은 **지금 도는 것을 끊지 않는다.** 이미 난 소리를 되감을 수 없고,
            // 끊으면 값을 바꾼 순간 화면에서 소리가 뭉텅 사라진다.
        }

        /// <summary>지금 도는 것을 전부 잊는다(스테이지 재시작·씬 진입).</summary>
        public void Clear() => _voices.Clear();

        /// <summary>끝난 소리를 목록에서 뺀다. <paramref name="now"/>가 시작 + 길이를 넘은 것.</summary>
        public void Retire(string id, float startedAt)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].id != id || _voices[i].startedAt != startedAt) continue;
                _voices.RemoveAt(i);
                return;
            }
        }

        /// <summary>이 소리가 지금 몇 개 도는가.</summary>
        public int CountOf(string id)
        {
            int n = 0;
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].id == id) n++;
            return n;
        }

        /// <summary>도는 소리 전체 수.</summary>
        public int ActiveCount => _voices.Count;

        /// <summary>재생 판정 하나.</summary>
        public struct Decision
        {
            /// <summary>소리를 낼 것인가.</summary>
            public bool play;
            /// <summary>쓸 자리 번호. 재생기의 <c>AudioSource</c> 배열 인덱스다.</summary>
            public int slot;
            /// <summary>가장 오래된 것을 끊고 그 자리를 빼앗았는가.</summary>
            public bool evicted;
        }

        /// <summary>
        /// 소리 하나를 청한다.
        ///
        /// **상한 안이면 빈 자리를 준다.** 같은 소리가 상한만큼 돌고 있거나 자리가 다 찼으면
        /// **가장 오래된 것을 끊고** 그 자리를 준다 — 새 소리가 안 나는 것보다 낫다.
        /// 지금 일어난 일을 알리는 것이 소리의 일이기 때문이다(1장).
        ///
        /// ⚠️ 상한이 0 이하면 **아무 소리도 안 난다** — 「무제한」이 아니다.
        /// 0을 무제한으로 읽으면 값을 안 채운 SO가 상한 없는 재생기가 된다.
        /// </summary>
        public Decision Take(string id, float now)
        {
            if (string.IsNullOrEmpty(id) || LimitPerSound <= 0 || SlotCount <= 0)
                return new Decision { play = false, slot = -1, evicted = false };

            int same = CountOf(id);
            bool overSame = same >= LimitPerSound;
            bool overAll = _voices.Count >= SlotCount;

            if (!overSame && !overAll)
            {
                int free = FreeSlot();
                _voices.Add(new Voice { id = id, startedAt = now, slot = free });
                return new Decision { play = true, slot = free, evicted = false };
            }

            // 넘쳤다 — 끊을 것을 고른다.
            // **같은 소리가 넘쳤으면 그 소리 중에서** 가장 오래된 것을 끊는다.
            // 다른 소리를 끊으면 상한이 「이 소리의 상한」이 아니라 「전체의 상한」이 된다.
            int victim = overSame ? OldestOf(id) : Oldest();
            if (victim < 0) return new Decision { play = false, slot = -1, evicted = false };

            int slot = _voices[victim].slot;
            _voices.RemoveAt(victim);
            _voices.Add(new Voice { id = id, startedAt = now, slot = slot });
            return new Decision { play = true, slot = slot, evicted = true };
        }

        private int FreeSlot()
        {
            for (int s = 0; s < SlotCount; s++)
            {
                bool used = false;
                for (int i = 0; i < _voices.Count && !used; i++) used = _voices[i].slot == s;
                if (!used) return s;
            }
            return 0;
        }

        private int Oldest()
        {
            int best = -1;
            for (int i = 0; i < _voices.Count; i++)
                if (best < 0 || _voices[i].startedAt < _voices[best].startedAt) best = i;
            return best;
        }

        private int OldestOf(string id)
        {
            int best = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].id != id) continue;
                if (best < 0 || _voices[i].startedAt < _voices[best].startedAt) best = i;
            }
            return best;
        }
    }
}
