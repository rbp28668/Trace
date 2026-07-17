using Microsoft.EntityFrameworkCore;
using Trace.Data;
using Trace.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<TraceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Trace")));

builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<WaypointService>();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<FleetService>();
builder.Services.AddScoped<FleetImportService>();
builder.Services.AddScoped<PilotService>();
builder.Services.AddScoped<EntryService>();
builder.Services.AddScoped<DayService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<PlanningService>();
builder.Services.AddScoped<DayEntryService>();
builder.Services.AddScoped<ScoringService>();

builder.Services.Configure<IgcStorageOptions>(builder.Configuration.GetSection("Igc"));
builder.Services.AddSingleton(sp =>
    new IgcStorage(sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<IgcStorageOptions>>().Value));

// NOTE: authentication is intentionally not configured yet (trusted-LAN
// deployment). When it is added, register ASP.NET Core Identity here and call
// app.UseAuthentication() before app.UseAuthorization() below; the pages already
// route through UseAuthorization().

var app = builder.Build();

// Apply any pending EF migrations on startup so a fresh deployment self-provisions
// its schema (trusted single-operator app; safe to do inline).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TraceDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Friendly pages for 404 and other status codes (re-executes /Error/{code}).
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
