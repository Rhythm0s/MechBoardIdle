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
        public void 표준탄은_가로로_긴_네모다()
        {
            BoardArtSet set = Load();
            Rect r = set.ItemContentRect(FlowKind.StandardAmmo);

            // 09-15 실측 — 64 캔버스 안에 40×20(탄피). **정사각이 아니다.**
            // 구 그리기가 이것을 정사각 상자에 밀어 넣어 뭉갰다.
            Assert.That(r.width, Is.GreaterThan(r.height),
                "탄피는 옆으로 누운 그림이다 — 정사각으로 그리면 뭉개진다");
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
        public void 표준탄이_구_동작에서_작게_그려졌다는_것을_수로_남긴다()
        {
            BoardArtSet set = Load();
            float span = set.ItemContentSpan(FlowKind.StandardAmmo);

            // 09-15 실측 0.625 — 캔버스 64 안에 40×20.
            // 구 셈은 캔버스를 0.26 칸에 맞췄으므로 **보이는 긴 변이 0.26 × 0.625 = 0.1625 칸**이었다.
            Assert.That(span, Is.LessThan(0.8f),
                "이 품목이 유난히 작게 보였던 근거다 — 값이 커졌다면 그림이 바뀐 것이니 "
                + "이 시험의 설명도 함께 고친다");
        }
    }
}
