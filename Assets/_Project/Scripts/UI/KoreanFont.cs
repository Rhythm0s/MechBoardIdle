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
        /// 글자 크기를 **사다리 위의 값 하나로 올린다** (2026-09-15 · 촬영 차단 결함).
        ///
        /// **왜 필요한가.** 유니티의 **동적 폰트는 크기마다 글리프를 따로 굽는다.**
        /// 화면 비례로 크기를 내면 창 크기·배율이 흔들릴 때마다 **새 크기가 생기고**,
        /// 한글은 완성형이라 글자 종류가 많아 **아틀라스가 금세 찬다.** 차면 굽지 못한
        /// 글리프가 **에러 없이 사라진다.**
        ///
        /// ⚠️ **09-14 의 「4칸 스냅」으로는 모자랐다.** 그것은 가짓수를 1/4 로 줄일 뿐
        /// **상한이 없어** 큰 글자가 아틀라스를 통째로 먹었고, 09-15 에 상한 64 를 넣어도
        /// 가짓수가 16 이라 여전히 넘쳤다. 이제 **쓸 수 있는 크기를 아예 네 개로 못 박는다.**
        ///
        /// ⚠️ **아래로 내리지 않는다** — 올림이다. 내리면 읽히는 크기를 밑돌 수 있다.
        /// </summary>
        public static int Snap(int px)
        {
            // ⚠️ **사다리 위의 값 하나로 올린다**(2026-09-15 · 촬영 차단 결함).
            //
            // 종전은 「4칸 올림 + 상한 64」였다. 그러면 크기 가짓수가 **16 가지**가 되는데,
            // 화면에 나올 수 있는 한글이 **539 자**라 아틀라스가 감당하지 못한다 —
            // 실측 계산으로 필요 면적이 2048² 를 크게 넘었다. 넓은 창(가로 1890)에서
            // **하단 글자가 통째로 사라지고** 「다리L」이 「다리I」로 찍힌 것이 그것이다.
            //
            // 사다리는 **네 단계**다. 539 자 × 네 단계로 **2048² 의 69%** 만 쓴다
            // (시험 `KoreanFontSnapTests` 가 그 셈을 지킨다).
            for (int i = 0; i < Ladder.Length; i++)
                if (px <= Ladder[i]) return Ladder[i];
            return Ladder[Ladder.Length - 1];
        }

        /// <summary>
        /// 쓸 수 있는 글자 크기 **전부** (2026-09-15 · 촬영 차단 결함).
        ///
        /// ⚠️ **값 셋은 가정이다**(설계 역기입) — 문서에 글자 크기 사다리 절이 없다.
        /// 고른 근거는 **아틀라스 예산**이다. 화면에 나올 수 있는 한글 539 자에 대해
        /// · **네 단계 [16·24·36·52] → 2.89 M px (2048² = 4.19 M 의 69%)** ← 고른 것
        /// · 세 단계 [24·36·52] → 2.71 M px (65%) — 4%p 아끼고 **작은 단을 잃는다**
        /// · 다섯 단계 [16·24·32·48·64] → 4.86 M px (**116% — 넘는다**)
        ///
        /// ⚠️ **작은 단 16 이 꼭 필요하다.** 세 단계로 두었더니 사다리 최소가 24 라
        /// **작은 창에서 글자가 오히려 커졌다** — 1890×1000 실측에서 HUD 가 화면 왼쪽을
        /// 통째로 먹고 첫 줄이 위로 잘렸다. 4%p 를 더 쓰고 그것을 산다.
        ///
        /// ⚠️ **단계를 늘리려면 예산을 다시 재야 한다.** 늘리는 순간 조용히 넘고,
        /// 넘으면 **에러 없이 글자만 사라진다** — 09-14·09-15 에 네 번 온 병이다.
        ///
        /// 쓰임 — **16** 잔글씨 · **24** 보조 · **36** 본문(버튼·이름표) · **52** 제목(구역 이름).
        /// </summary>
        public static readonly int[] Ladder = { 16, 24, 36, 52 };

        /// <summary>화면에 나올 수 있는 한글 유일 글자 수 (2026-09-15 실측 · 시험이 다시 센다).</summary>
        public const int MeasuredGlyphCount = 539;

        /// <summary>보수적으로 잡은 아틀라스 한 변 — WebGL 에서 기대할 수 있는 크기.</summary>
        public const int AtlasSideAssumed = 2048;

        /// <summary>⚠️ **폐기 — 사다리가 대신한다**(2026-09-15). 구 상한 64. 값은 `Ladder` 끝이다.</summary>
        public static int MaxSize => Ladder[Ladder.Length - 1];

        /// <summary>⚠️ **폐기 — 4칸 스냅은 가짓수 16 을 남겨 모자랐다**(2026-09-15).</summary>
        public const int Step = 4;

        /// <summary>⚠️ **폐기 — 사다리 첫 칸이 최소다**(2026-09-15). 구 8.</summary>
        public static int MinSize => Ladder[0];

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
