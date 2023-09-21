using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace Bulky.Areas.Admin.Controllers;
[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class CoverTypeController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    public CoverTypeController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public IActionResult Index()
    {
        IEnumerable<CoverType> cover = _unitOfWork.CoverType.GetAll();
        return View(cover);
    }
    public IActionResult Create()
    {
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CoverType cover)
    {
        if(cover.Name is null)
        {
            ModelState.AddModelError("name", "Must be not null");
        }
        if (ModelState.IsValid)
        {
            _unitOfWork.CoverType.Add(cover);
            _unitOfWork.Save();
            TempData["success"] = "CoverType Create Successfully";
            return RedirectToAction("Index");
        }
        return View(cover);
    }
    public IActionResult Edit(int? id)
    {
        if(id == null || id == 0)
        {
            return NotFound();
        }
        var obj = _unitOfWork.CoverType.GetFirstOrDefault(c => c.Id == id);
        if(obj == null)
        {
            return NotFound();
        }
        return View(obj);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CoverType cover)
    {
        if (cover.Name is null)
        {
            ModelState.AddModelError("name", "Must be not null");
        }
        if (ModelState.IsValid)
        {
            _unitOfWork.CoverType.Update(cover);
            _unitOfWork.Save();
            TempData["success"] = "CoverType Update Successfully";
            return RedirectToAction("Index");
        }
        return View(cover);
    }
    public IActionResult Delete(int? id)
    {
        if(id == 0 || id is null)
        {
            return NotFound();
        }
        var obj = _unitOfWork.CoverType.GetFirstOrDefault(c => c.Id == id);
        if(obj == null)
        {
            return NotFound();
        }
        return View(obj);
    }
    [HttpPost,ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeletePost(int? id)
    {
        var obj = _unitOfWork.CoverType.GetFirstOrDefault(x => x.Id == id);
        if(obj is null)
        {
            return NotFound();
        }
            _unitOfWork.CoverType.Remove(obj);
            _unitOfWork.Save();
            TempData["success"] = "CoverType Delete Successfully";
            return RedirectToAction("Index");
    }
}
