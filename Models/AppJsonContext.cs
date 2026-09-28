using System.Text.Json.Serialization;
using ShellOverlay.Config;

namespace ShellOverlay.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ShortcutsFile))]
internal partial class AppJsonContext : JsonSerializerContext;
