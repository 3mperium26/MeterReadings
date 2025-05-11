using MeterReading.Application.Dtos;

namespace MeterReading.Application.Interfaces
{
    public interface IMeterReadingUploadService
    {
        Task<MeterReadingUploadResultDto> ProcessUploadAsync(Stream csvStream, string originalFileName, CancellationToken cancellationToken = default);
    }
}