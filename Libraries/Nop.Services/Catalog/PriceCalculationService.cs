using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Data;
using System.Data.SqlClient;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Services.Discounts;


namespace Nop.Services.Catalog
{
    /// <summary>
    /// Price calculation service
    /// </summary>
    public partial class PriceCalculationService : IPriceCalculationService
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IDiscountService _discountService;
        private readonly ICategoryService _categoryService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductService _productService;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly CatalogSettings _catalogSettings;
        private readonly IDbContext _dbContext;

        #endregion

        #region Ctor

        public PriceCalculationService(IWorkContext workContext,
            IStoreContext storeContext,
            IDiscountService discountService, 
            ICategoryService categoryService,
            IProductAttributeParser productAttributeParser, 
            IProductService productService,
            ShoppingCartSettings shoppingCartSettings, 
            CatalogSettings catalogSettings,
            IDbContext dbContext)
        {
            this._workContext = workContext;
            this._storeContext = storeContext;
            this._discountService = discountService;
            this._categoryService = categoryService;
            this._productAttributeParser = productAttributeParser;
            this._productService = productService;
            this._shoppingCartSettings = shoppingCartSettings;
            this._catalogSettings = catalogSettings;
            this._dbContext = dbContext;

        }

        #endregion

        #region Utilities


        /// <summary>
        /// Returns discount amount for the part based on dealer discount settings.
        /// RGT-05.011.2018
        /// </summary>
        /// <param name="productID">Product</param>
        /// <param name="customer">Customer</param>
        /// <returns>Discount Amount</returns>
        public decimal GetShoppingCartItemDiscount(int productID, Customer customer) {
            var parmAcctNo = new SqlParameter("@AccountNo", customer.AccountNo);
         
            var dealerDiscounts = _dbContext.SqlQuery<DealerDiscount>("SPROC_PO_Discounts_ByDealer @AccountNo", parmAcctNo).ToList();

            decimal test;
            test = 0.00M;

            foreach (var dd in dealerDiscounts) {
                test = dd.DiscountPercentage;
                throw new Exception(dd.DiscountType.ToString());
            }
            return test;
        }
        
        /// <summary>
        /// Gets allowed discounts
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">Customer</param>
        /// <returns>Discounts</returns>
        protected virtual IList<Discount> GetAllowedDiscounts(Product product, 
            Customer customer)
            
        {
            
            var allowedDiscounts = new List<Discount>();
            if (_catalogSettings.IgnoreDiscounts)
                return allowedDiscounts;
            #region "RGT-NOT NEEDED"
            //RGT 05.19.2018
            //We don't have product discounts since they are applied at the dealer level...
            /*if (product.HasDiscountsApplied)
            {
                //we use this property ("HasDiscountsApplied") for performance optimziation to avoid unnecessary database calls
                foreach (var discount in product.AppliedDiscounts)
                {
                    //throw new Exception("RR-Product Discount");
                    if (_discountService.IsDiscountValid(discount, customer) &&
                        discount.DealerAccount == customer.AccountNo &&
                        discount.DiscountType == DiscountType.AssignedToSkus &&
                        !allowedDiscounts.ContainsDiscount(discount))
                        allowedDiscounts.Add(discount);
                }
            }
            */
            #endregion

            //performance optimization
            //load all category discounts just to ensure that we have at least one
            if (_discountService.GetAllDiscounts(DiscountType.AssignedToCategories).Any())
            {
                var productCategories = _categoryService.GetProductCategoriesByProductId(product.Id);
                if (productCategories != null)
                {
                    foreach (var productCategory in productCategories)
                    {
                        var category = productCategory.Category;
                        
                        if (category.HasDiscountsApplied)
                        {
                            //we use this property ("HasDiscountsApplied") for performance optimziation to avoid unnecessary database calls
                            var categoryDiscounts = category.AppliedDiscounts;
                            //throw new Exception("RR-Category Discount" + categoryDiscounts.Count.ToString());
                            foreach (var discount in categoryDiscounts)
                            {
                                //throw new Exception("RR-Category Discount" + discount.DealerAccount.ToString());
                                if (_discountService.IsDiscountValid(discount, customer, product) &&
                                    discount.DealerAccount == customer.AccountNo &&
                                    discount.DiscountType == DiscountType.AssignedToCategories &&
                                    !allowedDiscounts.ContainsDiscount(discount))
                                    allowedDiscounts.Add(discount);
                            }
                        }
                    }
                }
            }

            //Discount Parts Only...
            //RGT 05.19.2018
            var discounts = _discountService.GetAllDiscounts(DiscountType.AssignedToParts);

            if (discounts.Count > 0) {
                    foreach (var discount in discounts)
                    {
                        //throw new Exception("RR-Parts Discount Loop" + categoryDiscounts.Count.ToString());
                        if (_discountService.IsDiscountValid(discount, customer, product ) && 
                            discount.DealerAccount == customer.AccountNo &&
                            discount.DiscountType == DiscountType.AssignedToParts &&
                            !allowedDiscounts.ContainsDiscount(discount))
                            allowedDiscounts.Add(discount);
                    }
                
            }

            return allowedDiscounts;
        }

