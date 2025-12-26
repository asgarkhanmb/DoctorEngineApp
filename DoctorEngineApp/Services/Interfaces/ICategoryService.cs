using DoctorEngineApp.ViewModels.Categories;

namespace DoctorEngineApp.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<List<CategoryVM>> GetAllAsync();
        Task<CategoryEditVM?> GetForEditAsync(int id);
        Task CreateAsync(CategoryCreateVM vm);
        Task UpdateAsync(CategoryEditVM vm);
        Task DeleteAsync(int id);
    }
}
