using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// **자동 교대를 켤 것인가** (2026-09-16 신설 · 사용자 확정 · 플랜 §74-16 ③).
    ///
    /// · **켜짐** — 현행 규칙 그대로다. 대기 마운트가 만충이면 화려하게 등장하고,
    ///   활성 마운트가 마르면 어쩔 수 없이 교대한다(`TagSystem.EvaluateAuto`).
    /// · **꺼짐** — **손으로만** 교대한다. 쿨다운·잠금은 그대로 걸린다.
    ///
    /// ⚠️ **기본값 꺼짐은 가정이다**(설계 역기입 자리). 근거는 사용자 확정 문구
    /// 「탭 = 교대 · 길게 = 자동」이다 — 길게 눌러 **켜는** 것이 기본 동작이라면
    /// 처음 상태는 꺼짐이어야 말이 된다. 문서에 절이 서면 이 한 줄만 바뀐다.
    ///
    /// ⚠️⚠️ **`PlayerPrefs` 에 산다 — 세이브가 아니다.** 저장 초기화(`ResetSave`)로
    /// 안 지워진다는 뜻이다. 이것은 진행이 아니라 **조작 취향**이고, 촬영용 초기화가
    /// 조작 설정까지 되돌리면 매번 다시 켜야 한다.
    ///
    /// 📌 **읽는 곳이 값을 캐지 않는다.** `PlayerPrefs` 는 싸고, 캐두면 다른 창에서
    /// 바꾼 값과 어긋난다 — 한 값이 두 곳에 사는 꼴이다(지침 §7).
    /// </summary>
    public static class TagAutoMode
    {
        private const string Key = "mbi.tag.auto";

        /// <summary>⚠️ **가정** — 문서에 절이 없다. 꺼짐으로 시작한다.</summary>
        public const bool DefaultEnabled = false;

        /// <summary>자동 교대가 켜져 있는가.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, DefaultEnabled ? 1 : 0) != 0;
            set
            {
                if (Enabled == value) return;   // 안 바뀌면 안 쓴다 — 쓰기는 디스크에 닿는다
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 버튼 밑에 다는 안내.
        ///
        /// ⚠️ **「켬/끔」이 붙었다**(2026-09-16 사용자 육안 · 플랜 §74-21 ①) —
        /// 길게 누르기가 **켜기만** 하던 것이 **토글**로 바뀌었기 때문이다.
        /// 문구가 손짓과 다르면 화면이 거짓말을 한다. ⚠️ 뒷말은 **가정**이다.
        /// </summary>
        public const string Hint = "탭 = 교대 · 길게 = 자동 켬/끔";

        /// <summary>
        /// 길게 누른 것으로 볼 시간(초). ⚠️ **가정 0.6** — 문서에 길게 누르기 절이 없다.
        ///
        /// 너무 짧으면 **교대하려던 손이 자동을 켜고**, 너무 길면 눌러도 안 켜진 줄 안다.
        /// </summary>
        public const float LongPressSeconds = 0.6f;

        /// <summary>
        /// 켜져 있으면 끄고, 꺼져 있으면 켠다 — **길게 누르기와 토글 버튼이 같이 쓴다.**
        ///
        /// 📌 뒤집는 자리를 한 곳에 둔다. 두 곳에서 각자 뒤집으면 한쪽만 고쳐지는 날이 온다.
        /// </summary>
        public static bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }

        /// <summary>시험·초기화용 — 기본값으로 되돌린다.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
