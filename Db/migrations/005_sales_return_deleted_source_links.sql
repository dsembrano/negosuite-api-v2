-- Apply after 002-004 and BEFORE deploying the accompanying API build.
-- Keeps deleted-return history while allowing its original invoice/line to be removed.
-- No data is deleted and existing invoice links are left intact by this migration.
DELIMITER $$
DROP PROCEDURE IF EXISTS MigrateDeletedReturnSourceLinks$$
CREATE PROCEDURE MigrateDeletedReturnSourceLinks()
BEGIN
 IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesreturndetail' AND COLUMN_NAME='OriginalSourceDetailId') THEN
  ALTER TABLE salesreturndetail ADD COLUMN OriginalSourceDetailId int NULL;
 END IF;
 IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='salesreturn' AND CONSTRAINT_NAME='CK_SalesReturn_Source') THEN
  ALTER TABLE salesreturn DROP CHECK CK_SalesReturn_Source;
 END IF;
 ALTER TABLE salesreturn ADD CONSTRAINT CK_SalesReturn_Source CHECK (
  ((SalesInvoiceId IS NOT NULL) + (SalesReceiptId IS NOT NULL) = 1) OR
  (Status=-1 AND SalesInvoiceId IS NULL AND SalesReceiptId IS NULL));
 IF EXISTS (SELECT 1 FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='salesreturndetail' AND CONSTRAINT_NAME='CK_SalesReturnDetail_Source') THEN
  ALTER TABLE salesreturndetail DROP CHECK CK_SalesReturnDetail_Source;
 END IF;
 ALTER TABLE salesreturndetail ADD CONSTRAINT CK_SalesReturnDetail_Source CHECK (
  ((SalesInvoiceDetailId IS NOT NULL) + (SalesReceiptDetailId IS NOT NULL) = 1) OR
  (SalesInvoiceDetailId IS NULL AND SalesReceiptDetailId IS NULL AND OriginalSourceDetailId IS NOT NULL AND OriginalSourceDetailId>0));
END$$
CALL MigrateDeletedReturnSourceLinks()$$
DROP PROCEDURE MigrateDeletedReturnSourceLinks$$
DROP TRIGGER IF EXISTS protect_srt_detached_detail_insert$$
CREATE TRIGGER protect_srt_detached_detail_insert BEFORE INSERT ON salesreturndetail FOR EACH ROW
BEGIN
 IF NEW.SalesInvoiceDetailId IS NULL AND NEW.SalesReceiptDetailId IS NULL AND
    NOT EXISTS (SELECT 1 FROM salesreturn WHERE Id=NEW.SalesReturnId AND Status=-1) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Only deleted Sales Returns may have detached source lines';
 END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_detached_detail_update$$
CREATE TRIGGER protect_srt_detached_detail_update BEFORE UPDATE ON salesreturndetail FOR EACH ROW
BEGIN
 IF NEW.SalesInvoiceDetailId IS NULL AND NEW.SalesReceiptDetailId IS NULL AND
    NOT EXISTS (SELECT 1 FROM salesreturn WHERE Id=NEW.SalesReturnId AND Status=-1) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Only deleted Sales Returns may have detached source lines';
 END IF;
END$$
DROP TRIGGER IF EXISTS protect_srt_detached_reactivation$$
CREATE TRIGGER protect_srt_detached_reactivation BEFORE UPDATE ON salesreturn FOR EACH ROW
BEGIN
 IF NEW.Status<>-1 AND EXISTS (SELECT 1 FROM salesreturndetail WHERE SalesReturnId=OLD.Id AND SalesInvoiceDetailId IS NULL AND SalesReceiptDetailId IS NULL) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='A Sales Return with detached source lines cannot be reactivated';
 END IF;
END$$
DELIMITER ;
