using Lampverket.Core;

namespace Lampverket.Web;

internal sealed class UserSession : IUserSession
{
    public string? Namn { get; set; }
}
