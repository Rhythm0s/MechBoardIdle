using System.IO;
using UnityEngine;

namespace MBI.Editor
{
    /// <summary>
    /// balance_v4.json(리포 루트, 밸런스 1차 계약·schemaVersion 4.0)을 읽어 BalanceJson 으로 파싱하는 단일 진입점.
    /// 생성기(BalanceAssetGenerator·CombatAssetGenerator)와 검증 테스트(BalanceAnchorTests 등)가 공유한다 —
    /// 모두 §9 요약이 아니라 원천 json을 직접 읽게 하여 §7(요약 기반 산출) 실수를 차단.
    /// (Docs/balance_v3.1_measured.json 은 구 실측본·참고. 2026-07-29 v4 재배선.)
    /// </summary>
    public static class BalanceJsonLoader
    {
        /// <summary>수치 원천 파일의 프로젝트 상대 경로(CLAUDE.md §0·§3 1차 계약).</summary>
        public const string RelativePath = "balance_v4.json";

        /// <summary>Assets 폴더의 부모(= 프로젝트 루트) 기준 절대 경로.</summary>
        public static string AbsolutePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativePath));

        /// <summary>파일을 읽어 파싱. 실패 시 예외로 조기 노출(경로/스키마 드리프트).</summary>
        public static BalanceJson Load()
        {
            string path = AbsolutePath;
            if (!File.Exists(path))
                throw new FileNotFoundException($"[MBI] 수치 원천 없음: {path} (§9 재복사 필요)");

            string text = File.ReadAllText(path);
            // "params"는 C# 예약어 → JsonUtility 매핑 위해 키명만 치환(최상위 1회 등장).
            text = text.Replace("\"params\"", "\"paramList\"");

            BalanceJson model = JsonUtility.FromJson<BalanceJson>(text);
            if (model == null)
                throw new System.Exception($"[MBI] balance.json 파싱 실패: {path}");
            return model;
        }
    }
}
