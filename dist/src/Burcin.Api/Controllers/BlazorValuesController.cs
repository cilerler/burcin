using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Burcin.Domain;

namespace Burcin.Api.Controllers
{
    [Route("api/v1/[controller]")]
    public class BlazorValuesController : Controller
    {
        private IServiceProvider _serviceProvider;

        public BlazorValuesController(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private static string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet("[action]")]
        public IEnumerable<WeatherForecast> WeatherForecasts()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            });
        }
    }
}
