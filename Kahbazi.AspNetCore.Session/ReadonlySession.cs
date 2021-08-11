using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Kahbazi.AspNetCore.Session
{
    internal readonly struct ReadonlySession : ISession
    {
        private readonly ISession _session;

        public ReadonlySession(ISession session)
        {
            _session = session;
        }

        public bool IsAvailable => _session.IsAvailable;

        public string Id => _session.Id;

        public IEnumerable<string> Keys => _session.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return _session.LoadAsync(cancellationToken);
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            return _session.TryGetValue(key, out value);
        }

        public void Clear()
        {
            Throw();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Throw();
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            Throw();
        }

        public void Set(string key, byte[] value)
        {
            Throw();
        }

        private static void Throw()
        {
            throw new InvalidOperationException();
        }
    }
}
