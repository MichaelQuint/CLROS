using System.Text;
using Microsoft.Data.Sqlite;

// Direct ADO.NET contract measurement, independent of EF Core. A managed host
// run establishes neither NativeAOT linkage nor CLROS filesystem/durability.
internal static class Program
{
    private const string Text = "CLROS SQLite — café";
    private const string EngineSourceId = "2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc";
    private static readonly byte[] Blob = Enumerable.Range(0, 34003).Select(i => (byte)(i ^ (i >> 9))).ToArray();

    private static int Main(string[] args)
    {
        if (args is ["--recovery-seed" or "--recovery-spill" or "--recovery-verify", _])
            return Recovery(args[0][11..], args[1]);
        if (args is ["--verify-database", var existing])
            return VerifyDatabase(existing);
        Console.WriteLine("TEST_START m10.020");
        try
        {
            if (args is not ["--database", var supplied])
                throw new InvalidOperationException("arguments");
            string database = Path.GetFullPath(supplied);
            Require(Path.IsPathFullyQualified(supplied) && Directory.Exists(Path.GetDirectoryName(database)) &&
                !Path.Exists(database) && !Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") &&
                !Path.Exists(database + "-shm"), "fresh_database_required");
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = database,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 1
            }.ToString();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string version = Scalar(connection, "SELECT sqlite_version()"), source = Scalar(connection, "SELECT sqlite_source_id()");
                Require(version == "3.53.4" && source == EngineSourceId, "engine_pin");
                Console.WriteLine("SQLITE_ENGINE version=" + version + " sourceBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(source)));
                Require(Scalar(connection, "PRAGMA journal_mode=DELETE") == "delete", "journal_policy");
                Execute(connection, "PRAGMA synchronous=FULL; PRAGMA temp_store=MEMORY; PRAGMA page_size=4096;");
                Require(Scalar(connection, "PRAGMA synchronous") == "2" && Scalar(connection, "PRAGMA locking_mode=NORMAL") == "normal" &&
                    Scalar(connection, "PRAGMA page_size") == "4096", "file_policy");
                Console.WriteLine("SQLITE_POLICY journal=delete synchronous=2 locking=normal pageSize=4096");
                Execute(connection, "CREATE TABLE sample (id INTEGER PRIMARY KEY, text_value TEXT NOT NULL, blob_value BLOB NOT NULL)");
                using (var committed = connection.BeginTransaction(deferred: false))
                {
                    Insert(connection, committed, 1);
                    VerifyRow(connection, committed);
                    Require(File.Exists(database + "-journal"), "commit_journal");
                    committed.Commit();
                }
                Console.WriteLine("SQLITE_COMMIT rows=1 textBytes=" + Encoding.UTF8.GetByteCount(Text) + " blobBytes=" + Blob.Length + " journal=true");
                using (var rolledBack = connection.BeginTransaction(deferred: false))
                {
                    Insert(connection, rolledBack, 2);
                    Require(File.Exists(database + "-journal"), "rollback_journal");
                    rolledBack.Rollback();
                }
                Require(Scalar(connection, "SELECT count(*) FROM sample") == "1", "rollback_count");
                Console.WriteLine("SQLITE_ROLLBACK rows=1 journal=true");
                using (var locked = connection.BeginTransaction(deferred: false))
                {
                    Insert(connection, locked, 3);
                    using var competitor = new SqliteConnection(connectionString);
                    competitor.Open();
                    int busy = Error(() =>
{
    using var rejected = competitor.BeginTransaction(deferred: false);
}, 5);
                    locked.Rollback();
                    Require(Scalar(connection, "SELECT count(*) FROM sample") == "1", "lock_count");
                    Console.WriteLine("SQLITE_LOCK busy=" + busy + " rows=1");
                }
            }
            using (var reopened = new SqliteConnection(connectionString))
            {
                reopened.Open();
                VerifyRow(reopened, null);
                Require(Scalar(reopened, "SELECT count(*) FROM sample") == "1" && Scalar(reopened, "PRAGMA integrity_check") == "ok", "reopen");
                Console.WriteLine("SQLITE_REOPEN rows=1 integrity=ok");
            }
            int readOnly;
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder(connectionString) { Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                connection.Open();
                readOnly = Error(() => Execute(connection, "DELETE FROM sample"), 8);
            }
            string absent = Path.Combine(Path.GetDirectoryName(database)!, "missing-parent");
            Require(!Path.Exists(absent), "missing_parent_control");
            int missing = Error(() =>
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder(connectionString) { DataSource = Path.Combine(absent, "missing.db") }.ToString());
                connection.Open();
            }, 14);
            Console.WriteLine("SQLITE_FAILURE readonly=" + readOnly + " missingParent=" + missing);
            Require(!File.Exists(database + "-journal") && !File.Exists(database + "-wal") && !File.Exists(database + "-shm"), "closed_files");
            Console.WriteLine("SQLITE_CLEANUP journal=false wal=false shm=false");
            Console.WriteLine("TEST_PASS m10.020");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m10.020 " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    // Distinct read-only process after the ordinary writer has exited. Exact
    // reads and integrity verification use the real pinned provider. No crypto,
    // mutation, synthetic journal or durability assertion is supplied here.
    private static int VerifyDatabase(string supplied)
    {
        Console.WriteLine("TEST_START m10.020.verify");
        try
        {
            string database = Path.GetFullPath(supplied);
            Require(Path.IsPathFullyQualified(supplied) && File.Exists(database) &&
                !Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "verify_database_required");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = database,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 1
            }.ToString()))
            {
                connection.Open();
                string version = Scalar(connection, "SELECT sqlite_version()"), source = Scalar(connection, "SELECT sqlite_source_id()");
                Require(version == "3.53.4" && source == EngineSourceId, "engine_pin");
                Console.WriteLine("SQLITE_VERIFY_ENGINE version=" + version + " sourceBase64=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(source)));
                Require(Scalar(connection, "PRAGMA journal_mode") == "delete" && Scalar(connection, "PRAGMA page_size") == "4096", "verify_policy");
                VerifyRow(connection, null);
                Require(Scalar(connection, "SELECT count(*) FROM sample") == "1" && Scalar(connection, "PRAGMA integrity_check") == "ok", "verify_integrity");
            }
            Console.WriteLine("SQLITE_VERIFY rows=1 textBytes=" + Encoding.UTF8.GetByteCount(Text) + " blobBytes=" + Blob.Length + " integrity=ok readonly=true");
            Require(!Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "verify_closed_files");
            Console.WriteLine("SQLITE_VERIFY_CLEANUP journal=false wal=false shm=false");
            Console.WriteLine("TEST_PASS m10.020.verify");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m10.020.verify " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    // Separate process-kill contract candidate. The ordinary mode above keeps
    // its graceful transaction contract. This changed app needs a new native
    // publication before its recovery route can supply Linux/CLROS evidence.
    private static int Recovery(string mode, string supplied)
    {
        string marker = "m10.020.recovery." + mode;
        Console.WriteLine("TEST_START " + marker);
        try
        {
            string database = Path.GetFullPath(supplied);
            Require(Path.IsPathFullyQualified(supplied) && Directory.Exists(Path.GetDirectoryName(database)) &&
                !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "recovery_path");
            Require(mode == "seed" ? !Path.Exists(database) && !Path.Exists(database + "-journal") : File.Exists(database), "recovery_database");
            if (mode == "verify")
                Require(File.Exists(database + "-journal") && new FileInfo(database + "-journal").Length > 512, "recovery_journal_required");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = database,
                Mode = mode == "seed" ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 1
            }.ToString()))
            {
                connection.Open();
                Require(Scalar(connection, "SELECT sqlite_version()") == "3.53.4" && Scalar(connection, "SELECT sqlite_source_id()") == EngineSourceId, "engine_pin");
                Console.WriteLine("SQLITE_RECOVERY_ENGINE version=3.53.4 sourceBase64=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Scalar(connection, "SELECT sqlite_source_id()"))));
                Require(Scalar(connection, "PRAGMA journal_mode=DELETE") == "delete", "recovery_journal_policy");
                Execute(connection, "PRAGMA synchronous=FULL; PRAGMA temp_store=MEMORY; PRAGMA page_size=4096; PRAGMA cache_size=8; PRAGMA cache_spill=ON;");
                Require(Scalar(connection, "PRAGMA synchronous") == "2" && Scalar(connection, "PRAGMA page_size") == "4096" &&
                    Scalar(connection, "PRAGMA cache_size") == "8" && int.Parse(Scalar(connection, "PRAGMA cache_spill"), System.Globalization.CultureInfo.InvariantCulture) > 0 &&
                    Scalar(connection, "SELECT count(*) FROM pragma_database_list WHERE name<>'main'") == "0", "recovery_policy");
                if (mode == "seed")
                    Execute(connection, "CREATE TABLE recovery (id INTEGER PRIMARY KEY, generation INTEGER NOT NULL, value BLOB NOT NULL)");
                if (mode is "seed" or "spill")
                {
                    using var transaction = connection.BeginTransaction(deferred: false);
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = mode == "seed" ? "INSERT INTO recovery VALUES ($id,0,$value)" : "UPDATE recovery SET generation=1,value=$value WHERE id=$id";
                    var id = command.Parameters.Add("$id", SqliteType.Integer);
                    var data = command.Parameters.Add("$value", SqliteType.Blob);
                    for (int row = 1; row <= 64; row++)
                    {
                        id.Value = row;
                        data.Value = RecoveryPayload(row, mode == "spill");
                        Require(command.ExecuteNonQuery() == 1, "recovery_write");
                    }
                    if (mode == "seed")
                        transaction.Commit();
                    else
                    {
                        byte[] header = new byte[28];
                        long journalLength;
                        using (var journal = new FileStream(database + "-journal", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            journal.ReadExactly(header);
                            journalLength = journal.Length;
                            Require(journalLength is > 512 and <= 8 * 1024 * 1024 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd9, 0xd5, 5, 0xf9, 0x20, 0xa1, 0x63, 0xd7 }), "recovery_spill_header");
                        }
                        // Close only the observer before readiness. The SQLite
                        // command/transaction/connection remain uncommitted/live.
                        Console.WriteLine("SQLITE_RECOVERY_SPILL rows=64 journalBytes=" + journalLength + " headerBase64=" + Convert.ToBase64String(header));
                        Console.Out.Flush();
                        Thread.Sleep(TimeSpan.FromSeconds(60));
                        throw new InvalidOperationException("recovery_owned_kill_required");
                    }
                }
                using var read = connection.CreateCommand();
                read.CommandText = "SELECT id,generation,value FROM recovery ORDER BY id";
                using (var rows = read.ExecuteReader())
                {
                    int count = 0;
                    while (rows.Read())
                    {
                        int id = ++count;
                        Require(rows.GetInt32(0) == id && rows.GetInt32(1) == 0 &&
                        ((byte[])rows.GetValue(2)).AsSpan().SequenceEqual(RecoveryPayload(id, false)), "recovery_original_rows");
                    }
                    Require(count == 64, "recovery_rows");
                }
                Require(Scalar(connection, "PRAGMA integrity_check") == "ok", "recovery_integrity");
                Console.WriteLine(mode == "seed" ? "SQLITE_RECOVERY_SEED rows=64 pageSize=4096" : "SQLITE_RECOVERY_VERIFY rows=64 generation=0 integrity=ok");
            }
            Require(!Path.Exists(database + "-journal") && !Path.Exists(database + "-wal") && !Path.Exists(database + "-shm"), "recovery_closed_files");
            Console.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL " + marker + " " + error.GetType().Name + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static byte[] RecoveryPayload(int id, bool changed) => Enumerable.Range(0, 4096).Select(i => (byte)((id * 13 + i * 17 + (i >> 8)) ^ (changed ? 0x5a : 0))).ToArray();

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO sample (id,text_value,blob_value) VALUES ($id,$text,$blob)";
        command.Parameters.Add("$id", SqliteType.Integer).Value = id;
        command.Parameters.Add("$text", SqliteType.Text).Value = Text;
        command.Parameters.Add("$blob", SqliteType.Blob).Value = Blob;
        Require(command.ExecuteNonQuery() == 1, "insert");
    }
    private static void VerifyRow(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT text_value,blob_value FROM sample WHERE id=$id";
        command.Parameters.Add("$id", SqliteType.Integer).Value = 1;
        using var reader = command.ExecuteReader();
        Require(reader.Read() && reader.GetString(0) == Text && ((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(Blob) && !reader.Read(), "parameterized_read");
    }
    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    private static int Error(Action action, int expected)
    {
        try
        {
            action();
        }
        catch (SqliteException error)
        {
            Require(error.SqliteErrorCode == expected, "error_code");
            return error.SqliteErrorCode;
        }
        throw new InvalidOperationException("expected_sqlite_failure");
    }
    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
