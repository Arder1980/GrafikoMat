using GrafikoMat.Models;
using System.Threading.Tasks;

namespace GrafikoMat.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadSettingsAsync(bool forceReload = false);
        Task SaveSettingsAsync(AppSettings settings);
    }
}
