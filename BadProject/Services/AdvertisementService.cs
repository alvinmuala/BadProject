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
            this.maxRetryCount = int.Parse(ConfigurationManager.AppSettings["RetryCount"]);
            this.errors = new Queue<DateTime>();
        }

        public Advertisement GetAdvertisement(string id)
        {
            lock (lockObj)
            {
                var advertisement = GetAdvertisementFromCache(id);

                if (advertisement == null)
                {
                    var errorCount = GetHttpErrorsCount();

                    if (errorCount < 10)
                    {
                        advertisement = GetAdvertisementFromHttpProvider(id);
                    }

                    if (advertisement == null)
                    {
                        advertisement = GetAdvertisementFromBackupProvider(id);
                    }
                }

                return advertisement;
            }
        }

        private Advertisement GetAdvertisementFromCache(string id)
        {
            return (Advertisement)cache.Get($"AdvKey_{id}");
        }

        private Advertisement GetAdvertisementFromHttpProvider(string id)
        {
            Advertisement advertisement = null;
            var retry = 0;

            do
            {
                retry++;
                try
                {
                    var dataProvider = new NoSqlAdvProvider();
                    advertisement = dataProvider.GetAdv(id);
                }
                catch
                {
                    Thread.Sleep(1000);
                    errors.Enqueue(DateTime.Now);
                }
            } while (advertisement == null && retry < maxRetryCount);

            if (advertisement != null)
            {
                cache.Set($"AdvKey_{id}", advertisement, DateTimeOffset.Now.AddMinutes(5));
            }

            return advertisement;
        }

        private Advertisement GetAdvertisementFromBackupProvider(string id)
        {
            var advertisement = SQLAdvProvider.GetAdv(id);

            if (advertisement != null)
            {
                cache.Set($"AdvKey_{id}", advertisement, DateTimeOffset.Now.AddMinutes(5));
            }

            return advertisement;
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
