using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Web;
using System.Net;


namespace Server.Controllers
{
    
    [ApiController]
    [Route("[controller]")]
    public class ServiceController : ControllerBase
    {                                   
        private readonly ILogger<ServiceController> _logger;
        private readonly DataService _dataService;            
        private readonly DataSources _dataSources;
        public IConfiguration Configuration { get; }
        //private static LogHelper log;
        private readonly IWebHostEnvironment env;
        
        public ServiceController(ILogger<ServiceController> logger, DataSources dataSources, DataService dataService, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _logger = logger;
            _dataService = dataService;             
            _dataSources = dataSources;
            Configuration = configuration;
            env = environment;
        }
        
        [HttpGet("sources")]
        public IActionResult Get() {            
            return new ObjectResult (_dataSources);
        }
            
        [HttpPost("reports")]
        //public async Task<ActionResult> Post([FromBody] ServiceRequest serviceRequest)
        public string Post([FromBody] ServiceRequest serviceRequest)
        {   
            //Для каждого комбобокса diktionary
            //для ключа!
            //в Dictionary в качестве ключа элемент из комбобокса 
            //в качестве элемента набор API KEY
            //на кнопку наж => Запрос на сервере на внешний ресурс и его ответ возвращается на клиента(файл/ текст)
            try{      
                //var key = _dataSources.Key.Single(ds => ds.Name == serviceRequest.Key).Table;
                var key = serviceRequest.Key;
                var filter = serviceRequest.Filter;
                var kind =serviceRequest.Kind;
                //LogHelper.Write("Метод пост выполнен");
                string link = $"https://services.exactearth.com/gws/wfs?service=wfs&version=1.1.0&request=GetFeature&typeName=exactAIS:{filter}&authKey={key}&outputFormat={kind}";
                if (kind == "csv")
                    return link;  
                string result = null;
                //WebProxy proxyObject = new WebProxy("192.168.4.10:8080",true);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(link);
                HttpWebResponse myHttpWebResponse = (HttpWebResponse) request.GetResponse();                
                Stream receiveStream = myHttpWebResponse.GetResponseStream();
                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
                StreamReader readStream = new StreamReader( receiveStream, encode );
                Char[] read = new Char[256];
                int count = readStream.Read( read, 0, 256 );
                while (count > 0)
                    {
                        // Dumps the 256 characters on a string and displays the string to the console.
                        String str = new String(read, 0, count);
                        result += str; 
                        count = readStream.Read(read, 0, 256);
                    }
                // Releases the resources of the response.
                myHttpWebResponse.Close();
                // Releases the resources of the Stream.
                readStream.Close();
                //Process.Start(link);
                return result;
            }            
            catch (Exception e){
                _logger.LogError($"При выполнении метода отправки запроса произошла ошибка: {e.ToString()}");
                LogHelper.Write(e);
                return null;
            } 
        }       
    }
}