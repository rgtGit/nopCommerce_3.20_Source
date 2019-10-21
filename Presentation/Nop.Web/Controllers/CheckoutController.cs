using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Plugins;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using Nop.Web.Extensions;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Security;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Common;
using PayJS_Security;

using Nop.Web.Models.Customer;
using Nop.Web.Models.ShoppingCart;
using Newtonsoft.Json;
using System.IO;

namespace Nop.Web.Controllers
{
    [NopHttpsRequirement(SslRequirement.Yes)]
    public partial class CheckoutController : BaseNopController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ILocalizationService _localizationService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICountryService _countryService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IShippingService _shippingService;
        private readonly IPaymentService _paymentService;
        private readonly IPluginFinder _pluginFinder;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ILogger _logger;
        private readonly IOrderService _orderService;
        private readonly IWebHelper _webHelper;
        private readonly HttpContextBase _httpContext;
        private readonly IMobileDeviceHelper _mobileDeviceHelper;

        private readonly OrderSettings _orderSettings;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly AddressSettings _addressSettings;
        private readonly IDbContext _dbContext;

        #endregion

        #region Constructors

        public CheckoutController(IWorkContext workContext,
            IStoreContext storeContext,
            IShoppingCartService shoppingCartService,
            ILocalizationService localizationService,
            ITaxService taxService,
            ICurrencyService currencyService,
            IPriceFormatter priceFormatter,
            IOrderProcessingService orderProcessingService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            ICountryService countryService,
            IStateProvinceService stateProvinceService,
            IShippingService shippingService,
            IPaymentService paymentService,
            IPluginFinder pluginFinder,
            IOrderTotalCalculationService orderTotalCalculationService,
            ILogger logger,
            IOrderService orderService,
            IWebHelper webHelper,
            HttpContextBase httpContext,
            IMobileDeviceHelper mobileDeviceHelper,
            OrderSettings orderSettings,
            RewardPointsSettings rewardPointsSettings,
            PaymentSettings paymentSettings,
            ShippingSettings shippingSettings,
            AddressSettings addressSettings,
            IDbContext dbContext)
        {
            this._workContext = workContext;
            this._storeContext = storeContext;
            this._shoppingCartService = shoppingCartService;
            this._localizationService = localizationService;
            this._taxService = taxService;
            this._currencyService = currencyService;
            this._priceFormatter = priceFormatter;
            this._orderProcessingService = orderProcessingService;
            this._customerService = customerService;
            this._genericAttributeService = genericAttributeService;
            this._countryService = countryService;
            this._stateProvinceService = stateProvinceService;
            this._shippingService = shippingService;
            this._paymentService = paymentService;
            this._pluginFinder = pluginFinder;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._logger = logger;
            this._orderService = orderService;
            this._webHelper = webHelper;
            this._httpContext = httpContext;
            this._mobileDeviceHelper = mobileDeviceHelper;

            this._orderSettings = orderSettings;
            this._rewardPointsSettings = rewardPointsSettings;
            this._paymentSettings = paymentSettings;
            this._shippingSettings = shippingSettings;
            this._addressSettings = addressSettings;
            this._dbContext = dbContext;
        }

        #endregion

        #region Utilities

        [NonAction]
        protected bool IsPaymentWorkflowRequired(IList<ShoppingCartItem> cart, bool ignoreRewardPoints = false)
        {
            bool result = true;

            //check whether order total equals zero
            decimal? shoppingCartTotalBase = _orderTotalCalculationService.GetShoppingCartTotal(cart, 0.00M, ignoreRewardPoints);
            if (shoppingCartTotalBase.HasValue && shoppingCartTotalBase.Value == decimal.Zero)
                result = false;
            return result;
        }

        [NonAction]
        protected CheckoutBillingAddressModel PrepareBillingAddressModel(int? selectedCountryId = null,
            bool prePopulateNewAddressWithCustomerFields = false)
        {
            var model = new CheckoutBillingAddressModel();
            //existing addresses
            var addresses = _workContext.CurrentCustomer.Addresses.Where(a => a.Country == null || a.Country.AllowsBilling).ToList();
            foreach (var address in addresses)
            {
                var addressModel = new AddressModel();
                addressModel.PrepareModel(address,
                    false,
                    _addressSettings);
                model.ExistingAddresses.Add(addressModel);
            }

            //new address
            model.NewAddress.CountryId = selectedCountryId;
            model.NewAddress.PrepareModel(null,
                false,
                _addressSettings,
                _localizationService,
                _stateProvinceService,
                () => _countryService.GetAllCountriesForBilling(),
                prePopulateNewAddressWithCustomerFields,
                _workContext.CurrentCustomer);
            return model;
        }

        [NonAction]
        protected CheckoutShippingAddressModel PrepareShippingAddressModel(int? selectedCountryId = null,
            bool prePopulateNewAddressWithCustomerFields = false)
        {
            var model = new CheckoutShippingAddressModel();
            //existing addresses
            var addresses = _workContext.CurrentCustomer.Addresses.Where(a => a.Country == null || a.Country.AllowsShipping).ToList();
            foreach (var address in addresses)
            {
                var addressModel = new AddressModel();
                addressModel.PrepareModel(address,
                    false,
                    _addressSettings);
                model.ExistingAddresses.Add(addressModel);
            }

            //new address
            model.NewAddress.CountryId = selectedCountryId;
            model.NewAddress.PrepareModel(null,
                false,
                _addressSettings,
                _localizationService,
                _stateProvinceService,
                () => _countryService.GetAllCountriesForShipping(),
                prePopulateNewAddressWithCustomerFields,
                _workContext.CurrentCustomer);
            return model;
        }

        [NonAction]
        protected CheckoutShippingMethodModel PrepareShippingMethodModel(IList<ShoppingCartItem> cart)
        {

            var model = new CheckoutShippingMethodModel();

            // Add Master Spas Fixed Rate Shipping Options...
            // Call Store Procedure

            #region RGT-REMOVE
            /* RGT-OLD 08.28.2018
            var shippingOptions_NoCharge = new CheckoutShippingMethodModel.ShippingMethodModel();
            shippingOptions_NoCharge.ShippingRateComputationMethodSystemName = "NoCharge.CustomerPickup";
            shippingOptions_NoCharge.Name = "Customer Pickup";
            shippingOptions_NoCharge.Fee = _priceFormatter.FormatShippingPrice(0.00M, true);
            model.ShippingMethods.Add(shippingOptions_NoCharge);
            
            shippingOptions_NoCharge.ShippingRateComputationMethodSystemName = "NoCharge.NextTruck";
            shippingOptions_NoCharge.Name = "Next 2 Truck";
            shippingOptions_NoCharge.Fee = _priceFormatter.FormatShippingPrice(0.00M, true);
            model.ShippingMethods.Add(shippingOptions_NoCharge);
            */

            //var shippingFixedRate = _dbContext.SqlQuery<ShippingOption>("SPROC_PO_Shipping_FixedRates").ToList();
            //foreach (ShippingOption so in shippingFixedRate) {
            //    var soModel = new CheckoutShippingMethodModel.ShippingMethodModel()
            //    {
            //        Name = so.Name,
            //        Description = so.Description,
            //        ShippingRateComputationMethodSystemName = so.ShippingRateComputationMethodSystemName,
            //        ShippingOption = so,
            //    };

            //    soModel.Fee = so.Rate.ToString();
            //    model.ShippingMethods.Add(soModel);
            //}
            #endregion

            var getShippingOptionResponse = _shippingService
                .GetShippingOptions(cart, _workContext.CurrentCustomer.ShippingAddress,
                "", _storeContext.CurrentStore.Id);

            //FedEx:on, put false; otherwise; getShippingOptionResponse.
            if (getShippingOptionResponse.Success)
            {

                //performance optimization. cache returned shipping options.
                //we'll use them later (after a customer has selected an option).
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                                                       SystemCustomerAttributeNames.OfferedShippingOptions,
                                                       getShippingOptionResponse.ShippingOptions,
                                                       _storeContext.CurrentStore.Id);

                foreach (var shippingOption in getShippingOptionResponse.ShippingOptions)
                {

                    var soModel = new CheckoutShippingMethodModel.ShippingMethodModel()
                    {
                        Name = shippingOption.Name,
                        Description = shippingOption.Description,
                        ShippingRateComputationMethodSystemName = shippingOption.ShippingRateComputationMethodSystemName,
                        ShippingOption = shippingOption,
                    };

                    //adjust rate
                    Discount appliedDiscount = null;
                    var shippingTotal = _orderTotalCalculationService.AdjustShippingRate(
                        shippingOption.Rate, cart, out appliedDiscount);

                    int ttlQty = 0;
                    foreach (var item in cart)
                    {
                        ttlQty += item.Quantity;
                    }


                    soModel.Fee = shippingOption.Rate.ToString();
                    soModel.Description = shippingOption.DeliveryTimeStamp;
                    model.ShippingMethods.Add(soModel);
                }

                //find a selected (previously) shipping method
                var selectedShippingOption =
                    _workContext.CurrentCustomer.GetAttribute<ShippingOption>(
                        SystemCustomerAttributeNames.SelectedShippingOption, _storeContext.CurrentStore.Id);
                if (selectedShippingOption != null)
                {
                    var shippingOptionToSelect = model.ShippingMethods.ToList()
                                                      .Find(
                                                          so =>
                                                          !String.IsNullOrEmpty(so.Name) &&
                                                          so.Name.Equals(selectedShippingOption.Name,
                                                                         StringComparison.InvariantCultureIgnoreCase) &&
                                                          !String.IsNullOrEmpty(
                                                              so.ShippingRateComputationMethodSystemName) &&
                                                          so.ShippingRateComputationMethodSystemName.Equals(
                                                              selectedShippingOption
                                                                  .ShippingRateComputationMethodSystemName,
                                                              StringComparison.InvariantCultureIgnoreCase));
                    if (shippingOptionToSelect != null)
                        shippingOptionToSelect.Selected = true;
                }
                //if no option has been selected, let's do it for the first one
                if (model.ShippingMethods.FirstOrDefault(so => so.Selected) == null)
                {
                    var shippingOptionToSelect = model.ShippingMethods.FirstOrDefault();
                    if (shippingOptionToSelect != null)
                        shippingOptionToSelect.Selected = true;
                }
            }
            else
            {
                foreach (var error in getShippingOptionResponse.Errors)
                    model.Warnings.Add(error);
            }

