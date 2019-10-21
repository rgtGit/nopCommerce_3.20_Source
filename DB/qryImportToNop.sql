use PartsSage100PassThru

-- Customers
select distinct ARDivisionNo, CustomerNo, NOPUpdateDateTime from SageUpdatedCustomerRecords 
where NOPUpdateDateTime is null and ( ARDivisionNo = '90' or ARDivisionNo = '91')
order by ARDivisionNo, CustomerNo asc 

update SageUpdatedCustomerRecords
  set NOPUpdateDateTime = null
  
  
-- Shipping Addresses
select * from SageUpdatedShipToAddressRecords


