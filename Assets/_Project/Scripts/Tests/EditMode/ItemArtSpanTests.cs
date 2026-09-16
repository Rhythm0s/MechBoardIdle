using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 품목 그림 크기 — **캔버스가 아니라 그림에 맞춘다** (2026-09-15 · 육안 4차 ⑥).
    ///
    /// ⚠️ **이 결함은 눈으로 못 가른다.** 그림은 제대로 나오고 에러도 없다.
    /// 그냥 **작게** 나올 뿐이라 「원래 이런 그림인가 보다」로 읽힌다 —
    /// 실제로 09-15 에 제가 그렇게 읽고 「결함 아님」으로 닫았다.
    /// 그래서 화면이 아니라 **수**가 지킨다.
    /// </summary>
    public sealed class ItemArtSpanTests
    {
        private const string ArtSetPath = "Assets/_Project/ScriptableObjects/BoardArtSet.asset";

        private static BoardArtSet Load()
        {
            var set = AssetDatabase.LoadAssetAtPath<BoardArtSet>(ArtSetPath);
            if (set == null) Assert.Ignore("BoardArtSet 이 없다 — 자산 생성 전 클론");
            return set;
        }

        [Test]
        public void 그림이_붙은_품목은_전부_재어_두었다()
        {
            BoardArtSet set = Load();
            var unmeasured = new System.Collections.Generic.List<string>();

            foreach (BoardArtSet.ItemArt it in set.items)
            {
                if (it.sprite == null) continue;
                if (it.contentSpan <= 0.0001f) unmeasured.Add($"{it.kind}({it.sprite.name})");
            }

            Assert.That(unmeasured, Is.Empty,
                "「MBI/Measure Item Art Spans」 를 돌린다 — 안 재면 그 품목만 조용히 작게 그려진다: "
                + string.Join(", ", unmeasured));
        }

        [Test]
        public void 잰_값이_실제_그림과_같다()
        {
            BoardArtSet set = Load();

            foreach (BoardArtSet.ItemArt it in set.items)
            {
                if (it.sprite == null || it.contentSpan <= 0.0001f) continue;

                // ⚠️ **자산에 적힌 값을 믿지 않고 다시 잰다** — 그림이 바뀌었는데
                // 생성기를 안 돌리면 값만 옛것으로 남고, 그 품목이 다시 작아진다.
                float now = MBI.Editor.ItemArtSpanGenerator.MeasureSpan(
                    AssetDatabase.GetAssetPath(it.sprite));

                Assert.That(now, Is.EqualTo(it.contentSpan).Within(0.01f),
                    $"{it.kind}({it.sprite.name}) — 그림이 바뀌었다. "
                    + "「MBI/Measure Item Art Spans」 를 다시 돌린다");
            }
        }

        [Test]
        public void 비율은_0과_1_사이다()
        {
            BoardArtSet set = Load();
            foreach (BoardArtSet.ItemArt it in set.items)
            {
                if (it.sprite == null) continue;
                Assert.That(it.contentSpan, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),
                    $"{it.kind} — 1 을 넘으면 그림이 캔버스보다 크다는 뜻이라 셈이 틀린 것이다");
            }
        }

        [Test]
        public void 그려진_네모도_전부_재어_두었다()
        {
            BoardArtSet set = Load();
            foreach (BoardArtSet.ItemArt it in set.items)
            {
                if (it.sprite == null) continue;

                // ⚠️ **`contentSpan` 만 보면 안 된다** — 스프라이트로 그리는 쪽은 긴 변만
                // 알면 되지만 `GUI.DrawTextureWithTexCoords` 로 그리는 쪽은 **어디를
                // 잘라 쓸지**를 알아야 한다. 09-15 에 그 둘을 한 값으로 묶으려다
                // **한 경로만 고치고 다른 경로를 놓쳤다**(「주황 정사각」이 두 번 올라온 까닭).
                Assert.That(it.contentRect.width, Is.GreaterThan(0f),
                    $"{it.kind} — 「MBI/Measure Item Art Spans」 를 돌린다");
                Assert.That(it.contentRect.height, Is.GreaterThan(0f), it.kind.ToString());

                Assert.That(it.contentRect.xMax, Is.LessThanOrEqualTo(1.0001f), it.kind.ToString());
                Assert.That(it.contentRect.yMax, Is.LessThanOrEqualTo(1.0001f), it.kind.ToString());
                Assert.That(it.contentRect.x, Is.GreaterThanOrEqualTo(-0.0001f), it.kind.ToString());
                Assert.That(it.contentRect.y, Is.GreaterThanOrEqualTo(-0.0001f), it.kind.ToString());

                // 긴 변은 두 값이 같은 그림을 가리킨다는 증거다 — 어긋나면 한쪽만 갱신된 것이다.
                Assert.That(Mathf.Max(it.contentRect.width, it.contentRect.height),
                    Is.EqualTo(it.contentSpan).Within(0.01f),
                    $"{it.kind} — 네모와 긴 변이 다른 그림을 가리킨다");
            }
        }

        [Test]
        public void 표준탄은_세로로_긴_네모다()
        {
            BoardArtSet set = Load();
            Rect r = set.ItemContentRect(FlowKind.StandardAmmo);

            // ⚠️⚠️ **그림이 바뀌었다**(2026-09-16 · 아트 `ed1e7c6` 「표준탄 세우기」).
            //
            // 구: 64 캔버스 안에 **40×20** — 옆으로 누운 탄피. 20px 로 줄이면 20×10 이 되어
            //     화면에서 **주황 막대**로 읽혔다(사용자 육안 · 09-15).
            // 신: **22×55** — 세워서 짧고 굵게. 20px 에서 8×20 이라 세로가 두 배가 됐다.
            //
            // 📌 앞 시험이 「값이 커졌다면 그림이 바뀐 것이니 설명도 함께 고친다」고
            //    적어 두었고, 그대로 고친 자리다.
            Assert.That(r.height, Is.GreaterThan(r.width),
                "세운 탄약이다 — 눕혀 그리면 다시 막대가 된다");
        }

        [Test]
        public void 안_잰_품목은_1로_떨어진다()
        {
            BoardArtSet set = Load();

            // 목록에 없는 품목 — 구 동작(캔버스 기준)을 그대로 쓴다.
            Assert.That(set.ItemContentSpan(FlowKind.None), Is.EqualTo(1f),
                "못 잰 그림 때문에 크기가 갑자기 달라지면 안 된다");
            Assert.That(set.ItemContentRect(FlowKind.None), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "못 잰 그림은 캔버스 전체 — 구 동작 그대로다");
        }

        [Test]
        public void 표준탄이_다른_품목과_같은_대역이다()
        {
            BoardArtSet set = Load();
            float span = set.ItemContentSpan(FlowKind.StandardAmmo);

            // 🗑️ **폐기(2026-09-16)** — 종전 단언은 `span < 0.8` 이었다.
            //    근거는 09-15 실측 **0.625**(64 캔버스 안에 40×20)이고, 구 셈이 캔버스를
            //    0.26 칸에 맞췄으므로 보이는 긴 변이 0.1625 칸이었다는 것이었다.
            //
            // ✅ 아트가 그림을 갈았다(`ed1e7c6`) — 실측 **0.859**(22×55). 이제 다른 품목들과
            //    같은 대역이다(0.66~0.97). 「유난히 작다」는 더 이상 사실이 아니다.
            //
            // 📌 **시험을 지우지 않는다** — 이 칸이 다시 작아지면 같은 증상이 돌아온다.
            //    기준을 뒤집어 **작아지는 것**을 막는다.
            Assert.That(span, Is.GreaterThan(0.8f),
                "표준탄이 다시 작아졌다 — 09-15 에 화면에서 주황 막대로 읽혔던 그 자리다");
        }
    }
}
