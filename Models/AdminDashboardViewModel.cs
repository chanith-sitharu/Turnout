using System.Collections.Generic;
using TurnoutStore.Data;

namespace TurnoutStore.Models
{
    public class AdminDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrdersCount { get; set; }
        public int TotalCustomersCount { get; set; }
        public List<Order> RecentOrders { get; set; } = new List<Order>();
    }
}
