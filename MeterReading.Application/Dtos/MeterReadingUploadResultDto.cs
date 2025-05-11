namespace MeterReading.Application.Dtos
{
    public class MeterReadingUploadResultDto
    {
        public int SavedReadings { get; set; }
        public int FailedReadings { get; set; }
        public List<string> Errors { get; } = new List<string>();
        public string? FileName { get; set; }
    }
}