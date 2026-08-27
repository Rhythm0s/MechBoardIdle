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
