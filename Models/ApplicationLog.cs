using Microsoft.Identity.Client;

namespace JobSeekerNet.Models;

public class ApplicationLog
{
public int Id {get; set;}

public Application? Application {get; set;}
public int ApplicationId {get; set;}
public User? User {get; set;}
public int UserId {get; set;}
public required string Event {get;set;}
public string? OldValue {get; set;}
public string? NewValue {get;set;}
public string? Note {get; set;}
public DateTime? CreatedAt {get; set;}
public DateTime? UpdatedAt {get; set;}


}
