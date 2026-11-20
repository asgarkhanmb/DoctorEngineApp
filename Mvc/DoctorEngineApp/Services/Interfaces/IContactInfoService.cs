using DoctorEngineApp.ViewModels.ContactInfos;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface IContactInfoService
    {
        Task<ContactInfoVm> GetAsync(int id);
        Task<ContactInfoVm> GetFirstAsync();
        Task CreateAsync(ContactInfoVm model);
        Task UpdateAsync(ContactInfoVm model);
        Task<bool> ExistsAsync();
    }
}
