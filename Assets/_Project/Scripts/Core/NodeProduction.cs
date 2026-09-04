using System.Collections.Generic;
using MBI.Data;
using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 노드 한 대의 생산(260827_V01 §3). 순수 계산 — 씬 비의존이라 EditMode로 검증된다.
    ///
    /// **출력 버퍼가 가득 차면 그 노드는 멈춘다**(새티스팩토리 제작기의 출력 슬롯과 같다).
    /// 멈춤은 고장이 아니라 신호다 — 하류가 못 받아 가고 있다는 뜻이고, 그것이 병목의 얼굴이다.
    ///
    /// ⚠️ **이 버퍼는 만충 판정에 세지 않는다**(§3-3). 태그·과부하가 보는 「창고 100%」는
    /// 저장 노드(<see cref="AmmoInventory"/>)이고, 여기는 그 노드의 생산 정지 조건일 뿐이다.
    /// 노드 버퍼까지 세면 보드에 노드를 늘리는 것만으로 만충이 앞당겨져 태그 주기가 무너진다.
    /// </summary>
    public static class NodeProduction
    {
        /// <summary>
        /// 이번 틱 산출량(개). 버퍼 상한을 넘겨 만들지 않는다 —
        /// 넘긴 분을 버리면 「막히면 멈춘다」가 아니라 「막혀도 돌면서 버린다」가 된다.
        /// </summary>
        public static float Produce(in NodeRecipe recipe, float bufferNow, float dt)
        {
            if (!recipe.IsRunnable || dt <= 0f) return 0f;

            float room = FreeSpace(recipe, bufferNow);
            if (room <= 0f) return 0f; // 가득 참 → 정지

            return Mathf.Min(recipe.outputPerSec * dt, room);
        }

        /// <summary>
        /// 재료를 보는 산출량 (2026-09-04 신설 · `260904_W01` 3장).
        ///
        /// 위 <see cref="Produce"/>에 **재료 한도**를 하나 더 씌운 것이다. 재료가 모자라면
        /// 있는 만큼만 만들고, 아예 없으면 0을 낸다 — 그것이 「재료가 끊겨 멈췄다」이다.
        ///
        /// **소비는 여기서 하지 않는다.** 얼마나 만들지와 얼마나 먹을지를 한 함수가 같이 하면
        /// 호출자가 산출을 버릴 때 재료만 사라진다. <see cref="ConsumeFor"/>로 나눠 두었다.
        /// </summary>
        public static float Produce(in NodeRecipe recipe, float bufferNow, float dt,
            IReadOnlyDictionary<FlowKind, float> stock)
        {
            float byRoom = Produce(recipe, bufferNow, dt);
            if (byRoom <= 0f) return 0f;

            return Mathf.Min(byRoom, InputCap(recipe, stock));
        }

        /// <summary>
        /// 지금 재고로 만들 수 있는 최대 산출 개수. 재료를 안 먹는 조합표는 무제한이다.
        ///
        /// **가장 모자란 재료가 상한을 정한다** — 하나라도 없으면 0이 되고, 그 하나가 병목이다.
        /// </summary>
        public static float InputCap(in NodeRecipe recipe,
            IReadOnlyDictionary<FlowKind, float> stock)
        {
            List<RecipeInput> inputs = recipe.inputs;
            if (inputs == null || inputs.Count == 0) return float.PositiveInfinity;

            float cap = float.PositiveInfinity;
            for (int i = 0; i < inputs.Count; i++)
            {
                RecipeInput need = inputs[i];
                if (need.perOutput <= 0f) continue; // 0을 먹는 줄은 제약이 아니다

                float have = 0f;
                stock?.TryGetValue(need.kind, out have);
                cap = Mathf.Min(cap, have / need.perOutput);
                if (cap <= 0f) return 0f;
            }
            return cap;
        }

        /// <summary>
        /// 산출 <paramref name="made"/>개를 만들면서 먹은 재료를 재고에서 뺀다.
        /// **음수로 내려가지 않는다** — 부동소수 오차로 아주 작은 음수가 남으면 다음 틱의
        /// <see cref="InputCap"/>이 0을 내어 노드가 이유 없이 멈춘다.
        /// </summary>
        public static void ConsumeFor(in NodeRecipe recipe, float made,
            IDictionary<FlowKind, float> stock)
        {
            List<RecipeInput> inputs = recipe.inputs;
            if (inputs == null || inputs.Count == 0 || made <= 0f || stock == null) return;

            for (int i = 0; i < inputs.Count; i++)
            {
                RecipeInput need = inputs[i];
                if (need.perOutput <= 0f) continue;

                float have = 0f;
                stock.TryGetValue(need.kind, out have);
                stock[need.kind] = Mathf.Max(0f, have - need.perOutput * made);
            }
        }

        /// <summary>재료가 모자라 멈춰 있는가. 버퍼 만충(<see cref="IsStalled"/>)과 사유가 다르다.</summary>
        public static bool IsStarved(in NodeRecipe recipe,
            IReadOnlyDictionary<FlowKind, float> stock)
        {
            if (!recipe.IsRunnable) return false;
            return InputCap(recipe, stock) <= 0f;
        }

        /// <summary>남은 버퍼 공간(개). 상한이 미설정(0 이하)이면 무제한으로 본다.</summary>
        public static float FreeSpace(in NodeRecipe recipe, float bufferNow)
        {
            // stackLimitTbd는 미확정치다(조립 「품목과 재고」 장 신설 중). 0 = 미설정 센티넬 —
            // 하드코딩한 상한을 끼워 넣지 않는다. 확정되면 데이터만 채우면 된다.
            if (recipe.stackLimitTbd <= 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, recipe.stackLimitTbd - bufferNow);
        }

        /// <summary>버퍼가 가득 차 생산이 멈춰 있는가(진단·상태색용).</summary>
        public static bool IsStalled(in NodeRecipe recipe, float bufferNow)
        {
            if (!recipe.IsRunnable) return false;
            return FreeSpace(recipe, bufferNow) <= 0f;
        }

        /// <summary>
        /// **왜** 멈췄는가(260827_V02 §2-1). 플레이어가 할 행동은 같아도 읽히는 의미가 다르다:
        ///   출력 막힘   = 가져가는 쪽이 없다 → **물류 실패의 신호**
        ///   교체 잔여물 = 방금 자기가 바꿨다 → **조작의 정상적 결과**
        ///
        /// 둘을 똑같이 「정지」로 보여주면 레시피를 바꿀 때마다 공장이 고장 난 줄 안다.
        /// 그리고 레시피 교체는 상시 조작이다 — 회피가 모자라면 추진제로, 화력이 모자라면 드론으로.
        ///
        /// 구분 기준은 **버퍼에 든 것이 지금 조합표의 산출물인가**다.
        /// </summary>
        public static NodeStallReason StallReason(in NodeRecipe recipe, float bufferNow, FlowKind bufferKind)
        {
            if (!recipe.IsRunnable || bufferNow <= 0f) return NodeStallReason.None;

            // 버퍼에 이전 조합표의 산출물이 남아 있다 — 가득 차지 않았어도 새 산출물을 섞을 수 없다.
            if (bufferKind != recipe.output) return NodeStallReason.RecipeChangedResidue;

            return FreeSpace(recipe, bufferNow) <= 0f
                ? NodeStallReason.OutputBlocked
                : NodeStallReason.None;
        }

        /// <summary>
        /// 하류가 가져간다. 버퍼에 있는 만큼만 나가고, 없으면 0이 나간다.
        /// </summary>
        public static float Withdraw(float bufferNow, float requested, out float bufferAfter)
        {
            if (requested <= 0f || bufferNow <= 0f)
            {
                bufferAfter = Mathf.Max(0f, bufferNow);
                return 0f;
            }

            float taken = Mathf.Min(requested, bufferNow);
            bufferAfter = bufferNow - taken;
            return taken;
        }
    }
}
