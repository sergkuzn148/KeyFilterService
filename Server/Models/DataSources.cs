using System;
using System.Text.Json.Serialization;

namespace Server
{        
    public class DataSource
    {              
        [JsonIgnore]
        public string Table { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
    }

    public class DataSources
    {
        public DataSource[] Filter { get; set; }
        public DataSource[] Key { get; set; }
    }
}