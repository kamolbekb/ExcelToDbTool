namespace DataInserter.Models;

public class ProcessingResult
{
    public int TotalRecords { get; set; }
    public int SuccessfulRecords { get; set; }
    public int DuplicateRecords { get; set; }
    public int FailedRecords { get; set; }
    public List<ProcessingError> Errors { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }

    public double SuccessRate => TotalRecords > 0 
        ? (double)SuccessfulRecords / TotalRecords * 100 
        : 0;
}

public class ProcessingError
{
    public int ExcelRow { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class DuplicateRecord
{
    public int ExcelRow { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid ExistingUserId { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}
