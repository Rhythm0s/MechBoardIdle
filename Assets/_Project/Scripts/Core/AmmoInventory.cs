using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 탄약 재고(§5-7). **재고는 하나의 층이다** — 마운트 적재와 창고 비축이 별개가 아니고,
    /// 만충 판정도 이 하나를 본다(밸런스 문서「태그 시스템 수치」 미결 #9 해소, 07 문서「로봇 B」 공통 규칙).
    /// 드론도 같은 층을 쓴다.
    ///
    /// 왜 필요한가: 밸런스 문서「밸런스 확정 원칙」이 **탄약 소진 = 공격 정지**를 확정 원칙으로 둔다.
    /// 대체 수단이 없으므로 재고가 마르면 발사가 멈춰야 하고, 그러려면 잔량이라는 상태가 있어야 한다.
    ///
    /// 역할은 **버퍼**다. 생산과 소비가 직결이면 소비가 잠깐 튀거나 생산이 잠깐 끊길 때 완충할 것이 없어
    /// 짧은 흔들림이 그대로 발사 정지가 된다. 창고가 그 흔들림을 흡수한다.
    ///
    /// 흐름(조립 시스템 문서「시설·라인 구조」): 군수 → 벨트 → 저장(창고) → 벨트 → 마운트 소비.
    /// 활성 로봇은 초과분만 쌓이고, 대기 로봇은 소비가 0이라 생산 전량이 쌓인다.
    ///
    /// **탄종별 스택 · 총량 캡 공유**(260824_V02 §2 확정):
    /// 잔량은 탄종별로 따로 센다 — 「관통 3발 남았는데 폭발을 쏜다」가 표현되어야 하기 때문이다.
    /// 그러나 용량 40은 **셋이 나눠 쓴다**(잠식 허용) — 한 탄종이 창고를 채우면 다른 탄종은 쌓을 자리가 없다.
    /// 만충 판정은 **총합**으로 한다. 탄종마다 따로 세면 태그 주기가 탄종 수만큼 늘어나
    /// 「주기 10초 = 저장 40 ÷ 대표 생산 4발/초」(밸런스 문서「태그 시스템 수치」)가 깨진다.
    /// </summary>
    public sealed class AmmoInventory
    {
        private const float FullEpsilon = 1e-4f;
        private const int KindCount = 3; // AmmoKind: Pierce · Split · Explosive

        private readonly float[] _stacks = new float[KindCount];

        public AmmoInventory(float capacity)
        {
            Capacity = Mathf.Max(0f, capacity);
        }

        /// <summary>용량(발). 확정치 40 — **전 탄종이 공유하는 총량 상한**이다. 저장 노드 배치에 따른 증가 규칙은 TBD.</summary>
        public float Capacity { get; }

        /// <summary>전 탄종 합계(발). 만충·적재율 판정의 분자 — 트리거들이 보는 유일한 값.</summary>
        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < KindCount; i++) sum += _stacks[i];
                return sum;
            }
        }

        /// <summary>남은 적재 공간(발). 어느 탄종이든 여기까지만 더 들어간다.</summary>
        public float FreeSpace => Mathf.Max(0f, Capacity - Total);

        /// <summary>탄종별 잔량(발).</summary>
        public float StockOf(AmmoKind kind) => _stacks[(int)kind];

        /// <summary>적재율 0~1. 만충 판정(태그·과부하 트리거)의 기준 — 분모는 총량 캡 하나다.</summary>
        public float FillRatio => Capacity > 0f ? Total / Capacity : 0f;

        /// <summary>만재인가. 부분 발동이 없는 트리거들이 이 값을 본다.</summary>
        public bool IsFull => Capacity > 0f && Total >= Capacity - FullEpsilon;

        public bool IsEmpty => Total <= 0f;

        /// <summary>
        /// 생산 유입. **남은 공간까지만** 들어가고 넘는 분은 버려진다.
        /// 공간은 셋이 나눠 쓰므로, 다른 탄종이 창고를 채워 두면 이 탄종은 못 쌓는다(잠식).
        /// </summary>
        public void Produce(AmmoKind kind, float dt, float ratePerSec)
        {
            if (dt <= 0f || ratePerSec <= 0f) return;
            Add(kind, ratePerSec * dt);
        }

        /// <summary>직접 적재(초기 재고·치트). 남은 공간을 넘는 분은 버려진다.</summary>
        public void Add(AmmoKind kind, float rounds)
        {
            if (rounds <= 0f) return;
            _stacks[(int)kind] += Mathf.Min(rounds, FreeSpace);
        }

        /// <summary>
        /// 발사 소비. **그 탄종의 잔량만** 본다 — 다른 탄종이 쌓여 있어도 대신 쏘지 않는다.
        /// 모자라면 아무것도 깎지 않고 false(반발 소비 없음).
        /// </summary>
        public bool TryConsume(AmmoKind kind, float rounds)
        {
            if (rounds <= 0f) return false;

            int i = (int)kind;
            if (_stacks[i] < rounds) return false;

            _stacks[i] -= rounds;
            return true;
        }

        /// <summary>창고를 비운다(합체 발동 시 소진 등). 지금은 재시작 초기화에만 쓴다.</summary>
        public void Drain()
        {
            for (int i = 0; i < KindCount; i++) _stacks[i] = 0f;
        }

        /// <summary>남은 공간을 이 탄종으로 채운다.</summary>
        public void Fill(AmmoKind kind) => Add(kind, FreeSpace);
    }
}
