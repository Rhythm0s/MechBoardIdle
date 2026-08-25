using UnityEngine;

namespace MBI.Core
{
    /// <summary>보드 조작 모드(UI 문서 9-2). 기본은 이동이다.</summary>
    public enum BoardMode
    {
        /// <summary>보기·확인만. 터치 드래그 = 보드 스크롤.</summary>
        Pan = 0,
        /// <summary>배치·설치. 터치 드래그 = 벨트 설치.</summary>
        Build = 1,
    }

    /// <summary>
    /// 보드 스크롤 상태(UI 문서 9-3). 순수 계산 — 씬·입력 비의존이라 EditMode로 검증된다.
    ///
    /// 왜 모드를 나누는가(9-1): 벨트 설치가 터치 드래그인데 보드가 화면보다 커지면서
    /// 화면 이동도 터치 드래그가 됐다. **손가락 하나의 같은 동작에 두 뜻이 배정되면
    /// 기계는 둘을 구분할 수 없다** — 벨트를 그으려던 손짓이 화면을 옮기거나 그 반대가 된다.
    /// 둘 중 하나는 반드시 오작동하므로 모드로 충돌을 원천에서 없앤다.
    ///
    /// 스크롤 여유(9-3, 1440×2560 기준):
    ///   가로 보드 2304 − 가시 1440 = 864 · 세로 보드 2496 − 가시 1352 = 1144
    /// 양방향 스크롤이 필요하고, 보드가 화면 밖으로 나가는 것은 허용된 설계다.
    /// </summary>
    public sealed class BoardPan
    {
        private Vector2 _offset;

        /// <summary>보드 전체 크기(월드 유닛).</summary>
        public Vector2 BoardSize { get; private set; }
        /// <summary>화면에 보이는 범위(월드 유닛).</summary>
        public Vector2 ViewSize { get; private set; }

        public BoardPan(Vector2 boardSize, Vector2 viewSize)
        {
            Resize(boardSize, viewSize);
        }

        public void Resize(Vector2 boardSize, Vector2 viewSize)
        {
            BoardSize = new Vector2(Mathf.Max(0f, boardSize.x), Mathf.Max(0f, boardSize.y));
            ViewSize = new Vector2(Mathf.Max(0f, viewSize.x), Mathf.Max(0f, viewSize.y));
            Offset = _offset; // 새 한계로 다시 조인다
        }

        /// <summary>
        /// 스크롤 가능 범위(월드 유닛). 보드가 화면보다 작으면 0 — 움직일 이유가 없다.
        /// </summary>
        public Vector2 Range => new Vector2(
            Mathf.Max(0f, BoardSize.x - ViewSize.x),
            Mathf.Max(0f, BoardSize.y - ViewSize.y));

        /// <summary>현재 스크롤 오프셋. 대입 시 범위 안으로 조인다 — 보드 밖으로 흘러가지 않게.</summary>
        public Vector2 Offset
        {
            get => _offset;
            set
            {
                Vector2 r = Range * 0.5f;
                _offset = new Vector2(
                    Mathf.Clamp(value.x, -r.x, r.x),
                    Mathf.Clamp(value.y, -r.y, r.y));
            }
        }

        /// <summary>드래그 델타만큼 민다. 손가락을 왼쪽으로 끌면 보드가 왼쪽으로 따라온다.</summary>
        public void Drag(Vector2 delta) => Offset = _offset + delta;

        public void Reset() => _offset = Vector2.zero;

        /// <summary>
        /// 미니맵용 정규화 위치 0~1(UI 문서 2장 「현재 뷰포트 위치 표시」).
        /// 스크롤 여유가 없는 축은 0.5(가운데)로 둔다 — 0으로 두면 볼 것이 없는데 끝에 붙어 보인다.
        /// </summary>
        public Vector2 ViewportCenter01
        {
            get
            {
                Vector2 r = Range;
                return new Vector2(
                    r.x > 0f ? Mathf.Clamp01(0.5f - _offset.x / r.x) : 0.5f,
                    r.y > 0f ? Mathf.Clamp01(0.5f - _offset.y / r.y) : 0.5f);
            }
        }
    }
}
