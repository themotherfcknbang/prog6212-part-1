namespace RaceDay.Api.Models;

public class Result
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Finished";
    public DateTime RecordedAt { get; set; }

    public Enrolment Enrolment { get; set; } = null!;
}