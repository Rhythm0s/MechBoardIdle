using MBI.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 유닛 아트 배선(§8 교체 지점). 아트가 SO를 거쳐 들어오는지, 그리고 **캔버스 규격과
    /// PPU가 실제 자산에 걸려 있는지**를 본다.
    ///
    /// PPU 불일치는 에러를 내지 않고 크기만 어긋나므로 눈으로 잡기 어렵다 — 그래서 테스트가 본다.
    /// </summary>
    public sealed class UnitArtWiringTests
    {
        private const string ArtDir = "Assets/_Project/Art/Units";
        private const string RobotA = "Assets/_Project/ScriptableObjects/Robots/Robot_A.asset";
        private const string RobotB = "Assets/_Project/ScriptableObjects/Robots/Robot_B.asset";

        private static Sprite Art(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        private static TextureImporter Importer(string name) =>
            AssetImporter.GetAtPath($"{ArtDir}/{name}.png") as TextureImporter;

        // ---- 자산 존재와 캔버스 규격 ----

        [Test]
        public void UnitArt_ExistsWithConfirmedCanvasSizes()
        {
            var expected = new (string name, int canvas)[]
            {
                ("robot_a", ArtSpec.RobotCanvas),
                ("robot_b", ArtSpec.RobotCanvas),
                ("drone_n", ArtSpec.DroneCanvas),
                ("drone_w", ArtSpec.DroneCanvas),
            };

            foreach ((string name, int canvas) in expected)
            {
                Sprite s = Art(name);
                Assert.NotNull(s, $"{name}.png 자산이 있어야 한다");
                Assert.AreEqual(canvas, (int)s.rect.width, $"{name} 가로 캔버스");
                Assert.AreEqual(canvas, (int)s.rect.height, $"{name} 세로 캔버스");
            }
        }

        /// <summary>
        /// 임포트 규격은 AssetPostprocessor가 강제한다 — 손으로 지정한 값이 아니라는 확인.
        /// PPU가 어긋나면 스프라이트가 규격과 다른 칸 수를 차지한다.
        /// </summary>
        [Test]
        public void UnitArt_ImportRulesAreEnforced()
        {
            foreach (string name in new[] { "robot_a", "robot_b", "drone_n", "drone_w" })
            {
                TextureImporter imp = Importer(name);
                Assert.NotNull(imp, $"{name} 임포터");

                Assert.AreEqual(ArtSpec.PixelsPerUnit, imp.spritePixelsPerUnit, 0.001f, $"{name} PPU");
                Assert.AreEqual(FilterMode.Point, imp.filterMode, $"{name} 필터 — 도트에 보간이 들어가면 뭉갠다");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, $"{name} 압축");
                Assert.AreEqual(TextureImporterType.Sprite, imp.textureType, $"{name} 타입");
                Assert.IsFalse(imp.mipmapEnabled, $"{name} 밉맵 — 도트는 축소 단계가 필요 없다");
            }
        }

        /// <summary>스프라이트가 차지하는 월드 크기가 ArtSpec 계산과 같아야 한다.</summary>
        [Test]
        public void WorldSize_MatchesArtSpec()
        {
            Assert.AreEqual(ArtSpec.RobotSize, Art("robot_a").rect.width / ArtSpec.PixelsPerUnit, 0.001f);
            Assert.AreEqual(ArtSpec.DroneSize, Art("drone_n").rect.width / ArtSpec.PixelsPerUnit, 0.001f);
        }

        // ---- SO 참조 ----

        [Test]
        public void RobotDefinitions_ReferenceTheirArt()
        {
            var a = AssetDatabase.LoadAssetAtPath<RobotDefinition>(RobotA);
            var b = AssetDatabase.LoadAssetAtPath<RobotDefinition>(RobotB);

            Assert.NotNull(a, "Robot_A 자산");
            Assert.NotNull(b, "Robot_B 자산");

            Assert.AreSame(Art("robot_a"), a.sprite, "로봇 A 본체");
            Assert.AreSame(Art("robot_b"), b.sprite, "로봇 B 본체");
            Assert.AreSame(Art("drone_n"), b.droneSprite, "로봇 B 드론 = 누적형 기본 프리셋(params pB 1.0 × dB 100)");
        }

        /// <summary>
        /// 아트를 SO 참조로 넣는 이유: 경로 문자열이 런타임 코드에 박히지 않고,
        /// 자산이 없어도 뷰가 플레이스홀더로 폴백할 수 있다(§8).
        /// </summary>
        [Test]
        public void SpriteField_IsOptional_SoPlaceholderFallbackStaysPossible()
        {
            var probe = ScriptableObject.CreateInstance<RobotDefinition>();
            Assert.IsNull(probe.sprite, "아트 미투입 상태가 유효해야 한다");
            Object.DestroyImmediate(probe);
        }
    }
}
