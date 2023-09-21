using Bulky.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.DataAccess.Repository.IRepository
{
    public interface IOrderRepository : IRepository<Order>
    {
        void Update (Order order);
        void UpdateStatus(int id, string orderStatus, string? paymentStatus = null);
        void UpdateStripePayment(int id, string sessionID, string paymentIntentID);
    }
}
