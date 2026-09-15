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
        /// ⚠️ **폐기 — 동적에서 정적으로 갈았다**(2026-09-15 · 「다리L」 결함).
        ///
        /// 구 시험은 「동적이어야 임의의 한글이 다 그려진다 · 정적으로 바꾸면 문구를 바꾸는
        /// 순간 그 글자만 안 나온다」였다. **그 걱정은 옳다.** 그런데 실측이 반대를 보였다 —
        /// **동적인 채로도 라틴 대문자가 안 그려졌다**(한글은 전부 나오는데 「다리R」의 R 만
        /// 빠졌다 · WebGL 엔 시스템 폰트 폴백이 없다).
        ///
        /// 즉 동적 굽기는 **「다 그려진다」를 지켜 주지 못했다.** 그래서 정적으로 가되,
        /// 구 시험이 걱정한 위험은 **다른 장치가 맡는다** —
        /// `KoreanFontSnapTests.소스의_모든_글자가_폰트에_구워져_있다` 가 소스의 문자열을
        /// 다시 훑어 **안 구워진 글자가 하나라도 있으면 빨개진다.** 문구를 바꾸고 폰트를
        /// 다시 안 구우면 그 시험이 잡고, 메뉴 `MBI/Reimport Korean Font` 가 고친다.
        ///
        /// **걱정을 버린 것이 아니라 지키는 자리를 옮겼다.**
        /// </summary>
        [Test]
        public void FontIsStatic_AndEveryUsedGlyphIsBaked()
        {
            var font = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
            Assert.NotNull(font);
            Assert.IsFalse(font.dynamic,
                "정적으로 구워야 WebGL 에서 라틴이 안 빠진다 — 09-15 「다리L」");
        }
    }
}
