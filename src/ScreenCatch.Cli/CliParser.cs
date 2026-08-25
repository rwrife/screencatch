namespace ScreenCatch.Cli;

public sealed class CliUsageException(string message) : Exception(message);

public sealed class CliInvocation
{
    internal CliInvocation(
        string verb,
        string? action,
        IReadOnlyList<string> positional,
        IReadOnlyDictionary<string, string?> options)
    {
        Verb = verb;
        Action = action;
        Positional = positional;
        Options = options;
    }

    public string Verb { get; }

    public string? Action { get; }

    public IReadOnlyList<string> Positional { get; }

    public IReadOnlyDictionary<string, string?> Options { get; }

    public bool Json => Has("json");

    public bool Has(string name) => Options.ContainsKey(name);

    public string? Get(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public string Require(string name) => Get(name)
        ?? throw new CliUsageException($"Missing required option --{name}.");
}

public static class CliParser
{
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "record", "gif", "trim", "probe", "preset", "help",
    };

    private static readonly HashSet<string> BooleanOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json", "frame-accurate",
    };

    private static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "source", "rect", "title", "monitor", "fps", "format", "quality", "audio",
        "out", "in", "start", "end", "duration", "width", "preset", "ffmpeg", "ffprobe",
    };

    public static CliInvocation Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            throw new CliUsageException("A command is required. Use 'screencatch help'.");
        }

        var verb = arguments[0].ToLowerInvariant();
        if (!Verbs.Contains(verb))
        {
            throw new CliUsageException($"Unknown command '{arguments[0]}'.");
        }

        string? action = null;
        var index = 1;
        if (verb == "preset" && index < arguments.Count && !arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            action = arguments[index].ToLowerInvariant();
            index++;
        }

        var positional = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        while (index < arguments.Count)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(argument);
                index++;
                continue;
            }

            var token = argument[2..];
            var equalsIndex = token.IndexOf('=');
            var name = equalsIndex >= 0 ? token[..equalsIndex] : token;
            if (BooleanOptions.Contains(name))
            {
                if (equalsIndex >= 0)
                {
                    throw new CliUsageException($"Option --{name} does not take a value.");
                }

                options[name] = null;
                index++;
                continue;
            }

            if (!ValueOptions.Contains(name))
            {
                throw new CliUsageException($"Unknown option --{name}.");
            }

            if (equalsIndex >= 0)
            {
                var inlineValue = token[(equalsIndex + 1)..];
                if (inlineValue.Length == 0)
                {
                    throw new CliUsageException($"Option --{name} requires a value.");
                }

                options[name] = inlineValue;
                index++;
                continue;
            }

            if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new CliUsageException($"Option --{name} requires a value.");
            }

            options[name] = arguments[index + 1];
            index += 2;
        }

        ValidateCommandArguments(verb, action, positional, options);
        return new CliInvocation(verb, action, positional, options);
    }

    private static void ValidateCommandArguments(
        string verb,
        string? action,
        IReadOnlyList<string> positional,
        IReadOnlyDictionary<string, string?> options)
    {
        if (verb != "preset" && positional.Count != 0)
        {
            throw new CliUsageException($"Command '{verb}' does not accept positional arguments.");
        }

        var allowed = verb switch
        {
            "record" => CreateSet("source", "rect", "title", "monitor", "fps", "format", "quality", "audio", "out", "duration", "preset", "ffmpeg", "json"),
            "gif" => CreateSet("in", "out", "start", "end", "fps", "format", "width", "ffmpeg", "ffprobe", "json"),
            "trim" => CreateSet("in", "out", "start", "end", "frame-accurate", "ffmpeg", "json"),
            "probe" => CreateSet("in", "ffprobe", "json"),
            "help" => CreateSet(),
            "preset" when action == "save" => CreateSet("source", "rect", "title", "monitor", "fps", "format", "quality", "audio", "json"),
            "preset" => CreateSet("json"),
            _ => CreateSet(),
        };

        var invalid = options.Keys.FirstOrDefault(option => !allowed.Contains(option));
        if (invalid is not null)
        {
            var command = action is null ? verb : $"{verb} {action}";
            throw new CliUsageException($"Option --{invalid} does not apply to '{command}'.");
        }
    }

    private static HashSet<string> CreateSet(params string[] values) =>
        new(values, StringComparer.OrdinalIgnoreCase);
}