        /// <summary>
        /// Gets a preferred discount
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">Customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <param name="quantity">Product quantity</param>
        /// <returns>Preferred discount</returns>
        protected virtual Discount GetPreferredDiscount(Product product,
            Customer customer, decimal additionalCharge = decimal.Zero, int quantity = 1)
        {
            if (_catalogSettings.IgnoreDiscounts)
                return null;

            var allowedDiscounts = GetAllowedDiscounts(product, customer);
            //throw new Exception("I LONG TO BE WITH YOU, MY LOVE!!!" + allowedDiscounts.Count.ToString());
            
            decimal finalPriceWithoutDiscount = GetFinalPrice(product, customer, additionalCharge, false, out decimal itemDiscount, quantity);
            var preferredDiscount = allowedDiscounts.GetPreferredDiscount(finalPriceWithoutDiscount);
            return preferredDiscount;
        }

        /// <summary>
        /// Gets a tier price
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">Customer</param>
        /// <param name="quantity">Quantity</param>
        /// <returns>Price</returns>
        protected virtual decimal? GetMinimumTierPrice(Product product, Customer customer, int quantity)
        {
            if (!product.HasTierPrices)
                return decimal.Zero;

            var tierPrices = product.TierPrices
                .OrderBy(tp => tp.Quantity)
                .ToList()
                .FilterByStore(_storeContext.CurrentStore.Id)
                .FilterForCustomer(customer)
                .RemoveDuplicatedQuantities();

            int previousQty = 1;
            decimal? previousPrice = null;
            foreach (var tierPrice in tierPrices)
            {
                //check quantity
                if (quantity < tierPrice.Quantity)
                    continue;
                if (tierPrice.Quantity < previousQty)
                    continue;

                //save new price
                previousPrice = tierPrice.Price;
                previousQty = tierPrice.Quantity;
            }
            
            return previousPrice;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get variant special price (is valid)
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Special price</returns>
        public virtual decimal? GetSpecialPrice(Product product)
        {
            if (product == null)
                throw new ArgumentNullException("product");

            if (!product.SpecialPrice.HasValue)
                return null;

            //check date range
            DateTime now = DateTime.UtcNow;
            if (product.SpecialPriceStartDateTimeUtc.HasValue)
            {
                DateTime startDate = DateTime.SpecifyKind(product.SpecialPriceStartDateTimeUtc.Value, DateTimeKind.Utc);
                if (startDate.CompareTo(now) > 0)
                    return null;
            }
            if (product.SpecialPriceEndDateTimeUtc.HasValue)
            {
                DateTime endDate = DateTime.SpecifyKind(product.SpecialPriceEndDateTimeUtc.Value, DateTimeKind.Utc);
                if (endDate.CompareTo(now) < 0)
                    return null;
            }

            return product.SpecialPrice.Value;
        }

        /// <summary>
        /// Gets the final price
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for final price computation</param>
        /// <returns>Final price</returns>
        public virtual decimal GetFinalPrice(Product product, 
            bool includeDiscounts)
        {
            var customer = _workContext.CurrentCustomer;
            return GetFinalPrice(product, customer, includeDiscounts);
        }

        /// <summary>
        /// Gets the final price
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for final price computation</param>
        /// <returns>Final price</returns>
        public virtual decimal GetFinalPrice(Product product, 
            Customer customer, 
            bool includeDiscounts)
        {
            return GetFinalPrice(product, customer, decimal.Zero, includeDiscounts);
        }

        /// <summary>
        /// Gets the final price
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for final price computation</param>
        /// <returns>Final price</returns>
        public virtual decimal GetFinalPrice(Product product, 
            Customer customer, 
            decimal additionalCharge, 
            bool includeDiscounts)
        {
            return GetFinalPrice(product, customer, additionalCharge, 
                includeDiscounts, out decimal itemDiscount, 1);
        }

        /// <summary>
        /// Gets the final price
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for final price computation</param>
        /// <param name="quantity">Shopping cart item quantity</param>
        /// <returns>Final price</returns>
        public virtual decimal GetFinalPrice(Product product, 
            Customer customer,
            decimal additionalCharge, 
            bool includeDiscounts, 
            out decimal itemDiscount,
            int quantity)
        {
            if (product == null)
                throw new ArgumentNullException("product");

            //initial price
            decimal result = product.Price;

            //special price
            //var specialPrice = GetSpecialPrice(product);
            //if (specialPrice.HasValue)
            //    result = specialPrice.Value;

            //tier prices
            if (product.HasTierPrices)
            {
                decimal? tierPrice = GetMinimumTierPrice(product, customer, quantity);
                if (tierPrice.HasValue)
                    result = Math.Min(result, tierPrice.Value);
            }

            //discount + additional charge
            if (includeDiscounts)
            {
                Discount appliedDiscount = null;
                decimal priceDiscount = GetDiscountAmount(product, customer, additionalCharge, quantity, out appliedDiscount);
                //throw new Exception("discount+addl" + priceDiscount.ToString());
                // result = result + additionalCharge - discountAmount;
                itemDiscount = priceDiscount;
                
                result = product.Price - priceDiscount;
            }
            else
            {
                itemDiscount = 0.00M;

                result = result + additionalCharge;
            }
            if (result < decimal.Zero)
                result = decimal.Zero;

            return result;
        }



        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(Product product)
        {
            var customer = _workContext.CurrentCustomer;
            return GetDiscountAmount(product, customer, decimal.Zero);
        }

        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(Product product, 
            Customer customer)
        {
            return GetDiscountAmount(product, customer, decimal.Zero);
        }

        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(Product product, 
            Customer customer, 
            decimal additionalCharge)
        {
            Discount appliedDiscount = null;
            return GetDiscountAmount(product, customer, additionalCharge, out appliedDiscount);
        }

        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <param name="appliedDiscount">Applied discount</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(Product product, 
            Customer customer,
            decimal additionalCharge, 
            out Discount appliedDiscount)
        {
            return GetDiscountAmount(product, customer, additionalCharge, 1, out appliedDiscount);
        }

        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="customer">The customer</param>
        /// <param name="additionalCharge">Additional charge</param>
        /// <param name="quantity">Product quantity</param>
        /// <param name="appliedDiscount">Applied discount</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(Product product,
            Customer customer,
            decimal additionalCharge,
            int quantity,
            out Discount appliedDiscount)
        {
            if (product == null)
                throw new ArgumentNullException("product");

            appliedDiscount = null;
            decimal appliedDiscountAmount = decimal.Zero;

            //we don't apply discounts to products with price entered by a customer
            if (product.CustomerEntersPrice)
                return appliedDiscountAmount;

            appliedDiscount = GetPreferredDiscount(product, customer, additionalCharge, quantity);
            if (appliedDiscount != null)
            {
                decimal finalPriceWithoutDiscount = GetFinalPrice(product, customer, additionalCharge, false, out decimal itemDiscount, quantity);
                appliedDiscountAmount = appliedDiscount.GetDiscountAmount(finalPriceWithoutDiscount);
            }
            //throw new Exception("GetDiscountAmout " + appliedDiscountAmount.ToString());
            return appliedDiscountAmount;
        }


