using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeterReading.Application.Dtos
{
    public record MeterReadingCSVRecordDto(
            int RowNumber,
            string? AccountIdCSV,
            string? MeterReadingDateTimeCSV,
            string? MeterReadValueCSV,
            string? ParseError = null
        )
    {
        public bool IsParsed => ParseError == null;
    }
}
