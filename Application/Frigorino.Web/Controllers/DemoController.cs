﻿using Microsoft.AspNetCore.Mvc;

namespace Frigorino.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController : ControllerBase
    {
        public string Get()
        {
            return "Hello, World!";
        }
    }
}
