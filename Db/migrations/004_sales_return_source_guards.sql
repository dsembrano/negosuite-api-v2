-- Apply after 002. Keep financial source values stable while posted returns exist.
DELIMITER $$
DROP TRIGGER IF EXISTS protect_srt_salesinvoice$$
CREATE TRIGGER protect_srt_salesinvoice BEFORE UPDATE ON salesinvoice FOR EACH ROW
BEGIN
 IF (NOT (NEW.IsTaxExclusive <=> OLD.IsTaxExclusive) OR NOT (NEW.DiscountAmount <=> OLD.DiscountAmount) OR NOT (NEW.CustomerId <=> OLD.CustomerId) OR NOT (NEW.InvoiceDate <=> OLD.InvoiceDate) OR NOT (NEW.Amount <=> OLD.Amount) OR NOT (NEW.Status <=> OLD.Status) OR NOT (NEW.InventoryLocationId <=> OLD.InventoryLocationId) OR NOT (NEW.Taxes <=> OLD.Taxes) OR NOT (NEW.ResponsibilityCenterEntry <=> OLD.ResponsibilityCenterEntry)) AND EXISTS(SELECT 1 FROM salesreturn WHERE SalesInvoiceId=OLD.Id AND Status=1) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Returns protect the original invoice financial values'; END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_salesreceipt$$
CREATE TRIGGER protect_srt_salesreceipt BEFORE UPDATE ON salesreceipt FOR EACH ROW
BEGIN
 IF (NOT (NEW.IsTaxExclusive <=> OLD.IsTaxExclusive) OR NOT (NEW.DiscountAmount <=> OLD.DiscountAmount) OR NOT (NEW.CustomerId <=> OLD.CustomerId) OR NOT (NEW.ReceiptDate <=> OLD.ReceiptDate) OR NOT (NEW.Amount <=> OLD.Amount) OR NOT (NEW.Status <=> OLD.Status) OR NOT (NEW.InventoryLocationId <=> OLD.InventoryLocationId) OR NOT (NEW.Taxes <=> OLD.Taxes) OR NOT (NEW.ResponsibilityCenterEntry <=> OLD.ResponsibilityCenterEntry)) AND EXISTS(SELECT 1 FROM salesreturn WHERE SalesReceiptId=OLD.Id AND Status=1) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Returns protect the original invoice financial values'; END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_salesinvoicedetail$$
CREATE TRIGGER protect_srt_salesinvoicedetail BEFORE UPDATE ON salesinvoicedetail FOR EACH ROW
BEGIN
 IF (NOT (NEW.SalesInvoiceId <=> OLD.SalesInvoiceId) OR NOT (NEW.ItemId <=> OLD.ItemId) OR NOT (NEW.Quantity <=> OLD.Quantity) OR NOT (NEW.Amount <=> OLD.Amount) OR NOT (NEW.Rate <=> OLD.Rate) OR NOT (NEW.Cost <=> OLD.Cost) OR NOT (NEW.DiscountAmount <=> OLD.DiscountAmount) OR NOT (NEW.TaxAmount <=> OLD.TaxAmount) OR NOT (NEW.TaxRateId <=> OLD.TaxRateId) OR NOT (NEW.TaxExemptAmount <=> OLD.TaxExemptAmount) OR NOT (NEW.Status <=> OLD.Status) OR NOT (NEW.IsInventoryTransaction <=> OLD.IsInventoryTransaction) OR NOT (NEW.InventoryLocationId <=> OLD.InventoryLocationId)) AND EXISTS(SELECT 1 FROM salesreturn WHERE SalesInvoiceId=OLD.SalesInvoiceId AND Status=1) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Returns protect the original invoice financial values'; END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_salesreceiptdetail$$
CREATE TRIGGER protect_srt_salesreceiptdetail BEFORE UPDATE ON salesreceiptdetail FOR EACH ROW
BEGIN
 IF (NOT (NEW.SalesReceiptId <=> OLD.SalesReceiptId) OR NOT (NEW.ItemId <=> OLD.ItemId) OR NOT (NEW.Quantity <=> OLD.Quantity) OR NOT (NEW.Amount <=> OLD.Amount) OR NOT (NEW.Rate <=> OLD.Rate) OR NOT (NEW.Cost <=> OLD.Cost) OR NOT (NEW.DiscountAmount <=> OLD.DiscountAmount) OR NOT (NEW.TaxAmount <=> OLD.TaxAmount) OR NOT (NEW.TaxRateId <=> OLD.TaxRateId) OR NOT (NEW.TaxExemptAmount <=> OLD.TaxExemptAmount) OR NOT (NEW.Status <=> OLD.Status) OR NOT (NEW.IsInventoryTransaction <=> OLD.IsInventoryTransaction) OR NOT (NEW.InventoryLocationId <=> OLD.InventoryLocationId)) AND EXISTS(SELECT 1 FROM salesreturn WHERE SalesReceiptId=OLD.SalesReceiptId AND Status=1) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Returns protect the original invoice financial values'; END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_journalentry$$
CREATE TRIGGER protect_srt_journalentry BEFORE UPDATE ON journalentry FOR EACH ROW
BEGIN
 IF (NOT (NEW.AccountId <=> OLD.AccountId) OR NOT (NEW.Nature <=> OLD.Nature) OR NOT (NEW.Amount <=> OLD.Amount) OR NOT (NEW.Status <=> OLD.Status) OR NOT (NEW.CustomerId <=> OLD.CustomerId) OR NOT (NEW.SupplierId <=> OLD.SupplierId) OR NOT (NEW.ResponsibilityCenterEntry <=> OLD.ResponsibilityCenterEntry)) AND EXISTS(SELECT 1 FROM salesreturn WHERE Status=1 AND (SalesInvoiceId=OLD.SalesInvoiceId OR SalesReceiptId=OLD.SalesReceiptId)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Returns protect the original invoice journals'; END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_journalentry_delete$$
CREATE TRIGGER protect_srt_journalentry_delete BEFORE DELETE ON journalentry FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesreturn WHERE Status=1 AND (SalesInvoiceId=OLD.SalesInvoiceId OR SalesReceiptId=OLD.SalesReceiptId OR Id=OLD.SalesReturnId)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Posted Sales Return journals cannot be deleted'; END IF;
END$$
DELIMITER ;
