# `Assets/_Project/Art/_candidates/` 에서 옮겨 온 후보 32장 (2026-09-10)

**왜 옮겼나.** 이 그림들은 후보이지 자산이 아닌데 **Unity 의 `Assets/` 안에** 들어 있었다.
2026-09-09 에 「`Art/` 아래 `.meta` 를 전부 커밋한다」로 규칙이 바뀌면서(로그 3-22) 이 폴더의
`.meta` 35개가 함께 추적에 들어갔는데, **PNG 32장은 미추적이라 자산 없는 `.meta` 만 리포에 남는**
어긋난 상태가 됐다. Unity 는 고아 `.meta` 를 새로고침 때 지우므로 그대로 두면 커밋이 흔들린다.

**사용자 판정(2026-09-10): 옮긴다.** 이 세션의 후보 보관 관례가 `Docs/art_log/candidates/` 이고,
빌드에 들어갈 이유도 없다.

**무엇을 옮겼나** — **PNG 32장만.** `.meta` 는 `Assets/` 안에서만 뜻이 있는 파일이라 옮기지 않고 지웠다.
md5 를 대조해 **32장이 원본과 완전히 같음**을 확인한 뒤 원본 폴더를 지웠다.

| 무엇 | 장수 |
|---|---|
| `boss_256_ref.png` · `boss_dirs/` 8 · `boss_dirs_sheet.png` | 10 |
| `fusion_256_ref.png` · `fusion_512.png` | 2 |
| `mob_armor.png` · `mob_cannon.png` · `mob_cannon_dirs/` 8 | 10 |
| `mob_infantry.png` · `mob_infantry_dirs/` 8 · `mob_infantry_dirs_sheet.png` | 10 |

⚠️ **`boss_256_ref.png`(`26ed23e7`) 는 승인본이 아니다** — 512 의 축소본도, 09-04 벌의 첫 칸도 아니다(로그 3-23).
지금 보스 전투 승인본은 `Assets/_Project/Art/Units/boss_256.png`(`a909ae6a`) 다.
