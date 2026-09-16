using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// WebGL 빌드(§배포 — 2026-08-19 타깃 변경: Android APK → 웹 빌드).
    ///
    /// 배치모드에서 부를 수 있게 만든다 — 심사자에게 링크로 배포하는 것이 최종 형태이므로
    /// "브라우저에서 뜨는가"를 사람이 에디터를 열지 않고도 반복 확인할 수 있어야 한다.
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath &lt;repo&gt; \
    ///             -executeMethod MBI.Editor.WebGLBuilder.Build -logFile &lt;log&gt;
    ///
    /// 산출물: Build/WebGL (.gitignore 대상). 로컬 확인은 그 폴더를 정적 서버로 열면 된다 —
    /// file:// 로 직접 열면 브라우저 보안 정책 때문에 로드되지 않는다.
    /// </summary>
    public static class WebGLBuilder
    {
        private const string OutputDir = "Build/WebGL";
        private const string MainScene = "Assets/_Project/Scenes/Game.unity";

        /// <summary>
        /// 배포 빌드 — **심사자에게 링크로 가는 것.** 튜토리얼 복귀 버튼은 여기에 없다
        /// (260902_W08 §2-2: 스테이지 0은 스테이지 이동 목록에 뜨지 않는다).
        /// </summary>
        /// <summary>
        /// 만든 `index.html` 의 **캔버스 크기를 창에 맞춘다** (2026-09-15 · 「HUD 잘림」의 진짜 원인).
        ///
        /// ⚠️⚠️ **HUD 가 잘린 것이 아니라 페이지가 캔버스를 창 밖으로 밀어내고 있었다.**
        /// 유니티 기본 템플릿은 데스크톱에서 캔버스를 **`1440 x 2560` 으로 박아 둔다.**
        /// 창이 그보다 작으면 캔버스가 가운데 정렬로 **삐져나가고**, 실측으로 위가
        /// **431px** 잘려 있었다(`top: -431`) — 그래서 화면에는 HUD 윗줄들이 사라졌다.
        ///
        /// 📌 **게임 안에서는 못 고친다.** 유니티는 자기 캔버스가 다 보인다고 믿고 그린다 —
        /// `Screen.height` 는 2560 을 그대로 돌려주므로 **클램프도 소용이 없다.**
        /// 09-15 에 HUD 자리를 두 번 고치고도 증상이 남았던 까닭이 이것이다.
        ///
        /// ⚠️ **비율은 지킨다**(1440:2560). 늘리면 도트가 뭉개진다.
        ///
        /// 템플릿을 새로 만드는 대신 **만든 뒤 한 줄을 고치는** 쪽을 골랐다 —
        /// 템플릿을 복제하면 `TemplateData` 까지 따라와 유니티가 올릴 때마다 갈라진다.
        /// </summary>
        /// <summary>
        /// 레터박스 여백에 깔 **흙바닥 타일**을 산출 폴더로 옮긴다
        /// (2026-09-15 사용자 확정 · §73-42).
        ///
        /// ⚠️ **전투 바닥과 같은 그림을 쓴다** — 여백이 다른 그림이면 「여기도 화면인가」가 된다.
        /// 원본은 `Art/Backgrounds/bg_combat.png` 이고, 페이지가 읽을 수 있게 `TemplateData/` 로 옮긴다.
        ///
        /// ⚠️ **없으면 경고한다.** 그림이 안 가도 화면은 그냥 **검정**이라 —
        /// 안 된 것이 스스로 안 알려진다(오늘 여러 번 겪은 모양이다).
        /// </summary>
        /// <summary>
        /// 격납고 벽 그림을 빌드 산출 폴더로 옮긴다 (2026-09-16 사용자 승인 · §74-7 ①).
        ///
        /// ⚠️ **CSS 만 넣고 그림을 안 옮기면 조용히 안 나온다** — 404 는 콘솔에만 남고
        /// 화면에는 그냥 단색으로 보인다. 그래서 한 곳에서 같이 한다.
        ///
        /// 🗑️ **폐기(2026-09-16)** — 종전에는 `bg_combat.png`(흙 타일)를 `bg_letterbox.png` 로
        /// 내보내 여백에 반복해 깔았다. 사용자가 「없어 보인다」고 판정해 벽으로 갈았다.
        /// </summary>
        private static void CopyLetterboxWall()
        {
            const string src = "Assets/_Project/Art/Backgrounds/letterbox_wall.png";
            string dst = System.IO.Path.Combine(OutputDir, "TemplateData", "letterbox_wall.png");

            if (!System.IO.File.Exists(src))
            {
                Debug.LogWarning("[MBI] 격납고 벽 그림이 없다 — 여백이 단색 #13111B 로만 남는다: " + src);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dst));
            System.IO.File.Copy(src, dst, true);
            Debug.Log("[MBI] 격납고 벽을 내보냈다 — " + dst);
        }

        private static void FitCanvasToWindow()
        {
            string page = System.IO.Path.Combine(OutputDir, "index.html");
            if (!System.IO.File.Exists(page))
            {
                Debug.LogWarning("[MBI] index.html 이 없다 — 캔버스 맞춤을 건너뛴다: " + page);
                return;
            }

            string html = System.IO.File.ReadAllText(page);
            const string fixedSize = "canvas.style.width = \"1440px\";\n        canvas.style.height = \"2560px\";";

            // 기본 템플릿이 바뀌면 여기가 안 맞는다 — **조용히 지나가지 않는다.**
            if (html.IndexOf("canvas.style.width = \"1440px\"", System.StringComparison.Ordinal) < 0)
            {
                Debug.LogWarning("[MBI] index.html 에서 고정 캔버스 크기를 못 찾았다 — "
                                 + "유니티 템플릿이 바뀌었을 수 있다. 창보다 큰 캔버스는 "
                                 + "화면 가장자리를 잘라 먹으니 직접 확인한다.");
                return;
            }

            string fit = string.Join("\n", new[]
            {
                "(function () {",
                "          // ⚠️ **레터박스**(2026-09-15 사용자 확정 A안 · 육안 8차 ④).",
                "          //    내용은 언제나 9:16 이고, 창 비율이 다르면 남는 자리는 **검정**이다.",
                "          //    그래야 `UiLayout.Scale`(높이 기준)이 늘 같은 뜻을 갖고 버튼 크기가 안 변한다.",
                "          var W = 1440, H = 2560, lastW = -1, lastH = -1;",
                "          document.documentElement.style.overflow = 'hidden';",
                "          document.body.style.margin = '0';",
                "          document.body.style.overflow = 'hidden';",
                "          // 🗑️ **흰 타일은 폐기했다**(2026-09-16 사용자 판정) — 「없어 보인다」고 했다.",
                "          //    여백은 이제 **격납고 벽**이다(`letterbox_wall.png` 256×512 · 아트 09-16).",
                "          //    오른쪽 띠 = 원본 · 왼쪽 띠 = 미러 · 세로로만 반복 · 어둠 막은 없다",
                "          //    (그림이 이미 어둡다 · 휘도 p50 66.5). 바깥은 그림 바깥 열과 같은 #13111B 단색이다.",
                "          //",
                "          // ⚠️⚠️ **배경 한 장으로는 못 한다** — CSS 배경은 **거울로 못 뒤집는다.**",
                "          //    그래서 띠를 엘리먼트 둘로 두고 왼쪽에만 `scaleX(-1)` 을 건다.",
                "          //    띠는 캔버스 **밑에** 깔린다(z-index) — 캔버스가 가운데를 덮는다.",
                "          var box = document.querySelector('#unity-container');",
                "          var wallL = null, wallR = null;",
                "          if (box) {",
                "            box.className = '';",
                "            box.style.cssText = 'position:fixed;left:0;top:0;width:100%;height:100%;'",
                "              + 'display:flex;align-items:center;justify-content:center;transform:none';",
                "            box.style.background = '#13111B';",
                "            function band(mirror) {",
                "              var d = document.createElement('div');",
                "              // ⚠️ 그림은 **안쪽 변**에 붙인다. 오른쪽 띠는 왼쪽이 안쪽이고,",
                "              //    왼쪽 띠는 뒤집힌 좌표라 같은 'left' 가 바로 안쪽이 된다.",
                "              d.style.cssText = 'position:fixed;top:0;height:100%;z-index:0;pointer-events:none;'",
                "                + 'background-image:url(TemplateData/letterbox_wall.png);'",
                "                + 'background-repeat:repeat-y;background-position:left top;'",
                "                + (mirror ? 'left:0;transform:scaleX(-1);' : 'right:0;');",
                "              box.appendChild(d);",
                "              return d;",
                "            }",
                "            wallL = band(true);",
                "            wallR = band(false);",
                "          }",
                "          // ⚠️ 유니티 기본 템플릿은 좁은 화면에서 캔버스에 `unity-mobile` 을 붙이고",
                "          //    그 규칙이 `width:100%` 다 — 레터박스를 덮는다. 클래스를 떼고 !important 로 못 박는다.",
                "          canvas.className = '';",
                "          // ⚠️ 컨테이너를 fixed/flex 로 바꾸면 템플릿 푸터(유니티 로고·전체화면 단추)가",
                "          //    **캔버스 위로 올라와 버튼을 덮는다** — 레터박스를 넣다가 만든 회귀다.",
                "          //    포트폴리오 빌드에 그 줄은 필요 없다.",
                "          var foot = document.querySelector('#unity-footer');",
                "          if (foot) foot.style.display = 'none';",
                "          // ⚠️ 캔버스가 flex 흐름 밖에 있어서(템플릿이 자리를 따로 준다)",
                "          //    컨테이너의 가운데 정렬이 안 먹는다 — 캔버스에 직접 박는다.",
                "          canvas.style.setProperty('position', 'absolute', 'important');",
                "          canvas.style.setProperty('left', '50%', 'important');",
                "          canvas.style.setProperty('top', '50%', 'important');",
                "          canvas.style.setProperty('transform', 'translate(-50%, -50%)', 'important');",
                "          // 띠가 z-index 0 이라 캔버스를 그 위로 올린다 — 안 올리면 벽이 화면을 덮는다.",
                "          canvas.style.setProperty('z-index', '1', 'important');",
                "          function fit() {",
                "            var vw = window.innerWidth || document.documentElement.clientWidth;",
                "            var vh = window.innerHeight || document.documentElement.clientHeight;",
                "            if (!(vw > 0 && vh > 0)) return;",
                "            var k = Math.min(vw / W, vh / H);",
                "            var w = Math.round(W * k), h = Math.round(H * k);",
                "            if (w === lastW && h === lastH) return;",
                "            lastW = w; lastH = h;",
                "            canvas.style.setProperty('width', w + 'px', 'important');",
                "            canvas.style.setProperty('height', h + 'px', 'important');",
                "            // 띠 폭 = 캔버스 밖으로 남는 자리의 절반. 0 이면 띠가 없다(꽉 들어찬 창).",
                "            var side = Math.max(0, Math.round((vw - w) / 2));",
                "            // ⚠️ **그림을 게임과 같은 배율로 키운다** — 캔버스가 k 배로 서는데",
                "            //    벽만 날 픽셀로 두면 둘의 픽셀 크기가 어긋난다. 새 값을 안 지어냈다 — 같은 k 다.",
                "            var wallW = Math.max(1, Math.round(256 * k));",
                "            [wallL, wallR].forEach(function (d) {",
                "              if (!d) return;",
                "              d.style.width = side + 'px';",
                "              d.style.backgroundSize = wallW + 'px auto';",
                "            });",
                "          }",
                "          fit();",
                "          window.addEventListener('resize', fit);",
                "        })();",
            });

            // ⚠️⚠️ **갈래 밖에 넣는다**(2026-09-15 · 실측으로 잡았다).
            //
            // 종전에는 **데스크톱 `else` 안**의 고정 크기 두 줄을 갈아 끼웠다. 그런데 그 두 줄은
            // `if (모바일) { ... } else { ... }` 의 **한쪽**이라, 브라우저가 모바일로 읽히면
            // (좁은 창·터치 흉내) **레터박스가 아예 안 돈다** — 실제로 비율이 0.74 로 찍혔고
            // 캔버스 클래스가 `unity-mobile` 로 되돌아와 있었다.
            //
            // 📌 **두 갈래가 있는 자리에 한쪽만 고치는 실수**를 오늘만 세 번째 한다
            // (수동 이동 클램프 · 그림 그리는 경로 둘 · 여기).
            // 고정 크기 줄은 **지우고**, 맞춤은 갈래가 끝난 뒤 **한 번만** 돈다.
            html = html.Replace(fixedSize, "/* 캔버스 크기는 아래 레터박스가 정한다 */");
            const string afterBranch = "document.querySelector(\"#unity-loading-bar\").style.display = \"block\";";
            if (html.IndexOf(afterBranch, System.StringComparison.Ordinal) < 0)
            {
                Debug.LogWarning("[MBI] 레터박스를 넣을 자리를 못 찾았다 — 유니티 템플릿이 바뀌었을 수 있다");
                return;
            }
            html = html.Replace(afterBranch, fit + "\n\n      " + afterBranch);

            // ⚠️ **타일도 같이 내보낸다** — CSS 만 넣고 그림을 안 옮기면 **조용히 안 나온다**
            // (404 는 콘솔에만 남고 화면에는 그냥 검정으로 보인다). 한 곳에서 같이 한다.
            CopyLetterboxWall();
            System.IO.File.WriteAllText(page, html);
            Debug.Log("[MBI] index.html 캔버스를 창에 맞췄다 — 1440x2560 고정을 걷었다");
        }

        [MenuItem("MBI/Build WebGL")]
        public static void Build() => Build(development: false);

        /// <summary>
        /// **촬영·리허설용 개발 빌드**(260902_W08 §2-3). 같은 코드인데 스테이지 0 복귀가 열린다 —
        /// <c>Debug.isDebugBuild</c>가 참이 되기 때문이다.
        ///
        /// 복귀 경로가 없으면 9월 6일에 A구간 재테이크가 불가능해지고 **첫 테이크가 곧 최종본**이 된다.
        /// 그래서 이것은 편의가 아니라 촬영 조건이다.
        /// </summary>
        [MenuItem("MBI/Build WebGL (개발 — 촬영용)")]
        public static void BuildDevelopment() => Build(development: true);

        private static void Build(bool development)
        {
            string[] scenes = { MainScene };
            if (!File.Exists(MainScene))
            {
                Debug.LogError($"[MBI] 빌드 대상 씬 없음: {MainScene} — 'MBI/Create Game Scene' 먼저.");
                EditorApplication.Exit(2);
                return;
            }

            // 압축 = Gzip + **압축 해제 폴백**.
            // 폴백이 없으면 서버가 "Content-Encoding: gzip" 헤더를 붙여야만 로드된다. 정적 호스팅
            // (GitHub Pages·단순 파일 서버)은 그 헤더를 안 붙이므로 화면이 로고에서 멈춘다 —
            // 2026-08-19 첫 빌드에서 실제로 재현했다. 폴백을 켜면 Unity가 JS 디컴프레서를 동봉해
            // 헤더 없이도 풀리므로, 어느 호스팅에 올려도 링크 하나로 뜬다(심사자 직접 플레이가 전제).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                // 개발 빌드에서만 Debug.isDebugBuild가 참이 된다 — 그것이 튜토리얼 복귀의 문이다.
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                double mb = s.totalSize / (1024.0 * 1024.0);
                string kind = development ? "개발(촬영용)" : "배포";
                FitCanvasToWindow();
                Debug.Log($"[MBI] WebGL {kind} 빌드 성공: {OutputDir} · {mb:F1} MB · {s.totalTime.TotalSeconds:F0}초");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[MBI] WebGL 빌드 실패: {s.result} · 에러 {s.totalErrors}건");
                EditorApplication.Exit(1);
            }
        }
    }
}
