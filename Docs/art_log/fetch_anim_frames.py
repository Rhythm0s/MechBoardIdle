"""PixelLab 캐릭터 zip 에서 프레임을 꺼내 `Art/Anim/<clip>/<dir>/` 에 넣는다.
(구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/fetch_anim_frames.py <캐릭터 id> <zip 안 그룹 이름> <넣을 clip> [방향...]

    예) python Docs/art_log/fetch_anim_frames.py 1b1117e3-... robot_a_Move_v2 robot_a_Move

**왜 그룹 이름과 넣을 자리를 따로 받는가.** 벌을 다시 뽑을 때 기존 그룹에 덧붙이면 zip 안에서
같은 폴더 이름이 겹친다. 그래서 새 그룹은 `<clip>_v2` 처럼 다른 이름으로 만들고, 받을 때
**그 폴더에서 꺼내 원래 clip 자리에 넣는다.** 그러면 캐릭터 쪽 이력은 남고 리포는 한 벌만 갖는다.

zip 구조: `Idle/animations/<그룹 표시이름>/<방향>/frame_NNN.png`

**넣기 전에 대상 방향 폴더를 비운다.** 프레임 수가 줄면(7 → 5) 덮어쓰기만으로는 옛 `frame_005`·
`frame_006` 이 남아 벌의 길이가 틀어진다 — 2026-09-07 에 09-04 초안을 교체할 때 겪은 자리다.
"""
import io
import os
import re
import sys
import urllib.request
import zipfile

ROOT = "Assets/_Project/Art/Anim"
DOWNLOAD = "https://api.pixellab.ai/mcp/characters/%s/download"


def fetch(character_id):
    url = DOWNLOAD % character_id
    try:
        with urllib.request.urlopen(url) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        print("받지 못했다 (HTTP %s): %s" % (e.code, body))
        if e.code == 423:
            print("→ 아직 생성 중이다. 끝난 뒤 다시 돌린다.")
        return None


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 1

    character_id = sys.argv[1]
    group_name = sys.argv[2]
    target_clip = sys.argv[3]
    only = set(sys.argv[4:])          # 비면 그룹의 방향 전부

    raw = fetch(character_id)
    if raw is None:
        return 1

    z = zipfile.ZipFile(io.BytesIO(raw))
    pattern = re.compile(
        r"^Idle/animations/%s/([^/]+)/(frame_\d+\.png)$" % re.escape(group_name))

    by_dir = {}
    for entry in z.namelist():
        m = pattern.match(entry)
        if not m:
            continue
        direction, name = m.groups()
        if only and direction not in only:
            continue
        by_dir.setdefault(direction, []).append((name, entry))

    if not by_dir:
        names = sorted({e.split("/")[2] for e in z.namelist()
                        if e.startswith("Idle/animations/") and e.count("/") > 2})
        print("zip 에 그룹 '%s' 가 없다. 있는 그룹: %s" % (group_name, ", ".join(names)))
        return 1

    total = 0
    for direction, items in sorted(by_dir.items()):
        out = os.path.join(ROOT, target_clip, direction)
        # 먼저 비운다 — 프레임 수가 줄면 옛 프레임이 남는다.
        if os.path.isdir(out):
            for f in os.listdir(out):
                if f.startswith("frame_") and f.endswith(".png"):
                    os.remove(os.path.join(out, f))
        else:
            os.makedirs(out, exist_ok=True)

        for name, entry in sorted(items):
            open(os.path.join(out, name), "wb").write(z.read(entry))
        total += len(items)
        print("  %s/%s  <-  %s/%s  %d프레임" % (target_clip, direction, group_name, direction, len(items)))

    print("%s: %d방향 · %d프레임" % (target_clip, len(by_dir), total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
