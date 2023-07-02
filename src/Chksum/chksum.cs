using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Events;
using MurmurHash.Net;
using Standart.Hash.xxHash;

namespace Chksum.Utils;
public class ChksumUtils {
    private ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Error)
            .WriteTo.File("chksum.log")
            .CreateLogger();

    private int getTotalFileCount() {
        int totalFileCount = Directory.GetFiles(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories).Length;
        logger.Debug("Total file count is {totalFileCount}", totalFileCount);
        return totalFileCount - 4; // Remove the program, datbase, log and library from the totalFileCount
    }

    private string[] indexFiles() {
        string[] indexedFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories);
        string[] filesToExclude = { "Chksum", "chksum.db", "libe_sqlite3.so" };
        indexedFiles = indexedFiles.Where(file => !filesToExclude.Contains(Path.GetFileName(file))).ToArray();
        logger.Information("All files were indexed");
        return indexedFiles;
    }

    public string DatabaseRoot { get; set; } = string.Empty;
    public void getBaseDir() {
        DatabaseRoot = AppDomain.CurrentDomain.BaseDirectory;
        logger.Debug("DatabaseRoot is {DatabaseRoot}", DatabaseRoot);
    }

    public string libraryPath { get; set; } = string.Empty;
    public void ExtractEmbeddedLibrary() {
        libraryPath = Path.Combine(DatabaseRoot, "libe_sqlite3.so");

        using (Stream? resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chksum.Libraries.libe_sqlite3.so")) {
            if (resourceStream != null) {
                byte[] buffer = new byte[resourceStream.Length];
                resourceStream.Read(buffer, 0, buffer.Length);
                File.WriteAllBytes(libraryPath, buffer);
                logger.Debug("libe_sqlite3.so was successfully created");
            } else {
                logger.Error("libe_sqlite3.so could not be loaded");
                throw new Exception(libraryPath + " could not be loaded");
            }
        }
    }

    public void initializeDB() {
        if (File.Exists("chksum.db")) {
            logger.Information("A database already exits");
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
            logger.Information("Database was successfully created");
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
            logger.Debug("Database was successfully vacuumed");
        }
    }

    private Dictionary<string, string> CalculateChecksums(string[] filenames) {
        ConcurrentDictionary<string, string> checksums = new ConcurrentDictionary<string, string>();

        Parallel.ForEach(filenames, (filename, state) => {
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(filename)) {
                    var hash = md5.ComputeHash(stream);
                    var checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    lock (checksums) {
                        checksums.TryAdd(filename, checksum);
                    }
                }
            }
        });

        logger.Debug("All files were checksummed");
        return new Dictionary<string, string>(checksums);
    }

    private Dictionary<string, uint> CalculateChecksumsWithMurmur(string[] filenames) {
        ConcurrentDictionary<string, uint> checksums = new ConcurrentDictionary<string, uint>();

        Parallel.ForEach(filenames, (filename, state) => {
                using (var stream = File.OpenRead(filename)) {
                    var hash = CalculateMurmurHash32(stream);
                    lock (checksums) {
                        checksums.TryAdd(filename, hash);
                    }
            }
        });

        logger.Debug("All files were checksummed");
        return new Dictionary<string, uint>(checksums);
    }

    private uint CalculateMurmurHash32(Stream stream) {
        const int bufferSize = 4096;
        const uint seed = 123456U;
        
        var buffer = new byte[bufferSize];
        uint hash = seed;

        int bytesRead;
        ReadOnlySpan<byte> span = buffer;

        while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0) {
            hash = MurmurHash3.Hash32(bytes: span, seed: 123456U);
        }
        return hash;
    }

    private Dictionary<string, ulong> CalculateChecksumsWithXxHash3(string[] filenames) {
        ConcurrentDictionary<string, ulong> checksums = new ConcurrentDictionary<string, ulong>();

        Parallel.ForEach(filenames, (filename, state) => {
            using (var stream = File.OpenRead(filename)) {
                var hash = CalculateXxHash3(stream);
                checksums.TryAdd(filename, hash);
            }
        });

        return new Dictionary<string, ulong>(checksums);
    }

    private ulong CalculateXxHash3(Stream stream) {
        const int bufferSize = 4096;
        const ulong seed = 123456U;

        var buffer = new byte[bufferSize];
        ulong hash = seed;
        
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
            hash = xxHash3.ComputeHash(buffer, buffer.Length);
        }

        return hash;
    }


    public void doTheThing(string hashalgo, int bufferSize) {
        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            if (getTotalFileCount() < 1) {
                logger.Information("There were no files to checksum");
                return;
            }
            connection.Open();

            Dictionary<string, object> fileHashes;
            Dictionary<string, ulong>  fileHashesXxHash3;
            Dictionary<string, uint>  fileHashesMurmur;
            Dictionary<string, string> fileHashesMD5;

            switch (hashalgo) {
                case "MD5":
                    fileHashesMD5 = CalculateChecksums(indexFiles());
                    fileHashes = fileHashesMD5.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                    break;
                case "Murmur":
                    fileHashesMurmur = CalculateChecksumsWithMurmur(indexFiles());
                    fileHashes = fileHashesMurmur.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                    break;
                case "XxHash":
                    fileHashesXxHash3 = CalculateChecksumsWithXxHash3(indexFiles());
                    fileHashes = fileHashesXxHash3.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                    break;
                default:
                    logger.Error("No valid hash algorithm was selected");
                    throw new Exception($"{hashalgo} is not a valid option. Valid options are MD5, Murmur and XxHash");
            }
            
            foreach (var file in fileHashes) {
                string absolutePathToFile = file.Key;
                string fileName = Path.GetFileName(absolutePathToFile);
                string pathToFile = Path.GetRelativePath(DatabaseRoot, absolutePathToFile);
                var fileHash = file.Value;
                
                if (checkIfFileMovedAndUpdatePathToFile(fileHash, fileName, pathToFile) == false && checkIfFileAlreadyExistsInDatabase(fileHash, fileName) == false) {
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
                    logger.Verbose("{fileName} which is located at {pathToFile} relative to the database with the hash {fileHash} was successfully inserted into the database", fileName, pathToFile, fileHash);
                }
            }
            logger.Information("All files were successfully written to the database");
        }
    }

    private bool checkIfFileAlreadyExistsInDatabase(object fileHash, string pathToFile) {
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
            command.Parameters.AddWithValue("$filehash", fileHash.ToString());

            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    filehash = reader.GetString(0);
                    pathtofile = reader.GetString(1);
                }
            }
            logger.Verbose("{pathToFile} with the hash {fileHash} was successfully loaded", pathToFile, fileHash.ToString());
        }

        if (fileHash.ToString() == filehash) {
            logger.Verbose("File with filehash {filehash} already exists in the database", filehash);
            doesExist = true;
        }
        return doesExist;
    }

    private bool checkIfFileMovedAndUpdatePathToFile(object fileHash, string fileName, string pathToFile) {
        string pathtofile = string.Empty;
        bool wasMoved = false;

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT pathtofile FROM file WHERE filehash = $filehash
            ";
            command.Parameters.AddWithValue("$filehash", fileHash.ToString());

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
                command2.Parameters.AddWithValue("$filehash", fileHash.ToString());
                command2.ExecuteNonQuery();

                //Console.WriteLine("File moved or is a duplicate:");
                //Console.WriteLine($"\tfrom\t{pathToFile}");
                //Console.WriteLine($"\tto  \t{pathtofile}\n");
                wasMoved = true;
            }
            logger.Verbose("{fileName} which is located at {pathToFile} relative to the database with the hash {fileHash} was successfully checked", fileName, pathToFile, fileHash.ToString());
        }
        return wasMoved;
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
                    
                    if (File.Exists(pathToFile)) {
                        logger.Verbose("{pathToFile} exists", pathToFile);
                        continue;
                    }
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText =
                    @"
                        DELETE FROM file
                        WHERE pathtofile = $pathtofile
                    ";
                    deleteCommand.Parameters.AddWithValue("$pathtofile", pathToFile);
                    deleteCommand.ExecuteNonQuery();

                    //Console.WriteLine("File deleted:");
                    //Console.WriteLine($"\t{pathToFile}\n");
                    logger.Information("File deleted: {pathToFile}", pathToFile);
                }
            }
            logger.Information("All deleted files were successfully removed from the database");
        }
    }

    private List<string> getFilehashesFromDatabase(string connectionString) {
        List<string> filehashesFromDatabase = new List<string>();
        
        using (var connection = new SqliteConnection(connectionString)) {
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
                    filehashesFromDatabase.Add(filehash);
                }
            }
        }

        logger.Debug("All filehashes were successfully retrived from the database");
        return filehashesFromDatabase;
    }

    public void compareDatabases(string filePathToOtherDatabase) {
        if (!File.Exists(filePathToOtherDatabase)) {
            logger.Error("No database could be found at {filePathToOtherDatabase}", filePathToOtherDatabase);
            throw new Exception("No database could be found at " + filePathToOtherDatabase);
        }

        List<string> filesThatDoNotExistsInTheRemote = getFilehashesFromDatabase("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadOnly").Except(getFilehashesFromDatabase("Data Source=" + filePathToOtherDatabase + ";Mode=ReadOnly")).ToList();

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
                        logger.Information("{filename} could not be found in the remote database", filename);
                    }
                }
            }
        }
        logger.Information("Compared both databases successfully");
    }

    public void cleanup() {
        File.Delete(libraryPath);
        logger.Debug("Successfully deleted libe_sqlite3.so");
    }
}