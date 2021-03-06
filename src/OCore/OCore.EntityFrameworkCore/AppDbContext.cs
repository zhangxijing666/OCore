﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using OCore.Environment.Shell;
 
namespace OCore.EntityFrameworkCore
{


    public class AppDbContext : DbContext
    {
        public ShellSettings _shellSettings;
        private readonly IEntityTypeProvider _entityManager;
        private readonly AppDbMigrator _dbMigrator;
        private readonly AppDbContextOptions _appDbContextOptions;

        public AppDbContext(IServiceProvider serviceProvider)
        {
            _shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            _entityManager = serviceProvider.GetRequiredService<IEntityTypeProvider>();
            _dbMigrator = serviceProvider.GetRequiredService<AppDbMigrator>();
            _appDbContextOptions = serviceProvider.GetRequiredService<AppDbContextOptions>();
 
            if (_shellSettings.Name == "Default")
            {
                if(_shellSettings.TablePrefix == null && _shellSettings.ConnectionString == null && _shellSettings.DatabaseProvider == null)
                {
                    _shellSettings.TablePrefix = _appDbContextOptions?.TablePrefix;
                    _shellSettings.ConnectionString = _appDbContextOptions?.ConnectionString;
                    _shellSettings.DatabaseProvider = _appDbContextOptions?.DatabaseProvider;
                }
            }
        }
 
        protected override void OnModelCreating(ModelBuilder builder)
        {
            foreach (var type in _entityManager.GetEntityTypes())
            {
                if (builder.Model.FindEntityType(type) == null)//防止重复附加模型，否则会在生成指令中报错
                {
 
                    builder.Model.AddEntityType(type);
                }
            }

            foreach (var type in _entityManager.GetEntityTypeConfigurations())
            {
                dynamic mappingInstance = Activator.CreateInstance(type);
                builder.ApplyConfiguration(mappingInstance);
            }

            //foreach (var type in builder.Model.GetEntityTypes())
            //{
            //    type.Relational().TableName = $"Test_{type.Relational().TableName}";
            //}

            base.OnModelCreating(builder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseOrchardCore(_shellSettings, _dbMigrator?.MigrationsAssembly);
            //optionsBuilder.UseSqlServer(_shellSettings.ConnectionString,b => b.MigrationsAssembly(_shellSettings.RequestUrlHost));
        }
    }

    //https://github.com/OrchardCMS/OCore/issues/1343
    public static class OrchardEntityFrameworkExtensions
    {
        public static DbContextOptionsBuilder UseOrchardCore(this DbContextOptionsBuilder builder, ShellSettings settings, string migrationsAssemblyName)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            builder.ReplaceService<IModelCustomizer, OCoreModelCustomizer>();
            builder.ReplaceService<IModelCacheKeyFactory, OCoreModelCacheKeyFactory>();

            if (string.IsNullOrEmpty(settings.DatabaseProvider))
            {
                return builder;
            }

            switch (settings.DatabaseProvider)
            {
                case "SqlServer":
                    builder.UseSqlServer(settings.ConnectionString, options =>
                    {
                        if (!string.IsNullOrEmpty(settings.TablePrefix))
                            options.MigrationsHistoryTable($"{settings.TablePrefix}{HistoryRepository.DefaultTableName}");

                        if (!string.IsNullOrEmpty(migrationsAssemblyName))
                            options.MigrationsAssembly(migrationsAssemblyName);

                    });
                    break;

                // Add other providers (Sqlite, MySQL w/ Pomelo, etc.)

                default:
                    throw new InvalidOperationException($"The specified database provider is not supported: {settings.DatabaseProvider}.");
            }

            return builder;
        }

        private class OCoreModelCacheKeyFactory : IModelCacheKeyFactory
        {
            public object Create(DbContext context) => new MyModelCacheKey(context);//(context.GetType(), context.GetService<ShellSettings>().Name);

            //public object Create(DbContext context)
            //{
            //    return (context.GetType(), context.GetService<ShellSettings>().Name);
            //}

            //https://stackoverflow.com/questions/41979215/how-to-implement-imodelcachekeyfactory-in-ef-core
            class MyModelCacheKey : ModelCacheKey
            {
                string _schema;

                public MyModelCacheKey(DbContext context)
                    : base(context)
                {
                    _schema = (context as AppDbContext)?._shellSettings?.Name;
                }

                protected override bool Equals(ModelCacheKey other)
                    => base.Equals(other) && (other as MyModelCacheKey)?._schema == _schema;

                public override int GetHashCode()
                {
                    var hashCode = base.GetHashCode() * 397;
                    if (_schema != null)
                    {
                        hashCode ^= _schema.GetHashCode();
                    }

                    return hashCode;
                }
            }
        }

        private class OCoreModelCustomizer : RelationalModelCustomizer
        {
            public OCoreModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
            {
            }

            public override void Customize(ModelBuilder builder, DbContext context)
            {
                base.Customize(builder, context);

                var prefix = (context as AppDbContext)?._shellSettings.TablePrefix;
                //var prefix = context.GetService<ShellSettings>().TablePrefix;
                if (string.IsNullOrEmpty(prefix))
                {
                    return;
                }

                foreach (var type in builder.Model.GetEntityTypes())
                {
                    //type.Relational().TableName = $"{prefix}_{type.Relational().TableName}";
                    type.Relational().Schema = prefix;
                }

                //var sp = (IInfrastructure<IServiceProvider>)context;
                //var dbOptions = sp.Instance.GetServices<DbContextOptions>();
                //foreach (var item in dbOptions)
                //{
                //    if (item.ContextType == dbContext.GetType())
                //        ConfigureDbContextEntityService.Configure(modelBuilder, item, dbContext);
                //}
            }
        }
    }
}
