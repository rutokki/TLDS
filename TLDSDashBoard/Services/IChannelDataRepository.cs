using TLDSDashBoard.Models;

namespace TLDSDashBoard.Services;

/// <summary>
/// 6채널(주파수/전압) 텔레메트리 이력의 원천. 대시보드는 이미 디스크에 있는 데이터를 보여줄 뿐
/// 스스로 실시간 값을 생성하거나 받지 않는다(실제 _file_log 기반 구현은
/// <see cref="RealTelemetryRepository"/> 참고).
/// </summary>
public interface IChannelDataRepository
{
    /// <summary>주어진 장치의 [start,end](양끝 포함) 구간 행을 오래된 것부터 전부 읽는다. 그 구간 어디에도
    /// 실데이터가 없으면 예외를 던진다 — 호출한 쪽은 이를 "데이터 없음"으로 처리해야 하며, 다른 구간이나
    /// 생성된 데이터로 대체하면 안 된다.</summary>
    ChannelDataSet LoadRange(int deviceIdx, DateTime start, DateTime end);
}
