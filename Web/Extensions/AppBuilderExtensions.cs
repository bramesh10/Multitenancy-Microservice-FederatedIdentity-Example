﻿using System;
using System.Threading.Tasks;
using Owin;
using Server.Core.Container;
using Server.Service;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Owin.BuilderProperties;
using Web.Middleware;

namespace Web.Extensions
{
    public static class AppBuilderExtensions
    {
        public static IAppBuilder UseMultitenancy<TTenantRecord>(this IAppBuilder app, 
            MultitenancyNotifications<TTenantRecord> notifications)
            where TTenantRecord : class
        {
            return app.Use((context, next) =>
            {
                MultitenancyMiddleware<TTenantRecord> multitenancyMiddleware = 
                    ServiceLocator.Resolve<MultitenancyMiddleware<TTenantRecord>>(new { next, notifications });
                return multitenancyMiddleware.Invoke(context);
            });
        }

        public static IAppBuilder UsePerTenant(this IAppBuilder app, Action<TenantContext, IAppBuilder> newBranchAppConfig)
        {
            return app.Use<TenantPipelineMiddleware>(app, newBranchAppConfig);
        }

        public static IAppBuilder OnDispose(this IAppBuilder app, Action doOnDispose)
        {
            AppProperties properties = new AppProperties(app.Properties);
            CancellationToken token = properties.OnAppDisposing;
            if (token != CancellationToken.None)
            {
                token.Register(doOnDispose);
            }
            return app;
        }
    }
}