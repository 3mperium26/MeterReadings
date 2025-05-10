using Moq;
using Microsoft.Extensions.Logging;
using MeterReading.Infrastructure.CSVParsing;
using System.Text;


namespace MeterReading.Infrastructure.Tests.CSVParsing
{
    [TestClass]
    public class CSVParseHelperTests
    {
        private Mock<ILogger<CSVParseHelper>> _mockLogger = null!;
        private CSVParseHelper _parser = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<CSVParseHelper>>();
            _parser = new CSVParseHelper(_mockLogger.Object);
        }

        private MemoryStream CreateStreamFromString(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        [TestMethod]
        public async Task ParseMeterReadingsAsync_ValidCsv_ReturnsCorrectDtos()
        {
            // Arrange
            var csvContent = """
                             AccountId,MeterReadingDateTime,MeterReadValue
                             1111,22/05/2025 05:24,10001
                             9999,23/05/2025 05:30,99999
                             """;
            using var stream = CreateStreamFromString(csvContent);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(2, results.Count, "Should parse 2 data rows.");

            var firstRow = results[0];
            Assert.IsTrue(firstRow.IsParsed, "1st row should be parsed ok.");
            Assert.AreEqual(2, firstRow.RowNumber, "1st data row number should be 2.");
            Assert.AreEqual("1111", firstRow.AccountIdCSV);
            Assert.AreEqual("22/05/2025 05:24", firstRow.MeterReadingDateTimeCSV);
            Assert.AreEqual("10001", firstRow.MeterReadValueCSV);
            Assert.IsNull(firstRow.ParseError, "1st row parse error is not null.");

            var secondRow = results[1];
            Assert.IsTrue(secondRow.IsParsed, "2nd row should be parsed ok.");
            Assert.AreEqual(3, secondRow.RowNumber, "2nd data row number should be 3.");
            Assert.AreEqual("9999", secondRow.AccountIdCSV);
            Assert.AreEqual("23/05/2025 05:30", secondRow.MeterReadingDateTimeCSV);
            Assert.AreEqual("99999", secondRow.MeterReadValueCSV);
            Assert.IsNull(secondRow.ParseError, "2nd row parse error is not null.");
        }

        [TestMethod]
        public async Task ParseMeterReadingsAsync_CsvWithMissingField_ReturnsDtoWithNullForMissingFieldAndNoError()
        {
            // Arrange: MeterReadValue is missing in the first data row
            var csvContent = """
                             AccountId,MeterReadingDateTime,MeterReadValue
                             8888,22/04/2025 09:24
                             1234,23/04/2025 10:30,00001
                             """;
            using var stream = CreateStreamFromString(csvContent);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(2, results.Count);

            var firstRow = results[0];
            Assert.IsFalse(firstRow.IsParsed, "First row should indicate parsing issue due to missing field.");
            Assert.AreEqual(2, firstRow.RowNumber, "1st data row number should be 2.");
            Assert.AreEqual("8888", firstRow.AccountIdCSV);
            Assert.AreEqual("22/04/2025 09:24", firstRow.MeterReadingDateTimeCSV);
            Assert.IsNull(firstRow.MeterReadValueCSV, "Meter Read Value should be null in 1st row.");
            Assert.IsNotNull(firstRow.ParseError, "Parse Error is empty.");
            StringAssert.Contains(firstRow.ParseError, "fields are empty", "Parse error message should contains [fields are empty].");


            var secondRow = results[1];
            Assert.IsTrue(secondRow.IsParsed, "Second row should be parsed ok.");
            Assert.AreEqual("00001", secondRow.MeterReadValueCSV);
        }

        [TestMethod]
        public async Task ParseMeterReadingsAsync_CsvWithExtraField_ReturnsDtoWithFieldsAndNoError()
        {
            // Arrange: First data row has an extra field
            var csvContent = """
                             AccountId,MeterReadingDateTime,MeterReadValue
                             8888,22/04/2025 08:24,11110,TEST_EXTRA_DATA
                             5678,23/04/2019 10:30,12345
                             """;
            using var stream = CreateStreamFromString(csvContent);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(2, results.Count);

            var firstRow = results[0];
            Assert.IsTrue(firstRow.IsParsed, "1st row should parse ok and ignore extra data ");
            Assert.AreEqual(2, firstRow.RowNumber, "1st data row number should be 2.");
            Assert.AreEqual("8888", firstRow.AccountIdCSV);
            Assert.AreEqual("22/04/2025 08:24", firstRow.MeterReadingDateTimeCSV);
            Assert.AreEqual("11110", firstRow.MeterReadValueCSV);
            Assert.IsNull(firstRow.ParseError, "Parse Error is not empty.");
        }


        [TestMethod]
        public async Task ParseMeterReadingsAsync_EmptyCsvStream_ReturnsEmpty()
        {
            // Arrange
            var csvContent = "";
            using var stream = CreateStreamFromString(csvContent);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(0, results.Count, "Empty stream detected. No data should populated.");
        }

        [TestMethod]
        public async Task ParseMeterReadingsAsync_HeaderOnlyCsvStream_ReturnsEmpty()
        {
            // Arrange
            var csvContent = "AccountId,MeterReadingDateTime,MeterReadValue";
            using var stream = CreateStreamFromString(csvContent);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(0, results.Count, "Header-only stream detected. No data should populated.");
        }

        [TestMethod]
        public async Task ParseMeterReadingsAsync_MalformedRow_ReturnsDtoWithParseError()
        {
            // Arrange
            var csvContentMalformed = """
                                          ColA,ColB,ColC
                                          ValA,ValB,ValC
                                          """;
            using var stream = CreateStreamFromString(csvContentMalformed);

            // Act
            var results = await _parser.ParseMeterReadingsAsync(stream).ToListAsync();

            // Assert
            Assert.AreEqual(1, results.Count);
            var firstRow = results[0];
            Assert.IsFalse(firstRow.IsParsed);
            Assert.IsNotNull(firstRow.ParseError);
            StringAssert.Contains(firstRow.ParseError, "fields are empty", "Parse error message should contains [fields are empty].");
        }
    }
}