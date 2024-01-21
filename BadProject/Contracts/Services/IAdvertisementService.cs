using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using ThirdParty;

namespace BadProject.Contracts.Services
{
    public interface IAdvertisementService
    {
        Advertisement GetAdvertisement(string id);
    }
}
