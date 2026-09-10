// 캔버스 캡처 — 게임 페이지(127.0.0.1:8000)의 콘솔에서 돌린다.
//
// 왜 캔버스에서 뽑는가: 미리보기 패널의 창 스크린샷은 패널 크기에 맞춰 줄여 잡아
// 규격(1440x2560)이 안 나온다. 캔버스 백버퍼는 창을 1440x2560 으로 맞추면
// 정확히 그 크기다.
//
// 쓰는 법
//   await mbiShot('IMG-01_board_full.png')                 // 풀샷
//   await mbiShot('IMG-07_panel.png', {x:0,y:2100,w:1440,h:460})  // 크롭
//
// ⚠️ 먼저 창을 1440x2560 으로 맞춘다. mbiSize() 로 확인한다.

window.mbiSize = () => {
  const c = document.querySelector('canvas');
  return { canvas: [c.width, c.height], css: [c.clientWidth, c.clientHeight], dpr: devicePixelRatio };
};

// crop 은 캔버스 픽셀 좌표다(1440x2560 기준). 없으면 전체.
window.mbiShot = async (name, crop, sink = 'http://127.0.0.1:8010/') => {
  const c = document.querySelector('canvas');
  if (!c) throw new Error('캔버스가 없다');
  if (c.width !== 1440 || c.height !== 2560) {
    // 막지는 않는다 — 규격이 아니라는 것만 분명히 알린다. 판정은 사람이 한다.
    console.warn(`[mbi] 규격이 아니다: ${c.width}x${c.height} (1440x2560 이어야 한다)`);
  }

  let src = c;
  if (crop) {
    const cut = document.createElement('canvas');
    cut.width = crop.w;
    cut.height = crop.h;
    cut.getContext('2d').drawImage(c, crop.x, crop.y, crop.w, crop.h, 0, 0, crop.w, crop.h);
    src = cut;
  }

  const png = src.toDataURL('image/png');
  const res = await fetch(sink, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, png })
  });
  const out = await res.json();
  if (!res.ok) throw new Error(out.error || '저장 실패');
  return out;
};
