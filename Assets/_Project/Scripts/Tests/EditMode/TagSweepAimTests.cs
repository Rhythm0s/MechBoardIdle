using System.Collections.Generic;
using MBI.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// **빔은 무리를 본다** (2026-09-21 사용자 확정 · 화면 전체 녹색 섬광 폐기).
    ///
    /// 겨냥이 무리 쪽인지 회전 스윕인지는 받은 말에 없어서 **무리 쪽으로 가정**했다.
    /// 여기서 지키는 것은 그 가정의 내용 하나다 — **화면 안 적만 센다.**
    /// 판정은 화면 안 전부에 나누므로, 겨냥도 같은 무리를 봐야 한다.
    /// </summary>
    public sealed class TagSweepAimTests
    {
        private static readonly Rect Screen = new Rect(-10f, -10f, 20f, 20f);
        private static readonly Vector2 Fallback = new Vector2(99f, 99f);

        [Test]
        public void 화면_안_적들의_한가운데다()
        {
            var spots = new List<Vector2> { new Vector2(2f, 0f), new Vector2(4f, 0f) };
            Vector2 aim = TagSkillEffect.CrowdCenter(spots, Screen, Fallback);
            Assert.AreEqual(3f, aim.x, 0.001f);
            Assert.AreEqual(0f, aim.y, 0.001f);
        }

        /// <summary>
        /// ⚠️ **화면 밖은 안 센다** — 저쪽으로 빔이 가면 「저기는 왜 쏘나」가 된다.
        /// 판정도 화면 안에만 든다.
        /// </summary>
        [Test]
        public void 화면_밖_적은_안_센다()
        {
            var spots = new List<Vector2> { new Vector2(2f, 0f), new Vector2(500f, 0f) };
            Vector2 aim = TagSkillEffect.CrowdCenter(spots, Screen, Fallback);
            Assert.AreEqual(2f, aim.x, 0.001f, "화면 밖 적이 겨냥을 끌어갔다");
        }

        /// <summary>
        /// 화면 안에 아무도 없으면 **대신 쓸 자리**다 — 마지막 한 마리를 같은 프레임에
        /// 죽이면 나는 판이고, 원점을 겨냥하면 빔 길이가 0 이 된다.
        /// </summary>
        [Test]
        public void 아무도_없으면_대신_쓸_자리다()
        {
            Assert.AreEqual(Fallback, TagSkillEffect.CrowdCenter(
                new List<Vector2> { new Vector2(500f, 500f) }, Screen, Fallback));
            Assert.AreEqual(Fallback, TagSkillEffect.CrowdCenter(
                new List<Vector2>(), Screen, Fallback));
            Assert.AreEqual(Fallback, TagSkillEffect.CrowdCenter(null, Screen, Fallback));
        }
    }
}
