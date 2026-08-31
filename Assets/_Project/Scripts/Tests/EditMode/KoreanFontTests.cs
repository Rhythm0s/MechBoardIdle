using System.IO;
using MBI.UI;
using NUnit.Framework;
using UnityEngine;

namespace MBI.Tests
{
    /// <summary>
    /// 한글 폰트 동봉(2026-08-31 브라우저 실측 대응).
    ///
    /// **WebGL 빌드에는 시스템 폰트 폴백이 없다.** 내장 GUI 폰트에 한글 글리프가 없으므로
    /// 폰트를 동봉하지 않으면 화면의 한글이 **전부 사라진다** — 숫자·기호·영문만 남는다.
    ///
    /// ⚠️ 에디터에서는 OS 폰트가 대신 채워 주기 때문에 **에디터로만 보면 절대 안 드러난다.**
    /// 지침 §7 [08-30] 「실패하지 않는 결함」의 한 형태라, 자산이 사라지는 것을 여기서 막는다.
    /// </summary>
    public sealed class KoreanFontTests
    {
        private const string Ttf = "Assets/_Project/Resources/Fonts/NotoSansKR-Regular.ttf";
        private const string License = "Assets/_Project/Resources/Fonts/LICENSE-NotoSansKR.md";

        /// <summary>
        /// 폰트가 **Resources 경로에** 있어야 한다. 다른 폴더로 옮기면 런타임 로드가 조용히
        /// 실패하고, 그 실패는 WebGL 빌드에서만 보인다.
        /// </summary>
        [Test]
        public void FontIsUnderResources_AndLoadable()
        {
            Assert.IsTrue(File.Exists(Ttf), $"폰트 파일: {Ttf}");
            Assert.IsTrue(KoreanFont.IsAvailable, "Resources.Load로 읽혀야 한다");
        }

        /// <summary>
        /// **OFL 라이선스 사본이 폰트 옆에 있어야 한다.** 이 빌드는 GitHub Pages로 공개되므로
        /// 재배포 조건을 지키는 것이 배포의 전제다. 파일이 사라지면 조건 위반이 된다.
        /// </summary>
        [Test]
        public void LicenseSitsNextToTheFont()
        {
            Assert.IsTrue(File.Exists(License), $"라이선스 사본: {License}");

            string text = File.ReadAllText(License);
            Assert.IsTrue(text.Contains("SIL Open Font License"), "OFL 명시");
        }

        /// <summary>
        /// **Windows 동봉 폰트를 쓰지 않는다.** 맑은 고딕·굴림·바탕은 재배포 불가라
        /// 공개 빌드에 넣을 수 없다 — 이름이 섞여 들어오는 것을 여기서 막는다.
        /// </summary>
        [Test]
        public void DoesNotShipAProprietaryFont()
        {
            string dir = Path.GetDirectoryName(Ttf);
            Assert.IsTrue(Directory.Exists(dir), dir);

            foreach (string path in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(path).ToLowerInvariant();
                foreach (string banned in new[] { "malgun", "gulim", "batang", "dotum", "gungsuh" })
                    Assert.IsFalse(name.Contains(banned),
                        $"재배포 불가 폰트가 들어왔다: {name}");
            }
        }

        /// <summary>
        /// 폰트가 **동적 폰트**여야 임의의 한글이 다 그려진다. 정적 아틀라스로 바뀌면
        /// 문구를 바꾸는 순간 그 글자만 안 나온다 — 이번 주에 문구가 계속 바뀐다.
        /// </summary>
        [Test]
        public void FontIsDynamic_SoAnyKoreanRenders()
        {
            var font = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
            Assert.NotNull(font);
            Assert.IsTrue(font.dynamic, "동적 폰트여야 새 글자가 그려진다");
        }
    }
}
