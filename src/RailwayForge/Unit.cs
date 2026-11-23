namespace RailwayForge;

/// <summary>
/// Zero-size unit value representing successful completion of a void-equivalent operation
/// returning <see cref="Result{TError}"/>.
/// </summary>
public readonly record struct Unit
{
    /// <summary>The sole instance of <see cref="Unit"/>.</summary>
    public static readonly Unit Value = default;

    /// <inheritdoc />
    public override string ToString() => "()";
}
