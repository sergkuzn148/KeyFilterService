using System;

namespace RequestExecutor
{
    class Program
    {
        static void Main(string[] args)
        {
            var exc = new RequestExecutor("Host=pgsql.kosmosnimki.ru; Port=5432; Database=maps; User ID=postgres; Password=PgKosmo; CommandTimeout=6000; Timeout=1024; Pooling=true; MinPoolSize=1; MaxPoolSize=20;");
            exc.Run();
        }  
    }
}
