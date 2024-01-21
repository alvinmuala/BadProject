using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.Caching;
using System.Threading;
using ThirdParty;

namespace Adv
{
    public class AdvertisementService
    {
        private static MemoryCache cache = new MemoryCache("");
        private static Queue<DateTime> errors = new Queue<DateTime>();

        private Object lockObj = new Object();
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
            Advertisement adv = null;

            lock (lockObj)
            {
                // Use Cache if available
                adv = (Advertisement)cache.Get($"AdvKey_{id}");

                // Count HTTP error timestamps in the last hour
                while (errors.Count > 20) errors.Dequeue();
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
                            errors.Enqueue(DateTime.Now); // Store HTTP error timestamp              
                        }
                    } while ((adv == null) && (retry < int.Parse(ConfigurationManager.AppSettings["RetryCount"])));


                    if (adv != null)
                    {
                        cache.Set($"AdvKey_{id}", adv, DateTimeOffset.Now.AddMinutes(5));
                    }
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
    }
}
