using RelayChat.ControlPlane.Database;

namespace RelayChat.ControlPlane.Api;

public static class AuthMapper
{
    public static UserProfileDto ToDto(this User user)
    {
        return new UserProfileDto(
            user.Id,
            user.Name,
            user.Handle,
            user.AvatarUrl,
            user.Email);
    }
}
