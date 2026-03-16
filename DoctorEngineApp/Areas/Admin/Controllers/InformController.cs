using DoctorEngineApp.Data;
using DoctorEngineApp.Models;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Infroms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoctorEngineApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class InformController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public InformController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult Index()
        {
            var list = _context.Informs.ToList();
            return View(list);
        }

        // CREATE
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(InformCreateVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            string fileName = null;

            if (vm.Icon != null)
            {
                string uploadPath = Path.Combine(_env.WebRootPath, "uploads/inform");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                fileName = Guid.NewGuid() + Path.GetExtension(vm.Icon.FileName);
                string path = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                    vm.Icon.CopyTo(stream);
            }

            Inform inform = new Inform
            {
                Icon = fileName == null ? null : "/uploads/inform/" + fileName,
                Title = vm.Title,
                Description = vm.Description
            };

            _context.Informs.Add(inform);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // EDIT
        public IActionResult Edit(int id)
        {
            var item = _context.Informs.Find(id);
            if (item == null) return NotFound();

            InformEditVM vm = new InformEditVM
            {
                Id = item.Id,
                CurrentIcon = item.Icon,
                Title = item.Title,
                Description = item.Description
            };

            return View(vm);
        }

        [HttpPost]
        public IActionResult Edit(InformEditVM vm)
        {
            var item = _context.Informs.Find(vm.Id);
            if (item == null) return NotFound();

            if (vm.Icon != null)
            {
                string uploadPath = Path.Combine(_env.WebRootPath, "uploads/inform");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                string newFile = Guid.NewGuid() + Path.GetExtension(vm.Icon.FileName);
                string newPath = Path.Combine(uploadPath, newFile);

                using (var stream = new FileStream(newPath, FileMode.Create))
                    vm.Icon.CopyTo(stream);

                // delete old file
                if (!string.IsNullOrEmpty(item.Icon))
                {
                    string oldPath = _env.WebRootPath + item.Icon;
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                item.Icon = "/uploads/inform/" + newFile;
            }

            item.Title = vm.Title;
            item.Description = vm.Description;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // DELETE
        public IActionResult Delete(int id)
        {
            var item = _context.Informs.Find(id);
            if (item == null) return NotFound();

            // delete image
            if (!string.IsNullOrEmpty(item.Icon))
            {
                string path = _env.WebRootPath + item.Icon;
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }

            _context.Informs.Remove(item);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

    }
}
