using GameStash.Core.Data;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    Log.ForContext(typeof(Program)).Information("Serilog framework successfully attached to GameStash.Core execution bounds.");
    Log.ForContext(typeof(Program)).Information("Starting GameStash Web GUI host engine...");

    // Always use the directory containing the executable as the working directory.
    Environment.CurrentDirectory = AppContext.BaseDirectory;

    var options = new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    };

    var builder = WebApplication.CreateBuilder(options);

    // Use a safe fallback location for appsettings.json.
    builder.Configuration.SetBasePath(AppContext.BaseDirectory);
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

    // Disable background configuration file watchers to prevent StackOverflowException loops
    foreach (var source in builder.Configuration.Sources.OfType<FileConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }

    string databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? GameStash.Core.Data.GameStashDbContext.DefaultConnectionString;
    GameStashDbContext.ConfigureConnectionString(databaseConnectionString);

    // Rebuild the bootstrap logger from appsettings so the configured sinks are active.
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    builder.Host.UseSerilog(dispose: true);

    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "GameStashService";
    });

    builder.Services.AddHttpClient();

    // 2. Register Application Core Services in the IoC Container
    builder.Services.AddSingleton<ISettingsService, SettingsService>();
    builder.Services.AddSingleton<IRootFoldersService, RootFoldersService>();
    builder.Services.AddSingleton<ILocalStashService, LocalStashService>();
    builder.Services.AddSingleton<ISteamStashService, SteamStashService>();
    builder.Services.AddSingleton<IEpicStashService, EpicStashService>();
    builder.Services.AddSingleton<IGogStashService, GogStashService>();
    builder.Services.AddSingleton<IAmazonStashService, AmazonStashService>();
    builder.Services.AddScoped<IHomeDashboardService, HomeDashboardService>();
    builder.Services.AddScoped<IGameDetailsService, GameDetailsService>();
    builder.Services.AddScoped<IRecommendationService, RecommendationService>();
    builder.Services.AddScoped<IUnifiedStashService, UnifiedStashService>();

    // 3. Register Web API Routing Infrastructure
    builder.Services.AddControllers()
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddDbContext<GameStashDbContext>();

    var app = builder.Build();

    try
    {
        await PostgresDatabaseBootstrapper.EnsureDatabaseExistsAsync(databaseConnectionString);

        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<GameStashDbContext>();
        await dbContext.Database.MigrateAsync();

        // 1. Settings and PostgreSQL structure must complete first.
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        Log.ForContext(typeof(Program)).Information("Initializing settings ...");
        await settingsService.InitializeAsync();
    }
    catch (Exception ex)
    {
        Log.ForContext(typeof(Program)).Error(ex, "Background initialization sequence encountered an unexpected error.");
        throw;
    }

    // 4. Asynchronous Concurrent Background Initialization (Non-blocking Boot)
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();

            // 2. Initialize root folders using the configured database.
            var rootFoldersService = scope.ServiceProvider.GetRequiredService<IRootFoldersService>();
            await rootFoldersService.InitializeAsync();

            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // 2. Local Library initializes using the configured database and IGDB client
                        var libraryService = scope.ServiceProvider.GetRequiredService<ILocalStashService>();
                        await libraryService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext(typeof(Program)).Error(ex, "Local Library initialization encountered an unexpected error.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        var steamStashService = scope.ServiceProvider.GetRequiredService<ISteamStashService>();
                        await steamStashService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext(typeof(Program)).Error(ex, "Steam Stash initialization encountered an unexpected error.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        var epicStashService = scope.ServiceProvider.GetRequiredService<IEpicStashService>();
                        await epicStashService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext(typeof(Program)).Error(ex, "Epic Stash initialization encountered an unexpected error.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        var gogStashService = scope.ServiceProvider.GetRequiredService<IGogStashService>();
                        await gogStashService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext(typeof(Program)).Error(ex, "GOG Stash initialization encountered an unexpected error.");
                    }
                }),
                Task.Run(async () =>
                {
                    try
                    {
                        var amazonStashService = scope.ServiceProvider.GetRequiredService<IAmazonStashService>();
                        await amazonStashService.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext(typeof(Program)).Error(ex, "Amazon Stash initialization encountered an unexpected error.");
                    }
                })
            };

            await Task.WhenAll(tasks);

            Log.ForContext(typeof(Program)).Information("Background initialization sequence complete. All services are ready.");
        }
        catch (Exception ex)
        {
            Log.ForContext(typeof(Program)).Error(ex, "Background initialization sequence encountered an unexpected error.");
        }
    });

    // 5. Setup Kestrel Static File Pipeline (Reading embedded wwwroot from inside the .exe)
    if (app.Environment.IsDevelopment())
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
    }
    else
    {
        var embeddedProvider = new EmbeddedFileProvider(typeof(Program).Assembly, "GameStash.Web.GUI.wwwroot");
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedProvider });
    }

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