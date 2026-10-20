using System;
using JobSeekerNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobSeekerNet.Data;

public  static class DataExtension
{
    public static void MigrateDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobSeekerContext>();


        dbContext.Database.Migrate();
    }

    public static  void AddJobSeekerDb(this WebApplicationBuilder builder)
    {
        var connString = builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddSqlServer<JobSeekerContext>(
            connString,
            optionsAction: options => options.UseSeeding((context, _)=>
            {
                if (!context.Set<User>().Any())
                {
                    context.Set<User>().Add(
                        new User
                        {
                            Name = "Demo user",
                            Email =  "admin@gmail.com",
                            Password = "adminadmin123",
                            WeeklyTarget = 4
                        }
                    );
                    context.SaveChanges();
                }
            })
        );



    }
}
