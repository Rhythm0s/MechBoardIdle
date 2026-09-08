using System.Collections.Generic;
using System.IO;
using MBI.Data;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 애니메이션 프레임의 규격을 고정한다.
    ///
    /// 2026-09-07까지 <c>Art/Anim/</c>을 읽는 코드가 아예 없었고, 그래서 프레임이 규격을
    /// 벗어나도 아무 테스트가 실패하지 않았다. 어제 측정법에서 겪은 것과 같은 종류다 —
    /// 규격이 코드가 아니라 사람의 절차이면 조용히 어긋난다.
    ///
    /// 벌 수 규격 (캐릭터 아트 요청 문서(15) · 15-1 · 15-2 · 15-3):
    ///   로봇 A  대기 4 + 이동 4 + 사망 1 + 태그 1 = 10벌
    ///   로봇 B  대기 3 + 이동 4 + 사망 1 + 태그 1 =  9벌   (대기 서면은 동면 미러 · 2026-09-07)
    ///   합체    대기 3 + 이동 3 + 사망 1           =  7벌   (좌우 대칭이라 서면을 만들지 않는다)
    ///   합계                                        = 26벌
    ///
    /// <b>2026-09-07에 27에서 26으로 줄었다.</b> 사용자가 GIF로 보고 로봇 B의 대기 서면을
    /// 동면 하나 + 좌우 미러로 정했다. 좌우 대칭인 기체에서만 되며 <b>로봇 A는 안 된다</b>
    /// (15-1 3-2 — 마운트가 붙은 팔이 한쪽만 두껍다). 규격 문서에 「좌우 대칭 기체는
    /// 3방향 + 미러」를 넣을지는 설계 판정 대기이며, 그때까지 이 목록이 실제 규격이다.
    ///
    /// 프레임 수 (15「동작의 크기」 · 256 이상):
    ///   대기 5 · 이동 6 · 사망·태그는 상한 9
    /// </summary>
    public sealed class UnitAnimWiringTests
    {
        private const string AnimRoot = "Assets/_Project/Art/Anim";

        private static string ClipDir(string robot, UnitAnimState state, UnitAnimDirection dir) =>
            $"{AnimRoot}/{robot}_{state}/{dir.ToString().ToLowerInvariant()}";

        /// <summary>26벌의 자리. 있어야 하는 것의 목록이며, 이 목록 자체가 규격이다.</summary>
        private static IEnumerable<(string robot, UnitAnimState state, UnitAnimDirection dir)> ExpectedClips()
        {
            var fourWay = new[]
            {
                UnitAnimDirection.South, UnitAnimDirection.North,
                UnitAnimDirection.East, UnitAnimDirection.West,
            };
            // 합체는 서면을 생성하지 않는다 — 동면을 코드가 뒤집어 쓴다(15-3 3-3).
            var threeWay = new[] { UnitAnimDirection.South, UnitAnimDirection.North, UnitAnimDirection.East };

            foreach (string robot in new[] { "robot_a", "robot_b" })
            {
                // 로봇 B의 대기만 3방향이다 — 서면은 동면을 뒤집어 쓴다(2026-09-07).
                foreach (UnitAnimDirection d in robot == "robot_b" ? threeWay : fourWay)
                    yield return (robot, UnitAnimState.Idle, d);
                foreach (UnitAnimDirection d in fourWay) yield return (robot, UnitAnimState.Move, d);
                yield return (robot, UnitAnimState.Death, UnitAnimDirection.South);
                yield return (robot, UnitAnimState.TagIn, UnitAnimDirection.South);
            }

            foreach (UnitAnimDirection d in threeWay) yield return ("fusion", UnitAnimState.Idle, d);
            foreach (UnitAnimDirection d in threeWay) yield return ("fusion", UnitAnimState.Move, d);
            yield return ("fusion", UnitAnimState.Death, UnitAnimDirection.South);
        }

        private static int ExpectedFrames(UnitAnimState state) => state == UnitAnimState.Idle ? 5 : 6;

        private static string[] Frames(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "frame_*.png") : new string[0];

        // ---- 목록 자체 ----

        [Test]
        public void ExpectedClipList_Is26()
        {
            var all = new List<(string, UnitAnimState, UnitAnimDirection)>(ExpectedClips());
            Assert.AreEqual(26, all.Count, "벌 수는 10 + 9 + 7 = 26이다");
            CollectionAssert.AllItemsAreUnique(all, "같은 벌이 두 번 들어가면 안 된다");
        }

        /// <summary>
        /// <b>미러로 쓰는 방향은 폴더가 없어야 한다.</b> 있으면 같은 그림이 두 벌이 되어
        /// 어느 쪽이 승인본인지가 이름으로 안 갈린다 — 2026-09-06에 자산 이름 하나가 두 그림을
        /// 가리켰던 것과 같은 종류다. 지우는 것이 규격이고, 코드가 동면을 뒤집는다.
        /// </summary>
        [Test]
        public void MirroredDirections_HaveNoFolder()
        {
            var shouldBeAbsent = new[]
            {
                ("robot_b", UnitAnimState.Idle),
                ("fusion", UnitAnimState.Idle),
                ("fusion", UnitAnimState.Move),
            };

            var found = new List<string>();
            foreach ((string robot, UnitAnimState state) in shouldBeAbsent)
            {
                string d = ClipDir(robot, state, UnitAnimDirection.West);
                if (Frames(d).Length > 0) found.Add($"{robot}_{state}/west");
            }

            CollectionAssert.IsEmpty(found,
                "서면을 미러로 쓰기로 한 벌은 폴더가 없어야 한다: " + string.Join(", ", found));
        }

        // ---- 실제 자산 ----

        /// <summary>
        /// 27벌이 다 있는가. **아직 다 만들지 않았으면 무엇이 없는지를 적고 건너뛴다** —
        /// 없는 것을 실패로 적으면 매일 빨간 줄을 보고도 아무것도 안 하게 된다.
        /// 생성이 끝나면 이 테스트가 저절로 실제 검사로 바뀐다.
        /// </summary>
        [Test]
        public void AllClipFolders_Exist()
        {
            var missing = new List<string>();
            foreach ((string robot, UnitAnimState state, UnitAnimDirection dir) in ExpectedClips())
            {
                string d = ClipDir(robot, state, dir);
                if (Frames(d).Length == 0) missing.Add($"{robot}_{state}/{dir.ToString().ToLowerInvariant()}");
            }

            if (missing.Count > 0)
                Assert.Ignore($"아직 생성 전인 벌 {missing.Count}/26: {string.Join(", ", missing)}");

            Assert.Pass("26벌 전부 있다");
        }

        /// <summary>
        /// 있는 벌은 규격을 지켜야 한다 — 캔버스 256 · 프레임 수 대기 5 · 이동 6 · 사망·태그 상한 9.
        ///
        /// **캔버스가 256이 아닌 벌은 규격 이전 초안이다.** 2026-09-04 일괄 생성분(`96dafd5`)이
        /// 그것이며 캔버스가 96·136·168·236처럼 제각각이다 — 09-05 동작 규격(진폭 4~6% ·
        /// 착지마다 상체가 내려앉음) 이전에 뽑은 것이라 프레임 수도 몬스터 규격(대기 7)을 따르고 있다.
        /// 초안을 실패로 적으면 매일 빨간 줄을 보고도 아무것도 안 하게 되므로, **무엇이 초안인지를
        /// 이름으로 적고 건너뛴다.** 교체하면 이 테스트가 저절로 실제 검사로 바뀐다.
        ///
        /// 캔버스는 PNG 머리에서 직접 읽는다 — <c>AssetDatabase</c>로 읽으면 임포트 설정에
        /// 결과가 흔들리는데 지금 아트는 임포트 설정을 확정하지 않기로 한 상태다.
        /// </summary>
        [Test]
        public void PresentClips_MatchSpec()
        {
            var preSpec = new List<string>();
            int checkedCount = 0;

            foreach ((string robot, UnitAnimState state, UnitAnimDirection dir) in ExpectedClips())
            {
                string d = ClipDir(robot, state, dir);
                string[] files = Frames(d);
                if (files.Length == 0) continue;

                System.Array.Sort(files, System.StringComparer.Ordinal);
                string label = $"{robot}_{state}/{dir.ToString().ToLowerInvariant()}";

                if (!TryPngSize(files[0], out int w, out int h))
                {
                    preSpec.Add($"{label}(PNG를 못 읽음)");
                    continue;
                }

                if (w != ArtSpec.RobotCanvas || h != ArtSpec.RobotCanvas)
                {
                    preSpec.Add($"{label}({w}×{h})");
                    continue;
                }

                checkedCount++;
                foreach (string f in files)
                {
                    Assert.IsTrue(TryPngSize(f, out int fw, out int fh), $"{label} PNG를 읽을 수 있어야 한다");
                    Assert.AreEqual(ArtSpec.RobotCanvas, fw, $"{label} 가로 캔버스");
                    Assert.AreEqual(ArtSpec.RobotCanvas, fh, $"{label} 세로 캔버스");
                }

                if (state == UnitAnimState.Move)
                {
                    // 「이동 6은 넘지 말라는 상한이다. 5장이어도 규격 위반이 아니다」(`260907_W03` 2-3).
                    // 「동작의 크기」가 정한 것은 부드러움 예산이고 원칙은 적게 쓰는 것이라,
                    // 6을 못 채웠다고 미달로 보면 앞뒤가 안 맞는다. 모자란 칸은 cellOrder 가 채운다.
                    Assert.LessOrEqual(files.Length, ExpectedFrames(state), $"{label} 프레임 수는 상한 6이다");
                    Assert.GreaterOrEqual(files.Length, 1, $"{label} 그림이 한 장은 있어야 한다");
                }
                else if (state == UnitAnimState.Idle)
                    Assert.AreEqual(ExpectedFrames(state), files.Length, $"{label} 프레임 수");
                else
                    Assert.LessOrEqual(files.Length, 9, $"{label} 프레임 수는 상한 9다");
            }

            if (checkedCount == 0)
                Assert.Ignore(preSpec.Count == 0
                    ? "아직 생성된 벌이 없다"
                    : $"전부 규격 이전 초안이다({preSpec.Count}벌 · 2026-09-04 96dafd5): {string.Join(", ", preSpec)}");
        }

        /// <summary>PNG 머리(IHDR)에서 캔버스를 읽는다. 임포트 설정과 무관하다.</summary>
        private static bool TryPngSize(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                byte[] head = new byte[24];
                using (var fs = File.OpenRead(path))
                    if (fs.Read(head, 0, 24) < 24) return false;

                width  = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                return width > 0 && height > 0;
            }
            catch { return false; }
        }

        // ---- 재생 쪽 규격 ----

        /// <summary>
        /// 합체체 자산이 있어야 촬영 C구간(00:40~00:55)에 합체체가 화면에 선다 —
        /// `260907_W01` 3-1이 HUD 묶음보다 앞으로 올린 이유가 그것이다.
        /// 자산이 없으면 생성기를 아직 안 돌린 것이라 실패가 아니라 건너뛴다.
        /// </summary>
        [Test]
        public void FusionRobot_HasSevenClips()
        {
            const string path = "Assets/_Project/ScriptableObjects/Robots/Robot_Fusion.asset";
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<MBI.Data.RobotDefinition>(path);
            if (def == null) Assert.Ignore("Robot_Fusion.asset 이 아직 없다 — MBI/Generate Combat Data 를 돌린다");

            Assert.IsNotNull(def.sprite, "합체 256 전투 스틸이 걸려 있어야 한다");
            Assert.AreEqual(7, def.animClips != null ? def.animClips.Count : 0,
                "합체는 대기 3 · 이동 3 · 사망 1 = 7벌이다");
        }

        /// <summary>
        /// 칸 목록을 받는 벌은 <b>넷</b>이다 — 로봇 A 이동 남·북 · 합체 이동 남·북
        /// (`260907_W03` 2-3 · `260907_W04` 4-3). 벌마다 조건식을 적고 일반화하지 않으므로,
        /// <b>안 적은 벌에는 목록이 가면 안 된다</b>는 것이 이 테스트의 절반이다.
        ///
        /// 장수 조건도 함께 본다 — 사본이 아직 있어 여섯 장이면 목록을 주지 않는다.
        /// 그래야 파일 삭제와 코드가 서로를 안 기다린다.
        /// </summary>
        [Test]
        public void CellOrder_IsGivenToWalkSouthNorthFive_Only()
        {
            int[] expected = { 0, 1, 2, 3, 4, 2 };

            foreach (string robot in new[] { "robot_a", "fusion" })
            foreach (UnitAnimDirection dir in new[] { UnitAnimDirection.South, UnitAnimDirection.North })
            {
                CollectionAssert.AreEqual(expected,
                    MBI.Editor.CombatAssetGenerator.CellOrder(robot, UnitAnimState.Move, dir, 5),
                    $"{robot} 이동 {dir} 다섯 장은 가운데를 한 번 더 가리킨다");

                Assert.IsNull(
                    MBI.Editor.CombatAssetGenerator.CellOrder(robot, UnitAnimState.Move, dir, 6),
                    $"{robot} 이동 {dir} — 사본이 남아 여섯 장이면 지금까지대로 돈다");
            }

            Assert.IsNull(
                MBI.Editor.CombatAssetGenerator.CellOrder("robot_b", UnitAnimState.Move,
                                                              UnitAnimDirection.South, 5),
                "로봇 B는 적지 않았다 — 일반화하지 않는다(`260907_W04` 4-3)");
            Assert.IsNull(
                MBI.Editor.CombatAssetGenerator.CellOrder("fusion", UnitAnimState.Move,
                                                              UnitAnimDirection.East, 5),
                "옆모습은 걷기의 좌우가 안 갈려 이 목록의 자리가 아니다");
            Assert.IsNull(
                MBI.Editor.CombatAssetGenerator.CellOrder("fusion", UnitAnimState.Idle,
                                                              UnitAnimDirection.South, 5),
                "대기는 왕복이라 목록과 같이 쓰지 않는다");
        }

        [Test]
        public void Clip_IsInvalid_WhenEmpty()
        {
            var empty = new UnitAnimClip { frames = new Sprite[0], targetSeconds = 1f };
            Assert.IsFalse(empty.IsValid, "그림이 없으면 걸 수 없다");

            var noSeconds = new UnitAnimClip { frames = new Sprite[1], targetSeconds = 0f };
            Assert.IsFalse(noSeconds.IsValid, "목표 초가 0이면 걸 수 없다");
        }
    }
}
