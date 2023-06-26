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

                    if (checkIfFileMovedAndUpdatePathToFile(fileHash, fileName, pathToFile) == false && checkIfFileAlreadyExistsInDatabase(fileHash, fileName) == false) {
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

    private bool checkIfFileAlreadyExistsInDatabase(string fileHash, string pathToFile) {
        string filehash = string.Empty;
        string pathtofile = string.Empty;
        bool doesExist = false;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadOnly")) {
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

    public void checkIfFileWasDeleted() {
        string pathToFile = string.Empty;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText =
            @"
                Select pathtofile FROM file
            ";

            using (var reader = selectCommand.ExecuteReader()) {
                while (reader.Read()) {
                    pathToFile = reader.GetString(0);
                    
                    if (!File.Exists(pathToFile)) {
                        var deleteCommand = connection.CreateCommand();
                        deleteCommand.CommandText =
                        @"
                            DELETE FROM file
                            WHERE pathtofile = $pathtofile
                        ";
                        deleteCommand.Parameters.AddWithValue("$pathtofile", pathToFile);
                        deleteCommand.ExecuteNonQuery();

                        Console.WriteLine("File deleted:");
                        Console.WriteLine($"\t{pathToFile}\n");
                    }
                }
            }
        }
    }

    public void compareDatabases(string filePathToOtherDatabase) {
        List<string> filehashesOfOriginDatabase = new List<string>();
        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadOnly")) {
            string filehash = string.Empty;
            
            connection.Open();

            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText =
            @"
                Select filehash FROM file
            ";

            using (var reader = selectCommand.ExecuteReader()) {
                while (reader.Read()) {
                    filehash = reader.GetString(0);
                    filehashesOfOriginDatabase.Add(filehash);
                }
            }
        }

        List<string> filehashesOfRemoteDatabase = new List<string>();
        using (var connection = new SqliteConnection("Data Source=" + filePathToOtherDatabase + ";Mode=ReadOnly")) {
            string filehash = string.Empty;
            
            connection.Open();

            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText =
            @"
                Select filehash FROM file
            ";

            using (var reader = selectCommand.ExecuteReader()) {
                while (reader.Read()) {
                    filehash = reader.GetString(0);
                    filehashesOfRemoteDatabase.Add(filehash);
                }
            }
        }

        List<string> filesThatDoNotExistsInTheRemote = filehashesOfOriginDatabase.Except(filehashesOfRemoteDatabase).ToList();
        //List<string> filesThatDoNotExistsInTheOrigin = filehashesOfRemoteDatabase.Except(filehashesOfOriginDatabase).ToList();

        foreach (string file in filesThatDoNotExistsInTheRemote) {
            using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadOnly")) {
                string filename = string.Empty;
                
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText =
                @"
                    Select filename FROM file WHERE filehash = $filehash
                ";
                selectCommand.Parameters.AddWithValue("$filehash", file);

                using (var reader = selectCommand.ExecuteReader()) {
                    while (reader.Read()) {
                        filename = reader.GetString(0);
                        
                        Console.WriteLine("File not found in remote:");
                        Console.WriteLine($"\t{filename}\n");
                    }
                }
            }
        }
    }

    public void cleanup() {
        File.Delete(libraryPath);
    }
}