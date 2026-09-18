using IssueTracker.Api.Security;
using Microsoft.AspNetCore.Identity;
using IssueTracker.Api.Data;
using Microsoft.EntityFrameworkCore;
using IssueTracker.Api.Services;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// builder.Services.AddSingleton<ProjectService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<MembershipService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("IssueTracker")
    )
);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => {
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(
            allowIntegerValues: false
            )
        );
    });
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// app.UseAuthorization();
app.UseAuthentication();
app.UseAuthorization();
app.MapGroup("/auth")
    .MapIdentityApi<ApplicationUser>();

app.MapControllers();
app.MapHealthChecks("/health");
if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}
app.Run();

// Expose the entry point to the integration-test host.
public partial class Program { }
