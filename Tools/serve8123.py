# -*- coding: utf-8 -*-
"""
`Build/WebGL` 를 `http://localhost:8123` 에 내놓는다 — **개발용 · 게임 코드가 아니다.**

    python Tools/serve8123.py            # 리포 어디서 불러도 된다
    python Tools/serve8123.py 8124       # 포트를 바꾸고 싶을 때

**왜 있는가.** WebGL 빌드는 `file://` 로 못 연다 — 브라우저가 로컬 파일에서
wasm·데이터 내려받는 것을 막는다. 그래서 **주소가 하나** 필요하고, 그 주소를 내는 것이
이 파일이다. 리허설·촬영 때 사용자가 여는 `?v=<커밋>` 링크가 여기로 선다.

하는 일 넷 —

  ① `Build/WebGL` 를 낸다.

  ② ⚠️ `.unityweb` 에 **`Content-Encoding: gzip`** 을 붙인다.
     Unity 가 **눌러서 굽는데** 이 표시가 없으면 로더가 `Unable to parse` 로 죽는다.
     📌 **눌렸는지는 봐야 안다** — 빌드 설정을 바꾸면 brotli 이거나 안 눌렸을 수 있다.
        파일 앞 세 바이트가 `1f 8b 08` 이면 gzip 이다:
            head -c 3 Build/WebGL/Build/WebGL.data.unityweb | od -An -tx1

  ③ `Cache-Control: no-store` — 다시 구운 뒤에도 옛 빌드가 나오는 것을 막는다.

  ④ ⚠️⚠️ **연결을 여럿 받는다**(`ThreadingHTTPServer`).
     2026-09-28 에 한 줄짜리 `TCPServer` 로 띄웠다가 **남이 못 들어왔다** — 내가 열어 둔
     브라우저가 **keep-alive 로 연결을 물고 있어** 뒤의 요청이 전부 줄을 섰다.
     내 `curl` 은 200, 설계 세션의 `curl` 은 **000** 이었다.
     📌 **혼자 본 200 은 서버가 산 증거가 아니다** — 남이 못 들어오는 200 이 있다.
        살았는지 보려면 **동시에 둘 이상** 쳐 본다.

⚠️ **여기 두는 까닭** — 09-22 에 세션 임시 폴더에 두었더니 **다음 날 사라져 있었다.**
   매일 쓰는 도구는 리포 안에 산다(2026-09-28 사용자 확정).
   `Tools/` 는 `Assets/` 밖이라 Unity 가 임포트하지 않는다.
"""
import functools
import http.server
import os
import socketserver
import sys

# ⚠️⚠️ **찍는 글자가 콘솔을 죽인다.** 윈도 기본 콘솔은 CP949 라 em-dash 하나에
#    `UnicodeEncodeError` 로 **서버가 통째로 안 뜬다**(2026-09-28 에 여기서 실제로 났다 ·
#    `cut_sfx.py` 가 09-21 에 같은 병으로 죽은 적이 있다).
#    📌 **고치는 자리는 둘**이다 — 통로를 utf-8 로 열고(아래), 찍는 말은 **ASCII 로 쓴다**
#       (`main` 의 print 셋). 하나만 하면 다른 기계에서 또 난다.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass                                      # 통로를 못 바꾸는 판이면 ASCII 쪽이 지킨다

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8123

# ⚠️ 경로를 박지 않는다 — 이 파일이 리포 안에 있으므로 **제 자리에서** 리포 뿌리를 찾는다.
#    박아 두면 다른 사람 기계에서 안 돈다.
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Build", "WebGL")
ROOT = os.path.normpath(ROOT)


class Handler(http.server.SimpleHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def end_headers(self):
        if self.path.endswith(".unityweb"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, fmt, *args):
        pass                                  # 한 줄씩 찍으면 리허설 내내 시끄럽다


class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    allow_reuse_address = True


def main():
    if not os.path.isdir(ROOT):
        # ⚠️ 빌드가 없는 것과 서버가 죽은 것은 **다른 일**이다 — 갈라서 말한다.
        print("build not found: %s" % ROOT)
        print("run the WebGL build first.")
        return 1

    with Server(("127.0.0.1", PORT), functools.partial(Handler, directory=ROOT)) as srv:
        print("serving %s on http://localhost:%d (threaded)" % (ROOT, PORT), flush=True)
        srv.serve_forever()
    return 0


if __name__ == "__main__":
    sys.exit(main())
