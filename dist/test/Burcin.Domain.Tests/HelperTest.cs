using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Burcin.Domain;

namespace Burcin.Domain.Tests
{
	[TestClass]
	public class HelperTest
	{
		private static TestContext _testContext;
		private static IServiceProvider _serviceProvider;
        private static ILogger _logger;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext)
		{
			_testContext = testContext;
		    IServiceCollection serviceCollection = new ServiceCollection();
			using (IEnumerator<ServiceDescriptor> sc = Initialize.ServiceCollection.GetEnumerator())
		    {
				while (sc.MoveNext())
			    {
				    serviceCollection.Add(sc.Current);
			    }
			}

			serviceCollection.AddTransient<Helper>();

			_serviceProvider = serviceCollection.BuildServiceProvider();
		    _logger =_serviceProvider.GetRequiredService<ILogger<HelperTest>>();
		}

        [Priority(2)]
		[TestCategory("Beginner")]
		[TestMethod]
		public void Sample1Test()
        {
            _logger.LogInformation($"Testing {nameof(Sample1Test)}");
            const int expectedValue = 13;
            int actualValue = DateTime.Now.Month;
			Assert.AreNotEqual(expectedValue, actualValue);
		}

		[Priority(1)]
		[TestCategory("Intermediate")]
		[DataTestMethod]
		[DataRow("test1", 44)]
		[DataRow("test2", 55)]
		public void EchoTest(string name, int number)
		{
		    _logger.LogInformation($"Testing {nameof(EchoTest)}");
            try
			{
			    var helper = _serviceProvider.GetService<Helper>();
                string expectedName = name;
                string actualName = helper.Echo(name);
			    Assert.AreEqual(expectedName, actualName);
            }
			catch (ArgumentNullException ae) when (string.IsNullOrEmpty(name))
			{
				Assert.Fail(ae.Message);
			}
			catch (Exception ex)
			{
				Assert.Fail(ex.Message);
			}

		    Assert.IsTrue(number > default(int));
		}

		[Priority(1)]
		[TestCategory("Beginner")]
		[TestMethod]
        public void Sample2Test()
		{
		    _logger.LogInformation($"Testing {nameof(Sample2Test)}");
            const int maxDaysInAMonth = 31;
            int input = DateTime.UtcNow.Day;
		    Assert.IsTrue(maxDaysInAMonth >= input);
		}
	}
}
