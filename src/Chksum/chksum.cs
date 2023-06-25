using System;
using System.IO;
using Microsoft.Data.Sqlite;

// Go into folder
// Check if any file is in there
// If there is a file. Calculate md5sum > filename.md5
// If there is no file. Repeat

namespace Chksum.Utils;
public class ChksumUtils {

    // int getDirectoryCount() {
    //     int folderCount = Directory.GetDirectories(Directory.GetCurrentDirectory()).Length; // Get folder count in current directory
    //     return folderCount;
    // }

    private int getFileCount() {
        int fileCount = Directory.GetFiles(Directory.GetCurrentDirectory()).Length; // Get file count in current directory
        return fileCount;
    }

    // string getParentFolder() {
    //     string parentFolder = Directory.GetParent(Directory.GetCurrentDirectory()).ToString(); // Get parent folder of current directory
    //     return parentFolder;
    // }

    public string DatabaseRoot { get; set; }
    public void getBaseDir() {
        DatabaseRoot = AppDomain.CurrentDomain.BaseDirectory;
    }

    public void initializeDB() {
        if (!File.Exists("chksum.db")) {
            using (var connection = new SqliteConnection("Data Source=chksum.db")) {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    CREATE TABLE file (
                        filehash TEXT NOT NULL PRIMARY KEY,
                        filename TEXT NOT NULL,
                        pathtofile TEXT NOT NULL,
                        artist TEXT,
                        playbacklength INTEGER
                    );
                ";
                command.ExecuteNonQuery();
            }
        }
    }

    public void cleanDB() {
        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db")) {
            var command = connection.CreateCommand();
            command.CommandText =
            @"
                vacuum;
            ";
            command.ExecuteNonQuery();
        }
    }

    private string CalculateMD5(string filename) {
        using (var md5 = System.Security.Cryptography.MD5.Create()) {
            using (var stream = File.OpenRead(filename)) {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public void doTheThing() {
        foreach (var directory in Directory.GetDirectories(Directory.GetCurrentDirectory())) using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
                Directory.SetCurrentDirectory(directory); // Set new root
                if (getFileCount() >= 1) {
                    DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                    FileInfo[] files = dir.GetFiles();
                    foreach (FileInfo file in files) {
                        string fileName = file.Name;
                        string absolutePathToFile = Path.GetFullPath(fileName);
                        string pathToFile = Path.GetRelativePath(DatabaseRoot, absolutePathToFile);
                        string fileHash = CalculateMD5(fileName);

                        if (checkIfFileAlreadyExists(fileHash, fileName) == false) {
                            connection.Open();

                            var command = connection.CreateCommand();
                            command.CommandText =
                            @"
                            INSERT INTO file (filehash, filename, pathtofile)
                            VALUES ($filehash, $filename, $pathtofile)
                            ";
                            command.Parameters.AddWithValue("$filehash", fileHash);
                            command.Parameters.AddWithValue("$filename", fileName);
                            command.Parameters.AddWithValue("$pathtofile", pathToFile);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                doTheThing();
            }
    }

    private bool checkIfFileAlreadyExists(string fileHash, string fileName) {
        string filehash = string.Empty;
        string filename = string.Empty;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT filehash, filename FROM file WHERE filehash = $filehash
            ";
            command.Parameters.AddWithValue("$filehash", fileHash);

            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    filehash = reader.GetString(0);
                    filename = reader.GetString(1);
                }
            }
        }

        if (fileHash == filehash) {
            Console.WriteLine($"Duplicate files found: {fileName} with the hash {fileHash} is identical to {filename} with the hash {filehash}");
            return true;
        } else {
            return false;
        }
    }

    private int getTotalFileCount() {
        int totalFileCount = Directory.GetFiles(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories).Length;
        return totalFileCount - 1; // Remove the program from the totalFileCount
    }

    public void countAllMd5Checksums() {
        int totalMD5FileCount = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.md5", SearchOption.AllDirectories).Length;
        Console.WriteLine("There are " + totalMD5FileCount + " md5 checksums");
    }

    public void deleteAllMd5Checksums() {
        foreach (var directory in Directory.GetDirectories(Directory.GetCurrentDirectory())) {
            Directory.SetCurrentDirectory(directory); // Set new root
            if (getFileCount() >= 1) {
                DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files) {
                    string fileName = file.Name;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string checksumFile = Directory.GetCurrentDirectory() + "/" + fileNameWithoutExtension + ".md5";
                    File.Delete(checksumFile);
                    Console.WriteLine("Deleted " + checksumFile);
                }
            }
            deleteAllMd5Checksums();
        }
    }

    public void compareChecksums() {
        foreach (var directory in Directory.GetDirectories(Directory.GetCurrentDirectory())) {
            Directory.SetCurrentDirectory(directory); // Set new root
            if (getFileCount() >= 1) {
                DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                FileInfo[] files = dir.GetFiles();
                // files.ToList().ForEach(i => Console.WriteLine(i.ToString())); // Print all files in files array
                foreach (FileInfo file in files) {
                    string fileName = file.Name;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string checksumFile = Directory.GetCurrentDirectory() + "/" + fileNameWithoutExtension + ".md5";
                    string fileMd5Checksum = fileNameWithoutExtension + ".md5";
                    if (File.Exists(fileMd5Checksum)) {
                        string newFileChecksum = CalculateMD5(fileName) + "  " + fileName;
                        string existingFileChecksum = File.ReadAllText(fileMd5Checksum);
                        string newFileName = newFileChecksum.Substring(34);
                        string existingFileName = existingFileChecksum.Substring(34);
                        if (newFileChecksum.Equals(existingFileChecksum)) {
                            Console.WriteLine(newFileName + " and " + existingFileName + " are the same.");
                        } else {
                            Console.WriteLine(newFileName + " and " + existingFileName + " are not the same.");
                            Console.WriteLine("The checksum of " + newFileName + " is " + newFileChecksum);
                            Console.WriteLine("The checksum of the already exting file " + existingFileName + " is " + existingFileChecksum);
                            // TODO Tell the user to check which file is the correct one
                        }
                    } else {
                        File.AppendAllText(checksumFile, CalculateMD5(fileName) + "  " + fileName);
                        Console.WriteLine("Calculated checksum for: " + checksumFile);
                    }
                }
            }
            compareChecksums();
        }
    }
}