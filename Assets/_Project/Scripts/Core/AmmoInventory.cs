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
    /// </summary>
    public sealed class AmmoInventory
    {
        private const float FullEpsilon = 1e-4f;

        public AmmoInventory(float capacity, float initialStock)
        {
            Capacity = Mathf.Max(0f, capacity);
            Stock = Mathf.Clamp(initialStock, 0f, Capacity);
        }

        /// <summary>용량(발). 확정치 40 — 저장 노드 배치에 따른 증가 규칙은 TBD.</summary>
        public float Capacity { get; }

        /// <summary>현재 잔량(발).</summary>
        public float Stock { get; private set; }

        /// <summary>적재율 0~1. 만충 판정(태그·과부하 트리거)의 기준.</summary>
        public float FillRatio => Capacity > 0f ? Stock / Capacity : 0f;

        /// <summary>만재인가. 부분 발동이 없는 트리거들이 이 값을 본다.</summary>
        public bool IsFull => Capacity > 0f && Stock >= Capacity - FullEpsilon;

        public bool IsEmpty => Stock <= 0f;

        /// <summary>생산 유입. 용량을 넘는 분은 버려진다(창고가 꽉 차면 더 담을 곳이 없다).</summary>
        public void Produce(float dt, float ratePerSec)
        {
            if (dt <= 0f || ratePerSec <= 0f) return;
            Stock = Mathf.Min(Capacity, Stock + ratePerSec * dt);
        }

        /// <summary>발사 소비. 모자라면 **아무것도 깎지 않고** false — 반발 소비는 없다.</summary>
        public bool TryConsume(float rounds)
        {
            if (rounds <= 0f || Stock < rounds) return false;
            Stock -= rounds;
            return true;
        }

        /// <summary>창고를 비운다(합체 발동 시 소진 등). 지금은 재시작 초기화에만 쓴다.</summary>
        public void Drain() => Stock = 0f;

        /// <summary>만재로 채운다.</summary>
        public void Fill() => Stock = Capacity;
    }
}
