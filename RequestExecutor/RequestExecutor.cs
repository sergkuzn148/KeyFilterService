using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using System.IO.Compression;
using Request = System.Tuple<int,string>;

namespace RequestExecutor {
    
    class RequestExecutor {

        int id;
        string email;
        string file;
        string _connectionString;       
        
        string[] fields = new string[] {
            "id",
            "mmsi",
            "imo",
            "vessel_name", //vessel_name_id и тд
            "callsign", //
            "vessel_type", //
            "vessel_type_code",
            "vessel_type_cargo", //
            "vessel_class",
            "length",
            "width",
            "flag_country", //
            "flag_code",
            "destination", //
            "eta",
            "draught",
            "longitude",
            "latitude",
            "sog",
            "cog",
            "rot",
            "heading",
            "nav_status", //
            "nav_status_code", 
            "source", //
            "ts_pos_utc",
            "ts_static_utc",
            "ts_eta",
            "ts_insert_utc",
            "registry_name", //
            "registry_name_en", //
            "vessel_type_main", //
            "vessel_type_sub", //
            "message_type"
        };
        public RequestExecutor(string connectionString) //, EmailService emailService)
        {
            this._connectionString = connectionString;           
           // this._emailService = EmailService;
        }

        public void Run () {            
            var req = Select();
            if (req != null)
            {
                Execute(req);
                WriteData(); 
            }                            
        }

        void Execute(Request request)
        {
            string zip;
            string share;
            string filename;
            string path;

            try
            {
                using (var conn = new NpgsqlConnection(this._connectionString))
                {
                    conn.Open();
                    DateTime date = DateTime.Now;
                    filename = Path.GetRandomFileName();
                    share = @"\\10.1.2.33\share-f0beb8d4-a309-4550-9985-5569b8d9540e\kosmosnimki\downloads\ais\zip\";
                    path = Path.ChangeExtension( $@"{share}{filename}", ".csv");    //10.1.2.33/share-f0beb8d4-a309-4550-9985-5569b8d9540e/kosmosnimki/
                    file = Path.GetFileName(path);
                    zip = Path.ChangeExtension($"{share}{filename.ToString()}", ".zip");
                    File.Delete(@"\\10.1.2.33\share-f0beb8d4-a309-4550-9985-5569b8d9540e\kosmosnimki\downloads\ais\zip\txt.txt");
                    using (var cmd = new NpgsqlCommand(request.Item2,conn))
                    using (var rd = cmd.ExecuteReader())     
                    {                                                                     
                        File.AppendAllText(path, string.Concat(string.Join('\t', fields), System.Environment.NewLine)); //'\t'
                        var values = new List<string>();
                        while (rd.Read())
                        {
                            values = new List<string>();
                            foreach (string f in fields) {
                                values.Add(rd.GetValue(f).ToString());
                            }
                            File.AppendAllText(path, string.Concat(string.Join('\t', values), System.Environment.NewLine));
                        }                     
                        ZipFile.CreateFromDirectory(share,zip);
                        file = Path.GetFileName(zip);
                        File.Delete($@"\\10.1.2.33\share-f0beb8d4-a309-4550-9985-5569b8d9540e\kosmosnimki\downloads\ais\zip\{filename}.csv");
                    }
                }
                
           }

            catch (Exception e)
            {
                Console.WriteLine(e.ToString() );
                string error = e.ToString();
                string fileerror = $"ERRORER = {error}";
                file = fileerror;
            }
            
        }

        Request Select()
        {
            using (var conn = new NpgsqlConnection(this._connectionString)) {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT * FROM test_ais.ais_request WHERE ts_executed IS NULL LIMIT 1;", conn))
                using (var reader = cmd.ExecuteReader()) {
                    bool ok =  reader.Read();
                    if (ok) {
                        int i = reader.GetOrdinal("request_id");
                        int requestId = reader.GetInt32(i);
                        id = requestId;
                        i = reader.GetOrdinal("request");
                        string request = reader.GetString(i);
                        i = reader.GetOrdinal("email");
                        email = reader.GetString(i);
                        return new Request(requestId, request);
                    }
                    else
                    {
                        return null;
                    }
                }
            }                    
        } 
        void WriteData()
        {   
            DateTime date = DateTime.Now;
            using (var conn = new NpgsqlConnection(this._connectionString)){
                conn.Open();
                using (var cmd = new NpgsqlCommand("UPDATE test_ais.ais_request SET ts_executed = @curDate WHERE request_id = @request_id ;", conn)){
                    cmd.Parameters.AddWithValue("curDate", date);
                    cmd.Parameters.AddWithValue("request_id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            EmailService e = new EmailService();
            e.SendEmail(email, file);
             
        }
    }
}
