using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NCrontab;

namespace Burcin.Api.Services.GracePeriodManagerService
{
    public class Setting
    {
        public const string ConfigurationSectionName = nameof(GracePeriodManagerService);

	    [Crontab(ErrorMessage = "Crontab argument is not valid.")]
	    public string DelayTime { get; set; }

	    public bool IsEnabled => !string.IsNullOrWhiteSpace(DelayTime);

		public DateTime NextOccurence
		{
			get
			{
				if (!IsEnabled)
				{
					return DateTime.MinValue;
				}
				CrontabSchedule crontab = new CrontabAttribute().GetCrontab(DelayTime);
				return crontab?.GetNextOccurrence(DateTime.Now) ?? DateTime.MinValue;
			}
		}
    }
	
	public class CrontabAttribute : ValidationAttribute
	{
		public CrontabSchedule GetCrontab(string value)
		{
			bool includingSeconds = value?.Count(char.IsWhiteSpace) == 5;
			var parseOptions = new CrontabSchedule.ParseOptions
			                   {
				                   IncludingSeconds = includingSeconds,
			                   };
			CrontabSchedule crontab = CrontabSchedule.TryParse(value
			                                                 , parseOptions);
			return crontab;
		}

		public override bool IsValid(object value)
		{
			if (value == null)
			{
				return true;
			}
			var crontab = GetCrontab(value?.ToString());
			return crontab != null;
		}
	}
}
