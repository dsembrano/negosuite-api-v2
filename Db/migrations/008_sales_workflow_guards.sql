-- Protect delivery-linked invoices against writes from legacy applications.
-- Apply after 006/007. Dedicated trigger names only; existing return guards remain installed.
DELIMITER $$
DROP TRIGGER IF EXISTS sw_SI_header_update$$
CREATE TRIGGER sw_SI_header_update BEFORE UPDATE ON salesinvoice FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId) AND (NOT(OLD.UserConfigId<=>NEW.UserConfigId) OR NOT(OLD.CustomerId<=>NEW.CustomerId) OR NOT(OLD.InvoiceNo<=>NEW.InvoiceNo) OR NOT(OLD.InvoiceDate<=>NEW.InvoiceDate) OR NOT(OLD.Amount<=>NEW.Amount) OR NOT(OLD.DiscountAmount<=>NEW.DiscountAmount) OR NOT(OLD.DiscountPercent<=>NEW.DiscountPercent) OR NOT(OLD.Taxes<=>NEW.Taxes) OR NOT(OLD.IsTaxExclusive<=>NEW.IsTaxExclusive) OR NOT(OLD.InventoryLocationId<=>NEW.InventoryLocationId) OR NOT(OLD.ResponsibilityCenterEntry<=>NEW.ResponsibilityCenterEntry) OR NOT(OLD.SupplierId<=>NEW.SupplierId) OR NOT(OLD.DueDate<=>NEW.DueDate) OR NOT(OLD.PaymentTermId<=>NEW.PaymentTermId) OR (NOT(OLD.Status<=>NEW.Status) AND NOT(NEW.Status=-1 AND EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId AND w.Status=-1)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice financial values are immutable'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SI_header_delete$$
CREATE TRIGGER sw_SI_header_delete BEFORE DELETE ON salesinvoice FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId) OR (OLD.Status<>0 AND EXISTS(SELECT 1 FROM salesconfiguration c WHERE c.UserConfigId=OLD.UserConfigId)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cancel posted invoices; retain their audit records'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SI_line_update$$
CREATE TRIGGER sw_SI_line_update BEFORE UPDATE ON salesinvoicedetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.SalesInvoiceId) AND (NOT(OLD.SalesInvoiceId<=>NEW.SalesInvoiceId) OR NOT(OLD.ItemId<=>NEW.ItemId) OR NOT(OLD.Quantity<=>NEW.Quantity) OR NOT(OLD.Cost<=>NEW.Cost) OR NOT(OLD.Rate<=>NEW.Rate) OR NOT(OLD.Amount<=>NEW.Amount) OR NOT(OLD.DiscountAmount<=>NEW.DiscountAmount) OR NOT(OLD.DiscountPercent<=>NEW.DiscountPercent) OR NOT(OLD.TaxRateId<=>NEW.TaxRateId) OR NOT(OLD.TaxAmount<=>NEW.TaxAmount) OR NOT(OLD.TaxExemptAmount<=>NEW.TaxExemptAmount) OR NOT(OLD.IsInventoryTransaction<=>NEW.IsInventoryTransaction) OR NOT(OLD.InventoryLocationId<=>NEW.InventoryLocationId) OR (NOT(OLD.Status<=>NEW.Status) AND NOT(NEW.Status=-1 AND EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.SalesInvoiceId AND w.Status=-1)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice lines are immutable'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SI_line_delete$$
CREATE TRIGGER sw_SI_line_delete BEFORE DELETE ON salesinvoicedetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=OLD.SalesInvoiceId) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice lines cannot be deleted'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SI_line_insert$$
CREATE TRIGGER sw_SI_line_insert BEFORE INSERT ON salesinvoicedetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SI' AND w.InvoiceId=NEW.SalesInvoiceId) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot append lines to a delivery-based invoice'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SR_header_update$$
CREATE TRIGGER sw_SR_header_update BEFORE UPDATE ON salesreceipt FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId) AND (NOT(OLD.UserConfigId<=>NEW.UserConfigId) OR NOT(OLD.CustomerId<=>NEW.CustomerId) OR NOT(OLD.ReceiptNo<=>NEW.ReceiptNo) OR NOT(OLD.ReceiptDate<=>NEW.ReceiptDate) OR NOT(OLD.Amount<=>NEW.Amount) OR NOT(OLD.DiscountAmount<=>NEW.DiscountAmount) OR NOT(OLD.DiscountPercent<=>NEW.DiscountPercent) OR NOT(OLD.Taxes<=>NEW.Taxes) OR NOT(OLD.IsTaxExclusive<=>NEW.IsTaxExclusive) OR NOT(OLD.InventoryLocationId<=>NEW.InventoryLocationId) OR NOT(OLD.ResponsibilityCenterEntry<=>NEW.ResponsibilityCenterEntry) OR NOT(OLD.PaymentModeId<=>NEW.PaymentModeId) OR NOT(OLD.DepositToAccountId<=>NEW.DepositToAccountId) OR NOT(OLD.PaymentDetails<=>NEW.PaymentDetails) OR NOT(OLD.IsPOS<=>NEW.IsPOS) OR (NOT(OLD.Status<=>NEW.Status) AND NOT(NEW.Status=-1 AND EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId AND w.Status=-1)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice financial values are immutable'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SR_header_delete$$
CREATE TRIGGER sw_SR_header_delete BEFORE DELETE ON salesreceipt FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.Id AND w.UserConfigId=OLD.UserConfigId) OR (OLD.Status<>0 AND EXISTS(SELECT 1 FROM salesconfiguration c WHERE c.UserConfigId=OLD.UserConfigId)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cancel posted invoices; retain their audit records'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SR_line_update$$
CREATE TRIGGER sw_SR_line_update BEFORE UPDATE ON salesreceiptdetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.SalesReceiptId) AND (NOT(OLD.SalesReceiptId<=>NEW.SalesReceiptId) OR NOT(OLD.ItemId<=>NEW.ItemId) OR NOT(OLD.Quantity<=>NEW.Quantity) OR NOT(OLD.Cost<=>NEW.Cost) OR NOT(OLD.Rate<=>NEW.Rate) OR NOT(OLD.Amount<=>NEW.Amount) OR NOT(OLD.DiscountAmount<=>NEW.DiscountAmount) OR NOT(OLD.DiscountPercent<=>NEW.DiscountPercent) OR NOT(OLD.TaxRateId<=>NEW.TaxRateId) OR NOT(OLD.TaxAmount<=>NEW.TaxAmount) OR NOT(OLD.TaxExemptAmount<=>NEW.TaxExemptAmount) OR NOT(OLD.IsInventoryTransaction<=>NEW.IsInventoryTransaction) OR NOT(OLD.InventoryLocationId<=>NEW.InventoryLocationId) OR (NOT(OLD.Status<=>NEW.Status) AND NOT(NEW.Status=-1 AND EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.SalesReceiptId AND w.Status=-1)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice lines are immutable'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SR_line_delete$$
CREATE TRIGGER sw_SR_line_delete BEFORE DELETE ON salesreceiptdetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=OLD.SalesReceiptId) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Delivery-based invoice lines cannot be deleted'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_SR_line_insert$$
CREATE TRIGGER sw_SR_line_insert BEFORE INSERT ON salesreceiptdetail FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.Kind='SR' AND w.InvoiceId=NEW.SalesReceiptId) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot append lines to a delivery-based invoice'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_journal_update$$
CREATE TRIGGER sw_journal_update BEFORE UPDATE ON journalentry FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salespostingrecord p WHERE p.JournalEntryId=OLD.Id) AND (NOT(OLD.UserConfigId<=>NEW.UserConfigId) OR NOT(OLD.AccountId<=>NEW.AccountId) OR NOT(OLD.Nature<=>NEW.Nature) OR NOT(OLD.Amount<=>NEW.Amount) OR NOT(OLD.CustomerId<=>NEW.CustomerId) OR NOT(OLD.SupplierId<=>NEW.SupplierId) OR NOT(OLD.Source<=>NEW.Source) OR NOT(OLD.ReferenceNo<=>NEW.ReferenceNo) OR NOT(OLD.JournalDate<=>NEW.JournalDate) OR NOT(OLD.SalesInvoiceId<=>NEW.SalesInvoiceId) OR NOT(OLD.SalesReceiptId<=>NEW.SalesReceiptId) OR NOT(OLD.PaymentToJournalEntryId<=>NEW.PaymentToJournalEntryId) OR NOT(OLD.ResponsibilityCenterEntry<=>NEW.ResponsibilityCenterEntry) OR (NOT(OLD.Status<=>NEW.Status) AND NOT(NEW.Status=-1 AND EXISTS(SELECT 1 FROM salespostingrecord p LEFT JOIN salesworkflowdocument d ON p.DocumentType='DR' AND d.Id=p.DocumentId AND d.UserConfigId=p.UserConfigId LEFT JOIN salesworkflowinvoice w ON w.Kind=p.DocumentType AND w.InvoiceId=p.DocumentId AND w.UserConfigId=p.UserConfigId WHERE p.JournalEntryId=OLD.Id AND (d.Status=-1 OR w.Status=-1))))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Sales workflow journal components are immutable'; END IF;
END$$
DROP TRIGGER IF EXISTS sw_journal_insert$$
CREATE TRIGGER sw_journal_insert BEFORE INSERT ON journalentry FOR EACH ROW
BEGIN
 IF EXISTS(SELECT 1 FROM salesworkflowinvoice w WHERE w.UserConfigId=NEW.UserConfigId AND ((w.Kind='SI' AND w.InvoiceId=NEW.SalesInvoiceId) OR (w.Kind='SR' AND w.InvoiceId=NEW.SalesReceiptId))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot append journals to a delivery-based invoice'; END IF;
END$$
DELIMITER ;
