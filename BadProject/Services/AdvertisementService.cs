using BadProject.Contracts.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using ThirdParty;

namespace Adv
{
    public class AdvertisementService : IAdvertisementService
    {
        private readonly MemoryCache cache;
        private readonly object lockObj = new object();
        private readonly Queue<DateTime> errors;
        private readonly int maxRetryCount;
        private const int MaxErrorCount = 20;
        public AdvertisementService(MemoryCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.maxRetryCount = int.Parse(ConfigurationManager.AppSettings["MaxRetryCount"]);
            this.errors = new Queue<DateTime>();
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
            while (errors.Count > MaxErrorCount)
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
