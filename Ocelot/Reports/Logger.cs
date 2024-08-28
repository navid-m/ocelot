using Ocelot.Reports.Data;

namespace Ocelot.Reports;

internal static class Logger
{
    private const int StatusContainerWidth = 80;

    public static void LogIssue(
        string errorMessage,
        Location location = Location.MAIN,
        bool fatal = false,
        bool truncate = false
    )
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(FormatMessage(errorMessage, location, "Error", truncate));
        Console.ResetColor();
        if (fatal)
        {
            throw new Exception(errorMessage);
        }
    }

    public static void LogInfo(
        string infoMessage,
        Location location = Location.MAIN,
        bool truncate = false,
        bool specifyNoLocation = false
    )
    {
        string formattedMessage = FormatMessage(
            infoMessage,
            location,
            "Info",
            truncate,
            specifyNoLocation
        );
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(formattedMessage);
        Console.ResetColor();
    }

    private static string FormatMessage(
        string message,
        Location location,
        string type,
        bool truncate,
        bool silent = false
    )
    {
        string truncatedMessage = message;
        string truncationNotice = string.Empty;

        if (
            truncate
            && message.Length + location.ToString().Length + type.Length + 18
                > StatusContainerWidth - 4
        )
        {
            int maxMessageLength =
                StatusContainerWidth - 18 - location.ToString().Length - type.Length;
            truncatedMessage = message[..maxMessageLength] + "...";
            truncationNotice =
                $"[Message truncated: {message.Length - maxMessageLength} characters omitted]\n";
        }

        string locationSpecifier = string.Empty;

        if (!silent)
            locationSpecifier = $" {type} in {location}:\n".PadRight(StatusContainerWidth - 2);

        string divider = $"{new string('─', StatusContainerWidth - 2)}";
        string truncationLine = string.IsNullOrEmpty(truncationNotice)
            ? string.Empty
            : $" {truncationNotice, -(StatusContainerWidth - 4)}";

        return $"\n{divider}\n{locationSpecifier}"
            + $"{truncatedMessage, -(StatusContainerWidth - 4)} \n{truncationLine}{divider}\n";
    }
}
