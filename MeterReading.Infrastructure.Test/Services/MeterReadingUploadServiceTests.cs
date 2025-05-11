using MeterReading.Application.Dtos;
using MeterReading.Application.Interfaces;
using MeterReading.Application.Services;
using MeterReading.Domain.Entities;
using MeterReading.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace MeterReading.Infrastructure.Tests.Services
{
    [TestClass]
    public class MeterReadingUploadServiceTests
    {
        private Mock<ICSVParseHelper> _mockCsvParser = null!;
        private Mock<IAccountRepository> _mockAccountRepository = null!;
        private Mock<ILogger<MeterReadingUploadService>> _mockLogger = null!;
        private MeterReadingUploadService _service = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockCsvParser = new Mock<ICSVParseHelper>();
            _mockAccountRepository = new Mock<IAccountRepository>();
            _mockLogger = new Mock<ILogger<MeterReadingUploadService>>();
            _service = new MeterReadingUploadService(
                _mockCsvParser.Object,
                _mockAccountRepository.Object,
                _mockLogger.Object);
        }

        private MemoryStream CreateStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

        private async IAsyncEnumerable<MeterReadingCSVRecordDto> CreateCsvRowDtosAsync(IEnumerable<MeterReadingCSVRecordDto> dtos)
        {
            foreach (var dto in dtos)
            {
                yield return dto;
            }
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task ProcessUploadAsync_ValidRows_CallsUpdateAndSaveChangesSuccessfully()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, "111", "22/04/2025 09:30", "11111"),
                new MeterReadingCSVRecordDto(3, "222", "23/04/2025 10:00", "22222")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            var account111 = new Account(111, "Test", "User1");
            var account222 = new Account(222, "Test", "User2");
            _mockAccountRepository.Setup(r => r.GetByIdAsync(111, It.IsAny<CancellationToken>())).ReturnsAsync(account111);
            _mockAccountRepository.Setup(r => r.GetByIdAsync(222, It.IsAny<CancellationToken>())).ReturnsAsync(account222);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockAccountRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

            using var stream = CreateStream("dummy,csv,content");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "test.csv");

            // Assert
            Assert.AreEqual(2, result.SavedReadings, "SavedReadings count mismatch.");
            Assert.AreEqual(0, result.FailedReadings, "FailedReadings should be 0.");
            Assert.IsTrue(result.Errors.Count == 0, $"Expected no errors, but got: {string.Join(", ", result.Errors)}");

            _mockAccountRepository.Verify(r => r.UpdateAsync(account111, It.IsAny<CancellationToken>()), Times.Once);
            _mockAccountRepository.Verify(r => r.UpdateAsync(account222, It.IsAny<CancellationToken>()), Times.Once);
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessUploadAsync_RowWithParsingError_CountsAsFailed_DoesNotCallSaveChanges()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, null, null, null, "Bad CSV format")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            using var stream = CreateStream("bad,csv");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "bad.csv");

            // Assert
            Assert.AreEqual(0, result.SavedReadings);
            Assert.AreEqual(1, result.FailedReadings);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains(result.Errors[0], "Parse Error - Bad CSV format");
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }


        [TestMethod]
        public async Task ProcessUploadAsync_AccountNotFound_CountsAsFailed_AttemptsSaveChangesForOtherValidAccounts()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, "123", "22/04/2025 09:30", "00123"),
                new MeterReadingCSVRecordDto(3, "999", "23/04/2025 10:00", "00456")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            var account123 = new Account(123, "Test", "User1");
            _mockAccountRepository.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(account123);
            _mockAccountRepository.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
            _mockAccountRepository.Setup(r => r.UpdateAsync(account123, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockAccountRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);


            using var stream = CreateStream("dummy");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "test.csv");

            // Assert
            Assert.AreEqual(1, result.SavedReadings);
            Assert.AreEqual(1, result.FailedReadings);
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains(result.Errors[0], "Invalid Account ID [999]");
            _mockAccountRepository.Verify(r => r.UpdateAsync(account123, It.IsAny<CancellationToken>()), Times.Once);
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessUploadAsync_DomainValidationException_CountsAsFailed_AttemptsSaveChangesForOtherValidAccounts()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, "123", "22/04/2025 09:30", "00123"),
                new MeterReadingCSVRecordDto(3, "456", "23/04/2025 10:00", "INVALID_VALUE")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            var account123 = new Account(123, "Test", "User1");
            var account456 = new Account(456, "Test", "User2");
            _mockAccountRepository.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(account123);
            _mockAccountRepository.Setup(r => r.GetByIdAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync(account456);
            _mockAccountRepository.Setup(r => r.UpdateAsync(account123, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockAccountRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);


            using var stream = CreateStream("dummy");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "test.csv");

            // Assert
            Assert.AreEqual(1, result.SavedReadings);
            Assert.AreEqual(1, result.FailedReadings);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid meter read value format: 'INVALID_VALUE'")));
            _mockAccountRepository.Verify(r => r.UpdateAsync(account123, It.IsAny<CancellationToken>()), Times.Once);
            _mockAccountRepository.Verify(r => r.UpdateAsync(account456, It.IsAny<CancellationToken>()), Times.Never);
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessUploadAsync_SaveChangesAsyncThrows_AllAttemptedDomainAdditionsFail()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, "123", "22/04/2025 09:30", "00123"),
                new MeterReadingCSVRecordDto(3, "456", "23/04/2025 10:00", "00456")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            var account123 = new Account(123, "Test", "User1");
            var account456 = new Account(456, "Test", "User2");
            _mockAccountRepository.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(account123);
            _mockAccountRepository.Setup(r => r.GetByIdAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync(account456);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockAccountRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("DB Save Failed"));

            using var stream = CreateStream("dummy");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "test.csv");

            // Assert
            Assert.AreEqual(0, result.SavedReadings, "SavedReadings should be 0 if SaveChangesAsync fails.");
            Assert.AreEqual(2, result.FailedReadings, "All rows should be marked as failed if batch save fails.");
            Assert.AreEqual(1, result.Errors.Count);
            StringAssert.Contains(result.Errors[0], "Critical Error: Failed to save meter readings");
            StringAssert.Contains(result.Errors[0], "DB Save Failed");
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessUploadAsync_NoValidReadingsToPersist_DoesNotCallSaveChanges()
        {
            // Arrange
            var rows = new List<MeterReadingCSVRecordDto>
            {
                new MeterReadingCSVRecordDto(2, "123", "INVALID_DATE", "00123"),
                new MeterReadingCSVRecordDto(3, "456", "23/04/2025 10:00", "ABCDE")
            };
            _mockCsvParser.Setup(p => p.ParseMeterReadingsAsync(It.IsAny<Stream>()))
                          .Returns(CreateCsvRowDtosAsync(rows));

            var account456 = new Account(456, "Test", "User2");
            _mockAccountRepository.Setup(r => r.GetByIdAsync(456, It.IsAny<CancellationToken>())).ReturnsAsync(account456);

            using var stream = CreateStream("dummy");

            // Act
            var result = await _service.ProcessUploadAsync(stream, "test.csv");

            // Assert
            Assert.AreEqual(0, result.SavedReadings);
            Assert.AreEqual(2, result.FailedReadings);
            Assert.AreEqual(2, result.Errors.Count);
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}