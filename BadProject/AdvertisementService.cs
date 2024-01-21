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

        // **************************************************************************************************
        // Loads Advertisement information by id
        // from cache or if not possible uses the "mainProvider" or if not possible uses the "backupProvider"
        // **************************************************************************************************
        // Detailed Logic:
        // 
        // 1. Tries to use cache (and retuns the data or goes to STEP2)
        //
        // 2. If the cache is empty it uses the NoSqlDataProvider (mainProvider), 
        //    in case of an error it retries it as many times as needed based on AppSettings
        //    (returns the data if possible or goes to STEP3)
        //
        // 3. If it can't retrive the data or the ErrorCount in the last hour is more than 10, 
        //    it uses the SqlDataProvider (backupProvider)
        public Advertisement GetAdvertisement(string id)
        {
            lock (lockObj)
            {
                Advertisement adv = GetAdvertisementFromCache(id);

                if(adv == null)
                {
                    // add new logic here 
                }

                //todo: remove code below 
                // Count HTTP error timestamps in the last hour
                while (errors.Count > maxErrorCount) errors.Dequeue();
                int errorCount = 0;
                foreach (var dat in errors)
                {
                    if (dat > DateTime.Now.AddHours(-1))
                    {
                        errorCount++;
                    }
                }


                // If Cache is empty and ErrorCount<10 then use HTTP provider
                if ((adv == null) && (errorCount < 10))
                {
                    //add GetAdvertisementFromHttpProvider method here 
                }


                // if needed try to use Backup provider
                if (adv == null)
                {
                    adv = SQLAdvProvider.GetAdv(id);

                    if (adv != null)
                    {
                        cache.Set($"AdvKey_{id}", adv, DateTimeOffset.Now.AddMinutes(5));
                    }
                }
            }
            return adv;
        }

        private Advertisement GetAdvertisementFromCache(string id)
        {
            return (Advertisement)cache.Get($"AdvKey_{id}");
        }

        private Advertisement GetAdvertisementFromHttpProvider(string id)
        {
            Advertisement adv = null;
            int retry = 0;

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

        private void PruneOldErrors()
        {
            while (errors.Count > maxErrorCount)
            {
                errors.Dequeue();
            }
        }

        private int HttpErrors()
        {
            PruneOldErrors();

            return errors.Count(e => e > DateTime.Now.AddHours(-1));
        }
    }
}
