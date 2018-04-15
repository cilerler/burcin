﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Burcin.Domain
{
    public class Helper
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly HelperSetting _options;

        public Helper(IServiceProvider serviceProvider, ILogger<Helper> logger, IOptionsSnapshot<HelperSetting> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        public string Echo(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException();
            }

            _logger.LogInformation("Echo requested, input {value}", input);
            return $"{_options.Prefix} {input}";
        }

        public void DoWork()
        {
            //! Uncomment block below if you are using EntityFramework
            //var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            //using (var scope = scopeFactory.CreateScope())
            //    using (var context = scope.ServiceProvider.GetRequiredService<Burcin.Data.BurcinDbContext>())
            //    {

            //    _logger.LogInformation("Saving changes");
            //    context.SaveChanges();
            //    }
        }
    }
}