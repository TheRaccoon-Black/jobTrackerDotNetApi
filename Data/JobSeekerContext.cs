using JobSeekerNet.Models;
using Microsoft.EntityFrameworkCore;

namespace JobSeekerNet.Data;

public class JobSeekerContext(DbContextOptions<JobSeekerContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Interview> Interviews => Set<Interview>();



    //buat aturan tambahan/override kalo application kehapus document tidak ikut terhapus 
    // tapi hanya membuat applicationId nya menjadi null saja

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>()
        .HasOne(d => d.Application)
        .WithMany(a => a.Documents)
        .HasForeignKey(d => d.ApplicationId)
        .OnDelete(DeleteBehavior.SetNull);
    }

}
