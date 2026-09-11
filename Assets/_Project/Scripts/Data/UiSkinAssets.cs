using UnityEngine;

namespace MBI.Data
{
    /// <summary>
    /// UI 그릇 자산 — **껍데기가 그림을 찾는 한 자리** (2026-09-11 · `260911_W02` 9장).
    ///
    /// **왜 SO 인가.** `MBI.UI.UiSkin` 은 `MonoBehaviour` 가 아니라 정적 클래스라
    /// 인스펙터로 그림을 받을 수가 없다. 그리고 `Art/UI/` 는 `Resources` 밖이라
    /// 런타임에서 경로로 못 읽는다 — **이 SO 가 그 사이를 잇는다.**
    /// (`KoreanFont` 가 폰트를 `Resources` 에서 읽는 것과 같은 꼴이다.)
    ///
    /// ⚠️ **없으면 코드 생성 텍스처로 떨어진다.** 자산이 아직 안 온 상태에서도 화면이
    /// 서야 하고, 그래야 「자산이 오면 갈아끼운다」가 말이 된다.
    ///
    /// ⚠️ **여백은 그림이 정한다.** 9-슬라이스 원본 64 에 여백 16 이므로(W02 9장)
    /// 코드 생성본의 4 와 다르다 — 그림을 바꾸면 <see cref="border"/> 도 같이 바뀐다.
    /// </summary>
    public sealed class UiSkinAssets : ScriptableObject
    {
        [Tooltip("버튼 기본. 9-슬라이스 원본 64 · 여백 16.")]
        public Texture2D buttonNormal;

        [Tooltip("버튼 눌림. 없으면 기본을 어둡게 쓰지 않고 코드 생성본으로 떨어진다.")]
        public Texture2D buttonPressed;

        [Tooltip("버튼 잠김.")]
        public Texture2D buttonLocked;

        [Tooltip("패널·띠 바탕.")]
        public Texture2D panel;

        [Tooltip("9-슬라이스 여백(px). 원본 64 에 16 (260911_W02 9장).")]
        public int border = 16;

        // ── 노드 상태 표식 다섯 (2026-09-11 배선 · 플랜 §71-33 ③) ────────────────
        //
        // ⚠️ **자산은 09-04 부터 있었는데 코드가 한 곳에서도 안 읽었다**(실측 참조 0건).
        // 효과음 여덟과 같은 꼴이라 같은 방식으로 잇는다 — 경로는 생성기에만 두고
        // 런타임은 이 참조만 본다. 비면 표식을 **안 그린다**(자리표시로 대신하지 않는다:
        // 무엇을 뜻하는지가 그림에만 있어 흰 사각으로는 뜻이 안 선다).
        [Tooltip("정상 가동. ⚠️ 지금은 안 쓴다 — NodeStatusIcon.ShowWhenNormal 이 false(가정).")]
        public Texture2D iconLogiNormal;
        [Tooltip("감속 가동.")]
        public Texture2D iconLogiSlow;
        [Tooltip("정지.")]
        public Texture2D iconLogiStop;
        [Tooltip("끝단 미연결.")]
        public Texture2D iconNotConnected;
        [Tooltip("전력 부족.")]
        public Texture2D iconPowerShort;

        /// <summary>`Resources` 기준 경로 — 이름 하나를 둘이 쓰므로 여기 둔다.</summary>
        public const string ResourcePath = "UiSkinAssets";

        /// <summary>쓸 만한 그림이 하나라도 있는가. 없으면 껍데기는 코드 생성본을 쓴다.</summary>
        public bool HasAny => buttonNormal != null || buttonPressed != null
                              || buttonLocked != null || panel != null;
    }
}
