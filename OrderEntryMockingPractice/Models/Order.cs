using System.Collections.Generic;
using System.Linq;

namespace OrderEntryMockingPractice.Models
{
    public class Order
    {
        public Order()
        {
            this.OrderItems = new List<OrderItem>();
        }
        
        public int? CustomerId { get; set; }
        public List<OrderItem> OrderItems { get; set; }

        public virtual bool OrderHasAllUniqueProducts()
        {
            var dictionary = new Dictionary<Product, OrderItem>();

            foreach (var orderItem in OrderItems)
            {
                if (dictionary.ContainsKey(orderItem.Product))
                {
                    return false;
                }
                dictionary.Add(orderItem.Product, orderItem);
            }

            return true;
        }


        public virtual double CalculatedExpectedNetTotal()
        {
            return (
                from orderItem in OrderItems 
                let quantity = (double) orderItem.Quantity 
                let unitPrice = (double) orderItem.Product.Price 
                select quantity*unitPrice).Sum();
        }
    }
}
