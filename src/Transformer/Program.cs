using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using Transformer.Services;
using Transformer.Services.TransformFunctions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

builder.Services.AddSingleton<IConfigLoader, ConfigLoader>();
builder.Services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
builder.Services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
builder.Services.AddSingleton(_ => new TransformRegistry(
[
    ("trim",     new TrimTransformFunction()),
    ("round",    new RoundTransformFunction()),
    ("contains", new ContainsTransformFunction()),
    ("now",      new NowTransformFunction())
]));
builder.Services.AddSingleton<ITransformationEngine, TransformationEngine>();

builder.Build().Run();