            return model;
        }

        [NonAction]
        protected CheckoutPaymentMethodModel PreparePaymentMethodModel(IList<ShoppingCartItem> cart)
        {
            var model = new CheckoutPaymentMethodModel();

            //reward points
            if (_rewardPointsSettings.Enabled && !cart.IsRecurring())
            {
                int rewardPointsBalance = _workContext.CurrentCustomer.GetRewardPointsBalance();
                decimal rewardPointsAmountBase = _orderTotalCalculationService.ConvertRewardPointsToAmount(rewardPointsBalance);
                decimal rewardPointsAmount = _currencyService.ConvertFromPrimaryStoreCurrency(rewardPointsAmountBase, _workContext.WorkingCurrency);
                if (rewardPointsAmount > decimal.Zero &&
                    _orderTotalCalculationService.CheckMinimumRewardPointsToUseRequirement(rewardPointsBalance))
                {
                    model.DisplayRewardPoints = true;
                    model.RewardPointsAmount = _priceFormatter.FormatPrice(rewardPointsAmount, true, false);
                    model.RewardPointsBalance = rewardPointsBalance;
                }
            }

            //filter by country
            int filterByCountryId = 0;
            if (_addressSettings.CountryEnabled &&
                _workContext.CurrentCustomer.BillingAddress != null &&
                _workContext.CurrentCustomer.BillingAddress.Country != null)
            {
                filterByCountryId = _workContext.CurrentCustomer.BillingAddress.Country.Id;
            }
            /*
            var boundPaymentMethods = _paymentService
                .LoadActivePaymentMethods(_workContext.CurrentCustomer.Id, _storeContext.CurrentStore.Id, filterByCountryId)
                .Where(pm => pm.PaymentMethodType == PaymentMethodType.Standard || pm.PaymentMethodType == PaymentMethodType.Redirection)
                .ToList();
            foreach (var pm in boundPaymentMethods)
            {
                if (cart.IsRecurring() && pm.RecurringPaymentType == RecurringPaymentType.NotSupported)
                    continue;

                var pmModel = new CheckoutPaymentMethodModel.PaymentMethodModel()
                {
                    
                    Name = pm.GetLocalizedFriendlyName(_localizationService, _workContext.WorkingLanguage.Id),
                    PaymentMethodSystemName = pm.PluginDescriptor.SystemName,
                    LogoUrl = pm.PluginDescriptor.GetLogoUrl(_webHelper)
                };

                
                throw new Exception(pmModel.Name);
              
                //payment method additional fee
                decimal paymentMethodAdditionalFee = _paymentService.GetAdditionalHandlingFee(cart, pm.PluginDescriptor.SystemName);
                decimal rateBase = _taxService.GetPaymentMethodAdditionalFee(paymentMethodAdditionalFee, _workContext.CurrentCustomer);
                decimal rate = _currencyService.ConvertFromPrimaryStoreCurrency(rateBase, _workContext.WorkingCurrency);
                if (rate > decimal.Zero)
                    pmModel.Fee = _priceFormatter.FormatPaymentMethodAdditionalFee(rate, true);

                model.ExemptPrePay = true ;
                model.PaymentMethods.Add(pmModel);
            }
            */
            //find a selected (previously) payment method
            var selectedPaymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                SystemCustomerAttributeNames.SelectedPaymentMethod,
                _genericAttributeService, _storeContext.CurrentStore.Id);
            if (!String.IsNullOrEmpty(selectedPaymentMethodSystemName))
            {
                var paymentMethodToSelect = model.PaymentMethods.ToList()
                    .Find(pm => pm.PaymentMethodSystemName.Equals(selectedPaymentMethodSystemName, StringComparison.InvariantCultureIgnoreCase));
                if (paymentMethodToSelect != null)
                    paymentMethodToSelect.Selected = true;
            }
            //if no option has been selected, let's do it for the first one
            if (model.PaymentMethods.FirstOrDefault(so => so.Selected) == null)
            {
                var paymentMethodToSelect = model.PaymentMethods.FirstOrDefault();
                if (paymentMethodToSelect != null)
                    paymentMethodToSelect.Selected = true;
            }

            return model;
        }
        [NonAction]
        protected CheckoutPaymentInfoModel PreparePaymentInfoModel()
        {
            var model = new CheckoutPaymentInfoModel();
            string actionName;
            string controllerName;
            //RouteValueDictionary routeValues;
            //paymentMethod.GetPaymentInfoRoute(out actionName, out controllerName, out routeValues);
            model.PaymentInfoActionName = "N/A";//actionName;
            model.PaymentInfoControllerName = "N/A";//controllerName;
                                                    //model.PaymentInfoRouteValues = "N/A"; //routeValues;
            var tst = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.OrderTotal, _storeContext.CurrentStore.Id);


            var CreditCards = _dbContext.SqlQuery<CheckoutPaymentInfoModel.CreditCard>("SPROC_PO_CCVault_Select @OpCode, @CustomerID, @CCLast4",
                new SqlParameter("@OpCode", "VaultView"),
                new SqlParameter("@CustomerID", _workContext.CurrentCustomer.Id),
                new SqlParameter("@CCLast4", "XXXX")).ToList();

            foreach (CheckoutPaymentInfoModel.CreditCard cc in CreditCards)
            {
                var ccModel = new CheckoutPaymentInfoModel.CreditCard()
                {
                    CardType = cc.CardType,
                    CardNumber = cc.CardNumber,
                    CardExpirationMonth = cc.CardExpirationMonth,
                    CardExpirationYear = cc.CardExpirationYear,

                };

                model.CreditCards.Add(ccModel);
            }

            return model;
        }


        [NonAction]
        protected CheckoutConfirmModel PrepareConfirmOrderModel(IList<ShoppingCartItem> cart)
        {

            var model = new CheckoutConfirmModel();
            //terms of service
            model.TermsOfServiceOnOrderConfirmPage = _orderSettings.TermsOfServiceOnOrderConfirmPage;
            //min order amount validation
            bool minOrderTotalAmountOk = _orderProcessingService.ValidateMinOrderTotalAmount(cart);
            if (!minOrderTotalAmountOk)
            {
                decimal minOrderTotalAmount = _currencyService.ConvertFromPrimaryStoreCurrency(_orderSettings.MinOrderTotalAmount, _workContext.WorkingCurrency);
                model.MinOrderTotalWarning = string.Format(_localizationService.GetResource("Checkout.MinOrderTotalAmount"), _priceFormatter.FormatPrice(minOrderTotalAmount, true, false));
            }
            return model;
        }

        [NonAction]
        protected bool UseOnePageCheckout()
        {
            bool useMobileDevice = _mobileDeviceHelper.IsMobileDevice(_httpContext)
                && _mobileDeviceHelper.MobileDevicesSupported()
                && !_mobileDeviceHelper.CustomerDontUseMobileVersion();

            //mobile version doesn't support one-page checkout
            if (useMobileDevice)
                return false;

            //check the appropriate setting
            return _orderSettings.OnePageCheckoutEnabled;
        }

        [NonAction]
        protected bool IsMinimumOrderPlacementIntervalValid(Customer customer)
        {
            //prevent 2 orders being placed within an X seconds time frame
            if (_orderSettings.MinimumOrderPlacementInterval == 0)
                return true;

            var lastOrder = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                .FirstOrDefault();
            if (lastOrder == null)
                return true;

            var interval = DateTime.UtcNow - lastOrder.CreatedOnUtc;
            return interval.TotalSeconds > _orderSettings.MinimumOrderPlacementInterval;
        }

        #endregion

        #region Methods (common)

        public ActionResult Index()
        {


            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            //if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))


            //reset checkout data
            _customerService.ResetCheckoutData(_workContext.CurrentCustomer, _storeContext.CurrentStore.Id);

            //validation (cart)
            //var checkoutAttributesXml = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.CheckoutAttributes, _genericAttributeService);
            //var scWarnings = _shoppingCartService.GetShoppingCartWarnings(cart, checkoutAttributesXml, true);
            //if (scWarnings.Count > 0)
            //    return RedirectToRoute("ShoppingCart");
            //validation (each shopping cart item)
            /*foreach (ShoppingCartItem sci in cart)
            {
                var sciWarnings = _shoppingCartService.GetShoppingCartItemWarnings(_workContext.CurrentCustomer,
                    sci.ShoppingCartType,
                    sci.Product,
                    sci.StoreId,
                    sci.AttributesXml,
                    sci.CustomerEnteredPrice,
                    sci.Quantity,
                    false);
                if (sciWarnings.Count > 0)
                    return RedirectToRoute("ShoppingCart");
            }*/

            //if (UseOnePageCheckout())
            //    return RedirectToRoute("CheckoutOnePage");
            //else
            return RedirectToRoute("CheckoutBillingAddress");
        }

        public ActionResult Completed(int? orderId)
        {

            //validation
            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            Order order = null;
            if (orderId.HasValue)
            {
                //load order by identifier (if provided)
                order = _orderService.GetOrderById(orderId.Value);
            }
            if (order == null)
            {
                order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                    .FirstOrDefault();
            }
            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
            {
                return RedirectToRoute("HomePage");
            }


            //Clear Customer Attributes for next order...

            //model
            var model = new CheckoutCompletedModel()
            {
                OrderId = order.Id,
                ReferenceCode = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.ReferenceCode, _storeContext.CurrentStore.Id),
                OnePageCheckoutEnabled = _orderSettings.OnePageCheckoutEnabled
            };

            var attributes = _genericAttributeService.GetAttributesForEntity(_workContext.CurrentCustomer.Id, "Customer");
            foreach (var attribute in attributes)
            {
                _genericAttributeService.DeleteAttribute(attribute);
            }

            return View(model);
        }

        #endregion

        #region Methods (multistep checkout)

        public ActionResult BillingAddress()
        {
            //throw new Exception("In Billing Address (multi step)....");
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //model
            var model = PrepareBillingAddressModel(prePopulateNewAddressWithCustomerFields: true);
            return View(model);
        }
        public ActionResult SelectBillingAddress(int addressId)
        {

            var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null)
                return RedirectToRoute("CheckoutBillingAddress");

            _workContext.CurrentCustomer.BillingAddress = address;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);

            return RedirectToRoute("CheckoutShippingAddress");
        }
        [HttpPost, ActionName("BillingAddress")]
        [FormValueRequired("nextstep")]
        public ActionResult NewBillingAddress(CheckoutBillingAddressModel model)
        {


            //validation;
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                var address = model.NewAddress.ToEntity();
                address.CreatedOnUtc = DateTime.UtcNow;
                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;
                _workContext.CurrentCustomer.Addresses.Add(address);
                _workContext.CurrentCustomer.BillingAddress = address;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                return RedirectToRoute("CheckoutShippingAddress");
            }


            //If we got this far, something failed, redisplay form
            model = PrepareBillingAddressModel(selectedCountryId: model.NewAddress.CountryId);
            return View(model);
        }

        public ActionResult ShippingAddress()
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            //if (UseOnePageCheckout())
            //    return RedirectToRoute("CheckoutOnePage");

            //if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
            //    return new HttpUnauthorizedResult();

            //if (!cart.RequiresShipping())
            //{
            //   _workContext.CurrentCustomer.ShippingAddress = null;
            //   _customerService.UpdateCustomer(_workContext.CurrentCustomer);
            return RedirectToRoute("CheckoutShippingMethod");
            //}

            //model
            var model = PrepareShippingAddressModel(prePopulateNewAddressWithCustomerFields: true);
            return View(model);
        }
        public ActionResult SelectShippingAddress(int addressId)
        {
            var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null)
                return RedirectToRoute("CheckoutShippingAddress");

            _workContext.CurrentCustomer.ShippingAddress = address;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);

            return RedirectToRoute("CheckoutShippingMethod");
        }
        [HttpPost, ActionName("ShippingAddress")]
        [FormValueRequired("nextstep")]
        public ActionResult NewShippingAddress(CheckoutShippingAddressModel model)
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            if (!cart.RequiresShipping())
            {
                _workContext.CurrentCustomer.ShippingAddress = null;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                return RedirectToRoute("CheckoutShippingMethod");
            }

            if (ModelState.IsValid)
            {
                var address = model.NewAddress.ToEntity();
                address.CreatedOnUtc = DateTime.UtcNow;
                //some validation
                if (address.CountryId == 0)
                    address.CountryId = null;
                if (address.StateProvinceId == 0)
                    address.StateProvinceId = null;
                _workContext.CurrentCustomer.Addresses.Add(address);
                _workContext.CurrentCustomer.ShippingAddress = address;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                return RedirectToRoute("CheckoutShippingMethod");
            }


            //If we got this far, something failed, redisplay form
            model = PrepareShippingAddressModel(selectedCountryId: model.NewAddress.CountryId);
            return View(model);
        }


        public ActionResult ShippingMethod()
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            if (!cart.RequiresShipping())
            {
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, SystemCustomerAttributeNames.SelectedShippingOption, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentMethod");
            }

            //model
            var model = PrepareShippingMethodModel(cart);

            if (_shippingSettings.BypassShippingMethodSelectionIfOnlyOne &&
                model.ShippingMethods.Count == 1)
            {
                //if we have only one shipping method, then a customer doesn't have to choose a shipping method
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedShippingOption,
                    model.ShippingMethods.First().ShippingOption,
                    _storeContext.CurrentStore.Id);

                return RedirectToRoute("CheckoutPaymentMethod");
            }

            return View(model);
        }
        [HttpPost, ActionName("ShippingMethod")]
        [FormValueRequired("nextstep")]
        [ValidateInput(false)]
        public ActionResult SelectShippingMethod(string shippingoption)
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            if (!cart.RequiresShipping())
            {
                _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedShippingOption, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentMethod");
            }

            //parse selected method 
            if (String.IsNullOrEmpty(shippingoption))
                return ShippingMethod();
            var splittedOption = shippingoption.Split(new string[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            if (splittedOption.Length != 2)
                return ShippingMethod();
            string selectedName = splittedOption[0];
            string shippingRateComputationMethodSystemName = splittedOption[1];

            //find it
            //performance optimization. try cache first
            var shippingOptions = _workContext.CurrentCustomer.GetAttribute<List<ShippingOption>>(SystemCustomerAttributeNames.OfferedShippingOptions, _storeContext.CurrentStore.Id);
            if (shippingOptions == null || shippingOptions.Count == 0)
            {
                //not found? let's load them using shipping service
                shippingOptions = _shippingService
                    .GetShippingOptions(cart, _workContext.CurrentCustomer.ShippingAddress, shippingRateComputationMethodSystemName, _storeContext.CurrentStore.Id)
                    .ShippingOptions
                    .ToList();
            }
            else
            {
                //loaded cached results. let's filter result by a chosen shipping rate computation method
                shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
            }

            var shippingOption = shippingOptions
                .Find(so => !String.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
            if (shippingOption == null)
                return ShippingMethod();

            //save
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.SelectedShippingOption, shippingOption, _storeContext.CurrentStore.Id);

            return RedirectToRoute("CheckoutPaymentMethod");
        }


        public ActionResult PaymentMethod()
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //Check whether payment workflow is required
            //we ignore reward points during cart total calculation
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart, true);
            if (!isPaymentWorkflowRequired)
            {
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedPaymentMethod, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }

            //model
            var paymentMethodModel = PreparePaymentMethodModel(cart);

            if (_paymentSettings.BypassPaymentMethodSelectionIfOnlyOne &&
                paymentMethodModel.PaymentMethods.Count == 1 && !paymentMethodModel.DisplayRewardPoints)
            {
                //if we have only one payment method and reward points are disabled or the current customer doesn't have any reward points
                //so customer doesn't have to choose a payment method

                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedPaymentMethod,
                    paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName,
                    _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }

            return View(paymentMethodModel);
        }
        [HttpPost, ActionName("PaymentMethod")]
        [FormValueRequired("nextstep")]
        [ValidateInput(false)]
        public ActionResult SelectPaymentMethod(string paymentmethod, CheckoutPaymentMethodModel model)
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //reward points
            if (_rewardPointsSettings.Enabled)
            {
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.UseRewardPointsDuringCheckout, model.UseRewardPoints,
                    _storeContext.CurrentStore.Id);
            }

            //Check whether payment workflow is required
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedPaymentMethod, null, _storeContext.CurrentStore.Id);
                return RedirectToRoute("CheckoutPaymentInfo");
            }
            //payment method 
            if (String.IsNullOrEmpty(paymentmethod))
                return PaymentMethod();

            var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(paymentmethod);
            if (paymentMethodInst == null ||
                !paymentMethodInst.IsPaymentMethodActive(_paymentSettings) ||
                !_pluginFinder.AuthenticateStore(paymentMethodInst.PluginDescriptor, _storeContext.CurrentStore.Id))
                return PaymentMethod();

            //save
            _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                SystemCustomerAttributeNames.SelectedPaymentMethod, paymentmethod, _storeContext.CurrentStore.Id);

            return RedirectToRoute("CheckoutPaymentInfo");
        }


        public ActionResult PaymentInfo()
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //Check whether payment workflow is required
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {

                return RedirectToRoute("CheckoutConfirm");
            }

            //load payment method
            var paymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                SystemCustomerAttributeNames.SelectedPaymentMethod,
                _genericAttributeService, _storeContext.CurrentStore.Id);
            var paymentMethod = _paymentService.LoadPaymentMethodBySystemName(paymentMethodSystemName);
            if (paymentMethod == null)
                return RedirectToRoute("CheckoutPaymentMethod");

            //Check whether payment info should be skipped
            if (paymentMethod.SkipPaymentInfo)
            {
                //skip payment info page
                var paymentInfo = new ProcessPaymentRequest();
                //session save
                _httpContext.Session["OrderPaymentInfo"] = paymentInfo;

                return RedirectToRoute("CheckoutConfirm");
            }

            //model
            var model = PreparePaymentInfoModel();
            return View(model);
        }
        [HttpPost, ActionName("PaymentInfo")]
        [FormValueRequired("nextstep")]
        [ValidateInput(false)]
        public ActionResult EnterPaymentInfo(FormCollection form)
        {
            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //Check whether payment workflow is required
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart);
            if (!isPaymentWorkflowRequired)
            {
                return RedirectToRoute("CheckoutConfirm");
            }

            //load payment method
            var paymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                SystemCustomerAttributeNames.SelectedPaymentMethod,
                _genericAttributeService, _storeContext.CurrentStore.Id);
            var paymentMethod = _paymentService.LoadPaymentMethodBySystemName(paymentMethodSystemName);
            if (paymentMethod == null)
                return RedirectToRoute("CheckoutPaymentMethod");

            var paymentControllerType = paymentMethod.GetControllerType();
            var paymentController = DependencyResolver.Current.GetService(paymentControllerType) as BaseNopPaymentController;
            var warnings = paymentController.ValidatePaymentForm(form);
            foreach (var warning in warnings)
                ModelState.AddModelError("", warning);
            if (ModelState.IsValid)
            {
                //get payment info
                var paymentInfo = paymentController.GetPaymentInfo(form);
                //session save
                _httpContext.Session["OrderPaymentInfo"] = paymentInfo;
                return RedirectToRoute("CheckoutConfirm");
            }

            //If we got this far, something failed, redisplay form
            //model
            var model = PreparePaymentInfoModel();
            return View(model);
        }


        public ActionResult Confirm()
        {


            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (UseOnePageCheckout())
                return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            //model
            var model = PrepareConfirmOrderModel(cart);
            return View(model);
        }
        [HttpPost, ActionName("Confirm")]
        [ValidateInput(false)]
        public ActionResult ConfirmOrder()
        {


            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            //   if (UseOnePageCheckout())
            //       return RedirectToRoute("CheckoutOnePage");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();


            //model
            var model = PrepareConfirmOrderModel(cart);
            try
            {

                var processPaymentRequest = _httpContext.Session["OrderPaymentInfo"] as ProcessPaymentRequest;
                if (processPaymentRequest == null)
                {
                    //Check whether payment workflow is required
                    if (IsPaymentWorkflowRequired(cart))
                        return RedirectToRoute("CheckoutPaymentInfo");
                    else
                        processPaymentRequest = new ProcessPaymentRequest();
                }

                //prevent 2 orders being placed within an X seconds time frame
                if (!IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
                    throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));

                //place order
                processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                processPaymentRequest.PaymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                    SystemCustomerAttributeNames.SelectedPaymentMethod,
                    _genericAttributeService, _storeContext.CurrentStore.Id);

                var placeOrderResult = _orderProcessingService.PlaceOrder();


                if (placeOrderResult.Success)
                {

                    var paymentMethodModel = PreparePaymentMethodModel(cart);
                    var oneCheckOutModel = new OnePageCheckoutModel();

                    // Set Dealer Exempt to PrePay...   (CustomerRole = Exempt PrePay)
                    paymentMethodModel.ExemptPrePay = _customerService.CustomerCheckExemptPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

                    // Set Term PrePay...               (Check to see if Dealer has Terms set to PrePay.)
                    paymentMethodModel.TermsPrePay = _customerService.CustomerCheckPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

                    // Set Credit Hold...               (Check to see if Dealer has been placed on Credit Hold.)
                    paymentMethodModel.CreditHold = _customerService.CustomerCheckCreditHold(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);


                    // CreditHold...Customer won't be allowed to checkout until after calling Accounting...            
                    if (paymentMethodModel.CreditHold)
                    {
                        return OpcLoadCreditHold_Msg(oneCheckOutModel);
                    }


                    // Customer Terms = PrePay
                    if (paymentMethodModel.TermsPrePay)
                    {
                        //  Display Sage Credit Card API
                        var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                        var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName);
                        return OpcLoadStepAfterPaymentMethod(cart);
                    }


                    // Dealer has no Credit Hold or has Terms set to PrePay.
                    // Now see if Dealer is Exempt from PrePay (Accessories are PrePay)...
                    if (paymentMethodModel.ExemptPrePay)
                    {
                        ViewBag.ExemptPrePay = "Yes";
                        var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                        var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName);
                        return OpcLoadStepAfterPaymentMethod(cart);

                    }


                    var selectedPaymentMethodSystemName2 = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                    var paymentMethodInst2 = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName2);

                    return OpcLoadStepAfterPaymentMethod(cart);









                    /*_httpContext.Session["OrderPaymentInfo"] = null;
                    var postProcessPaymentRequest = new PostProcessPaymentRequest()
                    {
                        Order = placeOrderResult.PlacedOrder
                    };
                    _paymentService.PostProcessPayment(postProcessPaymentRequest);

                    if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                    {
                        //redirection or POST has been done in PostProcessPayment
                        return Content("Redirected");
                    }
                    else
                    {
                        return RedirectToRoute("CheckoutCompleted", new { orderId = placeOrderResult.PlacedOrder.Id });
                    }
                    */
                }
                else
                {
                    foreach (var error in placeOrderResult.Errors)
                        model.Warnings.Add(error);
                }
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc);
                model.Warnings.Add(exc.Message);
            }

            //If we got this far, something failed, redisplay form
            return View(model);
        }


        [ChildActionOnly]
        public ActionResult CheckoutProgress(CheckoutProgressStep step)
        {
            var model = new CheckoutProgressModel() { CheckoutProgressStep = step };
            return PartialView(model);
        }

        #endregion

        #region Methods (one page checkout)


        [NonAction]
        protected JsonResult OpcLoadStepAfterShippingMethod_Old(List<ShoppingCartItem> cart)
        {
            //Check whether payment workflow is required
            //we ignore reward points during cart total calculation
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart, true);
            if (isPaymentWorkflowRequired)
            {
                //payment is required
                var paymentMethodModel = PreparePaymentMethodModel(cart);

                if (_paymentSettings.BypassPaymentMethodSelectionIfOnlyOne &&
                    paymentMethodModel.PaymentMethods.Count == 1 && !paymentMethodModel.DisplayRewardPoints)
                {
                    //if we have only one payment method and reward points are disabled or the current customer doesn't have any reward points
                    //so customer doesn't have to choose a payment method

                    var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                    _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                        SystemCustomerAttributeNames.SelectedPaymentMethod,
                        selectedPaymentMethodSystemName, _storeContext.CurrentStore.Id);

                    var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName);
                    if (paymentMethodInst == null ||
                        !paymentMethodInst.IsPaymentMethodActive(_paymentSettings) ||
                        !_pluginFinder.AuthenticateStore(paymentMethodInst.PluginDescriptor, _storeContext.CurrentStore.Id))
                        throw new Exception("Selected payment method can't be parsed");

                    return OpcLoadStepAfterPaymentMethod(cart);
                }
                else
                {
                    //customer have to choose a payment method
                    return Json(new
                    {
                        update_section = new UpdateSectionJsonModel()
                        {
                            name = "payment-method",
                            html = this.RenderPartialViewToString("OpcPaymentMethods", paymentMethodModel)
                        },
                        goto_section = "payment_method"
                    });
                }
            }
            else
            {
                //payment is not required
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedPaymentMethod, null, _storeContext.CurrentStore.Id);

                var confirmOrderModel = PrepareConfirmOrderModel(cart);
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel()
                    {
                        name = "confirm-order",
                        html = this.RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                    },
                    goto_section = "confirm_order"
                });
            }
        }

        [NonAction]
        protected JsonResult OpcLoadStepAfterShippingMethod(List<ShoppingCartItem> cart)
        {

            //Check whether payment work flow is required
            //we ignore reward points during cart total calculation
            bool isPaymentWorkflowRequired = IsPaymentWorkflowRequired(cart, true);


            var paymentMethodModel = PreparePaymentMethodModel(cart);

            var oneCheckOutModel = new OnePageCheckoutModel();

            Customer cust = new Customer();


            // Set Dealer Exempt to PrePay...   (CustomerRole = Exempt PrePay)
            paymentMethodModel.ExemptPrePay = _customerService.CustomerCheckExemptPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

            // Set Term PrePay...               (Check to see if Dealer has Terms set to PrePay.)
            paymentMethodModel.TermsPrePay = _customerService.CustomerCheckPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

            // Set Credit Hold...               (Check to see if Dealer has been placed on Credit Hold.)
            paymentMethodModel.CreditHold = _customerService.CustomerCheckCreditHold(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);



            // CreditHold...Customer won't be allowed to checkout until after calling Accounting...            
            if (paymentMethodModel.CreditHold)
            {
                return OpcLoadCreditHold_Msg(oneCheckOutModel);
            }


            // Customer Terms = PrePay
            if (paymentMethodModel.TermsPrePay)
            {
                //  Display Sage Credit Card API
                var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName);
                return OpcLoadStepAfterPaymentMethod(cart);
            }


            // Dealer has no Credit Hold or has Terms set to PrePay.
            // Now see if Dealer is Exempt from PrePay (Accessories are PrePay)...
            if (paymentMethodModel.ExemptPrePay)
            {
                ViewBag.ExemptPrePay = "Yes";
                var selectedPaymentMethodSystemName = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
                var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName);
                return OpcLoadStepAfterPaymentMethod(cart);

            }


            var selectedPaymentMethodSystemName2 = paymentMethodModel.PaymentMethods[0].PaymentMethodSystemName;
            var paymentMethodInst2 = _paymentService.LoadPaymentMethodBySystemName(selectedPaymentMethodSystemName2);

            return OpcLoadStepAfterPaymentMethod(cart);





            // 05.17.2017 RGT -  Removed NOP code that was at this point..   

        }




        [NonAction]
        protected JsonResult OpcLoadStepAfterPaymentMethod(List<ShoppingCartItem> cart)
        // Used when No Credit Hold has placed been on the customer.
        {

            var paymentMethodModel = PreparePaymentMethodModel(cart);

            var oneCheckOutModel = new OnePageCheckoutModel();

            string paymentMethodInst = "Payments.Manual";

            Customer cust = new Customer();

            #region Get pay codes...

            // Set Dealer Exempt to PrePay...   (CustomerRole = Exempt PrePay)
            paymentMethodModel.ExemptPrePay = _customerService.CustomerCheckExemptPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

            // Set Term PrePay...               (Check to see if Dealer has Terms set to PrePay.)
            paymentMethodModel.TermsPrePay = _customerService.CustomerCheckPrePay(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

            // Set Credit Hold...               (Check to see if Dealer has been placed on Credit Hold.)
            paymentMethodModel.CreditHold = _customerService.CustomerCheckCreditHold(_workContext.CurrentCustomer.DivisionNo, _workContext.CurrentCustomer.AccountNo);

            #endregion 


            // CreditHold...Customer won't be allowed to checkout until after calling Accounting...            
            if (paymentMethodModel.CreditHold)
            {
                return OpcLoadCreditHold_Msg(oneCheckOutModel);
            }


            // Customer Terms = PrePay
            if (paymentMethodModel.TermsPrePay)
            {
                //  Display Sage Credit Card API
                ViewBag.PayType = "PrePay";
            }

            // Dealer has no Credit Hold or has Terms set to PrePay.
            // Now see if Dealer is Exempt from PrePay (Accessories are PrePay)...
            if (paymentMethodModel.ExemptPrePay)
            {
                ViewBag.PayType = "Exempt";
            }


            var paymentInfoModel = PreparePaymentInfoModel();

            paymentInfoModel.OrderTotal = _workContext.CurrentCustomer.GetAttribute<string>("OrderTotal", _storeContext.CurrentStore.Id);

            return Json(new
            {
                update_section = new UpdateSectionJsonModel()
                {
                    name = "payment-info",
                    html = this.RenderPartialViewToString("OpcPaymentInfo", paymentInfoModel)

                },
                goto_section = "payment_info"
            });


        }


        [NonAction]
        protected JsonResult OpcLoadCreditHold_Msg(OnePageCheckoutModel oneCheckOutModel)
        // Displays Credit Hold Message for a Customer.
        {

            return Json(new
            {
                update_section = new UpdateSectionJsonModel()
                {
                    name = "payment-info",
                    html = this.RenderPartialViewToString("OpcPaymentInfo_CreditHold", oneCheckOutModel)
                },
                goto_section = "payment_info"
            });


        }


        public ActionResult OnePageCheckout()
        {
            Session["Checkout"] = "Yes";

            //validation
            var cart = _workContext.CurrentCustomer.ShoppingCartItems
                .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                .ToList();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!UseOnePageCheckout())
                return RedirectToRoute("Checkout");

            if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                return new HttpUnauthorizedResult();

            var model = new OnePageCheckoutModel()
            {
                ShippingRequired = true,// cart.RequiresShipping(),
            };
            return View(model);
        }

        public ActionResult OpcShippingForm()
        {
            var shippingAddressModel = PrepareShippingAddressModel(prePopulateNewAddressWithCustomerFields: true);
            return PartialView("OpcShippingAddress", shippingAddressModel);
        }

        [ChildActionOnly]
        public ActionResult OpcBillingForm()
        {
            var billingAddressModel = PrepareBillingAddressModel(prePopulateNewAddressWithCustomerFields: true);
            return PartialView("OpcBillingAddress", billingAddressModel);
        }

        [ValidateInput(false)]
        public ActionResult OpcSaveBilling(FormCollection form)
        {
            try
            {
                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    throw new Exception("Anonymous checkout is not allowed");

                int billingAddressId = 0;
                int.TryParse(form["billing_address_id"], out billingAddressId);

                if (billingAddressId > 0)
                {
                    //existing address
                    var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == billingAddressId);
                    if (address == null)
                        throw new Exception("Address can't be loaded");

                    _workContext.CurrentCustomer.BillingAddress = address;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }
                else
                {
                    //new address
                    var model = new CheckoutBillingAddressModel();
                    TryUpdateModel(model.NewAddress, "BillingNewAddress");
                    //validate model
                    TryValidateModel(model.NewAddress);
                    if (!ModelState.IsValid)
                    {
                        //model is not valid. redisplay the form with errors
                        var billingAddressModel = PrepareBillingAddressModel(selectedCountryId: model.NewAddress.CountryId);
                        billingAddressModel.NewAddressPreselected = true;
                        return Json(new
                        {
                            update_section = new UpdateSectionJsonModel()
                            {
                                name = "billing",
                                html = this.RenderPartialViewToString("OpcBillingAddress", billingAddressModel)
                            }
                        });
                    }

                    //try to find an address with the same values (don't duplicate records)
                    var address = _workContext.CurrentCustomer.Addresses.ToList().FindAddress(
                        model.NewAddress.FirstName, model.NewAddress.LastName, model.NewAddress.PhoneNumber,
                        model.NewAddress.Email, model.NewAddress.FaxNumber, model.NewAddress.Company,
                        model.NewAddress.Address1, model.NewAddress.Address2, model.NewAddress.City,
                        model.NewAddress.StateProvinceId, model.NewAddress.ZipPostalCode, model.NewAddress.CountryId);
                    if (address == null)
                    {
                        //address is not found. let's create a new one
                        address = model.NewAddress.ToEntity();
                        address.CreatedOnUtc = DateTime.UtcNow;
                        //some validation
                        if (address.CountryId == 0)
                            address.CountryId = null;
                        if (address.StateProvinceId == 0)
                            address.StateProvinceId = null;
                        if (address.CountryId.HasValue && address.CountryId.Value > 0)
                        {
                            address.Country = _countryService.GetCountryById(address.CountryId.Value);
                        }
                        _workContext.CurrentCustomer.Addresses.Add(address);
                    }
                    _workContext.CurrentCustomer.BillingAddress = address;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }

                //if (cart.RequiresShipping())
                //{
                //RGT -- Commented if condition so Shipping is displayed...                        
                //shipping is required
                var shippingAddressModel = PrepareShippingAddressModel(prePopulateNewAddressWithCustomerFields: true);
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel()
                    {
                        name = "shipping",
                        html = this.RenderPartialViewToString("OpcShippingAddress", shippingAddressModel)
                    },
                    goto_section = "shipping"
                });
                /*}
                else
                {
                    //shipping is not required
                    _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer, SystemCustomerAttributeNames.SelectedShippingOption, null, _storeContext.CurrentStore.Id);

                    //load next step
                    return OpcLoadStepAfterShippingMethod(cart);
                }*/
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [ValidateInput(false)]
        public ActionResult OpcSaveShipping(FormCollection form)
        {
            try
            {
                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    throw new Exception("Anonymous checkout is not allowed");
                /*-- RGT if (!cart.RequiresShipping())
                   throw new Exception("Shipping is not required"); --- */



                int shippingAddressId = 0;
                int.TryParse(form["shipping_address_id"], out shippingAddressId);

                if (shippingAddressId > 0)
                {
                    //existing address
                    var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == shippingAddressId);
                    if (address == null)
                        throw new Exception("Address can't be loaded");

                    if (address.StoreAddress)
                    {
                        TempData["DropShip"] = "No";
                    }
                    else
                    {
                        TempData["DropShip"] = "Yes";
                    }


                    _workContext.CurrentCustomer.ShippingAddress = address;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);

                }
                else
                {


                    // RGT 10.11.2017 :  New address - Means a Drop Shipment
                    TempData["DropShip"] = "Yes";

                    //var modelOT = new OrderTotalsModel();
                    //modelOT.IsDropShipped =  true ;

                    var model = new CheckoutShippingAddressModel();
                    TryUpdateModel(model.NewAddress, "ShippingNewAddress");

                    //validate model
                    TryValidateModel(model.NewAddress);
                    /*
                    if (!ModelState.IsValid)
                    {
                        throw new Exception("Model not valid");
                        //model is not valid. redisplay the form with errors
                        var shippingAddressModel = PrepareShippingAddressModel(selectedCountryId: model.NewAddress.CountryId);
                        shippingAddressModel.NewAddressPreselected = true;
                        return Json(new
                        {
                            update_section = new UpdateSectionJsonModel()
                            {
                                name = "shipping",
                                html = this.RenderPartialViewToString("OpcShippingAddress", shippingAddressModel)
                            }
                        });
                    }
                    */
                    //try to find an address with the same values (don't duplicate records)
                    var address = _workContext.CurrentCustomer.Addresses.ToList().FindAddress(
                        model.NewAddress.FirstName, model.NewAddress.LastName, model.NewAddress.PhoneNumber,
                        model.NewAddress.Email, model.NewAddress.FaxNumber, model.NewAddress.Company,
                        model.NewAddress.Address1, model.NewAddress.Address2, model.NewAddress.City,
                        model.NewAddress.StateProvinceId, model.NewAddress.ZipPostalCode, model.NewAddress.CountryId);
                    if (address == null)
                    {
                        address = model.NewAddress.ToEntity();
                        address.CreatedOnUtc = DateTime.UtcNow;
                        //little hack here (TODO: find a better solution)
                        //EF does not load navigation properties for newly created entities (such as this "Address").
                        //we have to load them manually 
                        //otherwise, "Country" property of "Address" entity will be null in shipping rate computation methods
                        if (address.CountryId.HasValue)
                            address.Country = _countryService.GetCountryById(address.CountryId.Value);
                        if (address.StateProvinceId.HasValue)
                            address.StateProvince = _stateProvinceService.GetStateProvinceById(address.StateProvinceId.Value);

                        //other null validations
                        if (address.CountryId == 0)
                            address.CountryId = null;
                        if (address.StateProvinceId == 0)
                            address.StateProvinceId = null;
                        _workContext.CurrentCustomer.Addresses.Add(address);
                    }
                    _workContext.CurrentCustomer.ShippingAddress = address;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                }


                var shippingMethodModel = PrepareShippingMethodModel(cart);

                if (_shippingSettings.BypassShippingMethodSelectionIfOnlyOne &&
                    shippingMethodModel.ShippingMethods.Count == 1)
                {
                    //if we have only one shipping method, then a customer doesn't have to choose a shipping method
                    _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                        SystemCustomerAttributeNames.SelectedShippingOption,
                        shippingMethodModel.ShippingMethods.First().ShippingOption,
                        _storeContext.CurrentStore.Id);

                    //load next step


                    return OpcLoadStepAfterShippingMethod(cart);
                }
                else
                {

                    return Json(new
                    {
                        update_section = new UpdateSectionJsonModel()
                        {
                            name = "shipping-method",
                            html = this.RenderPartialViewToString("OpcShippingMethods", shippingMethodModel)
                        },
                        goto_section = "shipping_method"
                    });
                }
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [ValidateInput(false)]
        public ActionResult OpcSaveShippingMethod(FormCollection form)
        {
            try
            {

                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    throw new Exception("Anonymous checkout is not allowed");

                //RGT --- if (!cart.RequiresShipping())
                //throw new Exception("Shipping is not required");

                //parse selected method 
                string shippingoption = form["shippingoption"];
                //throw new Exception(shippingoption);

                if (String.IsNullOrEmpty(shippingoption))
                    throw new Exception("Selected shipping method can't be parsed");
                var splittedOption = shippingoption.Split(new string[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
                if (splittedOption.Length != 2)
                    throw new Exception("Selected shipping method can't be parsed");
                string selectedName = splittedOption[0];

                string shippingRateComputationMethodSystemName = splittedOption[1];

                //                throw new Exception(selectedName + "  " + shippingRateComputationMethodSystemName);
                //find it
                //performance optimization. try cache first
                var shippingOptions = _workContext.CurrentCustomer.GetAttribute<List<ShippingOption>>(SystemCustomerAttributeNames.OfferedShippingOptions, _storeContext.CurrentStore.Id);


                if (shippingOptions == null || shippingOptions.Count == 0)
                {
                    //not found? let's load them using shipping service
                    shippingOptions = _shippingService
                        .GetShippingOptions(cart, _workContext.CurrentCustomer.ShippingAddress, shippingRateComputationMethodSystemName, _storeContext.CurrentStore.Id)
                        .ShippingOptions
                        .ToList();
                }
                else
                {

                    //loaded cached results. let's filter result by a chosen shipping rate computation method
                    shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                        .ToList();
                }

                var shippingOption = shippingOptions
                    .Find(so => !String.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));

                // shippingOption.Rate = 75.00M;
                //throw new Exception(shippingOption.Rate.ToString());
                if (shippingOption == null)
                    throw new Exception("Selected shipping method can't be loaded");

                var desiredShipDate = form["desiredShipDate"];

                //save
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.DesiredShipDate, desiredShipDate, _storeContext.CurrentStore.Id);
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.SelectedShippingOption, shippingOption, _storeContext.CurrentStore.Id);
                //load next step
                // return OpcLoadStepAfterShippingMethod(cart);
                //new Exception("RR-At the bottom");

                return OpcConfirmOrder();
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [ValidateInput(false)]
        public ActionResult PaymentMethod_New()
        {
            try
            {
                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                // RGT 08.02.2018:   First, save order to tables, this will clear the cart.
                // Moved from OpcConfirmOrder()
                //var processPaymentRequest = _httpContext.Session["OrderPaymentInfo"] as ProcessPaymentRequest;
                //processPaymentRequest = new ProcessPaymentRequest();
                //processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                //processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                //processPaymentRequest.PaymentMethodSystemName = _workContext.CurrentCustomer.GetAttribute<string>(
                //    SystemCustomerAttributeNames.SelectedPaymentMethod,
                //    _genericAttributeService, _storeContext.CurrentStore.Id);
                //var placeOrderResult = _orderProcessingService.PlaceOrder();


                //if (placeOrderResult.Success)
                //{
                return OpcLoadStepAfterPaymentMethod(cart);
                //}

                //return Json(new { success = 1 });

            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }

        }

        [HttpPost]
        public JsonResult SageCCPayment_Vault_Update(string OpCode, string CardToken, string CardType, string CardNumber, string CardExpMonth, string CardExpYear) {
            //================================================
            //== Stored limited credit card info on the local 
            //== db.  AuthKey will be retrieved to process 
            //== future orders.
            //================================================
            try
            {
                var CreditCards = _dbContext.SqlQuery<CheckoutPaymentInfoModel.CreditCard>("SPROC_PO_CCVault_Update @OpCode, @CustomerID, @CardToken, @CardType, @CardNumber, @CardExpMonth, @CardExpYear",
                    new SqlParameter("@OpCode", OpCode),
                    new SqlParameter("@CustomerID", _workContext.CurrentCustomer.Id),
                    new SqlParameter("@CardToken", (CardToken != "N/A") ? Guid.Parse(CardToken) : Guid.NewGuid()),
                    new SqlParameter("@CardType", CardType),
                    new SqlParameter("@CardNumber", CardNumber),
                    new SqlParameter("@CardExpMonth", CardExpMonth),
                    new SqlParameter("@CardExpYear", CardExpYear)).ToList();

                return Json(new { success = 0 });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        [HttpPost]
        public JsonResult SageCCPayment_Vault_GetAuthKey(string CCLast4) {
            //================================================
            //== Retrieves AuthKey based on last 4 of CC No.
            //================================================

            try {

                Nonces Nonces = Shared.GetNonces();

                string orderTotal = _workContext.CurrentCustomer.GetAttribute<string>("OrderTotal", _storeContext.CurrentStore.Id);
                DateTime objDate = DateTime.Now;

                string orderRequestId = Vault_GetRequestID();

                if (CCLast4 != "----")
                {
                    var CreditCards = _dbContext.SqlQuery<CheckoutPaymentInfoModel.CreditCard>("SPROC_PO_CCVault_Select @OpCode, @CustomerID, @CCLast4",
                        new SqlParameter("@OpCode", "doTokenPayment"),
                        new SqlParameter("@CustomerID", _workContext.CurrentCustomer.Id),
                        new SqlParameter("@CCLast4", CCLast4)).ToList();

                    foreach (CheckoutPaymentInfoModel.CreditCard cc in CreditCards)
                    {
                        var request = new
                        {
                            merchantId = Shared.MerchantID,
                            merchantKey = Shared.MerchantKEY,
                            developerKey = Shared.DeveloperKEY,// don't include the Merchant Key in the JavaScript initialization!
                            requestType = "payment",
                            requestId = orderRequestId,
                            noncesiv = Nonces.IV,
                            salt = Nonces.Salt,
                            postbackUrl = Shared.PostbackUrl,
                            preAuth = Shared.PreAuth,
                            amount = orderTotal,
                            token = cc.CardToken.ToString("N")    //"913736057d1945f5982f03c147743aeb"
                        };

                        string jsonReqVault = JsonConvert.SerializeObject(request);
                        string AuthKey = Shared.GetAuthKey(jsonReqVault, Shared.DeveloperKEY, Nonces.IV, Nonces.Salt);


                        return Json(new authKeyData()
                        {
                            authKey = AuthKey,
                            requestID = orderRequestId,
                            salt = Nonces.Salt,
                            token = cc.CardToken.ToString("N")
                        });

                    }

                }
                else {  // Credit Card Manual Entry... (Not chose from vault.)
                    var request = new
                    {
                        merchantId = Shared.MerchantID,
                        merchantKey = Shared.MerchantKEY,
                        developerKey = Shared.DeveloperKEY,// don't include the Merchant Key in the JavaScript initialization!
                        requestType = "payment",
                        requestId = orderRequestId,
                        noncesiv = Nonces.IV,
                        salt = Nonces.Salt,
                        postbackUrl = Shared.PostbackUrl,
                        preAuth = Shared.PreAuth,
                        amount = orderTotal
                    };

                    string jsonReqVault = JsonConvert.SerializeObject(request);
                    string AuthKey = Shared.GetAuthKey(jsonReqVault, Shared.DeveloperKEY, Nonces.IV, Nonces.Salt);


                    return Json(new authKeyData()
                    {
                        authKey = AuthKey,
                        requestID = orderRequestId,
                        salt = Nonces.Salt
                    });

                }


                return Json(new { success = 1 });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        public string Vault_GetRequestID() {
            //================================================
            //== Returns RequestID to process a CC Payment.
            //================================================

            DateTime objDate = DateTime.Now;

            string orderRequestId = _workContext.CurrentCustomer.DivisionNo + _workContext.CurrentCustomer.AccountNo +
                +objDate.Month + objDate.Day + objDate.Year + objDate.Hour + objDate.Minute + objDate.Second;

            return orderRequestId;
        }

        [ValidateInput(false)]
        public ActionResult Verify() {
            return View();

        }


        [HttpPost]
        public JsonResult Hash_Verify(string request) {
            //================================================
            //== Returns hash of server so it can be compared
            //== to client hash.   This is needed to validate
            //== the integrity of the response.
            //================================================

            var results = new
            {
                hash = Shared.GetHmac(request, Shared.DeveloperKEY),
            };

            //string serverHash = JsonConvert.SerializeObject(results);

            return Json(results);
        }

        [HttpPost]
        public JsonResult OrderProcessingFinal(string poNumber, string CCLast4, string referenceCode) {
            //================================================
            //== Saves order and clears the Cart.
            //== Returns OrderID.
            //================================================


            try
            {
                // Place Order
                // Save transaction info for Credit Card transaction
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer, "PONumber", poNumber, 1);
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer, "CCLast4", CCLast4, 1);
                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer, "ReferenceCode", referenceCode, 1);

                var customer = _workContext.CurrentCustomer;
                var placeOrderResult = _orderProcessingService.PlaceOrder();
                
                
                IList<ShoppingCartItem> cart = null;

                cart = customer.ShoppingCartItems
                  .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                .Where(sci => sci.StoreId == 1)
                .ToList();


                if (cart.Count == 0)
                    throw new NopException("Cart is empty");

                //Send email
                Email_Confirmation(placeOrderResult.PlacedOrder, cart);

                // Clear shopping cart
                cart.ToList().ForEach(sci => _shoppingCartService.DeleteShoppingCartItem(sci, false));

                _genericAttributeService.SaveAttribute<string>(_workContext.CurrentCustomer, "OrderID", placeOrderResult.PlacedOrder.Id.ToString(), 1);


               
                return Json(placeOrderResult.PlacedOrder.Id);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });

            }
        }


        public void Email_Confirmation(Order order, IList<ShoppingCartItem> cart) {
        //================================================
        //== Sends confirmation email to order.
        //================================================       
            try 
            {
                string emailAddress = _workContext.CurrentCustomer.Email;
                MailMessage mail = new MailMessage("donotreplay@masterspas.com", emailAddress);
                SmtpClient client = new SmtpClient();
                client.Port = 25;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;

                var emailCreditials = new NetworkCredential("donotreply", "orelse");
                client.Credentials = emailCreditials;

                client.Host = "192.168.5.22";

                //string OrderID = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.OrderID, _storeContext.CurrentStore.Id);

                mail.Subject = "Parts Order Confirmation - Order No: " + order.Id.ToString();

                mail.IsBodyHtml = true;

                string DealerName = _workContext.CurrentCustomer.DealerName;
                
                DateTime OrderDate = DateTime.Now;
                
                // Email Header
                string BillAddress_Line1 = _workContext.CurrentCustomer.BillingAddress.Address1;
                string BillAddress_CSZ = _workContext.CurrentCustomer.BillingAddress.City + ", " +
                        _workContext.CurrentCustomer.BillingAddress.StateProvince.Abbreviation + " " +
                        _workContext.CurrentCustomer.BillingAddress.ZipPostalCode;
                
                string ShippingAddress_Name = _workContext.CurrentCustomer.ShippingAddress.FirstName + " " + _workContext.CurrentCustomer.ShippingAddress.LastName;
                string ShippingAddress_Line1 = _workContext.CurrentCustomer.ShippingAddress.Address1;
                string ShippingAddress_CSZ = _workContext.CurrentCustomer.ShippingAddress.City + ", " +
                        _workContext.CurrentCustomer.ShippingAddress.StateProvince.Abbreviation + " " + 
                        _workContext.CurrentCustomer.ShippingAddress.ZipPostalCode;

                string PONumber = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.PONumber, _storeContext.CurrentStore.Id);
                var ShippingMethod = _workContext.CurrentCustomer.GetAttribute<ShippingOption>(SystemCustomerAttributeNames.SelectedShippingOption, _storeContext.CurrentStore.Id);
                string ShipVia = ShippingMethod.Name.ToString();

                string DesiredShipDate = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.DesiredShipDate, _storeContext.CurrentStore.Id);
                DateTime tmpShipDate = DateTime.Parse(DesiredShipDate);
                DesiredShipDate = tmpShipDate.Date.Month.ToString() + "/" + tmpShipDate.Date.Day.ToString() + tmpShipDate.Date.Year.ToString();



                string PaymentType = _workContext.CurrentCustomer.GetAttribute<string>(SystemCustomerAttributeNames.ReferenceCode, _storeContext.CurrentStore.Id);

                if (PaymentType != "N/A")
                { // Credit Card transaction.
                    PaymentType = "Credit Card ( Authorization Code " + PaymentType + " )";
                }
                else
                { // Terms Transaction
                    PaymentType = _workContext.CurrentCustomer.Terms;
                }


                // Email Header
                string emailBody_Header = string.Format(getEmailTemplate("header"), DealerName, order.Id.ToString(), OrderDate.ToString("D"),
                    DealerName, BillAddress_Line1, BillAddress_CSZ,
                    ShippingAddress_Name, ShippingAddress_Line1, ShippingAddress_CSZ, PONumber, ShipVia, DesiredShipDate, PaymentType);

                // Email Detail
                string emailBody_Detail = "";
                string ProductName = "";
                string ProductDescription = "";
                string Qty = "";
                string UnitPrice = "";
                string Discount = "";
                decimal TotalPrice = 0.00M; ;
                decimal discountTotal = 0.00M;

                string imagePath = Properties.Settings.Default["Parts_ImagePath"].ToString();

                foreach (var sci in cart)
                {
                    ProductName = sci.Product.Sku + " - " + sci.Product.Name;
                    ProductDescription = sci.Product.FullDescription + " ";
                    Qty = sci.Quantity.ToString();
                    UnitPrice = sci.Product.Price.ToString("0.00");

                    if (order.OrderDiscount > 0)
                    {
                        discountTotal = order.OrderDiscount * sci.Quantity;

                        Discount = "<strong>Discount:</strong> $" + discountTotal.ToString("0.00");
                    }
                    else {
                        Discount = " ";
                    }

                    TotalPrice = ((sci.Quantity * sci.Product.Price) - discountTotal);


                    // Part Image
                    string tempImagePath;
                    tempImagePath  = imagePath + sci.Product.Sku + ".jpg";
                    

                    emailBody_Detail = emailBody_Detail + string.Format(getEmailTemplate("detail"), tempImagePath, ProductName, ProductDescription, Qty,
                    Qty, UnitPrice, Discount, TotalPrice.ToString("0.00"));

                    tempImagePath = "";
                }

                // Email Footer
                string subTotal = order.OrderSubtotalExclTax.ToString("0.00");
                string dropShipCharge = order.DropShipCharge.ToString();

                if (dropShipCharge == "0") {
                    dropShipCharge = "0.00";
                }

                string shipping = order.OrderShippingExclTax.ToString("0.00");
                string orderTotal = order.OrderTotal.ToString("0.00");
                
                string emailBody_Footer =  string.Format(getEmailTemplate("footer"), subTotal, dropShipCharge, shipping, orderTotal);

                mail.Body = emailBody_Header + emailBody_Detail + emailBody_Footer;
                client.Send(mail);

            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
            }

        }

        private string getEmailTemplate (string name) {
            //================================================
            //== Reads the selected HTML file for building
            //== the Confirmation Email.
            //================================================    

            string templatePath = "";

            switch (name) {
                case "header":
                    
                    templatePath = Properties.Settings.Default["Path_EmailConfirmatuon_Header"].ToString();
                        //ConfigurationManager.AppSettings["Path_EmailConfirmatuon_Header"].ToString();
                    break;
                case "detail":
                    templatePath = Properties.Settings.Default["Path_EmailConfirmatuon_Detail"].ToString();
                    break;
                case "footer":
                    templatePath = Properties.Settings.Default["Path_EmailConfirmatuon_Footer"].ToString();
                    break;
            }       
            
            using (StreamReader sr = new StreamReader(templatePath))
            {
                //string text = sr.ReadToEnd();
                return sr.ReadToEnd();   //ReadToEnd();
            } 
        }
        
        [HttpPost]
        public JsonResult Test() {

            return Json("4J");
        }
        

        [ValidateInput(false)]
        public ActionResult OpcSavePaymentInfo(FormCollection form)
        {
            try
            {
                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    throw new Exception("Anonymous checkout is not allowed");

                /*  RGT 07.07.2017 - Overriding Payment Method retrievl....We only do Credit Cards.
                    Plus, we changed the Order of One Page Checkout.   Confirmation is now before Credit 
                    Card Payment Panel.*/

                var paymentMethodSystemName = "Payments.Manual";

                string ccNumber = "";

                foreach (var key in form.AllKeys) {
                    if (key.Contains("ccNumber")) {
                        ccNumber = form[key].ToString();
                    }

                }

                string ccL4 = (ccNumber).Substring(12, 4);

                var CreditCards = _dbContext.SqlQuery<int>("EXEC SPROC_PO_CCVault_Update @CustomerID",
                    new SqlParameter("@CustomerID", _workContext.CurrentCustomer.Id),
                    new SqlParameter("@CardNumber", ccL4)).ToString();





                return Json(new { success = 1 });

            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }


        [ValidateInput(false)]
        public ActionResult OpcConfirmOrder()
        {
            
            try
            {
                Session["Checkout"] = "Yes";
                
                //validation
                var cart = _workContext.CurrentCustomer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .Where(sci => sci.StoreId == _storeContext.CurrentStore.Id)
                    .ToList();
                if (cart.Count == 0)
                    throw new Exception("Your cart is empty");

                if (!UseOnePageCheckout())
                    throw new Exception("One page checkout is disabled");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    throw new Exception("Anonymous checkout is not allowed");

                //prevent 2 orders being placed within an X seconds time frame
               // if (!IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
               //     throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));


                var confirmOrderModel = new CheckoutConfirmModel();
                return Json(new
                {
                    update_section = new UpdateSectionJsonModel()
                    {
                        name = "confirm-order",
                        html = this.RenderPartialViewToString("OpcConfirmOrder", confirmOrderModel)
                    },
                    goto_section = "confirm_order"
                });
            }
            


            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Json(new { error = 1, message = exc.Message });
            }
        }

        public ActionResult OpcCompleteRedirectionPayment()
        {
            try
            {
                //validation
                if (!UseOnePageCheckout())
                    return RedirectToRoute("HomePage");

                if ((_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed))
                    return new HttpUnauthorizedResult();

                //get the order
                var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                    .FirstOrDefault();
                if (order == null)
                    return RedirectToRoute("HomePage");

                
                var paymentMethod = _paymentService.LoadPaymentMethodBySystemName(order.PaymentMethodSystemName);
                if (paymentMethod == null)
                    return RedirectToRoute("HomePage");
                if (paymentMethod.PaymentMethodType != PaymentMethodType.Redirection)
                    return RedirectToRoute("HomePage");

                //ensure that order has been just placed
                if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes > 3)
                    return RedirectToRoute("HomePage");


                //Redirection will not work on one page checkout page because it's AJAX request.
                //That's why we process it here
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
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return Content(exc.Message);
            }
        }

        #endregion
    }
}
