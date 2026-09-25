use negosuitedb;

set @userId = 27;
set @configId = 13;

delete from journalentry where UserConfigId = @configId and  id > 0 and journalentry.PaymentToJournalEntryId > 0 ;
delete from journalentry where UserConfigId = @configId and  id > 0 and journalentry.Id > 0 ;

delete from billdetail where UserConfigId = @configId and   id > 0;
delete from bill where UserConfigId = @configId and   id > 0;
delete from expensepayment where UserConfigId = @configId and   id > 0;
delete from billpayment where UserConfigId = @configId and   id > 0;
delete from payment where UserConfigId = @configId and   id > 0;
delete from salesinvoicedetail where UserConfigId = @configId and   id > 0;
delete from salesinvoice where UserConfigId = @configId and   id > 0;
delete from salesinvoicepayment where UserConfigId = @configId and   id > 0;
delete from salesinvoice where UserConfigId = @configId and   id > 0;
delete from salesreceiptdetail where UserConfigId = @configId and   id > 0;
delete from salesreceipt where UserConfigId = @configId and   id > 0;
delete from generaljournal where UserConfigId = @configId and   id > 0;
delete from inventoryadjustmentdetail where UserConfigId = @configId and   id >  0;
delete from inventoryadjustment where UserConfigId = @configId and   id >  0;
delete from stocktransferdetail where UserConfigId = @configId and   id >  0;
delete from stocktransfer where UserConfigId = @configId and   id >  0;
delete from item where UserConfigId = @configId and   id > 0;

delete from responsibilitycenter where id > 0;
delete from responsibilitycentertype where id > 0;

delete from customeraddress where id > 0;
delete from customercontact where id > 0;
delete from supplieraddress where id > 0;
delete from suppliercontact where id > 0;
delete from customer where id > 0;
delete from supplier where id > 0;
delete from taxrate where id > 0;

update config set config.ARTradeAccountId = null where id > 0;
update config set config.APTradeAccountId = null where id > 0;
update config set config.DiscountAccountId = null where id > 0;

delete from accounttotal where id > 0;
delete from account where id > 0 and issubaccount = 1;
delete from account where id > 0 and issubaccount = 0;

commit;




/* //////////////////////////////////////////////////// */
/* Clean after test create user account */
set @maxconfigId = 2;

update user set configId = null, userRoleId = null where Id = 34;
update config set 
ARTradeAccountId = null, APTradeAccountId = null , DiscountAccountId = null, PurchaseDiscountAccountId = null
where Id > @maxconfigId;
update account set ParentAccountId = null where UserConfigId > @maxconfigId;
delete from generaljournal where UserConfigId > @maxconfigId;
delete from journalentry where UserConfigId > @maxconfigId;
delete from account where UserConfigId > @maxconfigId;
delete from accountcategory where UserConfigId > @maxconfigId;
delete from taxrate where UserConfigId > @maxconfigId;
delete FROM config where id > @maxconfigId;
