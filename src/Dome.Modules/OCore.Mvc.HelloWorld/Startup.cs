﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System;
using OCore.Modules;

namespace OCore.Mvc.HelloWorld
{
    public class Startup : StartupBase
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override void Configure(IApplicationBuilder builder, IRouteBuilder routes, IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(_configuration["Sample"]))
            {
                throw new Exception(":(");
            }

            routes.MapAreaRoute
            (
                name: "HelloWorld",
                areaName: "OCore.Mvc.HelloWorld",
                template: "Hello/{controller=Home}/{action=Index}/{id?}"
            );
        }
    }
}
