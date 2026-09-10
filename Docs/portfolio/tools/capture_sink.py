"""캡처 수신 서버 — 브라우저가 캔버스에서 뽑은 PNG 를 파일로 받는다.

**왜 이것이 필요한가.** 웹빌드 화면을 정확한 규격(1440x2560)으로 남기는 길은
캔버스 백버퍼를 그대로 읽는 것뿐이다. 브라우저 창 스크린샷은 미리보기 패널이
자기 크기에 맞춰 줄여 잡아서 규격이 안 나온다. 그런데 페이지가 스스로 파일을
저장하는 길(`<a download>`)은 막혀 있으므로, **받아 줄 자리를 하나 연다.**

    python Docs/portfolio/tools/capture_sink.py [--port 8010] [--out Docs/portfolio/img]

브라우저 쪽에서는 같은 폴더의 `capture_snippet.js` 를 쓴다.

⚠️ **127.0.0.1 에만 묶는다.** 이 서버는 받은 것을 그대로 디스크에 쓰므로
바깥에 열어 두지 않는다. 캡처가 끝나면 끈다.
"""

import argparse
import base64
import json
import os
import re
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

# 이름은 우리가 정한 꼴만 받는다 — 경로 조작(`../`)이 파일 이름으로 들어오는 것을 막는다.
NAME_OK = re.compile(r'^IMG-\d{2}[A-Za-z0-9_.-]*\.png$')

MAX_BYTES = 32 * 1024 * 1024  # 1440x2560 PNG 는 한참 아래다. 폭주만 막는다.


class Sink(BaseHTTPRequestHandler):
    out_dir = '.'

    def _cors(self):
        # 게임은 127.0.0.1:8000, 이 서버는 다른 포트라 브라우저가 교차 출처로 본다.
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.send_header('Access-Control-Allow-Methods', 'POST, OPTIONS')

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_POST(self):
        try:
            length = int(self.headers.get('Content-Length') or 0)
        except ValueError:
            length = 0
        if length <= 0 or length > MAX_BYTES:
            return self._fail(413, f'길이가 이상하다: {length}')

        try:
            payload = json.loads(self.rfile.read(length).decode('utf-8'))
        except Exception as e:
            return self._fail(400, f'JSON 아님: {e}')

        name = str(payload.get('name', ''))
        if not NAME_OK.match(name):
            return self._fail(400, f'이름 꼴이 아니다: {name!r} (IMG-nn_이름.png)')

        data = str(payload.get('png', ''))
        if not data.startswith('data:image/png;base64,'):
            return self._fail(400, 'png 는 data:image/png;base64, 로 시작해야 한다')

        try:
            raw = base64.b64decode(data.split(',', 1)[1])
        except Exception as e:
            return self._fail(400, f'base64 아님: {e}')

        # PNG 서명을 확인한다 — 빈 캔버스나 잘린 전송을 여기서 잡는다.
        if not raw.startswith(b'\x89PNG\r\n\x1a\n'):
            return self._fail(400, 'PNG 서명이 없다')

        path = os.path.join(self.out_dir, name)
        with open(path, 'wb') as f:
            f.write(raw)

        w, h = png_size(raw)
        msg = json.dumps({'saved': name, 'bytes': len(raw), 'width': w, 'height': h})
        print(f'[sink] {name} · {len(raw)} bytes · {w}x{h}', flush=True)

        body = msg.encode('utf-8')
        self.send_response(200)
        self._cors()
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _fail(self, code, why):
        body = json.dumps({'error': why}).encode('utf-8')
        self.send_response(code)
        self._cors()
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        print(f'[sink] 거절 {code} — {why}', flush=True)

    def log_message(self, *_):
        pass  # 기본 접근 로그는 끈다. 위에서 필요한 것만 찍는다.


def png_size(raw):
    """IHDR 에서 폭·높이를 읽는다 — 규격이 맞는지 저장 쪽에서 바로 확인한다."""
    if len(raw) < 24:
        return 0, 0
    return int.from_bytes(raw[16:20], 'big'), int.from_bytes(raw[20:24], 'big')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--port', type=int, default=8010)
    ap.add_argument('--out', default='Docs/portfolio/img')
    args = ap.parse_args()

    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    Sink.out_dir = out

    print(f'[sink] 127.0.0.1:{args.port} → {out}', flush=True)
    HTTPServer(('127.0.0.1', args.port), Sink).serve_forever()


if __name__ == '__main__':
    sys.exit(main())