        /// <summary>
        /// Gets the shopping cart item sub total
        /// </summary>
        /// <param name="shoppingCartItem">The shopping cart item</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for price computation</param>
        /// <returns>Shopping cart item sub total</returns>
        public virtual decimal GetSubTotal(ShoppingCartItem shoppingCartItem, bool includeDiscounts)
        {
            if (shoppingCartItem == null)
                throw new ArgumentNullException("shoppingCartItem");

            //throw new Exception("RR2INFINITY-GetSubTotal" + GetUnitPrice(shoppingCartItem, true).ToString());
            return GetUnitPrice(shoppingCartItem, includeDiscounts) * shoppingCartItem.Quantity;
        }

        /// <summary>
        /// Gets the shopping cart unit price (one item)
        /// </summary>
        /// <param name="shoppingCartItem">The shopping cart item</param>
        /// <param name="includeDiscounts">A value indicating whether include discounts or not for price computation</param>
        /// <returns>Shopping cart unit price (one item)</returns>
        public virtual decimal GetUnitPrice(ShoppingCartItem shoppingCartItem, bool includeDiscounts)
        {
            if (shoppingCartItem == null)
                throw new ArgumentNullException("shoppingCartItem");

            var customer = shoppingCartItem.Customer;
           
            decimal finalPrice = decimal.Zero;
            var product = shoppingCartItem.Product;

            if (product != null)
            {

                var combination = _productAttributeParser.FindProductVariantAttributeCombination(product, shoppingCartItem.AttributesXml);
                if (combination != null && combination.OverriddenPrice.HasValue)
                {
                    finalPrice = combination.OverriddenPrice.Value;
                }
                else
                {
                    //summarize price of all attributes
                    decimal attributesTotalPrice = decimal.Zero;
                    var pvaValues = _productAttributeParser.ParseProductVariantAttributeValues(shoppingCartItem.AttributesXml);
                    if (pvaValues != null)
                    {
                        foreach (var pvaValue in pvaValues)
                        {
                            attributesTotalPrice += GetProductVariantAttributeValuePriceAdjustment(pvaValue);
                        }
                    }

                    //get price of a product (with previously calculated price of all attributes)
                    if (product.CustomerEntersPrice)
                    {
                        finalPrice = shoppingCartItem.CustomerEnteredPrice;
                    }
                    else
                    {
                        var qty = 0;
                        if (_shoppingCartSettings.GroupTierPricesForDistinctShoppingCartItems)
                        {
                            //the same products with distinct product attributes could be stored as distinct "ShoppingCartItem" records
                            //so let's find how many of the current products are in the cart
                            qty = customer.ShoppingCartItems
                                .Where(x => x.ProductId == shoppingCartItem.ProductId)
                                .Where(x => x.ShoppingCartTypeId == shoppingCartItem.ShoppingCartTypeId)
                                .Sum(x => x.Quantity);
                            if (qty == 0)
                            {
                                qty = shoppingCartItem.Quantity;
                            }
                        }
                        else
                        {
                            qty = shoppingCartItem.Quantity;
                        }
                        finalPrice = GetFinalPrice(product,
                            customer,
                            attributesTotalPrice,
                            includeDiscounts,
                            out decimal itemDiscount,
                            qty);
                    }
                }
            }

            
            //finalPrice = product.Price;


            //rounding

            


            if (_shoppingCartSettings.RoundPricesDuringCalculation)
                finalPrice = Math.Round(finalPrice, 2);

            //throw new Exception("After GetFinalPrice " + finalPrice.ToString());
            return finalPrice;
        }

