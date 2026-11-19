using GameStash.Core.Data;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Builder;
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

    // Garante que o diretório de trabalho é SEMPRE a pasta onde está o executável
    Environment.CurrentDirectory = AppContext.BaseDirectory;

    var options = new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    };

    var builder = WebApplication.CreateBuilder(options);

    // ALTERADO: Adicionado fallback seguro para o appsettings.json
    builder.Configuration.SetBasePath(AppContext.BaseDirectory);
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

    // Disable background configuration file watchers to prevent StackOverflowException loops
    foreach (var source in builder.Configuration.Sources.OfType<FileConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }

    // Garante que o Serilog é o provedor de logs principal e respeita a consola externa
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
    builder.Services.AddScoped<IHomeDashboardService, HomeDashboardService>();
    builder.Services.AddScoped<IGameDetailsService, GameDetailsService>();
    builder.Services.AddScoped<IRecommendationService, RecommendationService>();

    // 3. Register Web API Routing Infrastructure
    builder.Services.AddControllers()
        .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddDbContext<GameStashDbContext>();

    var app = builder.Build();

    try
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<GameStashDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

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

            // 2. Root Folders initializition using the configured database
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