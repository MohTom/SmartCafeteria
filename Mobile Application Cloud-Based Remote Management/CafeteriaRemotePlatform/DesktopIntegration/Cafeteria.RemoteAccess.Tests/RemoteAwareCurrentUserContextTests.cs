using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class RemoteAwareCurrentUserContextTests
{
    [Fact]
    public void BeginRemoteAdmin_GrantsAdminOnlyInsideScope()
    {
        var inner = new FakeCurrentUserContext();
        var context = new RemoteAwareCurrentUserContext(inner);

        Assert.False(context.IsActiveAdmin);

        using (context.BeginRemoteAdmin(Guid.NewGuid(), "Mobile Admin"))
        {
            Assert.True(context.IsActiveAdmin);
            Assert.Equal("Mobile Admin", context.CurrentUser!.FullName);
            Assert.Equal(SmartCafeteria.Core.Enums.UserRole.Admin, context.CurrentUser.Role);
        }

        Assert.False(context.IsActiveAdmin);
        Assert.Null(context.CurrentUser);
    }
}
