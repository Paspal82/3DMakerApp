var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// CORS - allow the Angular dev server origins during development
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowLocalClient",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200", "https://localhost:4200", "http://127.0.0.1:4200", "https://127.0.0.1:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Register ProductService
builder.Services.AddSingleton<_3DMakerApp.Server.Services.ProductService>();

// Register ProductImageService
builder.Services.AddSingleton<_3DMakerApp.Server.Services.ProductImageService>();

var app = builder.Build();

// Do not map static assets or SPA fallback so root is served by controllers.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Use CORS policy
app.UseCors("AllowLocalClient");

app.UseAuthorization();

app.MapControllers();

app.Run();
