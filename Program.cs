public class Program {
    static void Main(string[] args) {
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Starting the checksum process.");
        Console.ResetColor();

        Chksum.doTheThing();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Checksum process finished");
        Console.ResetColor();

    }
}