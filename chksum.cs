// Go into folder
// Check if any file is in there
// If there is a file. Calculate md5sum > filename.md5
// If there is no file. Repeat
public class chksum {
    
    // int getDirectoryCount() {
    //     int folderCount = Directory.GetDirectories(Directory.GetCurrentDirectory()).Length; // Get folder count in current directory
    //     return folderCount;
    // }

    private static int getFileCount() {
        int fileCount = Directory.GetFiles(Directory.GetCurrentDirectory()).Length; // Get file count in current directory
        return fileCount;
    }

    // string getParentFolder() {
    //     string parentFolder = Directory.GetParent(Directory.GetCurrentDirectory()).ToString(); // Get parent folder of current directory
    //     return parentFolder;
    // }

    private static string CalculateMD5(string filename) {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            using (var stream = File.OpenRead(filename))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public static void doTheThing() {
        foreach (var directory in Directory.GetDirectories(Directory.GetCurrentDirectory())) {
            Directory.SetCurrentDirectory(directory); // Set new root
            if (getFileCount() >= 1) {
                DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files) {
                    string fileName = file.Name;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string checksumFile = Directory.GetCurrentDirectory() + "/" + fileNameWithoutExtension + ".md5";
                    File.AppendAllText(checksumFile, CalculateMD5(fileName) + "  " + fileName);
                    Console.WriteLine(checksumFile);
                }
            }
            doTheThing();
        }
    }
}