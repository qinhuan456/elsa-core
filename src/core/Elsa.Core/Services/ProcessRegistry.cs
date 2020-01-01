using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Caching;
using Elsa.Extensions;
using Elsa.Models;
using Elsa.Services.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Elsa.Services
{
    public class ProcessRegistry : IProcessRegistry
    {
        internal const string CacheKey = "elsa:workflow-registry";
        private readonly IServiceProvider serviceProvider;
        private readonly IMemoryCache cache;
        private readonly ISignal signal;

        public ProcessRegistry(
            IMemoryCache cache,
            ISignal signal,
            IServiceProvider serviceProvider)
        {
            this.cache = cache;
            this.signal = signal;
            this.serviceProvider = serviceProvider;
        }

        public async Task<Process> GetProcessAsync(string id, VersionOptions version, CancellationToken cancellationToken)
        {
            var processDefinitions = await ReadCacheAsync(cancellationToken);

            return processDefinitions
                .Where(x => x.DefinitionId == id)
                .OrderByDescending(x => x.Version)
                .WithVersion(version).FirstOrDefault();
        }

        private async Task<ICollection<Process>> ReadCacheAsync(CancellationToken cancellationToken)
        {
            return await cache.GetOrCreateAsync(
                CacheKey,
                async entry =>
                {
                    var workflowDefinitions = await GetProcessesAsync(cancellationToken);

                    entry.SlidingExpiration = TimeSpan.FromHours(1);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4);
                    entry.Monitor(signal.GetToken(CacheKey));
                    return workflowDefinitions;
                });
        }

        private async Task<ICollection<Process>> GetProcessesAsync(CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var providers = scope.ServiceProvider.GetServices<IProcessProvider>();
            var tasks = await Task.WhenAll(providers.Select(x => x.GetProcessesAsync(cancellationToken)));
            return tasks.SelectMany(x => x).ToList();
        }
    }
}