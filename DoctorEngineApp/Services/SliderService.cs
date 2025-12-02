using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Sliders;

namespace DoctorEngineApp.Services
{
    public class SliderService :ISliderService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SliderService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Fayl yükləmə metodu
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            // Upload qovluğunu yoxla və yarat
            string folder = Path.Combine(_env.WebRootPath, "uploads", "sliders");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string filename = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string path = Path.Combine(folder, filename);

            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            return filename;
        }

        // Bütün slider-ları götür
        public async Task<List<Slider>> GetAllAsync()
        {
            return await Task.FromResult(_context.Sliders.ToList());
        }

        // Id üzrə slider
        public async Task<Slider?> GetByIdAsync(int id)
        {
            return await _context.Sliders.FindAsync(id);
        }

        // Slider yarat
        public async Task CreateAsync(SliderVM vm)
        {
            string img1 = null, img2 = null;

            if (vm.ImageFile1 != null)
                img1 = await SaveFileAsync(vm.ImageFile1);

            if (vm.ImageFile2 != null)
                img2 = await SaveFileAsync(vm.ImageFile2);

            Slider slider = new Slider
            {
                Title = vm.Title,
                Subtitle = vm.Subtitle,
                Image1 = img1,
                Image2 = img2
            };

            _context.Sliders.Add(slider);
            await _context.SaveChangesAsync();
        }

        // Slider update
        public async Task UpdateAsync(SliderVM vm)
        {
            var slider = await _context.Sliders.FindAsync(vm.Id);
            if (slider == null) return;

            slider.Title = vm.Title;
            slider.Subtitle = vm.Subtitle;

            if (vm.ImageFile1 != null)
            {
                slider.Image1 = await SaveFileAsync(vm.ImageFile1);
            }

            if (vm.ImageFile2 != null)
            {
                slider.Image2 = await SaveFileAsync(vm.ImageFile2);
            }

            await _context.SaveChangesAsync();
        }

        // Slider sil
        public async Task DeleteAsync(int id)
        {
            var slider = await _context.Sliders.FindAsync(id);
            if (slider == null) return;

            _context.Sliders.Remove(slider);
            await _context.SaveChangesAsync();
        }
    }
}
