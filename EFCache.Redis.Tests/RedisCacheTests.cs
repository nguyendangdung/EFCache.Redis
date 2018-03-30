﻿using System;
using System.Threading;
using EFCache.Redis.Tests.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;

namespace EFCache.Redis.Tests
{
    [Serializable]
    public class TestObject
    {
        public string Message { get; set; }
    }

    [TestClass]
    [UsedImplicitly]
    public class RedisCacheTests
    {
        public RedisCacheTests()
        {
            RedisStorageEmulatorManager.Instance.StartProcess(false);
        }

        [TestMethod]
        public void Item_cached()
        {
            var cache = new RedisCache("localhost:6379");
            var item = new TestObject { Message = "OK" };

            cache.PutItem("key", item, new string[0], TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            object fromCache;

            Assert.IsTrue(cache.GetItem("key", out fromCache, null));
            Assert.AreEqual(item.Message, ((TestObject)fromCache).Message);

            Assert.IsTrue(cache.GetItem("key", out fromCache, null));
            Assert.AreEqual(item.Message, ((TestObject)fromCache).Message);
        }

        [TestMethod]
        public void Item_not_returned_after_absolute_expiration_expired()
        {
            var cache = new RedisCache("localhost:6379");
            var item = new TestObject { Message = "OK" };

            cache.PutItem("key", item, new string[0], TimeSpan.MaxValue, DateTimeOffset.Now.AddMinutes(-10), null);

            object fromCache;
            Assert.IsFalse(cache.GetItem("key", out fromCache, null));
            Assert.IsNull(fromCache);
        }

        [TestMethod]
        public void Item_not_returned_after_sliding_expiration_expired()
        {
            var cache = new RedisCache("localhost:6379");
            var item = new TestObject { Message = "OK" };

            cache.PutItem("key", item, new string[0], TimeSpan.Zero.Subtract(new TimeSpan(10000)), DateTimeOffset.MaxValue, null);

            object fromCache;
            Assert.IsFalse(cache.GetItem("key", out fromCache, null));
            Assert.IsNull(fromCache);
        }

        [TestMethod]
        public void Item_still_returned_after_sliding_expiration_period()
        {
            var cache = new RedisCache("localhost:6379");
            var item = new TestObject { Message = "OK" };

            // Cache the item with a sliding expiration of 10 seconds
            cache.PutItem("key", item, new string[0], TimeSpan.FromSeconds(10), DateTimeOffset.MaxValue, null);

            object fromCache = null;
            // In a loop of 20 seconds retrieve the item every 5 second seconds.
            for (var i = 0; i < 4; i++)
            {
                Thread.Sleep(5000); // Wait 5 seconds
                // Retrieve item again. This should update LastAccess and as such keep the item 'alive'
                // Break when item cannot be retrieved
                Assert.IsTrue(cache.GetItem("key", out fromCache, null));
            }
            Assert.IsNotNull(fromCache);
        }

        [TestMethod]
        public void InvalidateSets_invalidate_items_with_given_sets()
        {
            var cache = new RedisCache("localhost:6379");

            cache.PutItem("1", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);
            cache.PutItem("2", new object(), new[] { "ES2", "ES3" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);
            cache.PutItem("3", new object(), new[] { "ES1", "ES3", "ES4" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);
            cache.PutItem("4", new object(), new[] { "ES3", "ES4" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            cache.InvalidateSets(new[] { "ES1", "ES2" }, null);

            object item;
            Assert.IsFalse(cache.GetItem("1", out item, null));
            Assert.IsFalse(cache.GetItem("2", out item, null));
            Assert.IsFalse(cache.GetItem("3", out item, null));
            Assert.IsTrue(cache.GetItem("4", out item, null));
        }

        [TestMethod]
        public void InvalidateItem_invalidates_item()
        {
            var cache = new RedisCache("localhost:6379");

            cache.PutItem("1", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);
            cache.InvalidateItem("1", null);

            object item;
            Assert.IsFalse(cache.GetItem("1", out item, null));
        }

        [TestMethod]
        public void Count_returns_numers_of_cached_entries()
        {
            var cache = new RedisCache("localhost:6379,allowAdmin=true");

            cache.Purge();

            Assert.AreEqual(0, cache.Count);

            cache.PutItem("1", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            Assert.AreEqual(3, cache.Count); // "1", "ES1", "ES2"

            cache.InvalidateItem("1", null);

            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void Purge_removes_stale_items_from_cache()
        {
            var cache = new RedisCache("localhost:6379,allowAdmin=true");

            cache.Purge();

            cache.PutItem("1", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.Now.AddMinutes(-1), null);
            cache.PutItem("2", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            Assert.AreEqual(4, cache.Count); // "1", "2", "ES1", "ES2"

            cache.Purge();

            Assert.AreEqual(0, cache.Count);

            object item;
            Assert.IsFalse(cache.GetItem("1", out item, null));
            Assert.IsFalse(cache.GetItem("2", out item, null));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void GetItem_validates_parameters()
        {
            object item;

            var unused = new RedisCache("localhost:6379").GetItem(null, out item, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PutItem_validates_key_parameter()
        {
            new RedisCache("localhost:6379").PutItem(null, 42, new string[0], TimeSpan.Zero, DateTimeOffset.Now, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PutItem_validates_dependentEntitySets_parameter()
        {
            new RedisCache("localhost:6379").PutItem("1", 42, null, TimeSpan.Zero, DateTimeOffset.Now, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void InvalidateSets_validates_parameters()
        {
            new RedisCache("localhost:6379").InvalidateSets(null, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidateItem_validates_parameters()
        {
            new RedisCache("localhost:6379").InvalidateItem(null, null);
        }

        [TestMethod]
        public void GetItem_does_not_crash_if_cache_is_unavailable()
        {
            var cache = new RedisCache("unknown,abortConnect=false");
            RedisCacheException exception = null;
            cache.CachingFailed += (s, e) => exception = e;

            object item;
            var success = cache.GetItem("1", out item, null);

            Assert.IsFalse(success);
            Assert.IsNull(item);
            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception.InnerException, typeof(RedisConnectionException));
            Assert.AreEqual("Caching failed for GetItem", exception.Message);
        }

        [TestMethod]
        public void PutItem_does_not_crash_if_cache_is_unavailable()
        {
            var cache = new RedisCache("unknown,abortConnect=false");
            RedisCacheException exception = null;
            cache.CachingFailed += (s, e) => exception = e;

            cache.PutItem("1", new object(), new[] { "ES1", "ES2" }, TimeSpan.MaxValue, DateTimeOffset.MaxValue, null);

            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception.InnerException, typeof(RedisConnectionException));
        }

        [TestMethod]
        public void InvalidateItem_does_not_crash_if_cache_is_unavailable()
        {
            var cache = new RedisCache("unknown,abortConnect=false");
            RedisCacheException exception = null;
            cache.CachingFailed += (s, e) => exception = e;

            cache.InvalidateItem("1", null);

            Assert.IsNotNull(exception);
            Assert.IsInstanceOfType(exception.InnerException, typeof(RedisConnectionException));
        }
    }
}
