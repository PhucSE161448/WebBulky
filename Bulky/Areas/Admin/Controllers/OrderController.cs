using Bulky.DataAccess.Repository;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Stripe;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;

namespace Bulky.Areas.Admin.Controllers
{
    [Area("admin")]
    [Authorize]     
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public OrderVM OrderVM { get; set; }
        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderID)
        {
            OrderVM = new OrderVM()
            {
                Order = _unitOfWork.Order.GetFirstOrDefault(u => u.Id == orderID, includeProperties: "ApplicationUser"),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderId == orderID, includeProperties: "Product")
            };
            return View(OrderVM);
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult UpdateOrderDetails()
        {
            var order = _unitOfWork.Order.GetFirstOrDefault(u => u.Id == OrderVM.Order.Id);
            order.Name = OrderVM.Order.Name;
            order.PhoneNumber = OrderVM.Order.PhoneNumber;
            order.StreetAddress = OrderVM.Order.StreetAddress;
            order.City = OrderVM.Order.City;
            order.State = OrderVM.Order.State;
            order.PostalCode = OrderVM.Order.PostalCode;
            if (!string.IsNullOrEmpty(OrderVM.Order.Carrier))
            {
                order.Carrier = OrderVM.Order.Carrier;
            }
            if (!string.IsNullOrEmpty(OrderVM.Order.TrackingNumber))
            {
                order.TrackingNumber = OrderVM.Order.TrackingNumber;
            }
            _unitOfWork.Order.Update(order);
            _unitOfWork.Save();
            TempData["success"] = "Order Details Updated Successfully";
            return RedirectToAction(nameof(Details),new { orderId = order.Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult StartProcessing()
        {
            _unitOfWork.Order.UpdateStatus(OrderVM.Order.Id, SD.StatusInProcess);
            _unitOfWork.Save();
            TempData["success"] = "Order Details Updated Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult ShipOrder()
        {
            var order = _unitOfWork.Order.GetFirstOrDefault(x => x.Id == OrderVM.Order.Id);
            order.TrackingNumber = OrderVM.Order.TrackingNumber;
            order.Carrier = OrderVM.Order.Carrier;
            order.OrderStatus = SD.StatusShipped;
            order.ShippingDate = DateTime.Now;
            if(order.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                order.PaymentDueDate = DateTime.Now.AddDays(30);
            }
            _unitOfWork.Order.Update(order);
            _unitOfWork.Save();
            TempData["success"] = "Order Shipped Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Id });
        }
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult CancelOrder()
        {
            var order = _unitOfWork.Order.GetFirstOrDefault(x => x.Id == OrderVM.Order.Id);
            if(order.PaymentStatus == SD.PaymentStatusApproved)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = order.PaymentIntentId
                };
                var services = new RefundService();
                Refund refund = services.Create(options);
                _unitOfWork.Order.UpdateStatus(order.Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else
            {
                _unitOfWork.Order.UpdateStatus(order.Id, SD.StatusCancelled, SD.StatusCancelled);
            }
            _unitOfWork.Save();
            TempData["success"] = "Order Cancelled Successfully";
            return RedirectToAction(nameof(Details), new { orderId = OrderVM.Order.Id });
        }
        [ActionName("Details")]
        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult Details_PAY_NOW()
        {
            OrderVM.Order = _unitOfWork.Order.GetFirstOrDefault(x => x.Id == OrderVM.Order.Id, includeProperties: "ApplicationUser");
            OrderVM.OrderDetail = _unitOfWork.OrderDetail.GetAll(x => x.OrderId == OrderVM.Order.Id, includeProperties: "Product");
            var domain = "https://localhost:7066/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + $"admin/order/PaymentConfirmed?orderId={OrderVM.Order.Id}",
                CancelUrl = domain + $"admin/order/details?orderId={OrderVM.Order.Id}",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
            };
            foreach (var items in OrderVM.OrderDetail)
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
            _unitOfWork.Order.UpdateStripePayment(OrderVM.Order.Id, session.Id, session.PaymentIntentId);
            _unitOfWork.Save();
            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }
        public IActionResult PaymentConfirmed(int orderId)
        {
            Order order = _unitOfWork.Order.GetFirstOrDefault(x => x.Id == orderId);
            if (order.PaymentStatus == SD.PaymentStatusDelayedPayment)
            {
                var servcie = new SessionService();
                Session session = servcie.Get(order.SessionID);
                if (session.PaymentStatus.ToLower().Equals("paid"))
                {
                    _unitOfWork.Order.UpdateStripePayment(orderId, session.Id, session.PaymentIntentId);
                    _unitOfWork.Order.UpdateStatus(orderId, order.OrderStatus, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            }
            return View(orderId);
        }
        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<Order> listOrders;
            if(User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee)){
                listOrders = _unitOfWork.Order.GetAll(includeProperties: "ApplicationUser").ToList();
            }
            else
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
                listOrders = _unitOfWork.Order.GetAll(x => x.ApplicationUserId == claim, includeProperties:"ApplicationUser");
            }
            switch (status)
            {
                case "pending":
                    listOrders = listOrders.Where(x => x.PaymentStatus == SD.PaymentStatusPending);
                    break;
                case "inprocess":
                    listOrders = listOrders.Where(x => x.OrderStatus == SD.StatusInProcess);
                    break;
                case "completed":
                    listOrders = listOrders.Where(x => x.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    listOrders = listOrders.Where(x => x.OrderStatus == SD.StatusApproved);
                    break;
                default:
                    break;
            }
            return Json(new { data = listOrders });
        }
        #endregion
    }
}
