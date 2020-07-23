using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;


namespace Server
{
   public class ServiceRequest
   {
      [JsonProperty("filter")]
      public string Filter { get; set; }
      [JsonProperty("key")]
      public string Key { get; set; }
   [JsonProperty("kind")]
      public string Kind { get; set; }
   }
}