using System;
using UnityEngine;

namespace MBI.Data
{
    /// <summary>유닛 애니메이션의 상태. 폴더 이름의 <c>{State}</c> 자리와 같은 철자를 쓴다.</summary>
    public enum UnitAnimState
    {
        Idle,
        Move,
        Death,
        TagIn,
    }

    /// <summary>
    /// 유닛 애니메이션의 방향. 폴더 이름의 <c>{dir}</c> 자리이며 소문자로 쓴다.
    ///
    /// 합체 로봇은 좌우 대칭이라 서면을 생성하지 않는다(15-3 3-3·5-1). 서면 요청이 오면
    /// 동면을 <c>flipX</c>로 뒤집어 쓴다 — 그 판단은 <see cref="MBI.Combat.SpriteFrameAnimator"/>가 한다.
    /// </summary>
    public enum UnitAnimDirection
    {
        South,
        North,
        East,
        West,
    }

    /// <summary>
    /// 한 벌의 프레임 묶음. 캐릭터 아트 요청 문서(15)「애니메이션 공통 규격」이 벌 수와 프레임 수를 정한다.
    ///
    /// 경로 문자열은 여기에 두지 않는다(§8 명명 규칙) — 프레임은 씬 생성기가 주입한다.
    ///
    /// **초당 프레임은 2026-09-07에 폐기됐다**(`260907_W01` 2-2). 공통 속도를 두면
    /// 길이가 그림 수에 묶여 **그림 수가 부드러움과 길이를 겸한다** — 무거운 로봇이
    /// 가벼운 몬스터보다 빠르게 들썩였다. 지금은 기준 칸 시간 1/16초를 고정하고
    /// **벌마다 목표 초를 사람이 정한다.** 칸 배분은 <see cref="MBI.Core.Anim.AnimSchedule"/>가
    /// 계산한다 — 손으로 적지 않는다(W01 4-6).
    /// </summary>
    [Serializable]
    public struct UnitAnimClip
    {
        [Tooltip("이 벌이 어느 상태인가.")]
        public UnitAnimState state;

        [Tooltip("이 벌이 어느 방향인가. 사망·태그 전환은 남면 한 벌뿐이다.")]
        public UnitAnimDirection direction;

        [Tooltip("그림. 파일 이름 순서(frame_000, frame_001 …)가 재생 순서다. 9장 상한은 이 장수다.")]
        public Sprite[] frames;

        [Tooltip("목표 초. 칸 수 = 이 값 × 16이며 반올림이 생기면 실제 초를 문서에 되돌려 적는다.")]
        public float targetSeconds;

        [Tooltip("왕복(핑퐁)인가. 대기만 그렇다 — 1→N→2로 펴며 끝을 한 번만 쓴다.")]
        public bool pingPong;

        [Tooltip("머무름 칸 번호(1부터). 발이 닿는 칸을 한 칸 더 머무르게 한다. 왕복 벌에서는 쓰지 않는다.")]
        public int[] dwellCells;

        /// <summary>
        /// 칸 목록을 그림 순서 대신 직접 지정한다(0부터 세는 그림 번호). 비면 지금까지대로 동작한다.
        ///
        /// <b>왜 필요한가</b> — 걷기의 한 바퀴는 「왼발 앞 → 모임 → 오른발 앞 → 모임 → 왼발 앞」이라
        /// <b>모이는 자세를 두 번 지난다</b>(`260907_W03` 2-3). 그림은 다섯 장인데 칸은 여섯이다.
        /// 그 한 칸을 채우려고 <b>가운데 그림의 사본 파일을 폴더에 한 장 더 두고 있었다</b> —
        /// 「9프레임 상한은 그림 장수다. 재생 칸수가 아니다」(15 7-1)에 어긋난다.
        ///
        /// <b>왕복과 같이 쓰지 않는다.</b> 왕복은 목록을 자동으로 펴는 것이고 이것은 손으로 적는 것이라,
        /// 둘을 겹치면 어느 쪽이 이겼는지가 화면에서만 드러난다.
        /// </summary>
        [Tooltip("칸 목록(0부터 세는 그림 번호). 비면 그림 순서대로. 왕복과 같이 쓰지 않는다.")]
        public int[] cellOrder;

        public bool IsValid => frames != null && frames.Length > 0 && targetSeconds > 0f;
    }
}
