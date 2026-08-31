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
