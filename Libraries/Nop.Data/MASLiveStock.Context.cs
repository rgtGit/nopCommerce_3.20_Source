﻿//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Nop.Data
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Data.Objects;
    using System.Data.Objects.DataClasses;
    using System.Linq;
    
    public partial class PartsSage100PassThruEntities : DbContext
    {
        public PartsSage100PassThruEntities()
            : base("name=PartsSage100PassThruEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
    
        public virtual int spLiveInventory(string itemCodeStock)
        {
            var itemCodeStockParameter = itemCodeStock != null ?
                new ObjectParameter("ItemCodeStock", itemCodeStock) :
                new ObjectParameter("ItemCodeStock", typeof(string));
    
            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("spLiveInventory", itemCodeStockParameter);
        }
    }
}
