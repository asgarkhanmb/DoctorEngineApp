using DoctorEngineApp.Models;
using DoctorEngineApp.ViewModels.Admin;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface ISiteSettingService
    {
        /// <summary>Cari ayarlar (keşlənir). Bazada qeyd yoxdursa standart dəyərlər.</summary>
        Task<SiteSetting> GetAsync();

        Task SaveAsync(SiteSettingVM vm);
    }
}
