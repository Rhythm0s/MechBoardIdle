# Noto Sans KR — SIL Open Font License 1.1

`NotoSansKR-Regular.ttf` © Google. **SIL Open Font License, Version 1.1** (OFL-1.1)로 배포된다.
전문: <https://scripts.sil.org/OFL>

## 왜 이 폰트인가

WebGL 빌드에는 **시스템 폰트 폴백이 없다.** Unity 내장 GUI 폰트에는 한글 글리프가 없으므로,
빌드에 폰트를 동봉하지 않으면 화면의 한글이 **전부 사라진다**(2026-08-31 브라우저 실측 —
숫자·기호·영문만 남고 한글 글리프 0개).

맑은 고딕·굴림 등 Windows 동봉 폰트는 **재배포 불가**라 쓸 수 없다.
이 빌드는 GitHub Pages로 공개되므로 재배포 가능한 라이선스여야 한다.

OFL-1.1은 **소프트웨어에 임베드해 재배포하는 것을 허용한다.** 조건은
① 폰트 자체를 팔지 않을 것 ② 라이선스 사본을 함께 둘 것(이 파일) ③ 예약 폰트 이름을
쓸 경우 개명할 것 — 셋 다 그대로 지킨다(원본 무수정 동봉).

## 어디서 쓰이는가

`MBI.UI.KoreanFont`가 `Resources.Load`로 읽어 `GUI.skin.font`에 물린다.
IMGUI(OnGUI)를 쓰는 모든 화면이 이 폰트를 탄다.
