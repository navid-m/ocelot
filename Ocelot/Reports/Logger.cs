using Ocelot.Reports.Data;

namespace Ocelot.Reports
{
    internal static class Logger
    {
        public static void LogIssue(
            string errorMessage,
            Location location = Location.MAIN,
            bool fatal = false
        )
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(
                $"\n-------------\n\nError in {location}:\t {errorMessage}\n\n-------------\n"
            );
            Console.ResetColor();
            if (fatal)
            {
                throw new Exception(errorMessage);
            }
        }
    }
}
