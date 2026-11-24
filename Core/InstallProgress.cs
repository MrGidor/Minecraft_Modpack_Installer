namespace Modpack_Installer.Core;

public sealed record InstallProgress(string Message, double? Percent = null);

internal sealed class ScaledProgress : IProgress<InstallProgress>
{
    private readonly IProgress<InstallProgress>? _inner;
    private readonly double _start;
    private readonly double _end;

    public ScaledProgress(IProgress<InstallProgress>? inner, double start, double end)
    {
        _inner = inner;
        _start = start;
        _end = end;
    }

    public void Report(InstallProgress value)
    {
        if (_inner is null)
            return;

        double? percent = value.Percent is { } p
            ? _start + (_end - _start) * Math.Clamp(p, 0, 100) / 100.0
            : null;
        _inner.Report(value with { Percent = percent });
    }
}

internal sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
