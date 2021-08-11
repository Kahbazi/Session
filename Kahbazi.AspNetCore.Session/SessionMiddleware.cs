﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Kahbazi.AspNetCore.Session
{
    /// <summary>
    /// Enables the session state for the application.
    /// </summary>
    public class SessionMiddleware
    {
        private const int SessionKeyLength = 36; // "382c74c3-721d-4f34-80e5-57657b6cbc27"
        private static readonly Func<bool> ReturnTrue = () => true;
        private readonly RequestDelegate _next;
        private readonly EndpointAwareSessionOptions _options;
        private readonly ILogger _logger;
        private readonly ISessionStore _sessionStore;
        private readonly IDataProtector _dataProtector;

        /// <summary>
        /// Creates a new <see cref="SessionMiddleware"/>.
        /// </summary>
        /// <param name="next">The <see cref="RequestDelegate"/> representing the next middleware in the pipeline.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> representing the factory that used to create logger instances.</param>
        /// <param name="dataProtectionProvider">The <see cref="IDataProtectionProvider"/> used to protect and verify the cookie.</param>
        /// <param name="sessionStore">The <see cref="ISessionStore"/> representing the session store.</param>
        /// <param name="options">The session configuration options.</param>
        public SessionMiddleware(
            RequestDelegate next,
            ILoggerFactory loggerFactory,
            IDataProtectionProvider dataProtectionProvider,
            ISessionStore sessionStore,
            IOptions<EndpointAwareSessionOptions> options)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (dataProtectionProvider == null)
            {
                throw new ArgumentNullException(nameof(dataProtectionProvider));
            }

            if (sessionStore == null)
            {
                throw new ArgumentNullException(nameof(sessionStore));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _next = next;
            _logger = loggerFactory.CreateLogger<SessionMiddleware>();
            _dataProtector = dataProtectionProvider.CreateProtector(nameof(SessionMiddleware));
            _options = options.Value;
            _sessionStore = sessionStore;
        }

        /// <summary>
        /// Invokes the logic of the middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <returns>A <see cref="Task"/> that completes when the middleware has completed processing.</returns>
        public async Task Invoke(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var sessionState = endpoint?.Metadata.GetMetadata<ISessionStateMetadata>()?.State ?? _options.DefaultBehavior;

            // Skip the middleware if session state is disabled
            if (sessionState == SessionState.Disabled)
            {
                await _next(context);
                return;
            }

            var isNewSessionKey = false;
            Func<bool> tryEstablishSession = ReturnTrue;
            var cookieValue = context.Request.Cookies[_options.Cookie.Name!];
            var sessionKey = CookieProtection.Unprotect(_dataProtector, cookieValue, _logger);
            if (string.IsNullOrWhiteSpace(sessionKey) || sessionKey.Length != SessionKeyLength)
            {
                // No valid cookie, new session.
                var guidBytes = new byte[16];
                RandomNumberGenerator.Fill(guidBytes);
                sessionKey = new Guid(guidBytes).ToString();
                cookieValue = CookieProtection.Protect(_dataProtector, sessionKey);
                var establisher = new SessionEstablisher(context, cookieValue, _options);
                tryEstablishSession = establisher.TryEstablishSession;
                isNewSessionKey = true;
            }

            var feature = new SessionFeature();
            feature.Session = _sessionStore.Create(sessionKey, _options.IdleTimeout, _options.IOTimeout, tryEstablishSession, isNewSessionKey);
            context.Features.Set<ISessionFeature>(feature);

            if (sessionState == SessionState.ReadOnly)
            {
                feature.Session = new ReadonlySession(feature.Session);
            }

            if (sessionState == SessionState.EagerLoad || sessionState == SessionState.ReadOnly)
            {
                await feature.Session.LoadAsync();
            }

            try
            {
                await _next(context);
            }
            finally
            {
                context.Features.Set<ISessionFeature?>(null);

                if (feature.Session != null && sessionState != SessionState.ReadOnly)
                {
                    try
                    {
                        await feature.Session.CommitAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.SessionCommitCanceled();
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorClosingTheSession(ex);
                    }
                }
            }
        }

        private class SessionEstablisher
        {
            private readonly HttpContext _context;
            private readonly string _cookieValue;
            private readonly EndpointAwareSessionOptions _options;
            private bool _shouldEstablishSession;

            public SessionEstablisher(HttpContext context, string cookieValue, EndpointAwareSessionOptions options)
            {
                _context = context;
                _cookieValue = cookieValue;
                _options = options;
                context.Response.OnStarting(OnStartingCallback, state: this);
            }

            private static Task OnStartingCallback(object state)
            {
                var establisher = (SessionEstablisher)state;
                if (establisher._shouldEstablishSession)
                {
                    establisher.SetCookie();
                }
                return Task.CompletedTask;
            }

            private void SetCookie()
            {
                var cookieOptions = _options.Cookie.Build(_context);

                var response = _context.Response;
                response.Cookies.Append(_options.Cookie.Name!, _cookieValue, cookieOptions);

                var responseHeaders = response.Headers;
                responseHeaders[HeaderNames.CacheControl] = "no-cache,no-store";
                responseHeaders[HeaderNames.Pragma] = "no-cache";
                responseHeaders[HeaderNames.Expires] = "-1";
            }

            // Returns true if the session has already been established, or if it still can be because the response has not been sent.
            internal bool TryEstablishSession()
            {
                return (_shouldEstablishSession |= !_context.Response.HasStarted);
            }
        }
    }
}