using System;
using System.Threading.Tasks;
using Npgsql;

namespace Server
{
    public class DataService
    {
        private readonly string _connectionString;
        public DataService(string connectionString)
        {
            this._connectionString = connectionString;
        }
        public async Task Update(string request, string email) 
        {            
            await using var conn = new NpgsqlConnection(this._connectionString);
            await conn.OpenAsync();
            // Insert some data
            await using (var cmd = new NpgsqlCommand("INSERT INTO test_ais.ais_request (request, email) VALUES (@p,@e)", conn))
            {
                cmd.Parameters.AddWithValue("p", request);
                cmd.Parameters.AddWithValue("e", email);
                await cmd.ExecuteNonQueryAsync();
            }
        }  
    }

}