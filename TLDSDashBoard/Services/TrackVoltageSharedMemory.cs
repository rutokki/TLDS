using System.Buffers.Binary;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace TLDSDashBoard.Services;

/// <summary>
/// 다른 exe가 Windows 이름 있는 공유메모리(메모리 맵 파일)에 써 둔 궤도 인덱스와 측정값을 읽는다.
/// 쓰는 쪽과의 약속이므로 이름·배치를 바꾸면 쓰는 쪽도 같이 바꿔야 한다.
///
/// 배치(모두 little-endian, 기본 정렬 그대로 — #pragma pack 없이 C 구조체로 읽고 쓸 수 있다):
///   헤더 8바이트
///     +0  int32   Sequence — 쓰는 동안 홀수, 쓰기가 끝나면 짝수. 읽는 쪽은 읽기 전후의 값이 같고 짝수일 때만
///                            그 데이터를 믿는다(앞뒤가 다르거나 홀수면 다시 읽기).
///     +4  int32   Count    — 아래 레코드 수(0..512)
///   레코드(Count개, +8부터, 14바이트씩)
///     +0  uint16  Idx      — 궤도 인덱스(index_{역}.xml의 index = DB의 Idx/ObjectIndex)
///     +2  uint16[6]        — 측정값, ChannelCatalog 순서(궤도 주파수, 차상 주파수, 지상 주파수, 송신 전압, 수신A 전압,
///                            수신B 전압)의 **원시 정수** — 실제 값 = 원시값 × 배율: 1, 1, 0.01, 0.1, 0.01, 0.01
///                            (_file_log의 Value0..5와 같은 단위: 예) 1590 → 1590 Hz, 320 → 3.2 Hz, 1234 → 123.4 V,
///                            123 → 1.23 V). 읽는 쪽이 배율을 곱해 Hz/V로 바꾼다.
/// 측정값이 없는 궤도는 레코드에 넣지 않는다(읽는 쪽은 레코드가 없는 궤도의 측정 칸을 비운다).
/// 헤더에 버전/크기 필드가 없으므로, 배치를 바꿀 때는 공유메모리 이름(<see cref="MapName"/>)도 함께 바꿔 서로 다른
/// 배치끼리 만나지 않게 한다.
/// </summary>
public static class TrackVoltageSharedMemory
{
    /// <summary>공유메모리 이름. 접두사가 없으면 같은 로그인 세션(Local) 안에서만 보인다 — 다른 세션의 프로세스와
    /// 공유해야 하면 "Global\\TLDS_TrackVoltage"로 바꾸고 양쪽 모두 같은 이름을 쓴다.</summary>
    public const string MapName = "TLDS_TrackVoltage";

    public const int HeaderSize = 8;
    public const int RecordSize = 14;
    /// <summary>한 번에 담을 수 있는 최대 궤도 수.</summary>
    public const int MaxRecords = 512;
    /// <summary>쓰는 쪽이 만들어야 하는 공유메모리의 최소 크기(바이트) = 8 + 512 × 14.</summary>
    public const int TotalSize = HeaderSize + MaxRecords * RecordSize;

    /// <summary>공유메모리에서 궤도 인덱스 → 측정값(ChannelCatalog 순서, Hz/V 실제 단위)을 읽는다. 쓰는 쪽이 아직
    /// 공유메모리를 만들지 않았으면 null. 배치가 맞지 않거나 쓰는 중이라 일관된 데이터를 얻지 못하면 예외.
    /// 스냅샷 전체를 한 번에 복사한 뒤 번호를 확인하므로 쓰기와 겹칠 수 있는 구간이 아주 짧다.</summary>
    public static IReadOnlyDictionary<int, double[]>? Read()
    {
        MemoryMappedFile map;
        try { map = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read); }
        catch (FileNotFoundException) { return null; }

