public class Program {
    static void Main(string[] args) {

        Console.ForegroundColor = ConsoleColor.Red;
        if (args.Length == 0) {
            Console.WriteLine("Please specify an option.");
            PrintAvailableOptions();
            return;
        } else if (args.Length > 1) {
            Console.WriteLine("Too many options.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        switch (args[0]) {
            case "checksum":
                Console.WriteLine("Starting the checksum process.");
                Console.ResetColor();

                Chksum.doTheThing();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Checksum process finished");
                break;
            case "countmd5":
                Console.WriteLine("Counting md5 checksum files.");
                Console.ResetColor();

                Chksum.countAllMd5Checksums();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Finished counting all md5 checksum files.");
                break;
            case "deletemd5":
                Console.WriteLine("Deleting all md5 checksum files.");
                Console.ResetColor();

                Chksum.deleteAllMd5Checksums();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Deleted all md5 checksum files.");
                break;
            case "compareChecksums":
                Console.WriteLine("Comparing all md5 checksum files. If there is none, creating one.");
                Console.ResetColor();

                Chksum.compareChecksums();
                break;
            case "help":
                PrintAvailableOptions();
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid option. Maybe you mistyped it?");
                PrintAvailableOptions();
                break;
        }
    }

    static void PrintAvailableOptions() {
        String[] options = {
            "checksum",
            "countmd5",
            "deletemd5",
            "compareChecksums",
            "help"
        };

        Console.ResetColor();
        Console.WriteLine("usage: chksum [option]");
        Console.WriteLine("Here is a list of all available options:");
        foreach (String option in options) {
            Console.WriteLine("\t" + option);
        }
    }
}