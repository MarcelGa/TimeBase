using Microsoft.EntityFrameworkCore;
using TimeBase.Supervisor.Data;
using TimeBase.Supervisor.Services;
using TimeBase.Supervisor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TimeBaseDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("TimeBaseDb")));
builder.Services.AddServices();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var db = scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
  try { db.Database.Migrate(); } catch { }
}

app.AddTimeBaseEndpoints();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.Run();
