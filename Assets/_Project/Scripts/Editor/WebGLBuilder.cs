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
                "          var W = 1440, H = 2560, lastW = -1, lastH = -1;",
                "          // ⚠️ 스크롤바가 생기면 창 크기가 흔들린다 — html·body 둘 다 막는다.",
                "          document.documentElement.style.overflow = 'hidden';",
                "          document.body.style.margin = '0';",
                "          document.body.style.overflow = 'hidden';",
                "          function fit() {",
                "            // ⚠️⚠️ **clientHeight 를 쓰면 안 된다** — 캔버스가 창보다 커지면",
                "            //    문서가 같이 커져서 그 값이 **캔버스를 따라 올라간다.** 그러면 맞춤이",
                "            //    자기가 만든 크기를 보고 다시 맞추는 꼴이라 **세로로 넘친 채 멈춘다**",
                "            //    (실측: 창 740x1000 인데 캔버스 745x1324 — 아래 324px 이 잘렸다).",
                "            //    창의 크기는 innerWidth/innerHeight 가 말한다.",
                "            var vw = window.innerWidth || document.documentElement.clientWidth;",
                "            var vh = window.innerHeight || document.documentElement.clientHeight;",
                "            // 창이 0 을 말할 때가 있다(숨은 탭·레이아웃 도중). 그때 맞추면 캔버스가 0 이 된다.",
                "            if (!(vw > 0 && vh > 0)) return;",
                "            var k = Math.min(vw / W, vh / H);",
                "            var w = Math.round(W * k), h = Math.round(H * k);",
                "            // 값이 같으면 안 쓴다 — 쓰면 레이아웃이 바뀌고 resize 가 다시 와서 고리가 된다.",
                "            if (w === lastW && h === lastH) return;",
                "            lastW = w; lastH = h;",
                "            canvas.style.width = w + 'px';",
                "            canvas.style.height = h + 'px';",
                "          }",
                "          fit();",
                "          window.addEventListener('resize', fit);",
                "        })();",
            });

            html = html.Replace(fixedSize, fit);
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
