namespace TLDSDashBoard.Models;

/// <summary>어떤 <see cref="Station"/>에 속한 궤도회로(예: "3T", "21AT"). 시작할 때 TLDS 설치의
/// _file_system/index_{역}.xml(+ 배선 정보는 rack_{역}.xml)에서 읽는다 — Services/StationConfigLoader.cs 참고.</summary>
public sealed class TrackCircuit
{
    /// <summary>"{역 번호}:{이름}" — 두 역이 같은 궤도 이름을 쓰더라도 유일하다.</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string StationId { get; init; }

    /// <summary>숫자 장치 인덱스 — 실제 DB의 `Idx`(텔레메트리 로그) / `ObjectIndex`(SetValue.db 임계값) 컬럼,
    /// 그리고 index_{역}.xml 자체의 `index` 속성과 일치한다. 실제 리포지토리가 키로 쓰는 값이며,
    /// Id/Name은 표시용일 뿐이다.</summary>
    public required int Idx { get; init; }

    /// <summary>index xml의 `level_type`: "IMP_I"(임펄스 방식) 또는 "AF_BS"(AF/가청주파수 방식). 참고용
    /// (랙 힌트에 표시됨) — 모든 궤도는 동일한 6개 ChannelCatalog 채널로 읽는다.</summary>
    public required string LevelType { get; init; }

    /// <summary>index xml `type`: NT(일반)/PT(선로전환기 구간)/BT(폐색). Empty for the "전체 궤도" sentinel.</summary>
    public string TrackType { get; init; } = "";

    /// <summary>이 궤도가 연결된 임펄스 랙(rack_{역}.xml 기준) — 거기에 없으면 null
    /// (예: AF 궤도, 또는 랙 파일이 다루지 않는 궤도).</summary>
    public string? RackName { get; init; }
    public int? RackNo { get; init; }
    public int? RackSlot { get; init; }

    /// <summary>"임펄스랙3번 · 슬롯 1 · IMP_I" style one-liner for the device filter hint.</summary>
    public string RackLabel =>
        (RackName is null ? "랙 정보 없음" : RackSlot is { } s ? $"{RackName} · 슬롯 {s}" : RackName)
        + (LevelType.Length > 0 ? $" · {LevelType}" : "");
}
