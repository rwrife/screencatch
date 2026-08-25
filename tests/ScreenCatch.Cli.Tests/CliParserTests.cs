using ScreenCatch.Cli;

namespace ScreenCatch.Cli.Tests;

public sealed class CliParserTests
{
    [Fact]
    public void Parse_RecordCommand_CapturesScriptFriendlyOptions()
    {
        var invocation = CliParser.Parse([
            "record", "--source", "region", "--rect", "10,20,640,360",
            "--fps", "15", "--format", "mp4", "--quality", "27",
            "--audio", "mic", "--out", "demo.mp4", "--json",
        ]);

        Assert.Equal("record", invocation.Verb);
        Assert.Null(invocation.Action);
        Assert.Equal("region", invocation.Get("source"));
        Assert.Equal("10,20,640,360", invocation.Get("rect"));
        Assert.Equal("demo.mp4", invocation.Get("out"));
        Assert.True(invocation.Json);
    }

    [Fact]
    public void Parse_PresetCommand_SeparatesActionAndName()
    {
        var invocation = CliParser.Parse(["preset", "save", "issue-repro", "--fps", "12"]);

        Assert.Equal("preset", invocation.Verb);
        Assert.Equal("save", invocation.Action);
        Assert.Equal("issue-repro", invocation.Positional.Single());
        Assert.Equal("12", invocation.Get("fps"));
    }

    [Fact]
    public void Parse_MissingOptionValue_ThrowsUsageError()
    {
        var error = Assert.Throws<CliUsageException>(() => CliParser.Parse(["record", "--out"]));

        Assert.Contains("--out", error.Message);
    }

    [Fact]
    public void Parse_RejectsOptionsThatDoNotApplyToTheCommand()
    {
        var error = Assert.Throws<CliUsageException>(() => CliParser.Parse(["probe", "--in", "demo.mp4", "--fps", "10"]));

        Assert.Contains("--fps", error.Message);
        Assert.Throws<CliUsageException>(() => CliParser.Parse(["trim", "unexpected", "--in", "demo.mp4"]));
    }
}
