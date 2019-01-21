﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Pchp.Core.Utilities;
using System.Diagnostics;
using System.IO;
using Pchp.Core;

namespace Peachpie.AspNetCore.Web
{
    /// <summary>
    /// ASP.NET Core application middleware handling requests to compiled PHP scripts.
    /// </summary>
    internal sealed class PhpHandlerMiddleware
    {
        readonly RequestDelegate _next;
        readonly string _rootPath;
        readonly PhpRequestOptions _options;

        public PhpHandlerMiddleware(RequestDelegate next, IHostingEnvironment hostingEnv, PhpRequestOptions options)
        {
            if (hostingEnv == null)
            {
                throw new ArgumentNullException(nameof(hostingEnv));
            }

            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options;

            // determine Root Path:
            _rootPath = hostingEnv.GetDefaultRootPath();

            if (!string.IsNullOrEmpty(options.RootPath))
            {
                _rootPath = Path.GetFullPath(Path.Combine(_rootPath, options.RootPath));  // use the root path option, relative to the ASP.NET Core Web Root
            }

            _rootPath = NormalizeRootPath(_rootPath);

            //

            // TODO: pass hostingEnv.ContentRootFileProvider to the Context for file system functions

            // sideload script assemblies
            LoadScriptAssemblies(options);
        }

        /// <summary>
        /// Loads and reflects assemblies containing compiled PHP scripts.
        /// </summary>
        static void LoadScriptAssemblies(PhpRequestOptions options)
        {
            if (options.ScriptAssembliesName != null)
            {
                foreach (var assname in options.ScriptAssembliesName.Select(str => new System.Reflection.AssemblyName(str)))
                {
                    var ass = System.Reflection.Assembly.Load(assname);
                    if (ass != null)
                    {
                        Context.AddScriptReference(ass);
                    }
                    else
                    {
                        LogEventSource.Log.ErrorLog($"Assembly '{assname}' couldn't be loaded.");
                    }
                }
            }
        }

        /// <summary>
        /// Normalize slashes and ensures the path ends with slash.
        /// </summary>
        static string NormalizeRootPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : CurrentPlatform.NormalizeSlashes(path).TrimEndSeparator();
        }

        /// <summary>
        /// Handles new context.
        /// </summary>
        void OnContextCreated(RequestContextCore ctx)
        {
            _options.BeforeRequest?.Invoke(ctx);
        }

        public Task Invoke(HttpContext context)
        {
            var script = RequestContextCore.ResolveScript(context.Request);
            if (script.IsValid)
            {
                return Task.Run(() =>
                {
                    using (var phpctx = new RequestContextCore(context, _rootPath, _options.StringEncoding))
                    {
                        OnContextCreated(phpctx);
                        phpctx.ProcessScript(script);
                    }
                });
            }

            return _next(context);
        }
    }
}
