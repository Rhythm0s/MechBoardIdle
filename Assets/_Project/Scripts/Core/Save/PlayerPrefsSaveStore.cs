using UnityEngine;

namespace MBI.Core
{
    /// <summary>
    /// 세이브 저장소 구현(§5-7). 세이브 전체를 JSON 문자열 **한 덩어리**로 PlayerPrefs 키 하나에 넣는다.
    ///
    /// 왜 파일이 아니라 PlayerPrefs인가 — 배포처가 웹빌드이기 때문이다.
    ///   - WebGL에서 파일 경로(Application.persistentDataPath)에 쓴 내용은 브라우저 저장소(IndexedDB)로
    ///     따로 동기화돼야 남는다. 그 동기화 시점을 코드가 직접 챙겨야 해서 실패 지점이 하나 늘어난다.
    ///   - PlayerPrefs는 Unity가 플랫폼별 저장소(웹=IndexedDB)로 직접 넘기므로 경로가 하나뿐이고,
    ///     에디터·웹·스탠드얼론이 같은 코드로 돈다 → EditMode에서 그대로 검증할 수 있다.
    ///   - 필드를 키마다 흩뿌리지 않고 JSON 한 덩어리로 넣으므로, 스키마 버전 관리는 그대로 유지된다
    ///     (§3 한 파일=한 책임 — 여기는 "저장소에 넣고 뺀다"만 한다).
    ///
    /// ⚠️ 파싱 실패 폴백(견고성 1건, 2026-08-19 복원): 웹빌드에는 원자적 쓰기가 없어 저장 도중 탭을
    /// 닫으면 잘린 JSON이 남을 수 있다. 그대로 두면 다음 실행부터 예외가 터져 **게임이 아예 켜지지 않고**
    /// 브라우저 저장소를 직접 지우기 전까지 복구되지 않는다 — 심사자에겐 그냥 안 켜지는 게임이다.
    /// 그래서 읽기 실패는 예외로 올리지 않고 기본값으로 되돌린다.
    /// </summary>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public const string DefaultKey = "MBI_SAVE_V1";

        private readonly string _key;

        public PlayerPrefsSaveStore(string key = DefaultKey)
        {
            _key = string.IsNullOrEmpty(key) ? DefaultKey : key;
        }

        public bool TryLoad(out SaveDataV1 data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(_key)) return false; // 첫 실행 = 정상 경로(예외 아님)

            string json = PlayerPrefs.GetString(_key, string.Empty);
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                SaveDataV1 parsed = JsonUtility.FromJson<SaveDataV1>(json);
                if (parsed == null) return false;
                data = parsed;
                return true;
            }
            catch (System.Exception e)
            {
                // 잘린/손상된 세이브 → 기본값으로 새 시작. 조용히 삼키지 말고 흔적은 남긴다.
                Debug.LogWarning($"[MBI] 세이브 파싱 실패 — 기본값으로 시작한다: {e.Message}");
                return false;
            }
        }

        public void Save(SaveDataV1 data)
        {
            if (data == null) return;
            PlayerPrefs.SetString(_key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }
    }
}
