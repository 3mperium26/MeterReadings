# MeterReadings

## Overview

This project is a .NET Core application designed to manage and process meter readings. It allows users to upload CSV files containing meter reading data, validates the data, and stores it in a database. The application provides an API endpoint for uploading these files.

## Features

* **CSV Upload:** Accepts meter readings via CSV file upload.
* **Data Validation:**
    * Parses CSV and validates individual row data (Account ID, Meter Reading Date Time, Meter Read Value).
    * Checks for missing or incorrectly formatted fields.
    * Validates against existing account data.
    * Prevents duplicate meter readings for the same account, date, and value within a batch and in the database.
    * Ensures new meter readings are not older than the latest existing reading for an account.
    * Validates meter read value format (NNNNN, 1-5 digits).
* **Data Persistence:** Stores valid meter readings in a SQL Server database.
* **Error Reporting:** Provides feedback on successful and failed readings, including specific error messages for failed entries.
* **Database Seeding:** Includes functionality to seed initial account data from a CSV file.
* **Automated Migrations:** Supports applying Entity Framework Core migrations on startup.

## Technologies Used

* .NET Core (ASP.NET Core for Web API)
* Entity Framework Core (for ORM and database interaction)
* SQL Server (as the database)
* CsvHelper (for parsing CSV files)
* MSTest (for unit testing)
* Moq (for mocking dependencies in tests)
* Swagger/OpenAPI (for API documentation)

## Project Structure

The solution is organized into the following projects:

* **MeterReading.Domain:** Contains the core business logic, entities (Account, MeterReading), and domain-specific exceptions.
* **MeterReading.Application:** Includes application services, DTOs (Data Transfer Objects), and interfaces for business operations like processing uploads. [cite: 1]
* **MeterReading.Infrastructure:** Implements data persistence (repositories, DbContext), CSV parsing utilities, and database migrations.
* **MeterReading.Infrastructure.Test:** Contains unit tests for the infrastructure components, such as CSV parsing and application services.
* **MeterReading.Web:** The ASP.NET Core Web API project that exposes endpoints for interacting with the application (e.g., uploading meter readings). It also includes data seeding logic and application startup configuration.

## Setup and Installation

1.  **Prerequisites:**
    * .NET SDK (version compatible with the project, likely .NET 8 or as specified in global.json if present)
    * SQL Server instance (e.g., LocalDB, SQL Server Express)
2.  **Clone the repository:**
    ```bash
    git clone [<repository-url>](https://github.com/3mperium26/MeterReadings)
    cd MeterReadings
    ```
3.  **Configure Connection String:**
    * Open `MeterReading.Web/appsettings.json`.
    * Update the `DefaultConnection` string to point to your SQL Server instance.
        ```json
        {
          "ConnectionStrings": {
            "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MeterReadings;Trusted_Connection=True;MultipleActiveResultSets=true"
          },
          // ... other settings
        }
        ```
4.  **Database Migrations:**
    * The application is configured to apply pending migrations on startup if `ApplyMigrationsOnStartup` is set to `true` in `appsettings.json` (default is true).
    * Alternatively, you can apply migrations manually using the .NET CLI from the `MeterReading.Infrastructure` directory:
        ```bash
        dotnet ef database update --startup-project ../MeterReading.Web
        ```
5.  **Build the solution:**
    ```bash
    dotnet build
    ```
6.  **Run the application:**
    * Navigate to the `MeterReading.Web` directory.
    * Run the application:
        ```bash
        dotnet run
        ```
    * The API will be available at the URLs specified in `MeterReading.Web/Properties/launchSettings.json` (e.g., `https://localhost:7270`, `http://localhost:5065`).

## Usage

### API Endpoint

The primary way to interact with the application is through its API.

* **POST /api/meter-reading-uploads**
    * **Description:** Uploads a CSV file containing meter readings.
    * **Request:** `multipart/form-data` with a single file field. The file must be a CSV (`.csv`) with the following columns:
        * `AccountId`
        * `MeterReadingDateTime` (format: `dd/MM/yyyy HH:mm`)
        * `MeterReadValue` (format: NNNNN, 1-5 digits)
    * **Example CSV content:**
        ```csv
        AccountId,MeterReadingDateTime,MeterReadValue
        2344,22/04/2019 09:24,10023
        1234,22/04/2019 12:25,9999
        ```
    * **Responses:**
        * `200 OK`: Returns a `MeterReadingUploadResultDto` with the count of saved and failed readings, and a list of errors if any.
            ```json
            {
              "savedReadings": 1,
              "failedReadings": 1,
              "errors": [
                "Row 3 (AccId: 1234, ReadDate: 22/04/2019 12:25, ReadVal: 9999): Invalid meter read value format: '9999'. Must be NNNNN (1-5 digits)."
              ],
              "fileName": "yourfile.csv"
            }
            ```
        * `400 Bad Request`: If the file is missing, not a CSV, or has an invalid content type.
        * `500 Internal Server Error`: If an unexpected error occurs during processing.

### Swagger UI

When running in a development environment, Swagger UI is available for exploring and testing the API. By default, it can be accessed at `/swagger` (e.g., `https://localhost:7270/swagger`).

## Running Tests

The solution includes a test project `MeterReading.Infrastructure.Test`.

To run the tests:

1.  Navigate to the solution's root directory or the `MeterReading.Infrastructure.Test` directory.
2.  Use the .NET CLI:
    ```bash
    dotnet test
    ```

The tests cover:

* **CSV Parsing (`CSVParseHelperTests.cs`):**
    * Parsing valid CSV files.
    * Handling CSVs with missing or extra fields.
    * Handling empty or header-only CSVs.
    * Handling malformed rows.
* **Meter Reading Upload Service (`MeterReadingUploadServiceTests.cs`):**
    * Processing valid rows and successful database updates.
    * Handling rows with parsing errors.
    * Handling cases where accounts are not found.
    * Handling domain validation exceptions (e.g., invalid meter reading values, older dates).
    * Behavior when database save operations fail.
    * Ensuring no save operations if no valid readings are present.

## Data Seeding

The application seeds initial account data from `MeterReading.Web/DataSeed/Test_Accounts.csv` into the `Accounts` table when the database is created or migrations are applied. This is handled by the `ApplicationDbContext`.
