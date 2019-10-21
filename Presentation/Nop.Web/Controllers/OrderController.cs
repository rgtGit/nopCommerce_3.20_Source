using System;
using System.Configuration;
using System.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Web.Extensions;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Security;
using Nop.Web.Models.Order;


namespace Nop.Web.Controllers
{
    public partial class OrderController : BaseNopController
    {
		#region Fields

        private readonly IOrderService _orderService;
        private readonly IShipmentService _shipmentService;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly IPaymentService _paymentService;
        private readonly ILocalizationService _localizationService;
        private readonly IPdfService _pdfService;
        private readonly IShippingService _shippingService;
        private readonly ICountryService _countryService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IWebHelper _webHelper;

        private readonly OrderSettings _orderSettings;
        private readonly TaxSettings _taxSettings;
        private readonly CatalogSettings _catalogSettings;
        private readonly PdfSettings _pdfSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly AddressSettings _addressSettings;
        private readonly IDbContext _dbContext;

        #endregion

        #region Constructors

        public OrderController(IOrderService orderService, 
            IShipmentService shipmentService, IWorkContext workContext,
            ICurrencyService currencyService, IPriceFormatter priceFormatter,
            IOrderProcessingService orderProcessingService, IDateTimeHelper dateTimeHelper,
            IPaymentService paymentService, ILocalizationService localizationService,
            IPdfService pdfService, IShippingService shippingService,
            ICountryService countryService, IProductAttributeParser productAttributeParser,
            IWebHelper webHelper, 
            CatalogSettings catalogSettings, OrderSettings orderSettings,
            TaxSettings taxSettings, PdfSettings pdfSettings,
            ShippingSettings shippingSettings, AddressSettings addressSettings,
            IDbContext dbContext)
        {
            this._orderService = orderService;
            this._shipmentService = shipmentService;
            this._workContext = workContext;
            this._currencyService = currencyService;
            this._priceFormatter = priceFormatter;
            this._orderProcessingService = orderProcessingService;
            this._dateTimeHelper = dateTimeHelper;
            this._paymentService = paymentService;
            this._localizationService = localizationService;
            this._pdfService = pdfService;
            this._shippingService = shippingService;
            this._countryService = countryService;
            this._productAttributeParser = productAttributeParser;
            this._webHelper = webHelper;

            this._catalogSettings = catalogSettings;
            this._orderSettings = orderSettings;
            this._taxSettings = taxSettings;
            this._pdfSettings = pdfSettings;
            this._shippingSettings = shippingSettings;
            this._addressSettings = addressSettings;
            this._dbContext = dbContext;
        }

        #endregion

        #region Utilities

        [NonAction]
        protected OrderDetailsModel PrepareOrderDetailsModel(Order order)
        {
            // throw new Exception(order.OrderShippingExclTax.ToString());
            if (order == null)
                throw new ArgumentNullException("order");
            var model = new OrderDetailsModel();

            var currentCustomer = _workContext.CurrentCustomer;

            DateTime OrderDate = DateTime.Now;

            // Order Header
            model.Id = order.Id;
            model.PONumber = order.PurchaseOrderNumber;
            model.ShipVia = order.ShippingMethod;
            DateTime tmpShipDate = order.DesiredShipDate;
            string DesiredShipDate = tmpShipDate.Date.Month.ToString() + "/" + tmpShipDate.Date.Day.ToString() + "/" + tmpShipDate.Date.Year.ToString();
            model.DesiredShipDate = DesiredShipDate;

            // Invoice History
            SqlCommand cmd = new SqlCommand();
            SqlConnection SageDWConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["SageDataWarehouse"].ToString());

            SageDWConnection.Open();

            cmd = new SqlCommand("spParts_ARHistoryByOrderNo", SageDWConnection);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@ARDivisionNo", _workContext.CurrentCustomer.DivisionNo));
            cmd.Parameters.Add(new SqlParameter("@CustomerNo", _workContext.CurrentCustomer.AccountNo));
            cmd.Parameters.Add(new SqlParameter("@OrderNo", order.Id));

            var invoiceDetails = new OrderDetailsModel.InvoiceModel();

            var rdr = cmd.ExecuteReader();


