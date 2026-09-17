using MBI.Data;
using NUnit.Framework;
using UnityEditor;

namespace MBI.Tests
{
    /// <summary>
    /// 시험이 **밸런스 값을 읽는 하나뿐인 문**(지침 §7 「한 값이 두 곳에 살면 답이 둘이 된다」).
    ///
    /// ⚠️⚠️ **주기·필요치를 시험에 박지 않는다.** 2026-09-17 에 `propellantNeed` 가
    /// 150 → 30 으로 바뀌자 `1f / 15f` 를 박아 둔 자리 **넷이 한꺼번에 빨개졌다** —
    /// 값이 틀려서가 아니라 시험이 옛 수를 외우고 있어서다.
    /// 시험이 지키는 것은 「그 수」가 아니라 **같은 식에서 온다**이다.
    /// </summary>
    public static class BalanceFixture
    {
        private const string Path = "Assets/_Project/ScriptableObjects/BalanceConfig.asset";

        public static BalanceConfig Config()
        {
            var bal = AssetDatabase.LoadAssetAtPath<BalanceConfig>(Path);
            Assert.IsNotNull(bal, "BalanceConfig 자산이 없다 — 생성기를 먼저 돌린다");
            return bal;
        }

        /// <summary>추진제 산출률(개/초) = 노드 생산력 ÷ 필요 생산치.</summary>
        public static float PropellantPerSec()
        {
            BalanceConfig bal = Config();
            Assert.Greater(bal.propellantNeed, 0f, "필요 생산치가 0 이면 나눌 수 없다");
            return bal.nodeProductionPower / bal.propellantNeed;
        }
    }
}
