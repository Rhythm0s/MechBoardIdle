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

        [MenuItem("MBI/Build WebGL")]
        public static void Build()
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
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                double mb = s.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[MBI] WebGL 빌드 성공: {OutputDir} · {mb:F1} MB · {s.totalTime.TotalSeconds:F0}초");
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
