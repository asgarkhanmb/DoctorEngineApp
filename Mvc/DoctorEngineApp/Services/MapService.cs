using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Maps;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngineApp.Services
{
    public class MapService :IMapService
    {
        private readonly AppDbContext _context;

        public MapService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync()
        {
            return await _context.Maps.AnyAsync();
        }

        public async Task<MapVM?> GetSingleAsync()
        {
            var map = await _context.Maps.AsNoTracking().FirstOrDefaultAsync();
            if (map == null) return null;

            return new MapVM
            {
                Id = map.Id,
                Address = map.Address,
                MapUrl = map.MapUrl
            };
        }

        public async Task CreateAsync(MapVM model)
        {
            var map = new Map
            {
                Address = model.Address,
                MapUrl = model.MapUrl
            };

            await _context.Maps.AddAsync(map);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(MapVM model)
        {
            var map = await _context.Maps.FirstAsync();

            map.Address = model.Address;
            map.MapUrl = model.MapUrl;

            await _context.SaveChangesAsync();
        }

        public async Task<MapVM?> GetFirstAsync()
        {
            var map = await _context.Maps.FirstOrDefaultAsync();

            if (map == null)
                return null;

            return new MapVM
            {
                Id = map.Id,
                Address = map.Address,
                MapUrl = map.MapUrl
            };
        }

    }
}
