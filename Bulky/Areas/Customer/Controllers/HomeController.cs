using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace Bulky.Areas.Customer.Controllers;
[Area("Customer")]

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public IActionResult Index()
    {
        IEnumerable<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category,CoverType");
        return View(productList);
    }
    public IActionResult Details(int id)
    {
        ShoppingCart cartObj = new()
        {
            Count = 1,
            Product = _unitOfWork.Product.GetFirstOrDefault(u => u.Id == id, includeProperties: "Category,CoverType"),
            ProductId = id
        };
        return View(cartObj);
    }
    [HttpPost]
    [Authorize]
    public IActionResult Details(ShoppingCart cart)
    {
        cart.Id = 0;
        var claimsIdentity = (ClaimsIdentity)User.Identity;
        var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
        cart.ApplicationUserId = userId;
        ShoppingCart cartFromDB = _unitOfWork.ShoppingCart.GetFirstOrDefault(x => x.ApplicationUserId == userId &&
        x.ProductId == cart.ProductId);
        if (cartFromDB != null)
        {
            cartFromDB.Count += cart.Count;
            _unitOfWork.ShoppingCart.Update(cartFromDB);
        }
        else
        {
            _unitOfWork.ShoppingCart.Add(cart);
        }
        TempData["Success"] = "Add To Cart Successfully";
        _unitOfWork.Save();
        return RedirectToAction(nameof(Index));
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}