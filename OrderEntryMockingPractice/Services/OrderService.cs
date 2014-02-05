using System;
using System.Collections.Generic;
using OrderEntryMockingPractice.Models;

namespace OrderEntryMockingPractice.Services
{
    public class OrderService
    {

        private readonly IProductRepository _productRepository;
        private readonly IOrderFulfillmentService _orderFulfillmentService;
        private readonly ITaxRateService _taxRateService;
        private readonly ICustomerRepository _customerRepository;
        private readonly IEmailService _emailService;
        
        public OrderService(IProductRepository productRepository, IOrderFulfillmentService orderFulfillmentService,
            ITaxRateService taxRateService, ICustomerRepository customerRepository, IEmailService emailService)
        {
            _productRepository = productRepository;
            _orderFulfillmentService = orderFulfillmentService;
            _taxRateService = taxRateService;
            _emailService = emailService;
            _customerRepository = customerRepository;
        }

        public OrderSummary PlaceOrder(Order order)
        {
            if (!AllItemsInStock(order))
            {
                throw new Exception("Not all in Stock");
            }

            var orderConfirmation = _orderFulfillmentService.Fulfill(order);
            var customer = _customerRepository.Get((int) order.CustomerId);
            var taxRates = _taxRateService.GetTaxEntries(customer.PostalCode, customer.Country);
            var netTotal = SumNetTotal(order);
            var totalTaxes = SumTaxes(taxRates, netTotal);
            var orderTotal = netTotal - totalTaxes;
            var orderSummary = new OrderSummary()
                {
                    OrderNumber = orderConfirmation.OrderNumber,
                    OrderId = orderConfirmation.OrderId,
                    Taxes = taxRates,
                    NetTotal = netTotal,
                    Total = orderTotal,
                };

            _emailService.SendOrderConfirmationEmail(orderSummary.CustomerId, orderSummary.OrderId);

            return orderSummary;
        }

        private double SumTaxes(IEnumerable<TaxEntry> taxRates, double netTotal)
        {
            var totalTaxes = 0.0;
            foreach (var taxEntry in taxRates)
            {
                var tax = (double)taxEntry.Rate;
                totalTaxes += tax * netTotal;
            }

            return totalTaxes;
        }

        private double SumNetTotal(Order order)
        {
            var netTotal = 0.00;
            foreach (var orderItem in order.OrderItems)
            {
                var quantity = (double)orderItem.Quantity;
                var unitPrice = (double)orderItem.Product.Price;
                netTotal += quantity * unitPrice;
            }

            return netTotal;
        }


        private bool AllItemsInStock(Order order)
        {
            foreach (OrderItem item in order.OrderItems)
            {
                if (!_productRepository.IsInStock(item.Product.Sku))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
