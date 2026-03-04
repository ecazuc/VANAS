using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=vanas.db"));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    // Create the database if it doesn't exist
    context.Database.EnsureCreated();

    // Check if we already have users
    if (!context.Users.Any())
    {
        context.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = "admin123", // Later we will use proper encryption!
            BalanceMinutes = 9999,
            IsAdmin = true
        });
        context.SaveChanges();
    }
}

app.Run();
