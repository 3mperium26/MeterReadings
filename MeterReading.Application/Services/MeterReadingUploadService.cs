using MeterReading.Application.Dtos;
using MeterReading.Application.Interfaces;
using MeterReading.Domain.Entities;
using MeterReading.Domain.Exceptions;
using MeterReading.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MeterReading.Application.Services
{
    public class MeterReadingUploadService : IMeterReadingUploadService
    {
        private readonly ICSVParseHelper _csvParser;
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<MeterReadingUploadService> _logger;

        public MeterReadingUploadService(
            ICSVParseHelper csvParser,
            IAccountRepository accountRepository,
            ILogger<MeterReadingUploadService> logger)
        {
            _csvParser = csvParser;
            _accountRepository = accountRepository;
            _logger = logger;
        }

        public async Task<MeterReadingUploadResultDto> ProcessUploadAsync(Stream csvStream, string originalFileName, CancellationToken cancellationToken = default)
        {
            var result = new MeterReadingUploadResultDto { FileName = originalFileName };
            var accountsToUpdate = new Dictionary<int, Account>();
            var batchDuplicateCheck = new HashSet<(int AccountId, DateTime ReadingDate, string RawValue)>();

            int totalCSVDataRowsProcessed = 0;
            int nonDomainValidationFailedMeterReadings = 0;
            int meterReadingsSavedToDomain = 0;
            int domainValidationFailedMeterReadings = 0;

            await foreach (var rowDto in _csvParser.ParseMeterReadingsAsync(csvStream).WithCancellation(cancellationToken))
            {
                totalCSVDataRowsProcessed++;
                string errorPrefix = $"Row {rowDto.RowNumber} (AccId: {rowDto.AccountIdCSV ?? "NULL"}, ReadDate: {rowDto.MeterReadingDateTimeCSV ?? "NULL"}, ReadVal: {rowDto.MeterReadValueCSV ?? "NULL"}): ";

                if (!rowDto.IsParsed)
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Parse Error - {rowDto.ParseError}");
                    _logger.LogWarning("Parsing failed for row {RowNumber} in [{FileName}]: {Error}", rowDto.RowNumber, originalFileName, rowDto.ParseError);
                    continue;
                }

                if (!int.TryParse(rowDto.AccountIdCSV, out var accountId))
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Invalid Account ID format [{rowDto.AccountIdCSV}].");
                    continue;
                }

                if (!DateTime.TryParseExact(rowDto.MeterReadingDateTimeCSV, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var MeterReadingDateTime))
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Invalid Meter Reading Date Time format [{rowDto.MeterReadingDateTimeCSV}]. Expected dd/MM/yyyy HH:mm.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rowDto.MeterReadValueCSV))
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Meter Read Value is missing.");
                    continue;
                }

                var batchKey = (accountId, MeterReadingDateTime, rowDto.MeterReadValueCSV);
                if (!batchDuplicateCheck.Add(batchKey))
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Duplicate entry within this batch.");
                    continue;
                }

                Account? account;
                if (!accountsToUpdate.TryGetValue(accountId, out account))
                {
                    account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
                }

                if (account == null)
                {
                    nonDomainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Invalid Account ID [{accountId}]");
                    continue;
                }

                try
                {
                    account.AddMeterReading(MeterReadingDateTime, rowDto.MeterReadValueCSV);
                    meterReadingsSavedToDomain++;

                    if (!accountsToUpdate.ContainsKey(accountId))
                    {
                        accountsToUpdate.Add(accountId, account);
                    }
                }
                catch (ValidationException ex)
                {
                    domainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | {ex.Message}");
                    _logger.LogWarning("Domain validation failed for row {RowNumber}, Account [{AccountId}] in [{FileName}]: {Error}", rowDto.RowNumber, accountId, originalFileName, ex.Message);
                }
                catch (Exception ex)
                {
                    domainValidationFailedMeterReadings++;
                    result.Errors.Add($"{errorPrefix} | Unexpected domain error: {ex.Message}");
                    _logger.LogError(ex, "Unexpected domain error for row {RowNumber}, Account [{AccountId}] in [{FileName}]", rowDto.RowNumber, accountId, originalFileName);
                }
            } 

            result.FailedReadings = nonDomainValidationFailedMeterReadings + domainValidationFailedMeterReadings;

            if (accountsToUpdate.Any())
            {
                _logger.LogInformation("Saving changes for {Count} accounts from [{FileName}].", accountsToUpdate.Count, originalFileName);
                try
                {
                    foreach (var accToUpdate in accountsToUpdate.Values)
                    {
                        await _accountRepository.UpdateAsync(accToUpdate, cancellationToken);
                    }

                    await _accountRepository.SaveChangesAsync(cancellationToken);

                    result.SavedReadings = meterReadingsSavedToDomain;
                    _logger.LogInformation("Saved changes for accounts from [{FileName}]. Total readings saved: {ReadingsAdded}", originalFileName, meterReadingsSavedToDomain);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save batch for accounts from [{FileName}].", originalFileName);
                    result.Errors.Add($"Critical Error: Failed to save meter readings to the database due to a batch update failure: {ex.Message}");
                    result.SavedReadings = 0;
                    result.FailedReadings = totalCSVDataRowsProcessed;
                }
            }
            else
            {
                _logger.LogInformation("No accounts required updates after processing file [{FileName}].", originalFileName);
                result.SavedReadings = meterReadingsSavedToDomain;
            }

            if (meterReadingsSavedToDomain == 0 && !accountsToUpdate.Any())
            {
                result.SavedReadings = 0;
            }

            _logger.LogInformation("[{FileName}] processed. Total Rows: {TotalRows}, Saved Readings: {SuccessCount}, Failed Readings: {FailedCount}",
                originalFileName, totalCSVDataRowsProcessed, result.SavedReadings, result.FailedReadings);

            return result;
        }
    }
}