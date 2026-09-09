namespace MBI.Core.Audio
{
    /// <summary>
    /// 효과음 이름 여덟 (사운드 문서 10장 「영상 구간별 효과음」 · 2026-09-09 배선).
    ///
    /// **자산 파일명을 영문 표기 그대로 쓴다** — 코드가 그 이름으로 파일을 찾으므로
    /// 번역하면 못 찾는다(지침 §8 채택 셋째).
    ///
    /// **여덟만 있는 근거.** 사운드 문서 10장이 영상에 나오는 것으로 일곱 + 조건부 하나를
    /// 골랐고, 자산 레지스트리와 소리 자산 대장이 같은 여덟을 추적한다.
    /// ⚠️ `sfx_tagskill`은 **영상 범위 밖**이고(10장), `sfx_ammoout`을 비롯한 선택 등급 다섯도
    /// 같은 이유로 목록에 없다. **여기 없는 이름을 지어 넣지 않는다.**
    ///
    /// ⚠️ **파일은 아직 하나도 없다**(소리 자산 대장 2장 — 사용자가 무료 자산으로 조달 중).
    /// 그래도 훅은 지금 건다 — 자산이 오면 **파일만 넣으면 소리가 난다.**
    /// </summary>
    public static class SoundIds
    {
        // ── 조립 화면 (사운드 문서 3장 · 필수) ─────────────────────────────────────
        /// <summary>노드가 격자에 붙는 순간. 조작음이라 가장 작다.</summary>
        public const string NodeSnap = "sfx_nodesnap";
        /// <summary>벨트가 올바른 면에 닿아 연결이 성립한 순간. 스냅보다 크다.</summary>
        public const string BeltConnect = "sfx_beltconnect";
        /// <summary>병목이 생긴 순간. **경고**라 가장 크고, 상태가 바뀔 때 한 번만 난다.</summary>
        public const string Bottleneck = "sfx_bottleneck";

        // ── 전투 화면 (사운드 문서 4장 · 필수) ─────────────────────────────────────
        /// <summary>로봇 A 발사. 초당 여러 번이라 존재감을 낮게 잡는다.</summary>
        public const string FireA = "sfx_fire_a";
        /// <summary>드론 사출. 총소리와 다른 계열.</summary>
        public const string DroneLaunch = "sfx_dronelaunch";
        /// <summary>플레이어 로봇 피격.</summary>
        public const string Hit = "sfx_hit";
        /// <summary>합체 전환 연출. 유일하게 0.5초를 넘는다(최대 2.2초).</summary>
        public const string Fusion = "sfx_fusion";
        /// <summary>버스트 타격. 태그 스킬과 **음색으로** 갈린다.</summary>
        public const string Burst = "sfx_burst";

        /// <summary>훅이 걸린 여덟. 재생기가 자산을 찾는 목록이기도 하다.</summary>
        public static readonly string[] All =
        {
            NodeSnap, BeltConnect, Bottleneck,
            FireA, DroneLaunch, Hit, Fusion, Burst,
        };

        /// <summary>
        /// 이 소리의 볼륨 갈래. **경고 &gt; 효과음 &gt; 조작음**(사운드 문서 2장).
        ///
        /// 조립 화면의 딸깍 둘은 **플레이어가 낸 소리**라 조작음이고, 병목은 화면을
        /// 안 봐도 닿아야 하는 것이라 경고다 — 같은 화면에서 나지만 갈래가 다르다.
        /// </summary>
        public static SoundKind KindOf(string id)
        {
            switch (id)
            {
                case Bottleneck: return SoundKind.Warning;
                case NodeSnap:
                case BeltConnect: return SoundKind.Ui;
                default: return SoundKind.Effect;
            }
        }
    }
}
