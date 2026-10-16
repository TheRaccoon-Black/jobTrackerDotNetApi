using System.ComponentModel.DataAnnotations.Schema;

namespace JobSeekerNet.Models;

public class Application
{
    public int Id {get; set;}
    public User? User {get; set;}
    public int UserId {get; set;}
    public required string Company { get; set;}
    public required string Title {get; set;}
    public string Status { get; set;} = "Wishlist";
    public string? JobPostingLink {get; set;}
    public string? Contact {get;set;}
    public DateTime? ApplicationDate {get; set;}
    public string? SalaryRange {get;set;}
    public string? Notes {get; set;}
    public string? NextFollowUp {get; set;}
    public string? FollowUpNotes {get; set;}
    [Column(TypeName ="nvarchar(max)")]
    public string ? Tags {get; set;}

    public DateTime? CreatedAt {get; set;}
    public DateTime? UpdatedAt {get; set;}


    public List<ApplicationLog> Logs {get; set;} = new();
    public List<Document> Documents {get; set;} = new();
    public List<Interview> Interviews {get; set;} = new();



}
