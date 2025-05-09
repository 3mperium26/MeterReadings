namespace MeterReading.Domain.Entities
{
    public class MeterReading
    {
        public long Id { get; private set; }
        public int AccountId { get; private set; }
        public DateTime MeterReadingDateTime { get; private set; }
        public int MeterReadValue { get; private set; }
        private MeterReading() { }

        internal static MeterReading Create(int accountId, DateTime meterReadingDateTime, int meterReadValue)
        {
            return new MeterReading
            {
                AccountId = accountId,
                MeterReadingDateTime = meterReadingDateTime,
                MeterReadValue = meterReadValue
            };
        }

        public override bool Equals(object? meterReading)
        {
            if (meterReading is not MeterReading other) return false;
            if (ReferenceEquals(this, other)) return true;
            return AccountId == other.AccountId &&
                   MeterReadingDateTime == other.MeterReadingDateTime &&
                   MeterReadValue == other.MeterReadValue;
        }
        public override int GetHashCode()
        {
            // Fingerprint
            if (Id != 0) return Id.GetHashCode();
            return HashCode.Combine(AccountId, MeterReadingDateTime, MeterReadValue);
        }
    }
}
