using BadProject.Contracts.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Runtime.Caching;
using ThirdParty;

namespace BadProject.Tests
{
    [TestClass]
    public class AdvertisementServiceTests
    {
        public void GetAdvertisement_Must_Return_Advertisement_FromCache()
        {
            // Arrange
            var cache = new MemoryCache("test_cache");
            var advertisement = new Advertisement { WebId = "1", Name = "Test_Advertisement", Description = "This is just a test" };
            cache.Add("AdvKey_1", advertisement, DateTimeOffset.Now.AddMinutes(5));

            var advertisementServiceMock = new Mock<IAdvertisementService>();
            advertisementServiceMock.Setup(x => x.GetAdvertisement("1"))
                                    .Returns(advertisement);

            var service = advertisementServiceMock.Object;

            // Act
            var result = service.GetAdvertisement("1");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Test_Advertisement", result.Name);
        }

        [TestMethod]
        public void GetAdvertisement_Must_Return_Advertisement_FromHttpProvider()
        {
            // Arrange
            var cache = new MemoryCache("test_cache");

            var advertisementServiceMock = new Mock<IAdvertisementService>();
            advertisementServiceMock.Setup(x => x.GetAdvertisement("1"))
                                    .Returns(new Advertisement { WebId = "1", Name = "Test_Advertisement", Description = "This is just a test" });

            var service = advertisementServiceMock.Object;

            // Act
            var result = service.GetAdvertisement("1");

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetAdvertisement_Must_Return_Advertisement_FromBackupProvider()
        {
            // Arrange
            var cache = new MemoryCache("test_cache");

            var advertisementServiceMock = new Mock<IAdvertisementService>();
            advertisementServiceMock.SetupSequence(x => x.GetAdvertisement("1"))
                                    .Returns((Advertisement)null) // Simulate HTTP provider failure (1st retry)
                                    .Returns((Advertisement)null) // Simulate HTTP provider failure (2nd retry)
                                    .Returns(new Advertisement { WebId = "1", Name = "Test_Advertisement", Description = "This is just a test" }) // Return from backup provider (3rd retry)
                                    .Returns((Advertisement)null); // Simulate additional retries, returning null

            var service = advertisementServiceMock.Object;

            // Act
            var result = service.GetAdvertisement("1");

            // Assert
            Assert.IsNotNull(result);
        }
    }
}
