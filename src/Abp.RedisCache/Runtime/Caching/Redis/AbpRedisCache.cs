﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Domain.Entities;
using Abp.Reflection.Extensions;
using StackExchange.Redis;

namespace Abp.Runtime.Caching.Redis
{
    /// <summary>
    /// Used to store cache in a Redis server.
    /// </summary>
    public class AbpRedisCache : CacheBase
    {
        private readonly IDatabase _database;
        private readonly IRedisCacheSerializer _serializer;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AbpRedisCache(
            string name,
            IAbpRedisCacheDatabaseProvider redisCacheDatabaseProvider,
            IRedisCacheSerializer redisCacheSerializer)
            : base(name)
        {
            _database = redisCacheDatabaseProvider.GetDatabase();
            _serializer = redisCacheSerializer;
        }

        public override object GetOrDefault(string key)
        {
            var redisValue = _database.StringGet(GetLocalizedRedisKey(key));
            return redisValue.HasValue ? Deserialize(redisValue) : null;
        }

        public override object[] GetOrDefault(string[] keys)
        {
            var redisKeys = keys.Select(GetLocalizedRedisKey);
            var redisValues = _database.StringGet(redisKeys.ToArray());
            var values = redisValues.Select(obj => obj.HasValue ? Deserialize(obj) : null);
            return values.ToArray();
        }

        public override async Task<object> GetOrDefaultAsync(string key)
        {
            var redisValue = await _database.StringGetAsync(GetLocalizedRedisKey(key));
            return redisValue.HasValue ? Deserialize(redisValue) : null;
        }

        public override async Task<object[]> GetOrDefaultAsync(string[] keys)
        {
            var redisKeys = keys.Select(GetLocalizedRedisKey);
            var redisValues = await _database.StringGetAsync(redisKeys.ToArray());
            var values = redisValues.Select(obj => obj.HasValue ? Deserialize(obj) : null);
            return values.ToArray();
        }

        public override void Set(string key, object value, TimeSpan? slidingExpireTime = null, TimeSpan? absoluteExpireTime = null)
        {
            if (value == null)
            {
                throw new AbpException("Can not insert null values to the cache!");
            }

            _database.StringSet(
                GetLocalizedRedisKey(key),
                Serialize(value),
                absoluteExpireTime ?? slidingExpireTime ?? DefaultAbsoluteExpireTime ?? DefaultSlidingExpireTime
                );
        }

        public override async Task SetAsync(string key, object value, TimeSpan? slidingExpireTime = null, TimeSpan? absoluteExpireTime = null)
        {
            if (value == null)
            {
                throw new AbpException("Can not insert null values to the cache!");
            }

            await _database.StringSetAsync(
                GetLocalizedRedisKey(key),
                Serialize(value),
                absoluteExpireTime ?? slidingExpireTime ?? DefaultAbsoluteExpireTime ?? DefaultSlidingExpireTime
                );
        }

        public override void Set(KeyValuePair<string, object>[] pairs, TimeSpan? slidingExpireTime = null, TimeSpan? absoluteExpireTime = null)
        {
            if (pairs.Any(p => p.Value == null))
            {
                throw new AbpException("Can not insert null values to the cache!");
            }

            var redisPairs = pairs.Select(p => new KeyValuePair<RedisKey, RedisValue>
                                          (GetLocalizedRedisKey(p.Key), Serialize(p.Value))
                                         );

            if (slidingExpireTime.HasValue || absoluteExpireTime.HasValue)
            {
                Logger.WarnFormat("{0}/{1} is not supported for Redis bulk insert of key-value pairs", nameof(slidingExpireTime), nameof(absoluteExpireTime));
            }
            _database.StringSet(redisPairs.ToArray());
        }

        public override async Task SetAsync(KeyValuePair<string, object>[] pairs, TimeSpan? slidingExpireTime = null, TimeSpan? absoluteExpireTime = null)
        {
            if (pairs.Any(p => p.Value == null))
            {
                throw new AbpException("Can not insert null values to the cache!");
            }

            var redisPairs = pairs.Select(p => new KeyValuePair<RedisKey, RedisValue>
                                          (GetLocalizedRedisKey(p.Key), Serialize(p.Value))
                                         );
            if (slidingExpireTime.HasValue || absoluteExpireTime.HasValue)
            {
                Logger.WarnFormat("{0}/{1} is not supported for Redis bulk insert of key-value pairs", nameof(slidingExpireTime), nameof(absoluteExpireTime));
            }
            await _database.StringSetAsync(redisPairs.ToArray());
        }

        public override void Remove(string key)
        {
            _database.KeyDelete(GetLocalizedRedisKey(key));
        }

        public override async Task RemoveAsync(string key)
        {
            await _database.KeyDeleteAsync(GetLocalizedRedisKey(key));
        }

        public override void Remove(string[] keys)
        {
            var redisKeys = keys.Select(GetLocalizedRedisKey);
            _database.KeyDelete(redisKeys.ToArray());
        }

        public override async Task RemoveAsync(string[] keys)
        {
            var redisKeys = keys.Select(GetLocalizedRedisKey);
            await _database.KeyDeleteAsync(redisKeys.ToArray());
        }

        public override void Clear()
        {
            _database.KeyDeleteWithPrefix(GetLocalizedRedisKey("*"));
        }

        protected virtual string Serialize(object value)
        {
            return _serializer.Serialize(value);
        }

        protected virtual object Deserialize(RedisValue redisValue)
        {
            return _serializer.Deserialize<object>(redisValue);
        }

        protected virtual RedisKey GetLocalizedRedisKey(string key)
        {
            return "n:" + Name + ",c:" + key;
        }
    }
}
