using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Events;
using MurmurHash.Net;
using Standart.Hash.xxHash;
using StackExchange.Redis;

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

    private void initializeDB() {
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

            var walCommand = connection.CreateCommand();
            walCommand.CommandText =
            @"
                PRAGMA journal_mode = 'wal'
            ";
            walCommand.ExecuteNonQuery();

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

    private void UpdateProgressBar(int current, int total) {
        var progress = (int)((double)current / total * 100);
        string progressText = $"Progress: {progress}% [{current}/{total}]";

        Console.Write("\r" + progressText.PadRight(Console.WindowWidth));
    }

    private Dictionary<string, string> CalculateChecksums(string[] filenames) {
        ConcurrentDictionary<string, string> checksums = new ConcurrentDictionary<string, string>();

        int totalFiles = filenames.Length;
        int processedFiles = 0;

        Parallel.ForEach(filenames, (filename, state) => {
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(filename)) {
                    var hash = md5.ComputeHash(stream);
                    var checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    lock (checksums) {
                        checksums.TryAdd(filename, checksum);
                    }
                }
                Interlocked.Increment(ref processedFiles);
                UpdateProgressBar(processedFiles, totalFiles);
            }
        });

        return new Dictionary<string, string>(checksums);
    }

    private Dictionary<string, uint> CalculateChecksumsWithMurmur(string[] filenames, int userDefinedBufferSize) {
        ConcurrentDictionary<string, uint> checksums = new ConcurrentDictionary<string, uint>();

        int totalFiles = filenames.Length;
        int processedFiles = 0;

        Parallel.ForEach(filenames, (filename, state) => {
                using (var stream = File.OpenRead(filename)) {
                    var hash = CalculateMurmurHash32(stream, userDefinedBufferSize);
                    lock (checksums) {
                        checksums.TryAdd(filename, hash);
                    }
                Interlocked.Increment(ref processedFiles);
                UpdateProgressBar(processedFiles, totalFiles);
            }
        });

        return new Dictionary<string, uint>(checksums);
    }

    private uint CalculateMurmurHash32(Stream stream, int userDefinedBufferSize) {
        int bufferSize = userDefinedBufferSize;
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

    private Dictionary<string, ulong> CalculateChecksumsWithXxHash3(string[] filenames, int userDefinedBufferSize) {
        ConcurrentDictionary<string, ulong> checksums = new ConcurrentDictionary<string, ulong>();

        int totalFiles = filenames.Length;
        int processedFiles = 0;

        Parallel.ForEach(filenames, (filename, state) => {
            using (var stream = File.OpenRead(filename)) {
                var hash = CalculateXxHash3(stream, userDefinedBufferSize);
                checksums.TryAdd(filename, hash);
            }
            Interlocked.Increment(ref processedFiles);
            UpdateProgressBar(processedFiles, totalFiles);
        });

        return new Dictionary<string, ulong>(checksums);
    }

    private ulong CalculateXxHash3(Stream stream, int userDefinedBufferSize) {
        int bufferSize = userDefinedBufferSize;
        const ulong seed = 123456U;

        var buffer = new byte[bufferSize];
        ulong hash = seed;
        
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
            hash = xxHash3.ComputeHash(buffer, buffer.Length);
        }

        return hash;
    }

    public void doTheThing(string hashAlgo, int bufferSize = 4096) {

        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
        IDatabase db = redis.GetDatabase();

        if (getTotalFileCount() < 1) {
            logger.Information("There were no files to checksum");
            return;
        }

        Dictionary<string, object> fileHashes;
        Dictionary<string, ulong>  fileHashesXxHash3;
        Dictionary<string, uint>  fileHashesMurmur;
        Dictionary<string, string> fileHashesMD5;

        switch (hashAlgo) {
            case "md5":
                fileHashesMD5 = CalculateChecksums(indexFiles());
                fileHashes = fileHashesMD5.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                break;
            case "murmur":
                fileHashesMurmur = CalculateChecksumsWithMurmur(indexFiles(), bufferSize);
                fileHashes = fileHashesMurmur.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                break;
            case "xxhash":
                fileHashesXxHash3 = CalculateChecksumsWithXxHash3(indexFiles(), bufferSize);
                fileHashes = fileHashesXxHash3.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                break;
            default:
                logger.Error("No valid hash algorithm was selected");
                throw new Exception($"{hashAlgo} is not a valid option. Valid options are MD5, Murmur and XxHash");
        }

        logger.Information("All files were checksummed");

        HashEntry[] hashEntries = fileHashes.Select(kv => new HashEntry(kv.Key, kv.Value.ToString())).ToArray();
        string hashKey = "fileHashes";
        db.HashSet(hashKey, hashEntries);
        logger.Information("Dictionary inserted into Redis.");
    }

    public void saveToSqlite() {
        initializeDB();

        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
        IDatabase db = redis.GetDatabase();

        HashEntry[] fileHashes = db.HashGetAll("fileHashes");
        logger.Information("Retrived all values from redis");

        using (var connection = new SqliteConnection("Data Source=" + DatabaseRoot + "chksum.db;Mode=ReadWrite")) {
            connection.Open();

            foreach (var file in fileHashes) {
                var absolutePathToFile = file.Name.ToString();
                string fileName = Path.GetFileName(absolutePathToFile.ToString());
                string pathToFile = Path.GetRelativePath(DatabaseRoot, absolutePathToFile.ToString());
                var fileHash = file.Value.ToString();

                if (checkIfFileMovedAndUpdatePathToFile(fileHash, fileName, pathToFile) || checkIfFileAlreadyExistsInDatabase(fileHash, fileName)) {
                    continue;
                }

                var InsertCommand = connection.CreateCommand();
                InsertCommand.CommandText =
                @"
                    INSERT INTO file (filehash, filename, pathtofile)
                    VALUES ($filehash, $filename, $pathtofile)
                ";
                InsertCommand.Parameters.AddWithValue("$filehash", fileHash);
                InsertCommand.Parameters.AddWithValue("$filename", fileName);
                InsertCommand.Parameters.AddWithValue("$pathtofile", pathToFile);
                InsertCommand.ExecuteNonQuery();
                logger.Verbose("{fileName} which is located at {pathToFile} relative to the database with the hash {fileHash} was successfully inserted into the database", fileName, pathToFile, fileHash);
            }
        }
        logger.Information("All filehashes were successfully inserted into the database");
        
        var keys = db.Execute("KEYS", "*");
        if (keys == null) {
            logger.Error("No values found in redis");
            return;
        }
        foreach (var key in (RedisValue[])keys) {
            db.KeyDelete((RedisKey)key.ToString());
        }
        logger.Information("Redis was successfully cleared of any remaining data");
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
            logger.Verbose("{pathToFile} with the hash {fileHash} was successfully loaded", pathToFile, fileHash);
        }

        if (fileHash == filehash) {
            logger.Verbose("File with filehash {filehash} already exists in the database", filehash);
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

                logger.Verbose("File moved or is a duplicate:\n\tfrom\t{pathToFile}\n\tto  \t{pathtofile}\n", pathToFile, pathtofile);
                wasMoved = true;
            }
            logger.Verbose("{fileName} which is located at {pathToFile} relative to the database with the hash {fileHash} was successfully checked", fileName, pathToFile, fileHash);
        }
        return wasMoved;
    }

    public void checkIfFileWasDeleted() {

        saveToSqlite();

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

                    logger.Information("File deleted:\n\t{pathToFile}", pathToFile);
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

        saveToSqlite();

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
        logger.Information("Successfully deleted libe_sqlite3.so");
        Console.ResetColor();
    }
}