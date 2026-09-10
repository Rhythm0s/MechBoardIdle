"""IMG-09 — 종류가 형태로 갈리는 것을 한 장에 모은다 (플랜 §25-3).

보드 시트와 품목 시트를 **원본 픽셀 그대로**(최근접) 세로로 잇는다.
크기를 맞추려고 늘리거나 줄이면 도트가 뭉개져 「형태로 갈린다」는 주장 자체가 흐려진다.

    python Docs/portfolio/tools/compose_img09.py
"""

import os

from PIL import Image

BOARD = 'Assets/_Project/Art/Board/_board_sheet.png'
ITEMS = 'Assets/_Project/Art/Items/_items_sheet.png'
OUT = 'Docs/portfolio/img/IMG-09_sheets.png'

GAP = 24
PAD = 24
BG = (18, 18, 22, 255)  # 게임 조립 화면의 바탕과 같은 계열


def main():
    board = Image.open(BOARD).convert('RGBA')
    items = Image.open(ITEMS).convert('RGBA')

    # 배수 확대만 쓴다 — 좁은 쪽을 정수배로 키워 둘의 도트 크기를 비슷하게 맞춘다.
    scale = max(1, board.width // items.width) if items.width < board.width else 1
    if scale > 1:
        items = items.resize((items.width * scale, items.height * scale), Image.NEAREST)

    w = max(board.width, items.width) + PAD * 2
    h = board.height + items.height + GAP + PAD * 2

    out = Image.new('RGBA', (w, h), BG)
    out.paste(board, ((w - board.width) // 2, PAD), board)
    out.paste(items, ((w - items.width) // 2, PAD + board.height + GAP), items)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print(f'{OUT} · {out.size[0]}x{out.size[1]} · 품목 확대 x{scale}')


if __name__ == '__main__':
    main()
