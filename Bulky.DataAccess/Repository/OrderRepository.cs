using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.DataAccess.Repository
{
    public class OrderRepository : Repository<Order>, IOrderRepository
    {
        private ApplicationDbContext _db;
        public OrderRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(Order order)
        {
            _db.Orders.Update(order);
        }

        public void UpdateStatus(int id, string orderStatus, string? paymentStatus = null)
        {
            var order = _db.Orders.FirstOrDefault(x => x.Id == id);
            if (order != null)
            {
                order.OrderStatus = orderStatus;
                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    order.PaymentStatus = paymentStatus;
                }
            }
        }

        public void UpdateStripePayment(int id, string sessionID, string paymentIntentID)
        {
            var order = _db.Orders.FirstOrDefault(x => x.Id == id);
            if (!string.IsNullOrEmpty(sessionID))
            {
                order.SessionID = sessionID;
            }
            if (!string.IsNullOrEmpty(paymentIntentID))
            {
                order.PaymentIntentId = paymentIntentID;
                order.PaymentDate = DateTime.Now;
            }
        }
    }
}
