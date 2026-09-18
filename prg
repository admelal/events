using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using GaValveInspectionGisMaximo.Configuration;
using GaValveInspectionGisMaximo.Serialization;
using GaValveInspectionGisMaximo.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

const string UseAzureAppConfigurationSetting = "UseAzureAppConfiguration";
const string AppConfigurationEndpointSetting = "AppConfigEndpoint";
const string AppConfigurationLabelSetting = "AppConfigLabel";

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

var isRunningInAzure =
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

TokenCredential credential = isRunningInAzure
    ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
    : new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeManagedIdentityCredential = true,
        ExcludeInteractiveBrowserCredential = true
    });

var useAzureAppConfiguration =
    bool.TryParse(builder.Configuration[UseAzureAppConfigurationSetting], out var configuredValue) &&
    configuredValue;

if (useAzureAppConfiguration)
{
    var endpointValue = builder.Configuration[AppConfigurationEndpointSetting];

    if (string.IsNullOrWhiteSpace(endpointValue) ||
        !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
        !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"{AppConfigurationEndpointSetting} must be a valid absolute HTTPS endpoint.");
    }

    var label = builder.Configuration[AppConfigurationLabelSetting]?.Trim();

    if (string.IsNullOrWhiteSpace(label))
    {
        throw new InvalidOperationException(
            $"{AppConfigurationLabelSetting} is required.");
    }

    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options
            .Connect(endpoint, credential)
            .Select(KeyFilter.Any, label)
            .ConfigureKeyVault(keyVault => keyVault.SetCredential(credential));
    });
}
else if (isRunningInAzure)
{
    throw new InvalidOperationException(
        $"{UseAzureAppConfigurationSetting} must be true when running in Azure.");
}

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.Configure<JsonSerializerOptions>(JsonDefaults.Apply);
builder.Services.AddSingleton<TokenCredential>(credential);

builder.Services.AddOptions<ValveIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(ValveIntegrationOptions.SectionName));

builder.Services.AddOptions<DownstreamOptions>()
    .Bind(builder.Configuration);

builder.Services.AddHttpClient(MaximoClient.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient(ArcGisClient.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient(ArcGisTokenProvider.HttpClientName, c => c.Timeout = Timeout.InfiniteTimeSpan);

builder.Services.AddSingleton<IValveEventValidator, ValveEventValidator>();
builder.Services.AddSingleton<IGisToMaximoMapper, GisToMaximoMapper>();
builder.Services.AddSingleton<IMaximoToGisMapper, MaximoToGisMapper>();
builder.Services.AddSingleton<IArcGisTokenProvider, ArcGisTokenProvider>();
builder.Services.AddTransient<IMaximoClient, MaximoClient>();
builder.Services.AddTransient<IArcGisClient, ArcGisClient>();

builder.Services
   .AddOptions<DownstreamOptions>()
   .Bind(builder.Configuration)
   .ValidateOnStart();
builder.Services
   .AddApplicationInsightsTelemetryWorkerService()
   .ConfigureFunctionsApplicationInsights();
builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
   var applicationInsightsRule = options.Rules.FirstOrDefault(
       rule =>
           rule.ProviderName ==
           "Microsoft.Extensions.Logging.ApplicationInsights." +
           "ApplicationInsightsLoggerProvider");
   if (applicationInsightsRule is not null)
   {
       options.Rules.Remove(applicationInsightsRule);
   }
});
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter(
   "Azure.Core",
   LogLevel.Warning);
builder.Logging.AddFilter(
   "Azure.Storage",
   LogLevel.Warning);
builder.Logging.AddFilter(
   "System.Net.Http.HttpClient",
   LogLevel.Warning);

var host = builder.Build();
await host.RunAsync();