            while (rdr.Read())
            {
                invoiceDetails.InvoiceNo = Convert.ToString(rdr["InvoiceNo"]);
                
                DateTime invDate = Convert.ToDateTime(rdr["InvoiceDate"]);
                invoiceDetails.InvoiceDate = invDate.ToShortDateString();

                DateTime shipDate = Convert.ToDateTime(rdr["ShipDate"]);
                invoiceDetails.ShipDate = shipDate.ToShortDateString();
                                
                invoiceDetails.ShipVia = Convert.ToString(rdr["ShipVia"]);
                invoiceDetails.TrackingNo = Convert.ToString(rdr["TrackingNo"]);

                model.Invoices.Add(invoiceDetails);
            }

            SageDWConnection.Close();


            // Payment Method
            string PaymentType = order.AuthorizationTransactionCode;

            if (PaymentType != "N/A")
            { // Credit Card transaction.
                model.PaymentMethod = "Credit Card ( Authorization Code " + PaymentType + " )";
            }
            else
            { // Terms Transaction
                model.PaymentMethod = _workContext.CurrentCustomer.Terms;
            }


            // Addresses
            model.BillingAddress.Company = currentCustomer.DealerName;
            model.BillingAddress.Address1 = currentCustomer.BillingAddress.Address1;
            model.BillingAddress.Address2 = currentCustomer.BillingAddress.Address2;
            model.BillingAddress.CityStateZip = _workContext.CurrentCustomer.BillingAddress.City + ", " +
                _workContext.CurrentCustomer.BillingAddress.StateProvince.Abbreviation + " " +
                _workContext.CurrentCustomer.BillingAddress.ZipPostalCode;

            model.ShippingAddress.FirstName = _workContext.CurrentCustomer.ShippingAddress.FirstName;
            model.ShippingAddress.LastName = _workContext.CurrentCustomer.ShippingAddress.LastName;
            model.ShippingAddress.Address1 = currentCustomer.ShippingAddress.Address1;
            model.ShippingAddress.Address2 = currentCustomer.ShippingAddress.Address2;
            model.ShippingAddress.CityStateZip = _workContext.CurrentCustomer.ShippingAddress.City + ", " +
                _workContext.CurrentCustomer.ShippingAddress.StateProvince.Abbreviation + " " +
                _workContext.CurrentCustomer.ShippingAddress.ZipPostalCode;


            var orderItems = _orderService.GetAllOrderItems(order.Id, null, null, null, null, null, null);
            decimal subTotal = 0.00M;
            decimal discountTotal = 0.00M;


            foreach (var orderItem in orderItems)
            {
                var orderItemModel = new OrderDetailsModel.OrderItemModel();

                orderItemModel.Sku = orderItem.Product.Sku;   //FormatSku(orderItem.AttributesXml, _productAttributeParser),
                orderItemModel.ProductId = orderItem.Product.Id;
                orderItemModel.ProductName = orderItem.Product.Name;
                orderItemModel.FullDescription = orderItem.Product.FullDescription;
                orderItemModel.Quantity = orderItem.Quantity;
                orderItemModel.UnitPrice = orderItem.UnitPriceExclTax.ToString("0.00");

                subTotal = orderItem.Quantity * orderItem.UnitPriceExclTax;
                // Discount, if any...
                if (order.OrderDiscount > 0)
                {
                    discountTotal = orderItem.DiscountAmountExclTax * orderItem.Quantity;
                    subTotal = subTotal - discountTotal;
                }

                orderItemModel.ItemDiscountTotal = discountTotal.ToString("C");
                orderItemModel.SubTotal = subTotal.ToString("C");

                model.Items.Add(orderItemModel);
            }


            //*** TOTALS ***
            // Order Subotal
            model.OrderSubtotal = order.OrderSubtotalExclTax.ToString("C");

            // Discount (applied to order subtotal)
            var orderSubTotalDiscountExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderSubTotalDiscountExclTax, order.CurrencyRate);
            if (orderSubTotalDiscountExclTaxInCustomerCurrency > decimal.Zero)
                model.OrderSubTotalDiscount = _priceFormatter.FormatPrice(-orderSubTotalDiscountExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, _workContext.WorkingLanguage, false);

            // Order shipping
            model.OrderShipping = order.OrderShippingExclTax.ToString("C");

            // Drop Ship Charge
            model.DropShipCharge = String.Format("{0:C}", order.DropShipCharge);

            //discount (applied to order total)
            var orderDiscountInCustomerCurrency = _currencyService.ConvertCurrency(order.OrderDiscount, order.CurrencyRate);
            if (orderDiscountInCustomerCurrency > decimal.Zero)
                model.OrderTotalDiscount = _priceFormatter.FormatPrice(-orderDiscountInCustomerCurrency, true, order.CustomerCurrencyCode, false, _workContext.WorkingLanguage);

