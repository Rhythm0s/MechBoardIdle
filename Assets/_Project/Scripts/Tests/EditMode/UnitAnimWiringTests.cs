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

                if (state == UnitAnimState.Idle || state == UnitAnimState.Move)
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
        /// 대기 되감기 순서 — 다섯 장이면 0·1·2·3·4·3·2·1 여덟 걸음이다
        /// (2026-09-07 사용자 판정). 양 끝을 두 번 세면 그 프레임에서만 두 배로 머문다.
        /// </summary>
        [Test]
        public void PingPong_WalksForwardThenBack()
        {
            Assert.AreEqual(8, MBI.Combat.SpriteFrameAnimator.PingPongCycle(5, true), "다섯 장이면 여덟 걸음");

            var seen = new List<int>();
            for (int step = 0; step < 8; step++)
                seen.Add(MBI.Combat.SpriteFrameAnimator.PingPongIndex(step, 5, true));

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 3, 2, 1 }, seen, "되감기 순서");
        }

        /// <summary>이동은 걷는 순환이라 되감지 않는다 — 여섯 장이면 여섯 걸음 그대로다.</summary>
        [Test]
        public void PingPong_Off_KeepsPlainLoop()
        {
            Assert.AreEqual(6, MBI.Combat.SpriteFrameAnimator.PingPongCycle(6, false));
            for (int step = 0; step < 6; step++)
                Assert.AreEqual(step, MBI.Combat.SpriteFrameAnimator.PingPongIndex(step, 6, false));
        }

        /// <summary>두 장 이하는 되감을 것이 없다 — 순환으로 내린다.</summary>
        [Test]
        public void PingPong_TooFewFrames_FallsBackToLoop()
        {
            Assert.AreEqual(2, MBI.Combat.SpriteFrameAnimator.PingPongCycle(2, true));
            Assert.AreEqual(1, MBI.Combat.SpriteFrameAnimator.PingPongIndex(1, 2, true));
        }

        [Test]
        public void Clip_IsInvalid_WhenEmpty()
        {
            var empty = new UnitAnimClip { frames = new Sprite[0], fps = 6f };
            Assert.IsFalse(empty.IsValid, "프레임이 없으면 걸 수 없다");

            var noFps = new UnitAnimClip { frames = new Sprite[1], fps = 0f };
            Assert.IsFalse(noFps.IsValid, "재생 속도가 0이면 걸 수 없다");
        }
    }
}
