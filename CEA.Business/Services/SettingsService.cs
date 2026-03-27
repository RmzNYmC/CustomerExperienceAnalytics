using CEA.Core.Entities;
using CEA.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CEA.Business.Services
{
    public interface ISettingsService
    {
        Task<string?> GetSettingAsync(string key, string defaultValue = "");
        Task SetSettingAsync(string key, string value, string category = "General");
        Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category);
        Task<List<Setting>> GetAllSettingsAsync();
        Task SaveSettingsAsync(Dictionary<string, string> settings, string category);
    }

    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;

        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetSettingAsync(string key, string defaultValue = "")
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == key && !s.IsDeleted);

            return setting?.Value ?? defaultValue;
        }

        public async Task SetSettingAsync(string key, string value, string category = "General")
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new Setting
                {
                    Key = key,
                    Category = category,
                    CreatedAt = DateTime.Now
                };
                _context.Settings.Add(setting);
            }

            setting.Value = value;
            setting.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category)
        {
            return await _context.Settings
                .Where(s => s.Category == category && !s.IsDeleted)
                .ToDictionaryAsync(s => s.Key, s => s.Value);
        }

        public async Task<List<Setting>> GetAllSettingsAsync()
        {
            return await _context.Settings
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Key)
                .ToListAsync();
        }

        public async Task SaveSettingsAsync(Dictionary<string, string> settings, string category)
        {
            foreach (var item in settings)
            {
                await SetSettingAsync(item.Key, item.Value, category);
            }
        }
    }
}