using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 마운트 적재(260827_V03 §2·§3). **만충·소진 판정의 주체**다.
    ///
    /// 2026-08-27 개정으로 재고 층위가 분리됐다:
    ///   노드 출력 버퍼 → 세지 않는다(생산 정지 조건일 뿐)
    ///   저장 노드(창고) → **세지 않는다**
    ///   마운트 적재     → **센다**
    /// 구 「재고 층위 단일 판정」과 「창고 100%」는 폐기됐고, 태그·태그 스킬이 보는 것은 여기다.
    ///
    /// 슬롯 규칙:
    ///   - 슬롯 하나에는 **아이디가 같은 것만** 쌓인다
    ///   - 같은 품목이 **여러 슬롯**을 차지할 수 있다(네 칸 전부 관통탄도 가능)
    ///   - 슬롯을 먼저 차지하는 것은 **먼저 도착한 것**이다 —
    ///     분류기를 안 쓰면 한 탄종이 칸을 다 먹는데 그것이 의도된 결과다
    ///   - 적재량 = Σ(품목별 슬롯 수 × 그 품목의 스택)
    ///
    /// 비대칭(A 4슬롯 / B 8슬롯)은 의도다 — A는 다발형이라 자주 채우고 자주 쓰고,
    /// B는 단발 고밀도라 천천히 채워 크게 쓴다.
    ///
    /// ⚠️ **스택 수치가 미확정이면 만충을 판정할 수 없다.** 확정된 것은 추진제 3 하나뿐이고
    /// 탄약·드론은 검증 대장 TBD다. 스택이 0(미설정)인 품목은 상한이 없는 것으로 보며,
    /// 그 경우 <see cref="IsFull"/>은 성립하지 않는다 — 하드코딩한 상한을 끼우지 않는다.
    /// </summary>
    public sealed class MountLoad
    {
        private readonly MountItem[] _slotItem;
        private readonly float[] _slotAmount;

        /// <summary>품목별 스택 상한(0 = 미확정). 검증 대장이 채운다.</summary>
        private readonly System.Collections.Generic.Dictionary<MountItem, float> _stackLimits;

        public MountLoad(int slotCount, System.Collections.Generic.Dictionary<MountItem, float> stackLimits = null)
        {
            SlotCount = Mathf.Max(0, slotCount);
            _slotItem = new MountItem[SlotCount];
            _slotAmount = new float[SlotCount];
            _stackLimits = stackLimits ?? new System.Collections.Generic.Dictionary<MountItem, float>();
        }

        /// <summary>로봇 A의 슬롯 수. 다발형이라 자주 채우고 자주 쓴다.</summary>
        public const int SlotsRobotA = 4;

        /// <summary>로봇 B의 슬롯 수. 단발 고밀도라 천천히 채워 크게 쓴다.</summary>
        public const int SlotsRobotB = 8;

        /// <summary>
        /// 마운트 품목 스택 상한 — **탄약 3종·드론 2종 공통**(260901_V03 확정).
        ///
        /// 적재량은 슬롯의 파생값이다: 로봇 A 4 × 10 = 40 · 로봇 B 8 × 10 = 80.
        /// 그래서 A는 자주 채우고 자주 쓰고, B는 천천히 채워 크게 쓴다.
        ///
        /// ⚠️ 저장 노드 용량 40과 숫자가 같은 것은 **우연이다.** 층이 다르다 —
        /// 저장은 태그 주기를 만들고 만충 판정에 세지 않으며, 만충은 이 마운트 층이 본다.
        /// </summary>
        public static System.Collections.Generic.Dictionary<MountItem, float> StandardStacks(float limit)
        {
            return new System.Collections.Generic.Dictionary<MountItem, float>
            {
                { MountItem.Pierce, limit }, { MountItem.Split, limit },
                { MountItem.Explosive, limit }, { MountItem.Drone, limit },
            };
        }

        /// <summary>슬롯 수. 로봇 A = 4 · 로봇 B = 8.</summary>
        public int SlotCount { get; }

        /// <summary>그 품목의 스택 상한. 0이면 미확정(상한 없음).</summary>
        public float StackLimitOf(MountItem item) =>
            _stackLimits.TryGetValue(item, out float v) ? Mathf.Max(0f, v) : 0f;

        /// <summary>전 슬롯 적재량 합.</summary>
        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < SlotCount; i++) sum += _slotAmount[i];
                return sum;
            }
        }

        /// <summary>그 품목의 적재량(여러 슬롯에 걸쳐 있을 수 있다).</summary>
        public float AmountOf(MountItem item)
        {
            float sum = 0f;
            for (int i = 0; i < SlotCount; i++)
                if (_slotItem[i] == item) sum += _slotAmount[i];
            return sum;
        }

        /// <summary>그 품목이 차지한 슬롯 수.</summary>
        public int SlotsUsedBy(MountItem item)
        {
            int n = 0;
            for (int i = 0; i < SlotCount; i++)
                if (_slotItem[i] == item) n++;
            return n;
        }

        public MountItem ItemAt(int slot) => slot >= 0 && slot < SlotCount ? _slotItem[slot] : MountItem.None;
        public float AmountAt(int slot) => slot >= 0 && slot < SlotCount ? _slotAmount[slot] : 0f;

        /// <summary>빈 슬롯이 하나도 없는가.</summary>
        public bool AllSlotsClaimed
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                    if (_slotItem[i] == MountItem.None) return false;
                return SlotCount > 0;
            }
        }

        /// <summary>
        /// **마운트 만충** — 태그·태그 스킬의 발동 조건.
        /// 전 슬롯이 차지되고 각 슬롯이 그 품목의 스택 상한까지 찼을 때만 참이다.
        ///
        /// 스택 상한이 미확정(0)인 품목이 한 칸이라도 있으면 **판정이 성립하지 않는다** —
        /// 상한을 모르는데 「가득 찼다」고 말할 수 없기 때문이다. 임의 상한을 끼우지 않는다.
        /// </summary>
        public bool IsFull
        {
            get
            {
                if (!AllSlotsClaimed) return false;

                for (int i = 0; i < SlotCount; i++)
                {
                    float limit = StackLimitOf(_slotItem[i]);
                    if (limit <= 0f) return false;                 // 미확정 → 판정 불가
                    if (_slotAmount[i] < limit - 1e-4f) return false;
                }
                return true;
            }
        }

        /// <summary>만충 판정이 가능한 상태인가. false면 스택 수치가 아직 없다는 뜻이다.</summary>
        public bool CanJudgeFullness
        {
            get
            {
                if (!AllSlotsClaimed) return true; // 안 찬 것은 확실히 만충이 아니다
                for (int i = 0; i < SlotCount; i++)
                    if (StackLimitOf(_slotItem[i]) <= 0f) return false;
                return true;
            }
        }

        public bool IsEmpty => Total <= 0f;

        /// <summary>적재율 0~1. 상한이 미확정인 슬롯이 있으면 0을 돌려준다(표시 보류).</summary>
        public float FillRatio
        {
            get
            {
                float cap = Capacity;
                return cap > 0f ? Mathf.Clamp01(Total / cap) : 0f;
            }
        }

        /// <summary>적재 상한 = Σ(품목별 슬롯 수 × 그 품목의 스택). 미확정 품목은 0으로 빠진다.</summary>
        public float Capacity
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < SlotCount; i++)
                    if (_slotItem[i] != MountItem.None) sum += StackLimitOf(_slotItem[i]);
                return sum;
            }
        }

        /// <summary>
        /// 벨트가 실어 온 것을 적재한다. **먼저 도착한 것이 슬롯을 차지한다.**
        /// 같은 품목의 기존 슬롯을 먼저 채우고, 모자라면 빈 슬롯을 새로 연다.
        /// 들어간 양을 돌려준다 — 자리가 없으면 0이고, 남은 분은 호출자가 창고에 둔다.
        /// </summary>
        public float Load(MountItem item, float amount)
        {
            if (item == MountItem.None || amount <= 0f) return 0f;

            float limit = StackLimitOf(item);
            float remaining = amount;

            // 1) 같은 품목이 이미 쓰는 슬롯을 채운다.
            for (int i = 0; i < SlotCount && remaining > 0f; i++)
            {
                if (_slotItem[i] != item) continue;
                float room = limit > 0f ? limit - _slotAmount[i] : remaining;
                if (room <= 0f) continue;

                float put = Mathf.Min(room, remaining);
                _slotAmount[i] += put;
                remaining -= put;
            }

            // 2) 모자라면 빈 슬롯을 연다.
            for (int i = 0; i < SlotCount && remaining > 0f; i++)
            {
                if (_slotItem[i] != MountItem.None) continue;

                _slotItem[i] = item;
                float room = limit > 0f ? limit : remaining;
                float put = Mathf.Min(room, remaining);
                _slotAmount[i] = put;
                remaining -= put;
            }

            return amount - remaining;
        }

        /// <summary>
        /// 소비. **그 품목만** 본다 — 다른 품목이 쌓여 있어도 대신 쓰지 않는다.
        /// 모자라면 아무것도 깎지 않고 false.
        /// </summary>
        public bool TryConsume(MountItem item, float amount)
        {
            if (item == MountItem.None || amount <= 0f) return false;
            if (AmountOf(item) < amount - 1e-4f) return false;

            float remaining = amount;
            for (int i = 0; i < SlotCount && remaining > 0f; i++)
            {
                if (_slotItem[i] != item) continue;

                float take = Mathf.Min(_slotAmount[i], remaining);
                _slotAmount[i] -= take;
                remaining -= take;

                if (_slotAmount[i] <= 1e-5f) ReleaseSlot(i); // 빈 칸은 놓아 준다 — 다른 품목이 쓸 수 있게
            }
            return true;
        }

        /// <summary>
        /// **태그 스킬** — 마운트 재고 전량을 소진한다(V03 §4).
        /// 소진된 총량을 돌려준다. 저장 노드는 건드리지 않는다 —
        /// 마운트가 빈 동안 화력이 죽고 벨트가 다시 채우며, 물류가 좋을수록 그 공백이 짧다.
        /// </summary>
        public float DrainAll()
        {
            float drained = Total;
            for (int i = 0; i < SlotCount; i++) ReleaseSlot(i);
            return drained;
        }

        private void ReleaseSlot(int i)
        {
            _slotItem[i] = MountItem.None;
            _slotAmount[i] = 0f;
        }
    }
}
