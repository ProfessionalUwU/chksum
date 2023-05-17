public class Program {
    static void Main(string[] args) {

        if (args.Length == 0) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Please specify an option.");
            Console.ResetColor();
            Console.WriteLine("Options are: checksum, countmd5, deletemd5, compareChecksums");
        } else {
            switch(args[0]) {
            case "checksum":
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Starting the checksum process.");
                Console.ResetColor();

                Chksum.doTheThing();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Checksum process finished");
                Console.ResetColor();
                break;
            case "countmd5":
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Counting md5 checksum files.");
                Console.ResetColor();

                Chksum.countAllMd5Checksums();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Finished counting all md5 checksum files.");
                Console.ResetColor();
                break;
            case "deletemd5":
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Deleting all md5 checksum files.");
                Console.ResetColor();

                Chksum.deleteAllMd5Checksums();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Deleted all md5 checksum files.");
                Console.ResetColor();
                break;
            case "compareChecksums":
                Chksum.compareChecksums();
                break;
            default:
                break;
            }
        }

    }
}