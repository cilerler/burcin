using System;
using NCrontab;

namespace Burcin.Api.Services.GracePeriodManagerService
{
    public class Setting
    {
        public const string ConfigurationSectionName = nameof(GracePeriodManagerService);

        private string _delayTime;
        public string DelayTime
        {
            get
            {
                if (_delayTime == null)
                {
                    return null;
                }

                try
                {
                    CrontabSchedule crontab = CrontabSchedule.Parse(_delayTime, new CrontabSchedule.ParseOptions { IncludingSeconds = true });
                    DateTime nextOccurrence = crontab.GetNextOccurrence(DateTime.Now);
                    return nextOccurrence.ToString("s");
                }
                catch (CrontabException ce)
                {
                    //_logger.LogError(ce, ce.Message);
                    throw;
                }
                catch (Exception e)
                {
                    //_logger.LogError(e, e.Message);
                    throw;
                }
            }
            set => _delayTime = value;
        }
    }
}
