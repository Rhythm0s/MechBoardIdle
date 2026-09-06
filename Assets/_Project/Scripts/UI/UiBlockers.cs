using System.Collections.Generic;
using UnityEngine;

namespace MBI.UI
{
    /// <summary>
    /// 이번 프레임 IMGUI가 그린 **버튼·패널 자리**를 모으는 공용 등록소.
    /// 보드는 누르는 순간 여기에 걸리면 입력을 무시한다(오배치 방지).
    ///
    /// ⚠️ **왜 값이 아니라 자리인가.** 종전에는 각 컴포넌트가 OnGUI에서 `bool`을 세워 두고
    /// 입력 콜백이 그 값을 읽었다. 그런데 입력 콜백은 같은 프레임의 OnGUI **앞**에서 돌아
    /// 읽는 값이 항상 한 프레임 전 것이다. 마우스는 커서가 이미 얹혀 있었으니 맞았지만
    /// **터치에는 얹혀 있는 시간이 없다** — 손가락이 닿는 첫 프레임에는 값이 false라서
    /// 버튼을 눌러도 그 아래 보드가 같이 눌렸다. 배포가 WebGL이고 조작은 터치 기준이다.
    ///
    /// ⚠️ **왜 컴포넌트마다 두지 않고 한곳에 모으는가.** 자기 자리는 자기만 알기 때문이다.
    /// 보드가 자기 버튼만 알고 있으면 물류 변수 패널·방치 HUD처럼 다른 어셈블리가 그린 패널은
    /// 그냥 통과해 그 아래 칸에 노드가 놓인다. 실제로 변수 패널이 그랬다.
    /// 그리는 쪽이 <see cref="Add"/>로 자기 자리를 내놓으면 새 패널이 생겨도 자동으로 막힌다.
    /// </summary>
    public static class UiBlockers
    {
        private static readonly List<Rect> Rects = new List<Rect>();
        private static int _frame = -1;

        /// <summary>
        /// 이 자리는 UI가 쓴다. OnGUI에서 그리기 직전에 부른다.
        ///
        /// 프레임이 바뀌면 비운다. OnGUI는 한 프레임에 여러 번(Layout·Repaint…) 돌아
        /// 같은 자리가 여러 번 들어오지만, 판정이 「어느 하나에 걸리는가」라 결과가 같다.
        /// </summary>
        public static void Add(Rect rect)
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                Rects.Clear();
            }
            Rects.Add(rect);
        }

        /// <summary>
        /// 이 지점(IMGUI 좌표 — 위에서 아래로 잰다)이 UI에 가려져 있는가.
        ///
        /// 들고 있는 자리는 **직전 OnGUI**의 것이다. 입력 콜백이 OnGUI보다 앞서 도는 이상
        /// 이번 프레임 자리를 미리 알 수는 없다. 그래도 되는 이유는 **자리는 포인터와 무관하게
        /// 정해지기 때문**이다 — 화면 크기와 팔레트 길이로만 정해지므로 프레임이 바뀌어도
        /// 그대로다. 한 프레임 늦어서 틀렸던 것은 자리가 아니라 「포인터가 그 위에 있었는가」
        /// 쪽이고, 그 판정을 여기서 지금 포인터로 다시 한다.
        /// </summary>
        public static bool Contains(Vector2 guiPoint)
        {
            for (int i = 0; i < Rects.Count; i++)
                if (Rects[i].Contains(guiPoint)) return true;
            return false;
        }

        /// <summary>화면 좌표(아래에서 위로 잰다)를 IMGUI 좌표로 뒤집어 판정한다.</summary>
        public static bool ContainsScreenPoint(Vector2 screenPoint) =>
            Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));
    }
}
