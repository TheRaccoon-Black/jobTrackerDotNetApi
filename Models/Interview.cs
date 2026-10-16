namespace JobSeekerNet.Models;

public class Interview
{
    public int Id { get; set;}
    public Application? Application {get; set;}
    public int ApplicationId {get; set;}
    public string? InterviewStage {get;set;}
    public string? Interviewer {get;set;}
    public DateTime? ScheduleDate {get; set;}
    public string? LinkMeeting {get; set;}
    public string? Notes {get;set;}
    public DateTime? CreatedAt {get;set;}
    public DateTime? UpdatedAt {get; set;}
    
}
