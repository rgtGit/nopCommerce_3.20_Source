declare 
  @pid as int,
  @cid3 as int,
  @cid2 as int,
  @cid1 as int

select @pid = id from Product where Sku = N'X203112'
select @cid3 = CategoryId from Product_Category_Mapping where ProductId = @pid
select @cid2 = ParentCategoryId from Category where Id = @cid3
select @cid1 = ParentCategoryId from Category where id = @cid2

select * from Category where Id = @cid1 or id = @cid2 or id = @cid3
order by ParentCategoryId

