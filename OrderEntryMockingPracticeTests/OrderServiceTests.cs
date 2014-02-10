

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrderEntryMockingPractice.Models;
using OrderEntryMockingPractice.Services;
using Rhino.Mocks;

namespace OrderEntryMockingPracticeTests
{
    [TestFixture]
    public class OrderServiceTests
    {
        private IProductRepository _productRepository;
        private IOrderFulfillmentService _orderFulfillmentService;
        private ITaxRateService _taxRateService;
        private ICustomerRepository _customerRepository;
        private IEmailService _emailService;

        private OrderService _orderService;
        private Order _order;
        private OrderConfirmation _orderConfirmation;
        private Customer _customer;
        private List<TaxEntry> _taxEntryList;

        private const string ExpectedOrderConfirmationNumber = "18008008553";
        private const int ExpectedOrderId = 206;
        private const string ExpectedPostalCode = "90210";
        private const string ExpectedCountry = "USA";
        private const int ExpectedCustomerId = 8888;

        [SetUp]
        public void Setup()
        {
            _productRepository = MockRepository.GenerateStub<IProductRepository>();
            _orderFulfillmentService = MockRepository.GenerateStub<IOrderFulfillmentService>();
            _taxRateService = MockRepository.GenerateStub<ITaxRateService>();
            _customerRepository = MockRepository.GenerateStub<ICustomerRepository>();
            _emailService = MockRepository.GenerateStub<IEmailService>();
            _orderService = new OrderService(_productRepository, _orderFulfillmentService, _taxRateService, _customerRepository, _emailService);

            _order = CreateOrder();
            _customer = CreateCustomer();
            _orderConfirmation = CreateOrderConfirmation(_order);
            _taxEntryList = CreateTaxEntryList();

            _orderFulfillmentService.Expect(ofs => ofs.Fulfill(_order)).Return(_orderConfirmation);
            _customerRepository.Stub(cr => cr.Get(ExpectedCustomerId)).Return(_customer);
            _taxRateService.Stub(trs => trs.GetTaxEntries(_customer.PostalCode, _customer.Country)).Return(_taxEntryList);

        }


        [Test]
        public void PlaceOrder_AllItemsAreUnique_NoExceptionThrown()
        {
            //arrange

            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, valid: allItemsInStock);

            //act //Assert
            Assert.DoesNotThrow(() => _orderService.PlaceOrder(_order));
        }


        [Test]
        public void PlaceOrder_AllItemsAreNotUnique_ThrowsException()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //set each orderItem to contain the same product
            var theSameProduct = new Product() {Sku = "Item1", Price = (decimal) 1.11};
            foreach (var orderItem in _order.OrderItems)
            {
                orderItem.Product = theSameProduct;
            }

            //act //assert
            Assert.Throws<OrderValidationException>(() => _orderService.PlaceOrder(_order));
        }


        [Test]
        public void PlaceOrder_AllItemsInStock_NoExceptionThrown()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act //assert
            Assert.DoesNotThrow(() => _orderService.PlaceOrder(_order));
        }
        

        [Test]
        public void PLaceOrder_AllItemsNotInStock_ThrowsException()
        {
            //arrange
            const bool allItemsInStock = false;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act //assert
            Assert.Throws<OrderValidationException>(() => _orderService.PlaceOrder(_order));
        }
        

        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_SubmittedToOrderFulfillmentService()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            _orderFulfillmentService.AssertWasCalled(ofs => ofs.Fulfill(_order));
        }


        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_OrderFulfillmentConfirmationNumberContained()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            Assert.That(orderSummary.OrderNumber, Is.EqualTo(ExpectedOrderConfirmationNumber));
        }


        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_OrderFulfillmentOrderIDContained()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            Assert.That(orderSummary.OrderId, Is.EqualTo(ExpectedOrderId));
        }
        
        
        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_ApplicableCustomerTaxesContained()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            Assert.That(orderSummary.Taxes, Is.EqualTo(_taxEntryList));
        }
        

        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_NetTotalContained()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            var expectedNetTotal = _order.CalculatedExpectedNetTotal();

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            Assert.That(orderSummary.NetTotal, Is.EqualTo(expectedNetTotal));
        }
        

        [Test]
        public void PlaceOrder_ValidOrder_OrderSummaryIsReturned_OrderTotalContained()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            var expectedNetTotal = _order.CalculatedExpectedNetTotal();

            var expectedTotalTaxes = 0.0;
            foreach (var taxEntry in _taxEntryList)
            {
                var tax = (double) taxEntry.Rate;
                expectedTotalTaxes += tax * expectedNetTotal;
            }

            var expectedOrderTotal = expectedNetTotal - expectedTotalTaxes;

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            Assert.That(orderSummary.Total, Is.EqualTo(expectedOrderTotal));
        }


        [Test]
        public void PlaceOrder_ValidOrder_EmailIsSentToCustomer()
        {
            //arrange
            const bool allItemsInStock = true;
            StubProductRepositoryAllItemsInStock(_order, allItemsInStock);

            //act
            var orderSummary = _orderService.PlaceOrder(_order);

            //assert
            _emailService.AssertWasCalled(es => es.SendOrderConfirmationEmail(orderSummary.CustomerId,orderSummary.OrderId));
        }

//Tests above
//Methods below


        private Customer CreateCustomer()
        {
            return new Customer()
                {
                    AddressLine1 = "1918 8TH AVE N",
                    AddressLine2 = "SUITE 3100",
                    City = "SEATTLE",
                    Country = ExpectedCountry,
                    CustomerId = ExpectedCustomerId,
                    CustomerName = "STEVEN",
                    EmailAddress = "SU@PARAPORT.COM",
                    PostalCode = ExpectedPostalCode,
                    StateOrProvince = "WA"
                };
        }

        private List<TaxEntry> CreateTaxEntryList()
        {

            var taxEntry1 = new TaxEntry() { Description = "tax1", Rate = 0.1 };
            var taxEntry2 = new TaxEntry() { Description = "tax2", Rate = 0.2 };

            return new List<TaxEntry> { taxEntry1,taxEntry2 };

        }

        private static Order CreateOrder()
        {
            var product1 = new Product() {Sku = "Item1", Price = (decimal) 1.11};
            var product2 = new Product() {Sku = "Item2", Price = (decimal) 2.22};
            var product3 = new Product() {Sku = "Item3", Price = (decimal) 3.33};

            var orderItem1 = new OrderItem() {Product = product1, Quantity = 1.00};
            var orderItem2 = new OrderItem() {Product = product2, Quantity = 2.00};
            var orderItem3 = new OrderItem() {Product = product3, Quantity = 3.00};

            var allOrderItems = new List<OrderItem> {orderItem1, orderItem2, orderItem3};

            var order = new Order() {OrderItems = allOrderItems,CustomerId = ExpectedCustomerId};

            return order;
        }

        private void StubProductRepositoryAllItemsInStock(Order order, bool valid)
        {
            foreach (var orderItem in order.OrderItems)
            {
                this._productRepository.Stub(pr => pr.IsInStock(orderItem.Product.Sku)).Return(valid);
            }
        }

        private static OrderConfirmation CreateOrderConfirmation(Order order)
        {
            var orderConfirmation = new OrderConfirmation()
            {
                OrderNumber = ExpectedOrderConfirmationNumber,
                OrderId = ExpectedOrderId
            };
            return orderConfirmation;

        }
    }
}
