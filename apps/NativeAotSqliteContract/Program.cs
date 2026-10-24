using System.Text;
using Microsoft.Data.Sqlite;

// Direct ADO.NET contract measurement, independent of EF Core. A managed host
// run establishes neither NativeAOT linkage nor CLROS filesystem/durability.
internal static class Program
{
    private const string Text="CLROS SQLite — café";
    private static readonly byte[] Blob=Enumerable.Range(0,34003).Select(i=>(byte)(i^(i>>9))).ToArray();

    private static int Main(string[] args)
    {
        Console.WriteLine("TEST_START m10.020");
        try
        {
            if(args is not ["--database",var supplied])throw new InvalidOperationException("arguments");
            string database=Path.GetFullPath(supplied);
            Require(Path.IsPathFullyQualified(supplied) && Directory.Exists(Path.GetDirectoryName(database)) &&
                !Path.Exists(database) && !Path.Exists(database+"-journal") && !Path.Exists(database+"-wal") &&
                !Path.Exists(database+"-shm"),"fresh_database_required");
            string connectionString=new SqliteConnectionStringBuilder
                {DataSource=database,Mode=SqliteOpenMode.ReadWriteCreate,Cache=SqliteCacheMode.Private,Pooling=false,DefaultTimeout=1}.ToString();
            using(var connection=new SqliteConnection(connectionString))
            {
                connection.Open();
                string version=Scalar(connection,"SELECT sqlite_version()"),source=Scalar(connection,"SELECT sqlite_source_id()");
                Require(version=="3.53.4","engine_pin");
                Console.WriteLine("SQLITE_ENGINE version="+version+" sourceBase64="+Convert.ToBase64String(Encoding.UTF8.GetBytes(source)));
                Require(Scalar(connection,"PRAGMA journal_mode=DELETE")=="delete","journal_policy");
                Execute(connection,"PRAGMA synchronous=FULL; PRAGMA temp_store=MEMORY; PRAGMA page_size=4096;");
                Require(Scalar(connection,"PRAGMA synchronous")=="2" && Scalar(connection,"PRAGMA locking_mode=NORMAL")=="normal" &&
                    Scalar(connection,"PRAGMA page_size")=="4096","file_policy");
                Console.WriteLine("SQLITE_POLICY journal=delete synchronous=2 locking=normal pageSize=4096");
                Execute(connection,"CREATE TABLE sample (id INTEGER PRIMARY KEY, text_value TEXT NOT NULL, blob_value BLOB NOT NULL)");
                using(var committed=connection.BeginTransaction(deferred:false))
                {
                    Insert(connection,committed,1);VerifyRow(connection,committed);
                    Require(File.Exists(database+"-journal"),"commit_journal");committed.Commit();
                }
                Console.WriteLine("SQLITE_COMMIT rows=1 textBytes="+Encoding.UTF8.GetByteCount(Text)+" blobBytes="+Blob.Length+" journal=true");
                using(var rolledBack=connection.BeginTransaction(deferred:false))
                {Insert(connection,rolledBack,2);Require(File.Exists(database+"-journal"),"rollback_journal");rolledBack.Rollback();}
                Require(Scalar(connection,"SELECT count(*) FROM sample")=="1","rollback_count");
                Console.WriteLine("SQLITE_ROLLBACK rows=1 journal=true");
                using(var locked=connection.BeginTransaction(deferred:false))
                {
                    Insert(connection,locked,3);
                    using var competitor=new SqliteConnection(connectionString);competitor.Open();
                    int busy=Error(()=>{using var rejected=competitor.BeginTransaction(deferred:false);},5);
                    locked.Rollback();Require(Scalar(connection,"SELECT count(*) FROM sample")=="1","lock_count");
                    Console.WriteLine("SQLITE_LOCK busy="+busy+" rows=1");
                }
            }
            using(var reopened=new SqliteConnection(connectionString))
            {
                reopened.Open();VerifyRow(reopened,null);
                Require(Scalar(reopened,"SELECT count(*) FROM sample")=="1" && Scalar(reopened,"PRAGMA integrity_check")=="ok","reopen");
                Console.WriteLine("SQLITE_REOPEN rows=1 integrity=ok");
            }
            int readOnly;
            using(var connection=new SqliteConnection(new SqliteConnectionStringBuilder(connectionString){Mode=SqliteOpenMode.ReadOnly}.ToString()))
            {connection.Open();readOnly=Error(()=>Execute(connection,"DELETE FROM sample"),8);}
            string absent=Path.Combine(Path.GetDirectoryName(database)!,"missing-parent");Require(!Path.Exists(absent),"missing_parent_control");
            int missing=Error(()=>
            {using var connection=new SqliteConnection(new SqliteConnectionStringBuilder(connectionString){DataSource=Path.Combine(absent,"missing.db")}.ToString());connection.Open();},14);
            Console.WriteLine("SQLITE_FAILURE readonly="+readOnly+" missingParent="+missing);
            Require(!File.Exists(database+"-journal") && !File.Exists(database+"-wal") && !File.Exists(database+"-shm"),"closed_files");
            Console.WriteLine("SQLITE_CLEANUP journal=false wal=false shm=false");Console.WriteLine("TEST_PASS m10.020");return 0;
        }
        catch(Exception error)
        {Console.WriteLine("TEST_FAIL m10.020 "+error.GetType().Name+" "+error.Message.ReplaceLineEndings(" "));return 1;}
    }

    private static void Insert(SqliteConnection connection,SqliteTransaction transaction,int id)
    {
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO sample (id,text_value,blob_value) VALUES ($id,$text,$blob)";
        command.Parameters.Add("$id",SqliteType.Integer).Value=id;command.Parameters.Add("$text",SqliteType.Text).Value=Text;
        command.Parameters.Add("$blob",SqliteType.Blob).Value=Blob;Require(command.ExecuteNonQuery()==1,"insert");
    }
    private static void VerifyRow(SqliteConnection connection,SqliteTransaction? transaction)
    {
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT text_value,blob_value FROM sample WHERE id=$id";command.Parameters.Add("$id",SqliteType.Integer).Value=1;
        using var reader=command.ExecuteReader();Require(reader.Read() && reader.GetString(0)==Text && ((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(Blob) && !reader.Read(),"parameterized_read");
    }
    private static string Scalar(SqliteConnection connection,string sql)
    {using var command=connection.CreateCommand();command.CommandText=sql;return Convert.ToString(command.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture)!;}
    private static void Execute(SqliteConnection connection,string sql){using var command=connection.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();}
    private static int Error(Action action,int expected)
    {try{action();}catch(SqliteException error){Require(error.SqliteErrorCode==expected,"error_code");return error.SqliteErrorCode;}throw new InvalidOperationException("expected_sqlite_failure");}
    private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
}
