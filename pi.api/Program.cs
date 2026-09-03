using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Npgsql;
using OSMApi;
using pi.api.Additional;
using pi.api.Functions;
using pi.api.Middlewares;
using pi.api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApp =>
    {
        workerApp.UseMiddleware<JwtAuthMiddleware>();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddMicrosoftIdentityWebApi(
                   options =>
                   {
                       context.Configuration.GetSection("AzureAd").Bind(options);
                   },
                   options => context.Configuration.GetSection("AzureAd").Bind(options)
               );
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters.ValidAlgorithms = ["RS256"];
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
        });

        services.AddAuthorizationBuilder();

        //Rejestracja NpgSqlDataSource
        string connectionString = context.Configuration["DbConnectionString"]
            ?? throw new InvalidOperationException("Brak konfiguracji ConenctionString.");
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        //Włączenie obsługi danych geograficznych
        dataSourceBuilder.UseNetTopologySuite(); 

        services.AddSingleton(dataSourceBuilder.Build());

        services.AddScoped<ProjectsService>();
        services.AddScoped<PoolsService>();
        services.AddScoped<PoiService>();
        services.AddScoped<PowiatyService>();
        services.AddScoped<PowiatySummaryProcessor>();
        services.AddScoped<PoolsSummaryProcessor>();

    }).ConfigureLogging(logging =>
    {
        logging.AddFilter("pi.api.Functions", LogLevel.Information);
    })
    .Build();

await host.RunAsync();