            //total
            model.OrderTotal = order.OrderTotal.ToString("C");

            return model;
        }

        [NonAction]
        protected ShipmentDetailsModel PrepareShipmentDetailsModel(Shipment shipment)
        {
            if (shipment == null)
                throw new ArgumentNullException("shipment");

            var order = shipment.Order;
            if (order == null)
                throw new Exception("order cannot be loaded");
            var model = new ShipmentDetailsModel();
            
            model.Id = shipment.Id;
            if (shipment.ShippedDateUtc.HasValue)
                model.ShippedDate = _dateTimeHelper.ConvertToUserTime(shipment.ShippedDateUtc.Value, DateTimeKind.Utc);
            if (shipment.DeliveryDateUtc.HasValue)
                model.DeliveryDate = _dateTimeHelper.ConvertToUserTime(shipment.DeliveryDateUtc.Value, DateTimeKind.Utc);
            
            //tracking number and shipment information
            model.TrackingNumber = shipment.TrackingNumber;
            var srcm = _shippingService.LoadShippingRateComputationMethodBySystemName(order.ShippingRateComputationMethodSystemName);
            if (srcm != null &&
                srcm.PluginDescriptor.Installed &&
                srcm.IsShippingRateComputationMethodActive(_shippingSettings))
            {
                var shipmentTracker = srcm.ShipmentTracker;
                if (shipmentTracker != null)
                {
                    model.TrackingNumberUrl = shipmentTracker.GetUrl(shipment.TrackingNumber);
                    if (_shippingSettings.DisplayShipmentEventsToCustomers)
                    {
                        var shipmentEvents = shipmentTracker.GetShipmentEvents(shipment.TrackingNumber);
                        if (shipmentEvents != null)
                            foreach (var shipmentEvent in shipmentEvents)
                            {
                                var shipmentStatusEventModel = new ShipmentDetailsModel.ShipmentStatusEventModel();
                                var shipmentEventCountry = _countryService.GetCountryByTwoLetterIsoCode(shipmentEvent.CountryCode);
                                shipmentStatusEventModel.Country = shipmentEventCountry != null
                                                                       ? shipmentEventCountry.GetLocalized(x => x.Name)
                                                                       : shipmentEvent.CountryCode;
                                shipmentStatusEventModel.Date = shipmentEvent.Date;
                                shipmentStatusEventModel.EventName = shipmentEvent.EventName;
                                shipmentStatusEventModel.Location = shipmentEvent.Location;
                                model.ShipmentStatusEvents.Add(shipmentStatusEventModel);
                            }
                    }
                }
            }
            
            //products in this shipment
            model.ShowSku = _catalogSettings.ShowProductSku;
            foreach (var shipmentItem in shipment.ShipmentItems)
            {
                var orderItem = _orderService.GetOrderItemById(shipmentItem.OrderItemId);
                if (orderItem == null)
                    continue;
                
                var shipmentItemModel = new ShipmentDetailsModel.ShipmentItemModel()
                {
                    Id = shipmentItem.Id,
                    Sku = orderItem.Product.FormatSku(orderItem.AttributesXml, _productAttributeParser),
                    ProductId = orderItem.Product.Id,
                    ProductName = orderItem.Product.GetLocalized(x => x.Name),
                    ProductSeName = orderItem.Product.GetSeName(),
                    AttributeInfo = orderItem.AttributeDescription,
                    QuantityOrdered = orderItem.Quantity,
                    QuantityShipped = shipmentItem.Quantity,
                };
                model.Items.Add(shipmentItemModel);
            }

            //order details model
            model.Order = PrepareOrderDetailsModel(order);
            
            return model;
        }

        #endregion

        #region Order details

        [NopHttpsRequirement(SslRequirement.Yes)]
        public ActionResult OrderFilter()
        {
            return View();
        }

        [HttpPost]
        public JsonResult OrderFilter_Search(string OrderNo_Start, string OrderNo_End, DateTime? OrderDate_Start, DateTime? OrderDate_End, 
            string OrderType, string  PONumber, DateTime? ShipDate_Start,  DateTime? ShipDate_End, int PageOffSet) {

            try
            {
               
                SqlCommand cmd = new SqlCommand();
                SqlConnection SageDWConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["SageDataWarehouse"].ToString());
                
                SageDWConnection.Open();

                cmd = new SqlCommand("spParts_OrderFilter", SageDWConnection);
                cmd.CommandType = CommandType.StoredProcedure;
                              
                cmd.Parameters.Add(new SqlParameter("@ARDivisionNo", _workContext.CurrentCustomer.DivisionNo));
                cmd.Parameters.Add(new SqlParameter("@CustomerNo", _workContext.CurrentCustomer.AccountNo));

                AllowNull(cmd.Parameters.Add(new SqlParameter("@OrderNo_Start", OrderNo_Start)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@OrderNo_End", OrderNo_End)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@OrderDate_Start", OrderDate_Start)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@OrderDate_End", OrderDate_End)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@OrderType", OrderType)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@PONumber", PONumber)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@ShipDate_Start", ShipDate_Start)));
                AllowNull(cmd.Parameters.Add(new SqlParameter("@ShipDate_End", ShipDate_End)));
                cmd.Parameters.Add(new SqlParameter("@PageOffSet", PageOffSet));

                var orderDetails = new List<Order.OrderFilterDetails>();

                var rdr = cmd.ExecuteReader();

                              
                while (rdr.Read())
                {
                    string orderNo = Convert.ToString(rdr["SalesOrderNo"]);
                    string orderDate = Convert.ToString(rdr["OrderDate"]);
                    string orderStatus = Convert.ToString(rdr["OrderStatus"]);
                    string shipDate = Convert.ToString(rdr["ShipDate"]);
                    string searchCount = Convert.ToString(rdr["SearchCount"]);

                    orderDetails.Add(new Order.OrderFilterDetails()
                    {
                        OrderNo = orderNo,
                        OrderDate = orderDate,
                        OrderStatus = orderStatus,
                        ShipDate = shipDate,
                        SearchCount = searchCount
                    });

                }
                SageDWConnection.Close();
                return Json(orderDetails);
                
            }
            catch (Exception exc) {
                return Json(new { success = 1 });
                //_logger.Error(exc.Message, exc);
                //result.AddError(exc.Message);
            }
         
        }

        protected SqlParameter AllowNull(SqlParameter param)
        {
            param.IsNullable = true;
            param.Value = param.Value == null ? DBNull.Value : param.Value;
            return param;
        }


        [NopHttpsRequirement(SslRequirement.Yes)]
        public ActionResult Details(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            var model = PrepareOrderDetailsModel(order);

            return View(model);
        }

        [NopHttpsRequirement(SslRequirement.Yes)]
        public ActionResult PrintOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            var model = PrepareOrderDetailsModel(order);
            model.PrintMode = true;

            return View("Details", model);
        }

        public ActionResult GetPdfInvoice(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            var orders = new List<Order>();
            orders.Add(order);
            byte[] bytes = null;
            using (var stream = new MemoryStream())
            {
                _pdfService.PrintOrdersToPdf(stream, orders, _workContext.WorkingLanguage.Id);
                bytes = stream.ToArray();
            }
            return File(bytes, "application/pdf", string.Format("order_{0}.pdf", order.Id));
        }

        public ActionResult ReOrder(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            _orderProcessingService.ReOrder(order);
            return RedirectToRoute("ShoppingCart");
        }

        [HttpPost, ActionName("Details")]
        [FormValueRequired("repost-payment")]
        public ActionResult RePostPayment(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            if (!_paymentService.CanRePostProcessPayment(order))
                return RedirectToRoute("OrderDetails", new { orderId = orderId });

            var postProcessPaymentRequest = new PostProcessPaymentRequest()
            {
                Order = order
            };
            _paymentService.PostProcessPayment(postProcessPaymentRequest);

            if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
            {
                //redirection or POST has been done in PostProcessPayment
                return Content("Redirected");
            }
            else
            {
                //if no redirection has been done (to a third-party payment page)
                //theoretically it's not possible
                return RedirectToRoute("OrderDetails", new { orderId = orderId });
            }
        }

        [NopHttpsRequirement(SslRequirement.Yes)]
        public ActionResult ShipmentDetails(int shipmentId)
        {
            var shipment = _shipmentService.GetShipmentById(shipmentId);
            if (shipment == null)
                return new HttpUnauthorizedResult();

            var order = shipment.Order;
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return new HttpUnauthorizedResult();

            var model = PrepareShipmentDetailsModel(shipment);

            return View(model);
        }
        #endregion
    }
}
