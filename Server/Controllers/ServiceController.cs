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
        public async Task<ActionResult> Post([FromBody] ServiceRequest serviceRequest)
        {   
            
            try{        
                var sb = new StringBuilder();
                var t = _dataSources.Key.Single(ds => ds.Name == serviceRequest.Key).Table;
                //LogHelper.Write("Метод пост выполнен");
                return Ok(new  {
                    status = "ok"
                });
            }            
           catch (Exception e){
                _logger.LogError($"При выполнении метода отправки запроса произошла ошибка: {e.ToString()}");
                LogHelper.Write(e);
                return BadRequest();
            } 
        }       
    }
}