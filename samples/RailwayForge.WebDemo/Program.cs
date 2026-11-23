using RailwayForge;
using RailwayForge.AspNetCore;
using RailwayForge.WebDemo;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<OrderFulfillmentEngine>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/inventory", (OrderFulfillmentEngine engine) => Results.Ok(engine.GetInventory()));

app.MapPost("/api/inventory/restock", (OrderFulfillmentEngine engine) =>
{
    engine.RestockAll();
    return Results.Ok(engine.GetInventory());
});

app.MapGet("/api/history", (OrderFulfillmentEngine engine) =>
{
    var all = engine.GetHistory();
    var byCode = all.Where(h => h.ErrorCode is not null)
        .GroupBy(h => h.ErrorCode!)
        .ToDictionary(g => g.Key, g => g.Count());
    return Results.Ok(new
    {
        total = all.Count,
        succeeded = all.Count(h => h.Succeeded),
        failed = all.Count(h => !h.Succeeded),
        byErrorCode = byCode,
        entries = all.Reverse().ToList(),
    });
});

app.MapPost("/api/orders", async (OrderFulfillmentEngine engine, OrderRequest req) =>
{
    var (res, history) = await engine.ProcessOrderAsync(req);

    // Single error maps exhaustively via ForgeError -> ProblemDetails (400/401/403/404/409/503/500);
    // multiple ValidationBag errors map to 400 VALIDATION_MULTIPLE.
    if (res.TryGetValue(out var receipt, out var errors))
        return Results.Created($"/api/orders/{receipt.OrderId}", new { receipt, history });

    return errors.Count == 1
        ? errors[0].ToProblemDetails()
        : res.ToHttpResult();
});

app.Run();

public partial class Program;
