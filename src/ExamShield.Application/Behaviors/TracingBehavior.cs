using System.Diagnostics;
using MediatR;

namespace ExamShield.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps every command/query in a System.Diagnostics.Activity.
/// Compatible with any OTLP collector (Jaeger, Grafana Tempo, etc.) via the OpenTelemetry
/// auto-instrumentation agent — no NuGet packages required beyond what .NET ships.
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse>(ActivitySource activitySource)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        using var activity = activitySource.StartActivity(name, ActivityKind.Internal);

        if (activity is null)
            return await next();

        activity.SetTag("mediatR.request", typeof(TRequest).FullName);
        try
        {
            var response = await next();
            activity.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.AddEvent(new ActivityEvent("exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.type"]    = ex.GetType().FullName,
                    ["exception.message"] = ex.Message,
                }));
            throw;
        }
    }
}
