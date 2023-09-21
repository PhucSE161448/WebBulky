using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace Bulky.Areas.Customer.Controllers
{
    [Area("customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork unitOfWork;
        [BindProperty]
        public ShoppingCartVM shoppingCartVM { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            shoppingCartVM = new ShoppingCartVM()
            {
                ShoppingCarts = unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId,
                includeProperties: "Product"),
                Order = new Order()
            };
            foreach (var cart in shoppingCartVM.ShoppingCarts)
            {
                cart.Price = GetPriceBaseOnQuantity(cart);
                shoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
            }
            return View(shoppingCartVM);
        }
        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            shoppingCartVM = new ShoppingCartVM()
            {
                ShoppingCarts = unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId,
                includeProperties: "Product"),
                Order = new Order()
            };
            shoppingCartVM.Order.ApplicationUser = unitOfWork.ApplicationUser.GetFirstOrDefault(x => x.Id == userId);

            shoppingCartVM.Order.Name = shoppingCartVM.Order.ApplicationUser.Name;
            shoppingCartVM.Order.PhoneNumber = shoppingCartVM.Order.ApplicationUser.PhoneNumber;
            shoppingCartVM.Order.StreetAddress = shoppingCartVM.Order.ApplicationUser.StreetAddress;
            shoppingCartVM.Order.City = shoppingCartVM.Order.ApplicationUser.City;
            shoppingCartVM.Order.State = shoppingCartVM.Order.ApplicationUser.State;
            shoppingCartVM.Order.PostalCode = shoppingCartVM.Order.ApplicationUser.PostalCode;


            foreach (var cart in shoppingCartVM.ShoppingCarts)
            {
                cart.Price = GetPriceBaseOnQuantity(cart);
                shoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
            }
            return View(shoppingCartVM);
        }
        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            shoppingCartVM.ShoppingCarts = unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == userId,
                includeProperties: "Product");

            shoppingCartVM.Order.OrderDate = System.DateTime.Now;
            shoppingCartVM.Order.ApplicationUserId = userId;
            ApplicationUser applicationUser = unitOfWork.ApplicationUser.GetFirstOrDefault(x => x.Id == userId);

            foreach (var cart in shoppingCartVM.ShoppingCarts)
            {
                cart.Price = GetPriceBaseOnQuantity(cart);
                shoppingCartVM.Order.OrderTotal += (cart.Price * cart.Count);
            }
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                //it is a regular customer account and we need to capture payment
                shoppingCartVM.Order.PaymentStatus = SD.PaymentStatusPending;
                shoppingCartVM.Order.OrderStatus = SD.StatusPending;
            }
            else
            {
                // it is a company user
                shoppingCartVM.Order.PaymentStatus = SD.PaymentStatusDelayedPayment;
                shoppingCartVM.Order.OrderStatus = SD.StatusApproved;
            }
            unitOfWork.Order.Add(shoppingCartVM.Order);
            unitOfWork.Save();
            foreach (var cart in shoppingCartVM.ShoppingCarts)
            {
                OrderDetail orderDetail = new OrderDetail()
                {
                    ProductId = cart.ProductId,
                    OrderId = shoppingCartVM.Order.Id,
                    Price = cart.Price,
                    Count = cart.Count
                };
                unitOfWork.OrderDetail.Add(orderDetail);
                unitOfWork.Save();
            }
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
                var domain = "https://localhost:7066/";
                var options = new SessionCreateOptions
                {
                    SuccessUrl = domain + $"customer/cart/OrderConfirmed?id={shoppingCartVM.Order.Id}",
                    CancelUrl = domain + "customer/cart/index",
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                };
                foreach(var items in shoppingCartVM.ShoppingCarts)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(items.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = items.Product.Title,
                            }
                        },
                        Quantity = items.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }
                var service = new SessionService();
                Session session = service.Create(options);
                unitOfWork.Order.UpdateStripePayment(shoppingCartVM.Order.Id, session.Id, session.PaymentIntentId);
                unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }
            return RedirectToAction(nameof(OrderConfirmed), new { id = shoppingCartVM.Order.Id });
        }
        public IActionResult OrderConfirmed(int id)
        {
            Order order = unitOfWork.Order.GetFirstOrDefault(x=> x.Id == id, includeProperties: "ApplicationUser");
            if(order.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                var servcie = new SessionService();
                Session session = servcie.Get(order.SessionID);
                if (session.PaymentStatus.ToLower().Equals("paid"))
                {
                    unitOfWork.Order.UpdateStripePayment(id, session.Id, session.PaymentIntentId);
                    unitOfWork.Order.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    unitOfWork.Save();
                }
            }
            List<ShoppingCart> shoppingCarts = unitOfWork.ShoppingCart
                .GetAll(x => x.ApplicationUserId == order.ApplicationUserId).ToList();
            unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            unitOfWork.Save();
            return View(id);
        }
        public IActionResult Plus(int cartId)
        {
            var cartFromDb = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            cartFromDb.Count += 1;
            unitOfWork.ShoppingCart.Update(cartFromDb);
            unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Minus(int cartId)
        {
            var cartFromDb = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            if (cartFromDb.Count <= 1)
            {
                unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Remove(int cartId)
        {
            var cartFromDb = unitOfWork.ShoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            unitOfWork.ShoppingCart.Remove(cartFromDb);
            unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        private double GetPriceBaseOnQuantity(ShoppingCart shoppingCart)
        {
            if (shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if (shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }
    }
}
