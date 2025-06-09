namespace DataInserter.Configuration;

public class ApplicationConfiguration
{
    public string ExcelPath { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public bool EnableParallelProcessing { get; set; } = false;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
}

public class DatabaseConfiguration
{
    public string IAMConnection { get; set; } = string.Empty;
    public string SDGConnection { get; set; } = string.Empty;
}
