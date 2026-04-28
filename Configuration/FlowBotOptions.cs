namespace FlowBot;

public sealed class FlowBotOptions
{
    public const string SectionName = "FlowBot";

    public string? Token { get; init; }

    public ulong? ServerId { get; init; }

    public string TimeZone { get; init; } = "Europe/Stockholm";
}
