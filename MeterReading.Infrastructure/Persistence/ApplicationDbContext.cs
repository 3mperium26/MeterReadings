using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;
using MeterReading.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeterReading.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Domain.Entities.MeterReading> MeterReadings { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new AccountConfiguration());
            modelBuilder.ApplyConfiguration(new MeterReadingConfiguration());

            SeedAccounts(modelBuilder);
        }

        private void SeedAccounts(ModelBuilder modelBuilder)
        {
            var accountsToSeed = new List<Account>();
            var filePath = Path.Combine(AppContext.BaseDirectory, "DataSeed", "Test_Accounts.csv");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: Seed file not found at {filePath}. Skipping account seeding.");
                return;
            }

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, csvConfig);
                var records = csv.GetRecords<dynamic>().ToList();

                foreach (var record in records)
                {
                    var rec = (IDictionary<string, object>)record;
                    if (int.TryParse(Convert.ToString(rec["AccountId"]), out int AccountId) &&
                        rec.ContainsKey("FirstName") && rec.ContainsKey("LastName"))
                    {
                        accountsToSeed.Add(new Account(
                            AccountId,
                            Convert.ToString(rec["FirstName"]) ?? string.Empty,
                            Convert.ToString(rec["LastName"]) ?? string.Empty
                        ));
                    }
                }
                if (accountsToSeed.Any())
                {
                    modelBuilder.Entity<Account>().HasData(accountsToSeed);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding accounts from {filePath}: {ex.ToString()}");
                throw;
            }
        }
    }

    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(a => a.AccountId);
            builder.Property(a => a.AccountId).ValueGeneratedNever();

            builder.Property(a => a.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(a => a.LastName).IsRequired().HasMaxLength(100);

            builder.HasMany(a => a.MeterReadings)
                   .WithOne()
                   .HasForeignKey(mr => mr.AccountId)
                   .IsRequired()
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata.FindNavigation(nameof(Account.MeterReadings))!
                   .SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class MeterReadingConfiguration : IEntityTypeConfiguration<Domain.Entities.MeterReading>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.MeterReading> builder)
        {
            builder.HasKey(mr => mr.Id);
            builder.Property(mr => mr.Id).ValueGeneratedOnAdd();

            builder.Property(mr => mr.MeterReadingDateTime).IsRequired();
            builder.Property(mr => mr.MeterReadValue).IsRequired();

            builder.HasIndex(mr => new { mr.AccountId, mr.MeterReadingDateTime, mr.MeterReadValue })
                   .IsUnique();
        }
    }
}