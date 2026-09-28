using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ShellOverlay.Logging;

namespace ShellOverlay.Scripting;

public sealed class ShellGlobals
{
    public ShellGlobals(ShellApi shell) => Shell = shell;
    public ShellApi Shell { get; }
}

/// <summary>
/// Compiles CSX modules once, extracts hook delegates, and invokes them with a timeout.
/// Delegates are cached — no unbounded ScriptState growth between render ticks.
/// </summary>
public sealed class CsxModuleHost : IDisposable
{
    private sealed class Module
    {
        public string Name { get; init; } = "";
        public Action? OnInit { get; set; }
        public Action? OnClick { get; set; }
        public Func<string?>? OnRender { get; set; }
    }

    private readonly ConcurrentDictionary<string, Module> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ShellApi _api;
    private readonly string _directory;
    private readonly TimeSpan _hookTimeout;
    private int _disposed;

    public CsxModuleHost(ShellApi api, string directory, TimeSpan? hookTimeout = null)
    {
        _api = api;
        _directory = directory;
        _hookTimeout = hookTimeout ?? TimeSpan.FromMilliseconds(100);
    }

    public async Task LoadDirectoryAsync(CancellationToken ct = default)
    {
        _modules.Clear();

        if (!Directory.Exists(_directory))
        {
            OverlayLog.Warn($"Modules directory missing: {_directory}");
            return;
        }

        var options = CreateOptions();
        var files = Directory.GetFiles(_directory, "*.csx");

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) return;

            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var code = await File.ReadAllTextAsync(file, ct);
                var module = await CompileModuleAsync(name, code, options);
                _modules[name] = module;
                OverlayLog.Info($"Loaded module '{name}'");
            }
            catch (CompilationErrorException ex)
            {
                OverlayLog.Error($"Module '{name}' compile error: {string.Join("; ", ex.Diagnostics)}");
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                OverlayLog.Error($"Module '{name}' failed to load", ex);
            }
        }
    }

    private async Task<Module> CompileModuleAsync(string name, string code, ScriptOptions options)
    {
        var module = new Module { Name = name };
        var script = CSharpScript.Create(code, options, typeof(ShellGlobals));
        var state = await script.RunAsync(new ShellGlobals(_api));

        module.OnInit = await TryExtractActionAsync(state, "OnInit");
        module.OnClick = await TryExtractActionAsync(state, "OnClick");
        module.OnRender = await TryExtractFuncAsync(state, "OnRender");

        return module;
    }

    private static async Task<Action?> TryExtractActionAsync(ScriptState<object> state, string methodName)
    {
        try
        {
            // Wraps the top-level method in an Action, compiled ONCE.
            var next = await state.ContinueWithAsync<Action>($"new Action(() => {methodName}())");
            return next.ReturnValue;
        }
        catch (CompilationErrorException) { return null; }
        catch (Exception ex)
        {
            OverlayLog.Warn($"Failed to bind {methodName}: {ex.Message}");
            return null;
        }
    }

    private static async Task<Func<string?>?> TryExtractFuncAsync(ScriptState<object> state, string methodName)
    {
        try
        {
            var next = await state.ContinueWithAsync<Func<string?>>($"new Func<string?>(() => {methodName}())");
            return next.ReturnValue;
        }
        catch (CompilationErrorException) { return null; }
        catch (Exception ex)
        {
            OverlayLog.Warn($"Failed to bind {methodName}: {ex.Message}");
            return null;
        }
    }

    public Task InitAsync()
    {
        var tasks = _modules.Values
            .Where(m => m.OnInit is not null)
            .Select(m => InvokeActionAsync(m.OnInit!, m.Name, "OnInit"));
        return Task.WhenAll(tasks);
    }

    public Task ClickAsync(string moduleName)
    {
        if (!_modules.TryGetValue(moduleName, out var module)) return Task.CompletedTask;
        if (module.OnClick is null) return Task.CompletedTask;
        return InvokeActionAsync(module.OnClick, module.Name, "OnClick");
    }

    public Task<string?> RenderAsync(string moduleName)
    {
        if (!_modules.TryGetValue(moduleName, out var module)) return Task.FromResult<string?>(null);
        if (module.OnRender is null) return Task.FromResult<string?>(null);
        return InvokeFuncAsync(module.OnRender, module.Name, "OnRender");
    }

    private async Task InvokeActionAsync(Action action, string moduleName, string hook)
    {
        try
        {
            var task = Task.Run(action);
            var completed = await Task.WhenAny(task, Task.Delay(_hookTimeout));
            if (completed != task)
            {
                OverlayLog.Warn($"Module '{moduleName}' {hook} timed out ({_hookTimeout.TotalMilliseconds}ms)");
                return;
            }
            await task;
        }
        catch (Exception ex)
        {
            OverlayLog.Error($"Module '{moduleName}' {hook} failed", ex);
        }
    }

    private async Task<string?> InvokeFuncAsync(Func<string?> func, string moduleName, string hook)
    {
        try
        {
            var task = Task.Run(func);
            var completed = await Task.WhenAny(task, Task.Delay(_hookTimeout));
            if (completed != task)
            {
                OverlayLog.Warn($"Module '{moduleName}' {hook} timed out ({_hookTimeout.TotalMilliseconds}ms)");
                return null;
            }
            return await task;
        }
        catch (Exception ex)
        {
            OverlayLog.Error($"Module '{moduleName}' {hook} failed", ex);
            return null;
        }
    }

    private static ScriptOptions CreateOptions()
    {
        // Explicit reference set — avoids loading every assembly in the AppDomain
        // and keeps compile times predictable.
        var refs = new[]
        {
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<>).Assembly,
            typeof(Task).Assembly,
            typeof(DateTime).Assembly,
            typeof(ShellApi).Assembly,
        };

        return ScriptOptions.Default
            .WithReferences(refs)
            .WithImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Threading.Tasks");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _modules.Clear();
    }
}