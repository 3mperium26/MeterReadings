using System.Globalization;
using MeterReading.Application.Interfaces;
using MeterReading.Application.Dtos;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;

namespace MeterReading.Infrastructure.CSVParsing
{
    public class CSVParseHelper : ICSVParseHelper
    {
        private readonly ILogger<CSVParseHelper> _logger;

        public CSVParseHelper(ILogger<CSVParseHelper> logger)
        {
            _logger = logger;
        }

        public async IAsyncEnumerable<MeterReadingCSVRecordDto> ParseMeterReadingsAsync(Stream csvStream)
        {
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.Trim,
            };

            var reader = new StreamReader(csvStream);
            var csv = new CsvReader(reader, csvConfig);

            await csv.ReadAsync();
            csv.ReadHeader();

            int rowNumber = 1;
            while (await csv.ReadAsync())
            {
                rowNumber++;
                string? accountIdCSV = null;
                string? dateTimeCSV = null;
                string? valueCSV = null;
                string? parseError = null;

                try
                {
                    accountIdCSV = csv.GetField<string>("AccountId");
                    dateTimeCSV = csv.GetField<string>("MeterReadingDateTime");
                    valueCSV = csv.GetField<string>("MeterReadValue");
                }
                catch (CsvHelper.MissingFieldException ex)
                {
                    parseError = $"Missing field: {ex.Message}";
                    _logger.LogWarning(ex, "CSV parsing error at data row {RowNumber} due to missing field.", rowNumber);
                }
                catch (CsvHelperException ex)
                {
                    parseError = $"CSV parsing error: {ex.Message}";
                    _logger.LogWarning(ex, "CSV parsing error at data row {RowNumber}.", rowNumber);
                }
                catch (Exception ex)
                {
                    parseError = $"Unexpected error parsing row: {ex.Message}";
                    _logger.LogError(ex, "Unexpected error parsing CSV data row {RowNumber}.", rowNumber);
                }
                yield return new MeterReadingCSVRecordDto(rowNumber, accountIdCSV, dateTimeCSV, valueCSV, parseError);
            }
        }
    }
}