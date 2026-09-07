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
    /// 재생 속도는 어느 기획 문서도 정한 적이 없다. 지금 값은 **구현의 가정**이며
    /// `260907_V01`로 판정을 올려 두었다 — 설계가 값을 주면 그것으로 갈아 끼운다.
    /// </summary>
    [Serializable]
    public struct UnitAnimClip
    {
        [Tooltip("이 벌이 어느 상태인가.")]
        public UnitAnimState state;

        [Tooltip("이 벌이 어느 방향인가. 사망·태그 전환은 남면 한 벌뿐이다.")]
        public UnitAnimDirection direction;

        [Tooltip("프레임. 파일 이름 순서(frame_000, frame_001 …)가 재생 순서다.")]
        public Sprite[] frames;

        [Tooltip("초당 프레임. 기획 미확정 — 구현 가정값이다(260907_V01 판정 요청).")]
        public float fps;

        public bool IsValid => frames != null && frames.Length > 0 && fps > 0f;
    }
}
