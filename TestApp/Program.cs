﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TestApp.Data;
using TestApp.Models;

namespace TestApp
{
    public class Program
    {
        public static ApplicationDbContext context = null;

        public static void Main(string[] args)
        {
            Console.WriteLine("Initializing DB...");
            var host = CreateWebHostBuilder(args).Build();
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    context = services.GetRequiredService<ApplicationDbContext>();
                    DbInitializer dbInit = new DbInitializer(
                        context,
                        services.GetRequiredService<UserManager<User>>(),
                        services.GetRequiredService<SignInManager<User>>(),
                        services.GetRequiredService<ILogger<DbInitializer>>());
                        //dbInit.InitializeNew();
                        //dbInit.GetStatsForTest(1,100);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while seeding the database.");
                }
            }

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            string contentRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
        }
    }
}