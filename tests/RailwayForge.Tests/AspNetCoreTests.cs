using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RailwayForge.AspNetCore;

namespace RailwayForge.Tests;

public sealed class AspNetCoreTests
{
    private sealed record RateLimitError(int RetryAfterSeconds) : IHttpError
    {
        public string Code => "RATE_LIMITED";
        public string Message => $"Retry after {RetryAfterSeconds}s.";
        public int StatusCode => StatusCodes.Status429TooManyRequests;
        public string Title => "Too Many Requests";
        public IDictionary<string, object?>? Extensions =>
            new Dictionary<string, object?> { ["retryAfterSeconds"] = RetryAfterSeconds };
    }

    [Fact]
    public async Task ProblemDetailsExtensions_Maps_All_ForgeErrors_And_IHttpError()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        await using var app = builder.Build();
        app.MapGet("/ok", () => Result.Ok<string, ForgeError>("hello").ToHttpResult());
        app.MapGet("/created", () => Result.Ok<int, ForgeError>(7).ToCreatedHttpResult(id => $"/items/{id}"));
        app.MapGet("/notfound", () => Result.Fail<int, ForgeError>(
            new ForgeError.NotFound("Item", "9", "Item 9 missing")).ToHttpResult());
        app.MapGet("/forbidden", () => Result.Fail<ForgeError>(
            new ForgeError.Forbidden("Delete", "Admin required")).ToHttpResult());
        app.MapGet("/ratelimit", () => Result.Fail<int, RateLimitError>(new RateLimitError(30)).ToHttpResult());
        app.MapGet("/multi", () =>
        {
            var bag = new ValidationBag<ForgeError>();
            bag.Check(false, new ForgeError.Validation("a", "bad a"));
            bag.Check(false, new ForgeError.Validation("b", "bad b"));
            return bag.Build(() => "ok").ToHttpResult();
        });

        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ok")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.GetAsync("/created")).StatusCode);

        var nf = await client.GetAsync("/notfound");
        Assert.Equal(HttpStatusCode.NotFound, nf.StatusCode);
        Assert.Contains("NOT_FOUND", await nf.Content.ReadAsStringAsync());

        var fb = await client.GetAsync("/forbidden");
        Assert.Equal(HttpStatusCode.Forbidden, fb.StatusCode);
        Assert.Contains("FORBIDDEN", await fb.Content.ReadAsStringAsync());

        var rl = await client.GetAsync("/ratelimit");
        Assert.Equal(HttpStatusCode.TooManyRequests, rl.StatusCode);
        Assert.Contains("RATE_LIMITED", await rl.Content.ReadAsStringAsync());

        var multi = await client.GetAsync("/multi");
        Assert.Equal(HttpStatusCode.BadRequest, multi.StatusCode);
        Assert.Contains("VALIDATION_MULTIPLE", await multi.Content.ReadAsStringAsync());
    }
}
