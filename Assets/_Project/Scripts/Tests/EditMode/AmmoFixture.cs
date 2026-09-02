using System.Collections.Generic;
using MBI.Core;
using MBI.Data;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 전투 테스트용 창고 픽스처 — **재고를 미리 채워 둔 창고를 만든다**(260902_W08 §1).
    ///
    /// 왜 여기 있는가: 종전에는 <c>RobotSetup.ammoInitialStock</c>이 있어서 전투가 열릴 때마다
    /// 시뮬이 재고를 만들어 줬다. 그 동작이 폐기되면서(스테이지 전환은 재고에 손대지 않는다)
    /// 실제 코드에는 재고를 만드는 자리가 없어졌다 — 창고는 러너가 들고 다닌다.
    ///
    /// 그런데 발사·태그·합체를 보는 테스트들은 재고가 병목이 되면 안 된다. 그 테스트들이
    /// 재고를 채우는 방법이 여기다. **실제 코드에는 이 경로가 없다** — 픽스처 전용이고,
    /// 그래서 테스트 어셈블리에 둔다.
    /// </summary>
    internal static class AmmoFixture
    {
        /// <summary>
        /// 라인 수요 비율대로 채운 창고. 폐기된 <c>LoadInitialStock</c>과 같은 분배다 —
        /// 그래야 종전 테스트들이 재던 숫자가 그대로 성립한다(회귀 없음).
        /// </summary>
        public static AmmoInventory Stocked(float capacity, IReadOnlyList<AmmoLine> lines, float rounds)
        {
            var inv = new AmmoInventory(capacity);
            if (rounds <= 0f || lines == null || lines.Count == 0) return inv;

            float total = 0f;
            for (int i = 0; i < lines.Count; i++) total += Mathf.Max(0f, lines[i].shotsPerSec);
            if (total <= 0f) return inv;

            for (int i = 0; i < lines.Count; i++)
                inv.Add(lines[i].kind, rounds * (Mathf.Max(0f, lines[i].shotsPerSec) / total));
            return inv;
        }

        /// <summary>관통 한 줄짜리 픽스처. 배분이 무의미하므로 전량 관통으로 넣는다.</summary>
        public static AmmoInventory Pierce(float capacity, float rounds)
        {
            var inv = new AmmoInventory(capacity);
            if (rounds > 0f) inv.Add(AmmoKind.Pierce, rounds);
            return inv;
        }
    }
}
