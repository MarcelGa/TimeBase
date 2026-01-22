var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add gRPC services
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map gRPC services
app.MapGrpcService<ProviderService>();

// Add a simple welcome endpoint
app.MapGet("/", () => "TimeBase Supervisor v1.0.0");

app.Run();

// Placeholder for gRPC service - will be implemented in Phase 2
public class ProviderService : Timebase.Provider.DataProvider.DataProviderBase
{
    // Implementation will be added in Phase 2
}