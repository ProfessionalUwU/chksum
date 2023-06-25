using System.Reflection;
using Microsoft.Data.Sqlite;
namespace Chksum.Utils;
public class ChksumUtils {

    private int getFileCount() {
        int fileCount = Directory.GetFiles(Directory.GetCurrentDirectory()).Length; // Get file count in current directory
        return fileCount;
    }

    public string DatabaseRoot { get; set; } = string.Empty;
    public void getBaseDir() {
        DatabaseRoot = AppDomain.CurrentDomain.BaseDirectory;
    }

    public string libraryPath { get; set; } = string.Empty;
    public void ExtractEmbeddedLibrary() {
        libraryPath = Path.Combine(DatabaseRoot, "libe_sqlite3.so");

        using (Stream? resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chksum.Libraries.libe_sqlite3.so")) {
            if (resourceStream != null) {
                byte[] buffer = new byte[resourceStream.Length];
                resourceStream.Read(buffer, 0, buffer.Length);
                File.WriteAllBytes(libraryPath, buffer);
            } else {
                throw new Exception(libraryPath + " could not be loaded");
            }
        }
    }

    public void initializeDB() {
        if (File.Exists("chksum.db")) {
            return;
        }

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
        foreach (var directory in Directory.GetDirectories(Directory.GetCurrentDirectory())) 
        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            Directory.SetCurrentDirectory(directory); // Set new root
            if (getFileCount() >= 1) {
                DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files) {
                    string fileName = file.Name;
                    string absolutePathToFile = Path.GetFullPath(fileName);
                    string pathToFile = Path.GetRelativePath(DatabaseRoot, absolutePathToFile);
                    string fileHash = CalculateMD5(fileName);

                    if (checkIfFileMovedAndUpdatePathToFile(fileHash, fileName, pathToFile) == false && checkIfFileAlreadyExists(fileHash, fileName) == false) {
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

    private bool checkIfFileAlreadyExists(string fileHash, string pathToFile) {
        string filehash = string.Empty;
        string pathtofile = string.Empty;
        bool doesExist = false;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT filehash, pathtofile FROM file WHERE filehash = $filehash
            ";
            command.Parameters.AddWithValue("$filehash", fileHash);

            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    filehash = reader.GetString(0);
                    pathtofile = reader.GetString(1);
                }
            }
        }

        if (fileHash == filehash) {
            Console.WriteLine("Duplicate files found:");
            Console.WriteLine($"\toriginal\t{pathToFile}");
            Console.WriteLine($"\tduplicate\t{pathtofile}\n");
            doesExist = true;
        }
        return doesExist;
    }

    private bool checkIfFileMovedAndUpdatePathToFile(string fileHash, string fileName, string pathToFile) {
        string pathtofile = string.Empty;
        bool wasMoved = false;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT pathtofile FROM file WHERE filehash = $filehash
            ";
            command.Parameters.AddWithValue("$filehash", fileHash);

            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    pathtofile = reader.GetString(0);
                }
            }

            if (pathToFile != pathtofile && pathtofile != "") {
                var command2 = connection.CreateCommand();
                command2.CommandText =
                @"
                    UPDATE file
                    SET pathtofile = $newpathtofile
                    WHERE filehash = $filehash
                ";
                command2.Parameters.AddWithValue("$newpathtofile", pathToFile);
                command2.Parameters.AddWithValue("$filehash", fileHash);
                command2.ExecuteNonQuery();

                Console.WriteLine("File moved:");
                Console.WriteLine($"\tfrom\t{pathToFile}");
                Console.WriteLine($"\tto  \t{pathtofile}\n");
                wasMoved = true;
            }
            return wasMoved;
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

    public void cleanup() {
        File.Delete(libraryPath);
    }
}