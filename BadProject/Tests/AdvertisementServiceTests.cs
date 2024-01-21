using Adv;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Configuration;
using System.Runtime.Caching;
using ThirdParty;

namespace BadProject.Tests
{
    [TestClass]
    public class AdvertisementServiceTests
    {
        [TestMethod]
        public void GetAdvertisement_HttpProviderSucceeds_Must_ReturnAdvertisementFromHttpProvider()
        {
            // Arrange
            var cacheMock = new Mock<MemoryCache>();
            ConfigurationManager.AppSettings["MaxRetryCount"] = "3";
            var httpProviderMock = new Mock<NoSqlAdvProvider>();

            var service = CreateAdvertisementService(cacheMock.Object, httpProviderMock.Object);

            var advertisementId = "123";
            var expectedAdvertisement = new Advertisement { /* advertisement properties */ };

            // Act
            httpProviderMock.Setup(h => h.GetAdv(advertisementId)).Returns(expectedAdvertisement);
            var result = service.GetAdvertisement(advertisementId);

            // Assert
            Assert.AreEqual(expectedAdvertisement, result);
        }

        [TestMethod]
        public void GetAdvertisement_HttpProviderFails_Must_RetryAndReturnNull()
        {
            // Arrange
            var cacheMock = new Mock<MemoryCache>();
            ConfigurationManager.AppSettings["MaxRetryCount"] = "3";
            var httpProviderMock = new Mock<NoSqlAdvProvider>();

            var service = CreateAdvertisementService(cacheMock.Object, httpProviderMock.Object);

            var advertisementId = "123";

            // Act
            httpProviderMock.Setup(h => h.GetAdv(advertisementId)).Throws(new Exception("Simulating HTTP failure"));
            var result = service.GetAdvertisement(advertisementId);

            // Assert
            Assert.IsNull(result);
        }


        private AdvertisementService CreateAdvertisementService(
       MemoryCache cache, NoSqlAdvProvider httpProvider = null)
        {
            return new AdvertisementService(cache);
        }
    }
}
