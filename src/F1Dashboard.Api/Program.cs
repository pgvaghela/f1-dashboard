using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.Import;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render/Railway inject the port to listen on via $PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Allowed CORS origins: always the local dev frontend, plus any configured for
// the deployed frontend (set Cors__AllowedOrigins="https://your-app.vercel.app").
var allowedOrigins = new List<string> { "http://localhost:4200" };
var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];
if (!string.IsNullOrWhiteSpace(configuredOrigins))
{
    allowedOrigins.AddRange(
        configuredOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<F1DbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("F1Database"))
           .UseSnakeCaseNamingConvention());

// Importer pulls live F1 data from the Jolpica/Ergast public API.
builder.Services.AddHttpClient<F1DataImporter>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("F1Dashboard/1.0");
});

var app = builder.Build();

// Create the schema on a fresh database (e.g. a new hosted Postgres). No-op if
// the tables already exist. Data is loaded separately via the import endpoint.
if (!string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("F1Database")))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<F1DbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // Behind a hosting proxy TLS is terminated upstream, so only redirect locally.
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.UseCors("AppCors");

app.MapControllers();

app.Run();
