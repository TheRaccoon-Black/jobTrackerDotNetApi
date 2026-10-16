namespace JobSeekerNet.Models;

public class User
{
    public int Id {get; set;}

    public required string Name {get; set;}
    public required string Email {get; set;}
    public required string Password {get; set;}
    public int WeeklyTarget {get; set;}
    public DateTime? CreatedAt {get; set;}
    public DateTime? UpdatedAt {get; set;}


    //relasi
    public List<Application> Applications { get; set;} = new();
    public List<ApplicationLog> ApplicationLogs {get; set;} = new();
    public List<Document> Documents {get; set;} = new();
    
}
