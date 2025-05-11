using System.Text.RegularExpressions;
using MeterReading.Domain.Exceptions;

namespace MeterReading.Domain.Entities
{
    public partial class Account
    {
        private static readonly Regex MeterReadValueNNNNNRegexValidation = new(@"^\d{1,5}$", RegexOptions.Compiled);
        private readonly List<MeterReading> _meterReadings = [];

        public int AccountId { get; private set; }
        public string? FirstName { get; private set; }
        public string? LastName { get; private set; }
        public IReadOnlyCollection<MeterReading> MeterReadings => _meterReadings.AsReadOnly();

        private Account() { }

        public Account(int accountId, string firstName, string lastName)
        {
            #region Validation
            if (accountId <= 0) throw new ValidationException($"Invalid Account Id: '{accountId}'");
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)) throw new ValidationException("First name / Last name cannot be empty.");
            #endregion
            
            AccountId = accountId;
            FirstName = firstName;
            LastName = lastName;
        }

        public void AddMeterReading(DateTime meterReadingDateTime, string meterReadValue)
        {
            #region Validation
            var latestRead = _meterReadings.OrderByDescending(r => r.MeterReadingDateTime).FirstOrDefault();
            if (latestRead != null && meterReadingDateTime < latestRead.MeterReadingDateTime)
            {
                throw new ValidationException($"New meter reading date ({meterReadingDateTime:dd/MM/yyyy HH:mm}) is older than the latest existing meter reading date ({latestRead.MeterReadingDateTime:dd/MM/yyyy HH:mm}).");
            }

            if (string.IsNullOrWhiteSpace(meterReadValue) || !MeterReadValueNNNNNRegexValidation.IsMatch(meterReadValue)){throw new ValidationException($"Invalid meter read value format: '{meterReadValue}'. Must be NNNNN (1-5 digits).");}

            if (!int.TryParse(meterReadValue, out var iMeterReadValue)){throw new ValidationException($"Meter read value '{meterReadValue}' is not an integer.");}

            var newMeterReading = MeterReading.Create(this.AccountId, meterReadingDateTime, iMeterReadValue);
            if (_meterReadings.Any(r => r.Equals(newMeterReading)))
            {
                throw new ValidationException("Duplicate meter reading.");
            }
            #endregion

            _meterReadings.Add(newMeterReading);
        }

        internal void LoadReadings(IEnumerable<MeterReading> readings)
        {
            _meterReadings.Clear();
            _meterReadings.AddRange(readings);
        }
    }
}