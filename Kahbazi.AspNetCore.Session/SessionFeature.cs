// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Kahbazi.AspNetCore.Session
{
    /// <inheritdoc />
    public class SessionFeature : ISessionFeature
    {
        /// <inheritdoc />
        public ISession Session { get; set; } = default!;
    }
}