using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// IMGUI에 한글 폰트를 물린다.
    ///
    /// **왜 필요한가**: WebGL 빌드에는 시스템 폰트 폴백이 없다. Unity 내장 GUI 폰트에는
    /// 한글 글리프가 없으므로, 폰트를 동봉하지 않으면 화면의 한글이 **전부 사라진다** —
    /// 2026-08-31 브라우저 실측에서 숫자·기호·영문만 남고 한글 글리프가 0개였다.
    /// 에디터에서는 OS 폰트가 대신 채워 주기 때문에 **에디터로만 보면 절대 안 드러난다.**
    ///
    /// <see cref="Apply"/>를 각 OnGUI 맨 앞에서 부른다. `GUI.skin`은 OnGUI 바깥에서
    /// 건드리면 안 되므로 초기화 시점에 한 번 물려 둘 수가 없다.
    /// </summary>
    public static class KoreanFont
    {
        /// <summary>Resources 기준 경로(확장자 없음).</summary>
        private const string ResourcePath = "Fonts/NotoSansKR-Regular";

        private static Font _font;
        private static bool _tried;

        /// <summary>
        /// 글자 크기를 **네 칸 단위로 맞춘다** (2026-09-14 · §72-12 1 글리프 누락).
        ///
        /// **왜 필요한가.** 유니티의 **동적 폰트는 크기마다 글리프를 따로 굽는다.**
        /// 화면 비례로 크기를 내면(`rect.height * 0.15f` 같은 것) 창 크기·값이 흔들릴 때마다
        /// **새 크기가 생기고**, 한글은 완성형이라 글자 종류가 많아 **아틀라스가 금세 찬다.**
        /// 차면 굽지 못한 글리프가 빠지는데, 화면에서는 **받침부터 사라진 것처럼** 보인다 —
        /// 「마운트 · 드론」이 「마으ㅌ · 드로」로 나온 자리가 그것이다.
        ///
        /// 네 칸으로 스냅하면 **크기 가짓수가 1/4로 준다.** 글자는 한두 픽셀 커지거나
        /// 작아질 뿐이고, 그 대가로 **글리프가 빠지지 않는다.**
        ///
        /// ⚠️ **아래로 내리지 않는다** — 올림이다. 내리면 최소 크기 규정(읽히는 크기)을
        /// 밑돌 수 있다.
        /// </summary>
        public static int Snap(int px)
        {
            if (px <= MinSize) return MinSize;
            if (px >= MaxSize) return MaxSize;
            return (px + Step - 1) / Step * Step;
        }

        /// <summary>
        /// **이보다 크게는 안 굽는다** (2026-09-15 · 조립 화면 실측).
        ///
        /// ⚠️ **왜 상한이 필요한가.** 스냅만으로는 크기 **가짓수**만 줄 뿐, **한 글리프의
        /// 크기**는 안 줄어든다. 구역 이름표는 `칸 화면 픽셀 / 192 × 96` 이라 배율을 넣으면
        /// 한없이 커지는데, 큰 한글 글리프 몇 장이면 아틀라스가 그대로 찬다 —
        /// 09-15 조립 화면 실측에서 「몸통」·「다리」가 거대하게 뜬 화면에서
        /// **팔레트 버튼 글자·카테고리 탭 글자가 통째로 사라지고** 「다리L」이 「다리I」로 찍혔다.
        ///
        /// 즉 09-14 의 「마으ㅌ」, 09-15 아침의 「판1」과 **같은 병의 셋째 얼굴**이다.
        /// 앞의 둘은 가짓수가 원인이라 스냅으로 닫혔고, 이것은 **크기 자체**가 원인이다.
        ///
        /// ⚠️ **64 는 가정이다** — 문서에 글자 크기 상한 절이 없다(설계 역기입).
        /// 기준 캔버스 2560 에서 문서가 직접 지정한 가장 큰 글자가 이 언저리라 골랐다.
        /// 이보다 큰 글자가 필요하면 **그 글자만** 예외로 두는 편이 낫다 — 상한을 올리면
        /// 아틀라스가 다시 찬다.
        /// </summary>
        public const int MaxSize = 64;

        /// <summary>스냅 단위(px). 넷이면 가짓수가 1/4 이 된다.</summary>
        public const int Step = 4;

        /// <summary>이보다 작게는 안 굽는다 — 읽히지 않는 크기다.</summary>
        public const int MinSize = 8;

        /// <summary>
        /// 이번 OnGUI 호출에 한글 폰트를 적용한다. 폰트가 없으면 조용히 지나간다 —
        /// 폰트 하나 때문에 화면 전체가 안 그려지면 그게 더 나쁘다.
        /// </summary>
        public static void Apply()
        {
            Ensure();
            if (_font != null) GUI.skin.font = _font;
        }

        /// <summary>폰트 자산이 존재하는가(진단·테스트용). ⚠️ `GUI.skin`을 건드리지 않는다 —
        /// OnGUI 바깥에서 부를 수 있어야 테스트가 이 자산을 지킬 수 있다.</summary>
        public static bool IsAvailable
        {
            get
            {
                Ensure();
                return _font != null;
            }
        }

        private static void Ensure()
        {
            if (_tried) return;
            _tried = true;

            _font = Resources.Load<Font>(ResourcePath);
            if (_font == null)
                Debug.LogWarning($"[MBI] 한글 폰트 없음: Resources/{ResourcePath} — " +
                                 "WebGL 빌드에서 한글이 안 보인다.");
        }
    }
}