        using (map)
        using (var view = map.CreateViewAccessor(0, TotalSize, MemoryMappedFileAccess.Read))
        {
            var buffer = new byte[TotalSize];
            // 쓰는 도중에 읽은 데이터는 버리고 다시 읽는다(시퀀스 락).
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int before = view.ReadInt32(0);
                if (before % 2 != 0) { Thread.Sleep(2); continue; }

                int count = view.ReadInt32(4);
                if (count < 0 || count > MaxRecords)
                    throw new InvalidDataException($"공유메모리 레코드 수가 올바르지 않습니다 ({count}개, 최대 {MaxRecords}개).");

                int length = HeaderSize + count * RecordSize;
                view.ReadArray(0, buffer, 0, length);   // 스냅샷 전체를 한 번에 복사

                if (view.ReadInt32(0) != before || BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4)) != count) continue;

                var result = new Dictionary<int, double[]>(count);
                for (int i = 0; i < count; i++)
                {
                    var record = buffer.AsSpan(HeaderSize + i * RecordSize, RecordSize);
                    var values = new double[ChannelCatalog.ChannelCount];
                    for (int c = 0; c < values.Length; c++)
                    {
                        int raw = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(2 + c * 2));
                        // 규격 해상도로 반올림해서 320 × 0.01이 3.2000000000000002가 아니라 3.2가 되게 한다.
                        values[c] = Math.Round(raw * ChannelCatalog.ChannelScaleAt(c),
                            ChannelCatalog.ChannelDecimals(ChannelCatalog.ChannelIds[c]));
                    }
                    result[BinaryPrimitives.ReadUInt16LittleEndian(record)] = values;
                }
                return result;
            }
        }
        throw new IOException("공유메모리가 계속 쓰기 중이라 일관된 값을 읽지 못했습니다.");
    }

#if DEBUG
    private static readonly object Gate = new();
    private static MemoryMappedFile? _map;

    /// <summary>[테스트 전용 — Debug 빌드에만 있다] 샘플 데이터를 만들어 읽기 처리를 시험하기 위해 위 배치대로 쓴다.
    /// 값은 실제 단위(Hz/V)로 받아 원시 정수로 바꿔 쓴다(실제 값 ÷ 배율, 0..65535로 제한).
    /// 이름 있는 메모리 맵은 그것을 연 프로세스가 모두 끝나면 사라지므로, 이 프로세스가 사는 동안 핸들을 들고 있는다.
    /// 실제 배포(Release)에는 포함되지 않는다 — 실제로는 다른 exe가 쓴다.</summary>
    public static int Write(IReadOnlyList<(int Idx, double[] Values)> records)
    {
        if (records.Count > MaxRecords)
            throw new InvalidOperationException($"궤도가 {records.Count}개라 공유메모리 최대치({MaxRecords}개)를 넘습니다.");

        var buffer = new byte[records.Count * RecordSize];
        for (int i = 0; i < records.Count; i++)
        {
            var record = buffer.AsSpan(i * RecordSize, RecordSize);
            BinaryPrimitives.WriteUInt16LittleEndian(record, (ushort)Math.Clamp(records[i].Idx, 0, ushort.MaxValue));
            for (int c = 0; c < ChannelCatalog.ChannelCount; c++)
            {
                double raw = Math.Round(records[i].Values[c] / ChannelCatalog.ChannelScaleAt(c));
                BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(2 + c * 2), (ushort)Math.Clamp(raw, 0, ushort.MaxValue));
            }
        }

        lock (Gate)
        {
            _map ??= MemoryMappedFile.CreateOrOpen(MapName, TotalSize);
            using var view = _map.CreateViewAccessor(0, TotalSize);

            int seq = view.ReadInt32(0);
            if (seq % 2 != 0) seq++;            // 이전 쓰기가 중간에 끊긴 경우를 바로잡는다
            view.Write(0, seq + 1);             // 쓰기 시작: 홀수
            view.Write(4, records.Count);
            view.WriteArray(HeaderSize, buffer, 0, buffer.Length);
            view.Flush();
            view.Write(0, seq + 2);             // 쓰기 끝: 짝수
        }
        return records.Count;
    }
#endif
}
