using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MBI.Core.Audio;
using NUnit.Framework;

namespace MBI.Tests
{
    /// <summary>
    /// 버튼 클릭음 — **한 곳을 지나가는지 소스로 지킨다** (2026-09-15 사용자 확정 · 육안 ⑥).
    ///
    /// ⚠️ **왜 소스를 훑는가.** IMGUI 에는 「모든 버튼」 훅이 없어서 래퍼로 모았다.
    /// 래퍼는 **새 버튼을 만들 때 그냥 `GUI.Button` 을 쓰면 비켜 간다** — 그리고
    /// 비켜 간 버튼은 **에러 없이 조용하다.** 소리가 안 나는 것은 눈에 안 띈다.
    /// 그래서 빌드 전에 여기서 빨개지게 한다.
    /// </summary>
    public sealed class UiClickSoundTests
    {
        private const string Root = "Assets/_Project/Scripts";

        // 앞에 글자나 점이 붙지 않은 GUI.Button / GUILayout.Button 만 잡는다
        // (`UiSkin.Button` 은 이름이 달라 안 걸린다).
        private static readonly Regex Raw = new Regex(@"(?<![\w.])GUI(?:Layout)?\.Button\(");

        [Test]
        public void 화면_버튼은_전부_UiSkin_을_지나간다()
        {
            var offenders = new List<string>();

            foreach (string path in Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Tests/")) continue;
                // 래퍼 자신은 당연히 진짜 GUI.Button 을 부른다.
                if (norm.EndsWith("/UiSkin.cs")) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    if (Raw.IsMatch(lines[i]))
                        offenders.Add($"{norm}:{i + 1}");
            }

            Assert.That(offenders, Is.Empty,
                "이 자리는 클릭음을 안 낸다 — `UiSkin.Button` / `UiSkin.ButtonLayout` 으로 바꾼다:\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void 클릭음_이름이_목록에_있고_조작음_채널이다()
        {
            Assert.That(SoundIds.All, Contains.Item(SoundIds.UiClick));
            Assert.That(SoundIds.KindOf(SoundIds.UiClick), Is.EqualTo(SoundKind.Ui));
        }

        [Test]
        public void 이름이_안_겹친다()
        {
            Assert.That(SoundIds.All.Distinct().Count(), Is.EqualTo(SoundIds.All.Length),
                "같은 이름이 둘이면 문서와 자산 조달이 어긋난다");
        }
    }
}
