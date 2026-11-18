using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task<bool> SaveSettingsAsync(AppSettings settings);
}
