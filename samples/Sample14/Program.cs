﻿using System;
using System.Threading.Tasks;
using Elsa.Activities.Console.Extensions;
using Elsa.Extensions;
using Elsa.Models;
using Elsa.Persistence;
using Elsa.Persistence.EntityFrameworkCore;
using Elsa.Persistence.EntityFrameworkCore.Extensions;
using Elsa.Runtime;
using Elsa.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Sample14
{
    /// <summary>
    /// A simple demonstration of using Entity Framework Core persistence providers.
    /// To run the EF migration, first run the following command: `dotnet ef database update`.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var services = BuildServices();

            // Invoke startup tasks.
            var startupRunner = services.GetRequiredService<IStartupRunner>();
            await startupRunner.StartupAsync();

            // Create a workflow definition.
            var registry = services.GetService<IWorkflowRegistry>();
            var workflowDefinition = registry.RegisterWorkflow<HelloWorldWorkflow>();
            
            // Mark this definition as the "latest" version.
            workflowDefinition.IsLatest = true;
            workflowDefinition.Version = 1;

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ElsaContext>();
            
                // Ensure DB exists.
                await dbContext.Database.EnsureCreatedAsync();
                
                // Persist the workflow definition.
                var definitionStore = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>();
                await definitionStore.SaveAsync(workflowDefinition);

                // Flush to DB.
                await dbContext.SaveChangesAsync();

                // Load the workflow definition.
                workflowDefinition = await definitionStore.GetByIdAsync(workflowDefinition.Id, VersionOptions.Latest);

                // Execute the workflow.
                var invoker = scope.ServiceProvider.GetRequiredService<IWorkflowInvoker>();
                var executionContext = await invoker.InvokeAsync(workflowDefinition);

                // Persist the workflow instance.
                var instanceStore = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceStore>();
                var workflowInstance = executionContext.Workflow.ToInstance();
                await instanceStore.SaveAsync(workflowInstance);

                // Flush to DB.
                await dbContext.SaveChangesAsync();
            }
        }

        private static IServiceProvider BuildServices()
        {
            return new ServiceCollection()
                .AddWorkflowsCore()
                .AddStartupRunner()
                .AddConsoleActivities()
                .AddEntityFrameworkCore(
                    options => options
                        .UseSqlite(@"Data Source=c:\data\elsa.entity-framework-core.db;Cache=Shared")
                )
                .AddEntityFrameworkCoreWorkflowDefinitionStore()
                .AddEntityFrameworkCoreWorkflowInstanceStore()
                .BuildServiceProvider();
        }
    }
}