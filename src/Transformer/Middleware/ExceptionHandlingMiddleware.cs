using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace Transformer.Middleware;

public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var httpContext = context.GetHttpContext();
            var correlationId = httpContext?.Items["CorrelationId"] as string;

            var problem = ExceptionMapper.Map(ex);

            if (problem.Status >= 500)
                _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
            else
                _logger.LogWarning(ex, "Client error. Status={Status} CorrelationId={CorrelationId}", problem.Status, correlationId);

            // 500 responses omit exception detail to avoid leaking internals
            var detail = problem.Status < 500
                ? ex.Message
                : $"An internal error occurred. Use correlationId for support reference.";

            if (httpContext is not null)
            {
                httpContext.Response.StatusCode = problem.Status;
                httpContext.Response.ContentType = "application/json";

                var body = JsonSerializer.Serialize(new
                {
                    type          = problem.Type,
                    title         = problem.Title,
                    status        = problem.Status,
                    detail,
                    correlationId
                }, JsonOptions);

                await httpContext.Response.WriteAsync(body);
            }
        }
    }
}
