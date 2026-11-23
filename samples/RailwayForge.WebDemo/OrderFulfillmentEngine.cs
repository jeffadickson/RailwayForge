using System.Diagnostics;
using RailwayForge;
using RailwayForge.OpenTelemetry;

namespace RailwayForge.WebDemo;

public sealed record OrderRequest(
    string CustomerEmail,
    string Sku,
    int Quantity,
    string PaymentToken,
    bool SimulateGatewayOutage = false);

public sealed record SkuItem(string Sku, string Name, decimal UnitPrice, int Stock);

public sealed record OrderReceipt(
    string OrderId,
    string CustomerEmail,
    string Sku,
    int Quantity,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt);

public sealed record RailwayStepTrace(
    int StepIndex,
    string Name,
    string Track,
    string Detail,
    double DurationMs);

public sealed record ExecutionHistoryEntry(
    string RunId,
    DateTimeOffset Timestamp,
    string TraceId,
    OrderRequest Input,
    bool Succeeded,
    int HttpStatus,
    string? ErrorCode,
    string Summary,
    OrderReceipt? Receipt,
    IReadOnlyList<RailwayStepTrace> Steps);

/// <summary>
/// Thread-safe append-only execution history & inventory store matching the
/// formally verified <c>HistoryIsAppendOnlyAndValid</c> invariant in <c>formal/RailwayLaws.dfy</c>.
/// </summary>
public sealed class OrderFulfillmentEngine
{
    public static readonly ActivitySource TraceSource = new("RailwayForge.WebDemo");

