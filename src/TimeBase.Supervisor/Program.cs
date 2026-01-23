using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeBase.Supervisor.Data;
using TimeBase.Supervisor.Services;
using TimeBase.Supervisor.Entities;
using System.Net.Http.Json;

// Phase 2 MVP: Minimal endpoints (no controllers)
var builder = WebApplication.CreateBuilder(args);

// Configure EF Core with TimescaleDB (PostgreSQL)
builder.Services.AddDbContext<TimeBaseDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("TimeBaseDb")));

builder.Services.AddSingleton<ProviderRegistry>();
builder.Services.AddSingleton<DataCoordinator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Migrations (best effort for MVP)
using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
  try { db.Database.Migrate(); } catch { /* ignore */ }
}

// Health endpoint
app.MapGet("/health", () => Results.Json(new { status = "healthy" }));

// Minimal endpoints (Phase 2 MVP)
app.MapGet("/api/providers", async (TimeBaseDbContext db) => {
  var list = await db.Providers.ToListAsync();
  return Results.Ok(new { providers = list });
});

app.MapPost("/api/providers", async (ProviderRegistry registry, HttpRequest request) => {
  var payload = await request.ReadFromJsonAsync<Dictionary<string, string>>();
  if (payload == null || !payload.ContainsKey("repository"))
    return Results.BadRequest(new { error = "repository is required" });
  await registry.InstallProviderAsync(payload["repository"]);
  return Results.Json(new Dictionary<string, string> { { "message", "Provider installation started" } });
});

app.MapDelete("/api/providers/{id}", (string id) => {
  return Results.Ok(new { message = $"Provider {id} uninstall not implemented in MVP" });
});

app.MapGet("/api/data/{symbol}", (string symbol, string interval) => {
  // MVP: return empty data structure for now
  return Results.Ok(new { symbol, interval, data = new object[0] });
});

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.Run();
