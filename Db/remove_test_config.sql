use negosuitedb;

set @userId = 27;
set @configId = 13;

update user set configId = null, userRoleId = null where Id = @userId;
update config set 
ARTradeAccountId = null, APTradeAccountId = null , DiscountAccountId = null, PurchaseDiscountAccountId = null
where Id = @configId;
update account set ParentAccountId = null where UserConfigId = @configId;
delete from generaljournal where UserConfigId = @configId;
delete from responsibilitycentertype where UserConfigId = @configId;
delete from journalentry where UserConfigId = @onfigId;
delete from customer where UserConfigId = @configId;
delete from supplier where UserConfigId = @configId;
delete from inventorylocation where UserConfigId = @configId;
delete from item where UserConfigId = @configId;
delete from account where UserConfigId = @configId;
delete from accountcategory where UserConfigId = @configId;
delete from taxrate where UserConfigId = @configId;
delete FROM config where id = @configId;
