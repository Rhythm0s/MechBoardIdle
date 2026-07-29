using UnityEngine;

namespace MBI.Combat
{
    /// <summary>
    /// 런타임 생성 1유닛 흰 사각 스프라이트(공유). 아트 자산 없이 플레이스홀더 렌더용.
    /// 본체·HP바·탄선 FX가 공유한다. 아트 리소스 준비 시 실제 스프라이트로 교체.
    /// </summary>
    public static class PlaceholderSprite
    {
        private static Sprite _white;

        public static Sprite White()
        {
            if (_white != null) return _white;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _white.name = "MBI_WhiteSquare";
            return _white;
        }
    }
}
