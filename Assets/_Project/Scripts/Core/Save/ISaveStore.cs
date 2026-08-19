namespace MBI.Core
{
    /// <summary>
    /// 세이브 저장소 계약(§5-7). 구현은 플랫폼 쪽(WebGL = 브라우저 저장소)이 맡고,
    /// 순수 로직은 이 인터페이스만 안다 — 그래야 오프라인 정산을 디스크 없이 테스트할 수 있다.
    ///
    /// ⚠️ 원자적 교체(temp write → File.Replace)는 웹빌드에 그런 개념이 없어 계약에 넣지 않는다.
    /// 견고성 항목(부패 폴백·시계 롤백 방어 등)은 2026-08-18 컷 — 판단 기준은 "영상 촬영 중에 깨지는가".
    /// 다만 **파일이 아직 없는 첫 실행**은 정상 경로이므로 TryLoad가 예외를 던지지 않고 false를 준다.
    /// </summary>
    public interface ISaveStore
    {
        bool TryLoad(out SaveDataV1 data);
        void Save(SaveDataV1 data);
        void Delete();
    }
}
