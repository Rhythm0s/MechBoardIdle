using MBI.Data;
using UnityEngine;

namespace MBI.Logistics
{
    /// <summary>
    /// 벨트 흐름 무늬 — **방향 표시를 흐르게 한다**(영상 D구간 보드 클로즈업).
    ///
    /// 벨트 위 개별 아이템의 이동은 MVP 범위 밖이다(MVP 문서 ❌ 목록). 여기서 하는 것은
    /// 이미 있는 방향 화살표를 입력 면 → 출력 면으로 반복해 밀어 주는 것뿐이다 —
    /// **새 오브젝트도 새 아트도 만들지 않는다.**
    ///
    /// 왜 필요한가: 정지한 화살표는 「이 벨트가 이 방향이다」만 말하고
    /// 「지금 흐르고 있다」는 말하지 않는다. 끊긴 벨트와 도는 벨트가 화면에서 같아 보이면
    /// 재설계 장면이 그냥 블록을 옮기는 그림이 된다(주간 일정표 2장 B컷 급소와 같은 이유).
    ///
    /// ⚠️ **판정은 하지 않는다.** 흐르는지 여부는 보드가 <see cref="Flowing"/>으로 알려 준다
    /// (지침 §3 UI는 매핑만). 속도는 표시 전용이라 밸런스 값이 아니다.
    /// </summary>
    public sealed class BeltFlowAnimator : MonoBehaviour
    {
        /// <summary>움직일 화살표. 벨트 마커의 자식이다.</summary>
        public SpriteRenderer arrow;

        /// <summary>흐르고 있는가. 보드가 매 갱신마다 넣는다.</summary>
        public bool Flowing { get; set; }

        /// <summary>한 칸을 지나는 데 걸리는 시간(초). 표시 전용.</summary>
        private const float TravelSeconds = 0.9f;

        /// <summary>셀 중심에서 면까지의 거리(마커 로컬 좌표). 마커 생성과 같은 값이다.</summary>
        private const float FaceReach = 0.32f;

        private Vector2 _from;
        private Vector2 _to;
        private float _t;

        /// <summary>흐르는 방향을 잡는다. 입력 면에서 출력 면으로 간다.</summary>
        public void SetPath(Vector2 fromFace, Vector2 toFace)
        {
            // 같은 방향이면 위상을 건드리지 않는다 — 보드를 한 칸 고칠 때마다 갱신이 도는데,
            // 그때마다 처음으로 되돌리면 화면의 벨트가 전부 같이 튄다.
            if (_from == fromFace && _to == toFace) return;

            _from = fromFace;
            _to = toFace;
            _t = 0f;
        }

        private void Update()
        {
            if (arrow == null) return;

            if (!Flowing)
            {
                // 멈춘 벨트는 **출력 면에 붙여 둔다** — 종전 모습 그대로다.
                // 끊긴 벨트가 화면에서 얌전한 것이 「여기는 안 돈다」는 표시가 된다.
                Park();
                return;
            }

            _t += Time.deltaTime / TravelSeconds;
            while (_t >= 1f) _t -= 1f;

            Vector2 p = Vector2.Lerp(_from, _to, _t) * FaceReach;
            arrow.transform.localPosition = new Vector3(p.x, p.y, 0f);

            // 양 끝에서 흐려 사라졌다 나타난다 — 그래야 튀어 돌아오는 것으로 안 보인다.
            SetAlpha(Mathf.Sin(_t * Mathf.PI));
        }

        private void Park()
        {
            Vector2 p = _to * FaceReach;
            arrow.transform.localPosition = new Vector3(p.x, p.y, 0f);
            SetAlpha(1f);
        }

        /// <summary>
        /// 색은 건드리지 않고 투명도만 쓴다. 화살표의 색은 **보드가 정한다**(연결 여부) —
        /// 여기서 색까지 만지면 경고 표시가 무늬에 먹힌다.
        /// </summary>
        private void SetAlpha(float a)
        {
            Color c = arrow.color;
            arrow.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        /// <summary>면 → 로컬 방향. 보드의 FaceOffset과 같은 규칙이다.</summary>
        public static Vector2 Offset(PortFace face)
        {
            switch (face)
            {
                case PortFace.East: return new Vector2(1f, 0f);
                case PortFace.West: return new Vector2(-1f, 0f);
                case PortFace.North: return new Vector2(0f, 1f);
                default: return new Vector2(0f, -1f); // South
            }
        }
    }
}