    private readonly Lock _lock = new();
    private readonly List<ExecutionHistoryEntry> _history = [];
    private readonly Dictionary<string, SkuItem> _inventory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RF-PRO-100"] = new("RF-PRO-100", "Precision DAQ Module", 249.00m, 12),
        ["RF-CELL-24"] = new("RF-CELL-24", "Li-Ion Reference Cell", 45.50m, 40),
        ["RF-HSM-KEY"] = new("RF-HSM-KEY", "Hardware Signing Token", 189.00m, 2),
        ["RF-EMPTY-0"] = new("RF-EMPTY-0", "Backordered Sensor Pack", 99.00m, 0),
    };

    public IReadOnlyList<SkuItem> GetInventory()
    {
        lock (_lock) return [.. _inventory.Values.OrderBy(i => i.Sku, StringComparer.Ordinal)];
    }

    public IReadOnlyList<ExecutionHistoryEntry> GetHistory()
    {
        lock (_lock) return [.. _history];
    }

    public void RestockAll()
    {
        lock (_lock)
        {
            _inventory["RF-PRO-100"] = _inventory["RF-PRO-100"] with { Stock = 12 };
            _inventory["RF-CELL-24"] = _inventory["RF-CELL-24"] with { Stock = 40 };
            _inventory["RF-HSM-KEY"] = _inventory["RF-HSM-KEY"] with { Stock = 2 };
            _inventory["RF-EMPTY-0"] = _inventory["RF-EMPTY-0"] with { Stock = 0 };
        }
    }

    /// <summary>
    /// Executes the 4-stage RailwayForge pipeline:
    ///   1. ValidateOrder (ValidationBag multi-error)
    ///   2. ReserveInventory (NotFound 404 / Conflict 409)
    ///   3. AuthorizePayment (Unauthorized 401 / Unavailable 503)
    ///   4. CommitOrder (201 Created)
    /// Records every step outcome into the append-only history log.
    /// </summary>
    public async Task<(Result<OrderReceipt, IReadOnlyList<ForgeError>> Result, ExecutionHistoryEntry History)> ProcessOrderAsync(
        OrderRequest req)
    {
        string runId = $"run-{Guid.NewGuid():N}"[..12];
        List<RailwayStepTrace> steps = [];
        string traceId = Activity.Current?.TraceId.ToString() ?? runId;

        var pipelineResult = await TraceSource.TraceAsync("webdemo.order_pipeline", async () =>
        {
            traceId = Activity.Current?.TraceId.ToString() ?? traceId;

            // Step 1: Multi-error input validation via ValidationBag<ForgeError>
            long t1 = Stopwatch.GetTimestamp();
            var validated = ValidateInput(req);
            double d1 = Stopwatch.GetElapsedTime(t1).TotalMilliseconds;
            if (validated.TryGetError(out var valErrors))
            {
                steps.Add(new(0, "ValidateOrder", "Error",
                    string.Join("; ", valErrors.Select(e => e.Message)), d1));
                return RailwayForge.Result.Fail<OrderReceipt, IReadOnlyList<ForgeError>>(valErrors);
            }
            steps.Add(new(0, "ValidateOrder", "Ok", $"{req.Quantity}x {req.Sku} for {req.CustomerEmail}", d1));

            // Step 2: Check & reserve inventory
            long t2 = Stopwatch.GetTimestamp();
            var stockRes = CheckInventory(req);
            double d2 = Stopwatch.GetElapsedTime(t2).TotalMilliseconds;
            if (stockRes.TryGetError(out var stockErr))
            {
                steps.Add(new(1, "CheckInventory", "Error", $"{stockErr.Code}: {stockErr.Message}", d2));
                return RailwayForge.Result.Fail<OrderReceipt, IReadOnlyList<ForgeError>>([stockErr]);
            }
            var item = stockRes.Match(v => v, _ => throw new UnreachableException());
            steps.Add(new(1, "CheckInventory", "Ok", $"In stock ({item.Stock} available @ ${item.UnitPrice:F2})", d2));

            // Step 3: Authorize payment
            long t3 = Stopwatch.GetTimestamp();
            var payRes = await AuthorizePaymentAsync(req, item.UnitPrice * req.Quantity);
            double d3 = Stopwatch.GetElapsedTime(t3).TotalMilliseconds;
            if (payRes.TryGetError(out var payErr))
            {
                steps.Add(new(2, "AuthorizePayment", "Error", $"{payErr.Code}: {payErr.Message}", d3));
                return RailwayForge.Result.Fail<OrderReceipt, IReadOnlyList<ForgeError>>([payErr]);
            }
            steps.Add(new(2, "AuthorizePayment", "Ok", $"Authorized via {req.PaymentToken[..Math.Min(8, req.PaymentToken.Length)]}...", d3));

            // Step 4: Commit stock deduction & issue receipt
            long t4 = Stopwatch.GetTimestamp();
            var commitRes = CommitOrder(req);
            double d4 = Stopwatch.GetElapsedTime(t4).TotalMilliseconds;
            if (commitRes.TryGetError(out var commitErr))
            {
                steps.Add(new(3, "CommitOrder", "Error", $"{commitErr.Code}: {commitErr.Message}", d4));
                return RailwayForge.Result.Fail<OrderReceipt, IReadOnlyList<ForgeError>>([commitErr]);
            }
            var receipt = commitRes.Match(v => v, _ => throw new UnreachableException());
            steps.Add(new(3, "CommitOrder", "Ok", $"Order {receipt.OrderId} committed (total ${receipt.Total:F2})", d4));

            return RailwayForge.Result.Ok<OrderReceipt, IReadOnlyList<ForgeError>>(receipt);
        });

        var entry = BuildHistoryEntry(runId, traceId, req, pipelineResult, steps);
        lock (_lock) _history.Add(entry);
        return (pipelineResult, entry);
    }

    private static Result<OrderRequest, IReadOnlyList<ForgeError>> ValidateInput(OrderRequest req)
    {
        var bag = new ValidationBag<ForgeError>();
        bag.Check(!string.IsNullOrWhiteSpace(req.CustomerEmail) && req.CustomerEmail.Contains('@'),
                new ForgeError.Validation("customerEmail", "Valid customer email is required."))
           .Check(!string.IsNullOrWhiteSpace(req.Sku),
                new ForgeError.Validation("sku", "SKU is required."))
           .Check(req.Quantity is >= 1 and <= 100,
                new ForgeError.Validation("quantity", "Quantity must be between 1 and 100."))
           .Check(!string.IsNullOrWhiteSpace(req.PaymentToken),
                new ForgeError.Validation("paymentToken", "Payment token is required."));
        return bag.Build(() => req);
    }

    private Result<SkuItem, ForgeError> CheckInventory(OrderRequest req)
    {
        lock (_lock)
        {
            if (!_inventory.TryGetValue(req.Sku, out var item))
                return new ForgeError.NotFound("Sku", req.Sku, $"SKU '{req.Sku}' does not exist in catalog.");
            if (item.Stock < req.Quantity)
                return new ForgeError.Conflict("Inventory",
                    $"Insufficient stock for '{item.Sku}': requested {req.Quantity}, available {item.Stock}.");
            return item;
        }
    }

    private static async Task<Result<string, ForgeError>> AuthorizePaymentAsync(OrderRequest req, decimal amount)
    {
        await Task.Yield();
        if (req.SimulateGatewayOutage)
            return new ForgeError.Unavailable("PaymentGateway", "Upstream payment processor timed out.");
        if (!req.PaymentToken.StartsWith("tok_", StringComparison.Ordinal))
            return new ForgeError.Unauthorized("Payment token rejected (must start with 'tok_').");
        return $"auth_{Guid.NewGuid():N}"[..14];
    }

    private Result<OrderReceipt, ForgeError> CommitOrder(OrderRequest req)
    {
        lock (_lock)
        {
            if (!_inventory.TryGetValue(req.Sku, out var item))
                return new ForgeError.NotFound("Sku", req.Sku, $"SKU '{req.Sku}' disappeared before commit.");
            if (item.Stock < req.Quantity)
                return new ForgeError.Conflict("Inventory",
                    $"Concurrent stock depletion for '{item.Sku}' (available {item.Stock}).");

            _inventory[item.Sku] = item with { Stock = item.Stock - req.Quantity };
            decimal subtotal = item.UnitPrice * req.Quantity;
            decimal tax = Math.Round(subtotal * 0.0825m, 2);
            return new OrderReceipt(
                $"ORD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                req.CustomerEmail,
                item.Sku,
                req.Quantity,
                subtotal,
                tax,
                subtotal + tax,
                DateTimeOffset.UtcNow);
        }
    }

    private static ExecutionHistoryEntry BuildHistoryEntry(
        string runId,
        string traceId,
        OrderRequest req,
        Result<OrderReceipt, IReadOnlyList<ForgeError>> result,
        List<RailwayStepTrace> steps)
    {
        if (result.TryGetValue(out var receipt, out var errors))
        {
            return new ExecutionHistoryEntry(
                runId, DateTimeOffset.UtcNow, traceId, req,
                Succeeded: true, HttpStatus: 201, ErrorCode: null,
                Summary: $"Order {receipt.OrderId} fulfilled (${receipt.Total:F2})",
                Receipt: receipt, Steps: steps);
        }

        int status = errors.Count > 1 ? 400 : errors[0] switch
        {
            ForgeError.Validation => 400,
            ForgeError.Unauthorized => 401,
            ForgeError.Forbidden => 403,
            ForgeError.NotFound => 404,
            ForgeError.Conflict => 409,
            ForgeError.Unavailable => 503,
            ForgeError.Unexpected => 500,
        };
        string code = errors.Count > 1 ? "VALIDATION_MULTIPLE" : errors[0].Code;
        string summary = string.Join(" | ", errors.Select(e => e.Message));

        return new ExecutionHistoryEntry(
            runId, DateTimeOffset.UtcNow, traceId, req,
            Succeeded: false, HttpStatus: status, ErrorCode: code,
            Summary: summary, Receipt: null, Steps: steps);
    }
}
