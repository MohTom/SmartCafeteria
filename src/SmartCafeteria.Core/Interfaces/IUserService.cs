using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface IUserService
{
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User> CreateUserAsync(User user, string plainPassword);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> ChangePasswordAsync(string userId, string newPlainPassword);
    Task<bool> ChangePasswordAsync(string userId, string oldPlainPassword, string newPlainPassword);
    Task<bool> DeleteUserAsync(string userId);
    Task<bool> SetActiveAsync(string userId, bool isActive);
    Task<User?> AuthenticateAsync(string username, string plainPassword);
    Task<LoginResult> AuthenticateDetailedAsync(string username, string plainPassword);
    Task<bool> UserExistsAsync(string username, string? excludeId = null);
}
