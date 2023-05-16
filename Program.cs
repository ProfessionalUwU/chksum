// Go into folder
// Check if any file is in there
// If there is a file. Calculate md5sum > filename.md5
// If there is no file. Repeat
public class Program {
    static void Main(string[] args) {
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Starting the checksum process.");
        Console.ResetColor();

        chksum.doTheThing();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Checksum process finished");
        Console.ResetColor();

    }
}