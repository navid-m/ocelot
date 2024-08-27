using Ocelot.Reports.Data;

namespace Ocelot.Reports
{
    internal static class StatusLogger
    {
        public static void LogFailure(string errorMessage, Location location = Location.MAIN)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(
                $"\n-------------\n\nError in {location}:\t {errorMessage}\n\n-------------\n"
            );
            Console.ResetColor();
        }
    }
}
