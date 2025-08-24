using IntelligenceTaskTracker.Web.Data;
using IntelligenceTaskTracker.Web.Services.AI;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=IntelligenceTaskTracker;Trusted_Connection=True;MultipleActiveResultSets=true;";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// AI Services
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddHttpClient<GeminiProvider>();
builder.Services.AddHttpClient<OpenAIProvider>();
builder.Services.AddScoped<GeminiProvider>();
builder.Services.AddScoped<OpenAIProvider>();
builder.Services.AddScoped<IAiProvider>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>();
    return config.Value.Provider.ToUpperInvariant() switch
    {
        "OPENAI" => serviceProvider.GetRequiredService<OpenAIProvider>(),
        "GEMINI" => serviceProvider.GetRequiredService<GeminiProvider>(),
        _ => serviceProvider.GetRequiredService<OpenAIProvider>() // Default to OpenAI
    };
});
builder.Services.AddScoped<IInsightsService, AiInsightsService>();
builder.Services.AddMemoryCache();

// Ensure static web assets (scoped CSS, libs) are available when running via dll or non-standard hosts
builder.WebHost.UseStaticWebAssets();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(db);
}

app.Run();
