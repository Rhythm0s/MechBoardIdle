using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// 파츠 종류별 색 다섯 (2026-09-11 사용자 확정 · UI 문서 3-0 「파츠 색상 아웃라인」 · 플랜 §71-16 ⑧).
    ///
    /// **왜 색을 가르는가.** 구역 표시가 **전부 같은 미색**이라, 점선이 어디서 끊기는지를
    /// 눈으로 좇지 않으면 **어느 칸이 어느 파츠인지**가 안 읽혔다. 117칸이 한 덩어리로 보이면
    /// 「파츠라는 제한 공간」(조립 문서 11장)이 화면에서 사라진다.
    ///
    /// ⚠️ **좌우는 같은 색이다.** 팔L·팔R 을 가르면 색이 여덟이 되어 **색이 정보를 잃는다** —
    /// 좌우는 이름표(`팔L`·`팔R`)가 이미 가르고 있고, 색이 말할 것은 **종류**다.
    ///
    /// ⚠️ **저채도로 묶는다.** 보드 위에서 정보를 내는 것은 **품목과 노드**이고 구역은 바탕이다
    /// (배치 규격 「타일 위 · 품목 아래」). 채도를 올리면 바탕이 앞으로 나와 코어를 가린다 —
    /// 지침 §1 이 이 게임의 코어를 「물류와 그 애니메이션」으로 정해 두었다.
    ///
    /// ⚠️ **값은 가정이다.** UI 문서 3-0 이 「파츠 색상 아웃라인」을 적었을 뿐 **색을 안 정했다.**
    /// 설계 역기입 자리이며, 값이 서면 <see cref="Of"/> 하나만 바뀐다.
    /// </summary>
    public static class PartPalette
    {
        /// <summary>구역 표시의 바탕 미색 — 파츠를 못 가릴 때 쓰는 값이다.</summary>
        public static readonly Color Neutral = new Color(0.96f, 0.94f, 0.86f, 1f);

        /// <summary>
        /// 파츠 종류 → 색. ⚠️ **가정값 다섯**(저채도 · 좌우 같음).
        ///
        /// 서로 이웃한 파츠가 비슷한 색으로 붙지 않게 골랐다 — 몸통(모래)을 가운데 두고
        /// 머리(청), 어깨(보라), 팔(주황), 다리(초록)가 각각 다른 쪽에서 붙는다.
        /// </summary>
        public static Color Of(RobotPart part)
        {
            switch (part)
            {
                case RobotPart.Head: return new Color(0.62f, 0.78f, 0.84f, 1f);   // 청
                case RobotPart.Torso: return new Color(0.88f, 0.82f, 0.66f, 1f);  // 모래
                case RobotPart.ShoulderL:
                case RobotPart.ShoulderR: return new Color(0.76f, 0.70f, 0.84f, 1f); // 보라
                case RobotPart.ArmL:
                case RobotPart.ArmR: return new Color(0.90f, 0.76f, 0.62f, 1f);   // 주황
                case RobotPart.LegL:
                case RobotPart.LegR: return new Color(0.70f, 0.82f, 0.68f, 1f);   // 초록
                default: return Neutral;
            }
        }

        /// <summary>색이 다섯인가(시험이 센다). 좌우를 가르면 여덟이 되어 색이 정보를 잃는다.</summary>
        public static Color[] Five => new[]
        {
            Of(RobotPart.Head), Of(RobotPart.Torso), Of(RobotPart.ShoulderL),
            Of(RobotPart.ArmL), Of(RobotPart.LegL),
        };

        /// <summary>경계 점선 불투명도 40% — 밑의 타일을 가리지 않는다(배치 규격).</summary>
        public const float LineAlpha = 0.40f;

        /// <summary>
        /// 바닥 틴트 불투명도. ⚠️ **가정 0.22** — 바닥 그림 위에 얹는 것이라
        /// 진하면 타일 무늬가 사라지고 옅으면 색이 안 읽힌다.
        /// </summary>
        public const float FloorAlpha = 0.22f;

        /// <summary>경계 점선 색.</summary>
        public static Color LineOf(RobotPart part) => WithAlpha(Of(part), LineAlpha);

        /// <summary>
        /// 바닥 틴트 색 — **흰색에 섞어 낸다.** `SpriteRenderer.color` 는 그림에 곱해지므로
        /// 알파를 낮추면 **투명해질 뿐 색이 안 섞인다.** 흰 쪽으로 당겨야 무늬가 남는다.
        /// </summary>
        public static Color FloorOf(RobotPart part)
        {
            Color c = Of(part);
            return new Color(
                Mathf.Lerp(1f, c.r, FloorAlpha),
                Mathf.Lerp(1f, c.g, FloorAlpha),
                Mathf.Lerp(1f, c.b, FloorAlpha),
                1f);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
