using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using DomainMatcherPolicy;

namespace Burcin.Api.Controllers
{
	[ApiController]
    [Route("api/v1/[controller]")]
    public class ValuesController : ControllerBase
    {
        private IServiceProvider _serviceProvider;

        public ValuesController(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

		[Domain("*:5000", "*:5001", "127.0.0.1")]
		[Route("[action]")]
        [HttpGet()]
        public string GetForLocalhost()
        {
            return "*:5000,*:5001,127.0.0.1:*";
        }

        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
