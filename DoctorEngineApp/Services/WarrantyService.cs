using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Warranties;
using Microsoft.EntityFrameworkCore;

public class WarrantyService : IWarrantyService
{
    private readonly AppDbContext _db;
    public WarrantyService(AppDbContext db) => _db = db;

    public async Task CreateAsync(WarrantyCreateVM vm)
    {
        var warranty = new Warranty
        {
            CustomerName = vm.CustomerName,
            Phone = vm.Phone,
            Brand = vm.Brand,
            Model = vm.Model,
            Year = vm.Year,
            Fuel = vm.Fuel,
            Master = vm.Master,
            Terms = vm.Terms,
            WarrantyLogoId = vm.WarrantyLogoId,
            Jobs = vm.Jobs
                .Where(x => x.IsSelected)
                .Select(x => new WarrantyJob
                {
                    JobCatalogId = x.JobId,
                    WarrantyMonths = x.WarrantyMonths
                }).ToList()
        };

        _db.Warranties.Add(warranty);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Warranty>> GetAllAsync()
        => await _db.Warranties.Include(x => x.WarrantyLogo).ToListAsync();

    public async Task<Warranty> GetAsync(int id)
        => await _db.Warranties
            .Include(x => x.WarrantyLogo)
            .Include(x => x.Jobs).ThenInclude(x => x.JobCatalog)
            .FirstAsync(x => x.Id == id);
}
