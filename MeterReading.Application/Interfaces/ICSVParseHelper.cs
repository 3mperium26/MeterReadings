using MeterReading.Application.Dtos;

namespace MeterReading.Application.Interfaces
{
    public interface ICSVParseHelper
    {
        IAsyncEnumerable<MeterReadingCSVRecordDto> ParseMeterReadingsAsync(Stream csvStream);
    }
}