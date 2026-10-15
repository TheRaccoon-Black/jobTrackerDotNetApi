using Microsoft.Identity.Client;

namespace JobSeekerNet.Models;

public class Document
{
    public int Id {get; set;}
    public User? User { get; set;}
    public int UserId {get; set;}
    public required string FileName {get; set;}
    public string Type {get; set;} = "CV";
    public required string FilePath {get; set;}
    public Application? Application {get; set;}
    public int? ApplicationId {get; set;}
    public DateTime? CreatedAt {get; set;}
    public DateTime? UpdatedAt {get;set;}

    //relasi 


}
