using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ShellOverlay.Logging;

namespace ShellOverlay.Scripting;

public sealed class ShellGlobals
{
    public ShellGlobals(ShellApi shell) => Shell = shell;
    public ShellApi Shell { get; }
}

public sealed class CsxModuleHost : IDisposable
{
    private readonly Dictionary<string, ScriptState<object>> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _noRender = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public CsxModuleHost(ShellApi api) => _loadApi = api;

    private readonly ShellApi _loadApi;

    public async Task LoadDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            OverlayLog.Warn($"Modules directory missing: {directory}");
            return;
        }

        var options = CreateOptions();
        foreach (var file in Directory.GetFiles(directory, "*.csx"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var code = await File.ReadAllTextAsync(file);
                var script = CSharpScript.Create(code, options, typeof(ShellGlobals));
                var state = await script.RunAsync(new ShellGlobals(_loadApi));
                _states[name] = state;
                OverlayLog.Info($"Loaded module '{name}'");
            }
            catch (CompilationErrorException ex)
            {
                OverlayLog.Error($"Module '{name}' compile error: {string.Join("; ", ex.Diagnostics)}");
            }
            catch (Exception ex)
            {
                OverlayLog.Error($"Module '{name}' failed to load", ex);
            }
        }
    }

    public async Task InitAsync()
    {
        foreach (var name in _states.Keys.ToArray())
            await TryInvokeAsync(name, "OnInit()");
    }

    public Task ClickAsync(string module) => TryInvokeAsync(module, "OnClick()");

    public async Task<string?> RenderAsync(string module)
    {
        if (_noRender.Contains(module)) return null;
        if (!_states.TryGetValue(module, out var state)) return null;
        try
        {
            var next = await state.ContinueWithAsync<string?>("OnRender()");
            _states[module] = next;
            return next.ReturnValue;
        }
        catch (CompilationErrorException)
        {
            _noRender.Add(module);
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task TryInvokeAsync(string module, string expression)
    {
        if (!_states.TryGetValue(module, out var state))
        {
            OverlayLog.Warn($"Module '{module}' is not loaded");
            return;
        }

        try
        {
            var next = await state.ContinueWithAsync(expression);
            _states[module] = next;
        }
        catch (CompilationErrorException)
        {
            // optional hook missing
        }
        catch (Exception ex)
        {
            OverlayLog.Error($"Module '{module}' {expression} failed", ex);
        }
    }

    private static ScriptOptions CreateOptions()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .ToArray();

        return ScriptOptions.Default
            .AddReferences(refs)
            .AddImports("System", "System.IO", "System.Linq", "System.Diagnostics", "System.Threading.Tasks");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _states.Clear();
    }
}
