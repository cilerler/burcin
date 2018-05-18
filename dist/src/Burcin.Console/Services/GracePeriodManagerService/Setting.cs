using System;
using System.Diagnostics;
using NCrontab;

namespace Burcin.Console.Services.GracePeriodManagerService
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
                    Debug.WriteLine(ce.Message);
                    throw;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    throw;
                }
            }
            set => _delayTime = value;
        }
    }
}