        /// <summary>
        /// Gets the product cost (one item)
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="attributesXml">Shopping cart item attributes in XML</param>
        /// <returns>Product cost (one item)</returns>
        public virtual decimal GetProductCost(Product product, string attributesXml)
        {
            if (product == null)
                throw new ArgumentNullException("product");

            decimal cost = product.ProductCost;
            var pvaValues = _productAttributeParser.ParseProductVariantAttributeValues(attributesXml);
            foreach (var pvaValue in pvaValues)
            {
                switch (pvaValue.AttributeValueType)
                {
                    case AttributeValueType.Simple:
                        {
                            //simple attribute
                            cost += pvaValue.Cost;
                        }
                        break;
                    case AttributeValueType.AssociatedToProduct:
                        {
                            //bundled product
                            var associatedProduct = _productService.GetProductById(pvaValue.AssociatedProductId);
                            if (associatedProduct != null)
                                cost += associatedProduct.ProductCost * pvaValue.Quantity;
                        }
                        break;
                    default:
                        break;
                }
            }

            return cost;
        }



        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="shoppingCartItem">The shopping cart item</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(ShoppingCartItem shoppingCartItem)
        {
            Discount appliedDiscount;
            return GetDiscountAmount(shoppingCartItem, out appliedDiscount);
        }

