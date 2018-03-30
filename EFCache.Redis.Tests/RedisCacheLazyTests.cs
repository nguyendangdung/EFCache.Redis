﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;

namespace EFCache.Redis.Tests
{
    [TestClass]
    public class RedisCacheLazyTests
    {

        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() => 
            ConnectionMultiplexer.Connect("localhost:6379"));

        public RedisCacheLazyTests()
        {
            RedisStorageEmulatorManager.Instance.StartProcess(false);
        }


        [TestMethod]
        public void Item_cached_with_lazy()
        {
            var cache = new RedisCache(LazyConnection.Value);
            var item = new TestObject { Message = "OK" };

            cache.PutItem("key", item, new string[0], TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            object fromCache;

            Assert.IsTrue(cache.GetItem("key", out fromCache, null));
            Assert.AreEqual(item.Message, ((TestObject)fromCache).Message);

            Assert.IsTrue(cache.GetItem("key", out fromCache, null));
            Assert.AreEqual(item.Message, ((TestObject)fromCache).Message);
        }
    }
}
