using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Threading.Tasks;

// 1. Initialize the global Serilog logger configuration straight at bootup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.ForContext(typeof(Program)).Information("Serilog framework successfully attached to GameStash.Core execution bounds.");

try
{
    Log.ForContext(typeof(Program)).Information("Starting GameStash Web GUI host engine...");

    var options = new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
    };

    var builder = WebApplication.CreateBuilder(options);

    // Disable background configuration file watchers to prevent StackOverflowException loops
    foreach (var source in builder.Configuration.Sources.OfType<FileConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }

    builder.Host.UseSerilog();
    builder.Host.UseWindowsService();

    // 2. Register Application Core Services in the IoC Container
    builder.Services.AddSingleton<ISettingsService, SettingsService>();
    builder.Services.AddSingleton<IRootFoldersService, RootFoldersService>();
    // LibraryService registered as Singleton to hold the static in-memory library cache
    builder.Services.AddSingleton<IStashService, StashLibraryService>();
    builder.Services.AddScoped<IHomeDashboardService, HomeDashboardService>();
    builder.Services.AddScoped<IGameDetailsService, GameDetailsService>();

    // 3. Register Web API Routing Infrastructure
    builder.Services.AddControllers()
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    try
    {
        using var scope = app.Services.CreateScope();

        // 1. Settings & SQLite structure must complete first
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        Log.ForContext(typeof(Program)).Information("Initializing settings ...");
        await settingsService.InitializeAsync();
    }
    catch (Exception ex)
    {
        Log.ForContext(typeof(Program)).Error(ex, "Background initialization sequence encountered an unexpected error.");
    }

    // 4. Asynchronous Concurrent Background Initialization (Non-blocking Boot)
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();

            // 2. Local Library initializes using the configured database and IGDB client
            var rootFoldersService = scope.ServiceProvider.GetRequiredService<IRootFoldersService>();
            await rootFoldersService.InitializeAsync();
            // 2. Local Library initializes using the configured database and IGDB client
            var libraryService = scope.ServiceProvider.GetRequiredService<IStashService>();
            await libraryService.InitializeAsync();
            Log.ForContext(typeof(Program)).Information("Background initialization sequence complete. All services are ready.");
        }
        catch (Exception ex)
        {
            Log.ForContext(typeof(Program)).Error(ex, "Background initialization sequence encountered an unexpected error.");
        }
    });

    // 5. Setup Kestrel Static File Pipeline
    app.UseDefaultFiles();
    app.UseStaticFiles();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseRouting();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.ForContext(typeof(Program)).Fatal(ex, "Host terminated unexpectedly during startup sequence.");
}
finally
{
    Log.CloseAndFlush();
}