using Chksum.Utils;

public class Program {
    static void Main(string[] args) {

        Console.ForegroundColor = ConsoleColor.Red;
        if (args.Length == 0) {
            Console.WriteLine("Please specify an option.");
            PrintAvailableOptions();
            return;
        } else if (args.Length > 3) {
            Console.WriteLine("Too many options.");
            return;
        }

        ChksumUtils utils = new ChksumUtils();

        utils.getBaseDir();

        utils.ExtractEmbeddedLibrary();

        Console.ForegroundColor = ConsoleColor.Green;
        switch (args[0]) {
            case "checksum":
                Console.WriteLine("Starting the checksum process.");
                Console.ResetColor();

                try {
                    int bufferSize = int.Parse(args[2]);
                    utils.doTheThing(args[1], bufferSize);
                }
                catch (FormatException) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Buffer was not a valid integer value. Please specify a valid integer value for the buffer size");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Checksum process finished");
                break;
            case "saveToSqlite":
                Console.ResetColor();
                utils.saveToSqlite();
                break;
            case "compareDatabases":
                Console.ResetColor();

                utils.compareDatabases(args[1]);
                break;
            case "checkIfFileWasDeleted":
                Console.ResetColor();
                utils.checkIfFileWasDeleted();
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

        utils.cleanup();
    }

    static void PrintAvailableOptions() {
        String[] options = {
            "checksum - MD5, Murmur and XxHash",
            "compareDatabases",
            "compareChecksums",
            "saveToSqlite",
            "checkIfFileWasDeleted",
            "help"
        };

        Console.ResetColor();
        Console.WriteLine("usage: chksum [option] \nHere is a list of all available options:");
        foreach (String option in options) {
            Console.WriteLine("\t" + option);
        }
    }
}