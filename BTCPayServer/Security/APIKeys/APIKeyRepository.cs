using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Security.APIKeys
{
    public class APIKeyRepository
    {
        private readonly ApplicationDbContextFactory _applicationDbContextFactory;

        public APIKeyRepository(ApplicationDbContextFactory applicationDbContextFactory)
        {
            _applicationDbContextFactory = applicationDbContextFactory;
        }

        public async Task<APIKeyData> GetKey(string apiKey)
        {
            using (var context = _applicationDbContextFactory.CreateContext())
            {
                return await EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(context.ApiKeys,
                    data => data.Id == apiKey && data.Type != APIKeyType.Legacy);
            }
        }

        public async Task<List<APIKeyData>> GetKeys(APIKeyQuery query)
        {
            using (var context = _applicationDbContextFactory.CreateContext())
            {
                var queryable = context.ApiKeys.AsQueryable();
                if (query?.UserId != null && query.UserId.Any())
                {
                    queryable = queryable.Where(data => query.UserId.Contains(data.UserId));
                }

                return await queryable.ToListAsync();
            }
        }

        public async Task CreateKey(APIKeyData key)
        {
            if (key.Type == APIKeyType.Legacy || !string.IsNullOrEmpty(key.StoreId) || string.IsNullOrEmpty(key.UserId))
            {
                throw new InvalidOperationException("cannot save a bitpay legacy api key with this repository");
            }

            using (var context = _applicationDbContextFactory.CreateContext())
            {
                await context.ApiKeys.AddAsync(key);
                await context.SaveChangesAsync();
            }
        }

        public async Task Remove(string id, string getUserId)
        {
            using (var context = _applicationDbContextFactory.CreateContext())
            {
                var key = await EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(context.ApiKeys,
                    data => data.Id == id && data.UserId == getUserId);
                context.ApiKeys.Remove(key);
                await context.SaveChangesAsync();
            }
        }

        public class APIKeyQuery
        {
            public string[] UserId { get; set; }
        }
    }
}