        /// <summary>
        /// Gets discount amount
        /// </summary>
        /// <param name="shoppingCartItem">The shopping cart item</param>
        /// <param name="appliedDiscount">Applied discount</param>
        /// <returns>Discount amount</returns>
        public virtual decimal GetDiscountAmount(ShoppingCartItem shoppingCartItem, out Discount appliedDiscount)
        {
            if (shoppingCartItem == null)
                throw new ArgumentNullException("shoppingCartItem");

            var customer = shoppingCartItem.Customer;
            appliedDiscount = null;
            decimal totalDiscountAmount = decimal.Zero;
            var product = shoppingCartItem.Product;
            if (product != null)
            {
                decimal attributesTotalPrice = decimal.Zero;

                var pvaValues = _productAttributeParser.ParseProductVariantAttributeValues(shoppingCartItem.AttributesXml);
                foreach (var pvaValue in pvaValues)
                {
                    attributesTotalPrice += GetProductVariantAttributeValuePriceAdjustment(pvaValue);
                }

                decimal productDiscountAmount = GetDiscountAmount(product, customer, attributesTotalPrice, shoppingCartItem.Quantity, out appliedDiscount);
                totalDiscountAmount = productDiscountAmount * shoppingCartItem.Quantity;
            }
            
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
                totalDiscountAmount = Math.Round(totalDiscountAmount, 2);
            return totalDiscountAmount;
        }





        /// <summary>
        /// Get a price adjustment of a product variant attribute value
        /// </summary>
        /// <param name="pvav">Product variant attribute value</param>
        /// <returns>price adjustment</returns>
        public virtual decimal GetProductVariantAttributeValuePriceAdjustment(ProductVariantAttributeValue pvav)
        {
            if (pvav == null)
                throw new ArgumentNullException("pvav");

            var adjustment = decimal.Zero;
            switch (pvav.AttributeValueType)
            {
                case AttributeValueType.Simple:
                    {
                        //simple attribute
                        adjustment = pvav.PriceAdjustment;
                    }
                    break;
                case AttributeValueType.AssociatedToProduct:
                    {
                        //bundled product
                        var associatedProduct = _productService.GetProductById(pvav.AssociatedProductId);
                        if (associatedProduct != null)
                        {
                            adjustment = GetFinalPrice(associatedProduct, true) * pvav.Quantity;
                        }
                    }
                    break;
                default:
                    break;
            }

            return adjustment;
        }

        #endregion
    }
}
