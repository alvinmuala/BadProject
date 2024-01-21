using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using ThirdParty;

namespace Adv
{
    public class AdvertisementService
    {
        private readonly MemoryCache cache = new MemoryCache("");
        private readonly Queue<DateTime> errors = new Queue<DateTime>();
        private readonly object lockObj = new object();
        private readonly int maxRetryCount;
        private readonly int maxErrorCount = 20;

        public AdvertisementService()
        {
            maxRetryCount = int.Parse(ConfigurationManager.AppSettings["RetryCount"]);
        }

        public Advertisement GetAdvertisement(string id)
        {
            lock (lockObj)
            {
                var adv = GetAdvertisementFromCache(id);

                if (adv == null)
                {
                    var errorCount = GetHttpErrorsCount();

                    if (errorCount < 10)
                    {
                        adv = GetAdvertisementFromHttpProvider(id);
                    }

                    if (adv == null)
                    {
                        adv = GetAdvertisementFromBackupProvider(id);
                    }
                }

                return adv;
            }
        }

        private Advertisement GetAdvertisementFromCache(string id)
        {
            return (Advertisement)cache.Get($"AdvKey_{id}");
        }

        private Advertisement GetAdvertisementFromHttpProvider(string id)
        {
            Advertisement adv = null;
            var retry = 0;

            do
            {
                retry++;
                try
                {
                    var dataProvider = new NoSqlAdvProvider();
                    adv = dataProvider.GetAdv(id);
                }
                catch
                {
                    Thread.Sleep(1000);
                    errors.Enqueue(DateTime.Now);
                }
            } while (adv == null && retry < maxRetryCount);

            if (adv != null)
            {
                cache.Set($"AdvKey_{id}", adv, DateTimeOffset.Now.AddMinutes(5));
            }

            return adv;
        }

        private Advertisement GetAdvertisementFromBackupProvider(string id)
        {
            var adv = SQLAdvProvider.GetAdv(id);

            if (adv != null)
            {
                cache.Set($"AdvKey_{id}", adv, DateTimeOffset.Now.AddMinutes(5));
            }

            return adv;
        }

        private void PruneOldErrors()
        {
            while (errors.Count > maxErrorCount)
            {
                errors.Dequeue();
            }
        }

        private int GetHttpErrorsCount()
        {
            PruneOldErrors();

            return errors.Count(e => e > DateTime.Now.AddHours(-1));
        }
    }
}
