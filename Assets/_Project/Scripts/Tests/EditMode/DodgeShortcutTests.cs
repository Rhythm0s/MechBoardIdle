using MBI.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 회피 PC 단축키의 방향 (2026-09-16 · `260915_W01` 판정 5).
    ///
    /// ⚠️⚠️ **왜 단축키를 넣었나.** 회피가 화면 플릭 하나뿐이라 **촬영본에 입력이 안 남는다** —
    /// 영상에서는 로봇이 저 혼자 피한 것으로 보이고, 「손으로 피할 수 있다」가 증명되지 않는다.
    ///
    /// 📌 여기서 지키는 것은 **「근거가 없으면 스택을 안 쓴다」**이다. 아무 쪽으로나 굴리면
    /// 플레이어가 아껴 둔 스택이 뜻 없이 사라진다 — 그것은 버그보다 나쁘다(손해가 조용하다).
    /// </summary>
    public sealed class DodgeShortcutTests
    {
        [Test]
        public void 가고_있던_쪽이_먼저다()
        {
            // 누르던 방향과 다른 쪽으로 튀면 「내가 시킨 것」으로 안 읽힌다.
            Vector2? d = DodgeShortcut.Direction(new Vector2(0f, -3f), new Vector2(1f, 0f));

            Assert.That(d.HasValue, Is.True);
            Assert.That(d.Value.x, Is.EqualTo(0f).Within(0.001f), "표적 쪽으로 끌려갔다");
            Assert.That(d.Value.y, Is.EqualTo(-1f).Within(0.001f), "가던 쪽이 아니다");
        }

        [Test]
        public void 손이_가만있으면_표적_반대쪽이다()
        {
            Vector2? d = DodgeShortcut.Direction(Vector2.zero, new Vector2(4f, 0f));

            Assert.That(d.HasValue, Is.True);
            Assert.That(d.Value.x, Is.EqualTo(-1f).Within(0.001f), "표적 쪽으로 피했다");
            Assert.That(d.Value.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 근거가_없으면_피하지_않는다()
        {
            // ⚠️ **이 줄이 요점이다.** 방향이 없으면 스택을 쓰지 않는다.
            Assert.That(DodgeShortcut.Direction(Vector2.zero, null).HasValue, Is.False,
                "가는 쪽도 표적도 없는데 스택을 썼다");
        }

        [Test]
        public void 길이가_0에_가까우면_없는_것으로_본다()
        {
            // 아날로그 입력의 미세한 떨림·표적이 정확히 겹친 순간을 0 으로 본다.
            Assert.That(DodgeShortcut.Direction(new Vector2(1e-7f, 0f), null).HasValue, Is.False,
                "떨림을 방향으로 읽었다");
            Assert.That(DodgeShortcut.Direction(Vector2.zero, Vector2.zero).HasValue, Is.False,
                "표적이 겹친 순간을 방향으로 읽었다");
        }

        [Test]
        public void 언제나_단위_길이로_준다()
        {
            // `RequestDodge` 는 정규화된 방향을 받는 약속이다 — 길이가 세기가 되면 안 된다.
            Assert.That(DodgeShortcut.Direction(new Vector2(9f, 9f), null).Value.magnitude,
                Is.EqualTo(1f).Within(0.001f), "이동 쪽");
            Assert.That(DodgeShortcut.Direction(Vector2.zero, new Vector2(0f, 12f)).Value.magnitude,
                Is.EqualTo(1f).Within(0.001f), "표적 쪽");
        }
    }
}
