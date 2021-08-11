using System.Linq;
using System.Text;

namespace Kahbazi.AspNetCore.Session
{
    public interface ISessionStateMetadata
    {
        SessionState State { get; }
    }
}
