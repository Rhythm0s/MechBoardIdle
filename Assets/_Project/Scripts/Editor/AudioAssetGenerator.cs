using System.IO;
using MBI.Core.Audio;
using MBI.Data;
using UnityEditor;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// 효과음 여덟을 <see cref="AudioConfig.sfxClips"/> 에 꽂는다
    /// (2026-09-11 · 플랜 §71-33 ① · 아트 실측 O-1).
    ///
    /// **왜 신설하는가.** 파일 여덟이 `Audio/sfx/` 에 다 있는데 **칸 여덟이 전부 비어 있었다**
    /// (`fileID: 0`). 훅도 재생기도 다 서 있었으므로 **소리만 한 번도 안 난 것**이고,
    /// 에러는 없었다 — `SfxPlayer` 가 `clip == null` 을 조용히 건너뛴다(자산이 없던 시절의
    /// 정상 경로였다). **안 나는 것은 스스로 알려 주지 않는다.**
    ///
    /// ⚠️ **칸은 이름이 아니라 차례로 맞물린다** — `sfxClips[i]` 가 `SoundIds.All[i]` 다.
    /// 그래서 **순서가 곧 배선**이고, 손으로 끌어다 넣으면 한 칸만 밀려도 **발사에서 병목
    /// 소리가 난다.** 여기서 이름으로 채워 그 실수를 없앤다.
    ///
    /// ⚠️ **파일은 리포 밖이다**(`.gitignore` 가 `sfx/*.ogg` 와 `*.meta` 를 막는다 —
    /// 출처 규약이 재배포를 금한다). 그래서 **새 클론에서는 칸이 비는 것이 정상**이고,
    /// `Docs/audio_manifest.md` 를 보고 다시 받은 뒤 이 생성기를 돌린다.
    /// </summary>
    public static class AudioAssetGenerator
    {
        private const string SfxDir = "Assets/_Project/Audio/sfx";
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/AudioConfig.asset";

        [MenuItem("MBI/Generate Audio Wiring")]
        public static void Generate()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[MBI] AudioConfig 없음: {ConfigPath}");
                return;
            }

            // ⚠️ **길이를 목록에 맞춘다.** 짧으면 `SfxPlayer` 가 `Min(All.Length, sfxClips.Length)`
            // 로 자르므로 **뒤쪽 소리가 조용히 사라진다.**
            if (config.sfxClips == null || config.sfxClips.Length != SoundIds.All.Length)
                config.sfxClips = new AudioClip[SoundIds.All.Length];

            int wired = 0, missingFile = 0, presentButEmpty = 0;

            for (int i = 0; i < SoundIds.All.Length; i++)
            {
                string id = SoundIds.All[i];
                string path = $"{SfxDir}/{id}.ogg";
                bool onDisk = File.Exists(path);

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                config.sfxClips[i] = clip;

                if (clip != null) { wired++; continue; }

                // ⚠️ **여기가 증빙 장치다** — 둘을 갈라서 적는다.
                //
                // 파일이 **없는** 것은 새 클론의 정상 경로다(리포 밖 자산). 그때 경고를 올리면
                // 늘 켜져 있는 경고가 되어 **아무도 안 본다.**
                // 파일이 **있는데 못 읽은** 것은 진짜 결함이다 — 임포트가 안 됐거나 형식이
                // 다르다. 09-11 에 칸 여덟이 빈 채로 지나간 것이 바로 이 경우였다.
                if (onDisk) { presentButEmpty++; Debug.LogWarning(
                    $"[MBI] 효과음 파일은 있는데 못 읽었다: {path} — 임포트·형식을 본다."); }
                else missingFile++;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            string tail = missingFile > 0
                ? $" · 파일 없음 {missingFile}(리포 밖 — Docs/audio_manifest.md 로 재조달)"
                : string.Empty;
            Debug.Log($"[MBI] 효과음 배선 — 꽂힘 {wired}/{SoundIds.All.Length}"
                      + $" · ⚠️ 파일 있는데 빈 칸 {presentButEmpty}{tail}");
        }
    }
}
