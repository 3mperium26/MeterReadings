using MeterReading.Application.Dtos;
using MeterReading.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MeterReading.Web.Controllers
{
    [ApiController]
    [Route("api/meter-reading-uploads")]
    public class MeterReadingUploadsController : ControllerBase
    {
        private readonly IMeterReadingUploadService _uploadService;
        private readonly ILogger<MeterReadingUploadsController> _logger;

        public MeterReadingUploadsController(IMeterReadingUploadService uploadService, ILogger<MeterReadingUploadsController> logger)
        {
            _uploadService = uploadService;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(MeterReadingUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadMeterReadings(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(nameof(file), "Please provide a file to upload.");
                return BadRequest(new ValidationProblemDetails(ModelState));
            }
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                !(string.Equals(file.ContentType, "text/csv", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(file.ContentType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(file.ContentType, "application/csv", StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(file), "Invalid file type. Please upload a valid CSV file (.csv).");
                return BadRequest(new ValidationProblemDetails(ModelState));
            }

            _logger.LogInformation("Received file [{FileName}].", file.FileName);
            try
            {
                using var stream = file.OpenReadStream();
                var result = await _uploadService.ProcessUploadAsync(stream, file.FileName, cancellationToken);
                _logger.LogInformation("[{FileName}] uploaded. Success: {SuccessCount}, Failed: {FailedCount}",
                    file.FileName, result.SavedReadings, result.FailedReadings);
                return Ok(result);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Upload for [{FileName}] was canceled.", file.FileName);
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Upload operation was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload Exception: Unexpected error processing file [{FileName}].", file.FileName);
                return StatusCode(StatusCodes.Status500InternalServerError,
                     new ProblemDetails { Title = "Internal Server Error", Detail = $"An unexpected error occurred: {ex.Message}", Status = StatusCodes.Status500InternalServerError });
            }
        }
    }
}