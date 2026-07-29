namespace Keepass.Background.Service;

public class MergeOptions
{
    public string BasePath { get; set; } = string.Empty;
    public string IncomingPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
