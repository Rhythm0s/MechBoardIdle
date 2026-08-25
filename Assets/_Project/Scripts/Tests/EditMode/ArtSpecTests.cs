using MBI.Data;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 아트 임포트 규격(260824_V02 §4 승인분). 여기가 깨지면 스프라이트 교체 시
    /// 모든 오브젝트 크기가 조용히 어긋난다 — PPU 불일치는 에러를 내지 않고 크기만 틀어지므로
    /// 눈으로 잡기 어렵다.
    /// </summary>
    public sealed class ArtSpecTests
    {
        private const float D = 0.001f;

        /// <summary>
        /// PPU 192의 근거: 격자 한 칸이 192 화면 픽셀이다(2304 ÷ 12 = 2496 ÷ 13 = 192).
        /// 그래서 타일 1장 = 1 월드 유닛 = 한 칸이 되어 격자 좌표와 월드 좌표가 1:1로 붙는다.
        /// </summary>
        [Test]
        public void Ppu_MatchesBoardPixelPitch()
        {
            Assert.AreEqual(192f, ArtSpec.PixelsPerUnit, D);
            Assert.AreEqual(ArtSpec.PixelsPerUnit, 2304f / 12f, D, "가로 12칸");
            Assert.AreEqual(ArtSpec.PixelsPerUnit, 2496f / 13f, D, "세로 13칸");
        }

        [Test]
        public void TileSprite_IsExactlyOneCell()
        {
            Assert.AreEqual(1f, ArtSpec.TileSize, D, "노드·벨트 타일 192px = 1칸");
        }

        [Test]
        public void CanvasSizes_ConvertToExpectedWorldUnits()
        {
            Assert.AreEqual(1.333f, ArtSpec.RobotSize, 0.001f, "로봇 256px");
            Assert.AreEqual(2.667f, ArtSpec.LargeSize, 0.001f, "합체·보스 512px");
            Assert.AreEqual(0.667f, ArtSpec.MonsterSize, 0.001f, "몬스터 128px");
            Assert.AreEqual(0.333f, ArtSpec.DroneSize, 0.001f, "드론 64px");
        }

        /// <summary>월드 크기는 캔버스 ÷ PPU다 — 어디에도 따로 적힌 배율이 없어야 한다.</summary>
        [Test]
        public void WorldSize_IsCanvasOverPpu()
        {
            Assert.AreEqual(ArtSpec.RobotCanvas / ArtSpec.PixelsPerUnit, ArtSpec.RobotSize, D);
            Assert.AreEqual(ArtSpec.LargeCanvas / ArtSpec.PixelsPerUnit, ArtSpec.LargeSize, D);
            Assert.AreEqual(ArtSpec.MonsterCanvas / ArtSpec.PixelsPerUnit, ArtSpec.MonsterSize, D);
            Assert.AreEqual(ArtSpec.DroneCanvas / ArtSpec.PixelsPerUnit, ArtSpec.DroneSize, D);
        }

        /// <summary>보드용 폴더 판정 — 이 경로들만 192 배수 검사를 받는다(V02 §4 단서).</summary>
        [Test]
        public void BoardArtPaths_AreDetected()
        {
            Assert.IsTrue(ArtSpec.IsBoardArtPath("Assets/_Project/Art/Nodes/core.png"));
            Assert.IsTrue(ArtSpec.IsBoardArtPath("Assets/_Project/Art/Belts/straight.png"));
            Assert.IsFalse(ArtSpec.IsBoardArtPath("Assets/_Project/Art/Robots/robotA.png"));
            Assert.IsFalse(ArtSpec.IsBoardArtPath(null));
        }

        /// <summary>
        /// 카메라 규칙: orthographicSize = (뷰포트 세로 픽셀 ÷ 2) ÷ PPU.
        /// 보드 뷰포트 1352px → 3.52 · 화면 전체 2560px → 6.67.
        /// </summary>
        [Test]
        public void OrthographicSize_FollowsViewportOverPpu()
        {
            Assert.AreEqual(3.52f, (1352f / 2f) / ArtSpec.PixelsPerUnit, 0.01f);
            Assert.AreEqual(6.67f, (2560f / 2f) / ArtSpec.PixelsPerUnit, 0.01f);
        }
    }
}
