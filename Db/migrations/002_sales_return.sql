-- Additive MySQL 8 migration. Apply to the intended database before deploying Sales Returns.
-- No application table: allocations remain journalentry.PaymentToJournalEntryId.
DELIMITER $$
DROP PROCEDURE IF EXISTS PreflightSalesReturn$$
CREATE PROCEDURE PreflightSalesReturn()
BEGIN
 IF EXISTS(SELECT 1 FROM navigationitem WHERE Id=4130 AND COALESCE(Link,'')<>'sales/sales-return') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Permission 4130 already belongs to another module; resolve the collision before installing';
 END IF;
END$$
CALL PreflightSalesReturn()$$
DROP PROCEDURE PreflightSalesReturn$$
DELIMITER ;
CREATE TABLE IF NOT EXISTS `salesreturn` (
 `Id` int NOT NULL AUTO_INCREMENT, `UserConfigId` int NOT NULL,
 `ReferenceNo` varchar(50) NOT NULL, `ReferenceDate` datetime(6) NOT NULL,
 `CustomerId` int NOT NULL, `ReceivableAccountId` int NOT NULL, `SalesInvoiceId` int NULL, `SalesReceiptId` int NULL,
 `InventoryLocationId` int NULL, `Reason` varchar(250) NOT NULL, `Notes` longtext NULL,
 `ResponsibilityCenterEntry` json NULL, `SourceSnapshotJson` json NOT NULL, `ApplicationsJson` json NULL,
 `Amount` decimal(20,4) NOT NULL DEFAULT 0, `Balance` decimal(20,4) NOT NULL DEFAULT 0,
 `DiscountAmount` decimal(20,4) NOT NULL DEFAULT 0, `TaxAmount` decimal(20,4) NOT NULL DEFAULT 0, `Taxes` json NULL,
 `Status` smallint NOT NULL DEFAULT 0, `Version` bigint NOT NULL DEFAULT 1, `RequestKey` varchar(36) NOT NULL, `RequestHash` varchar(64) NOT NULL,
 `CreatedDate` datetime(6) NOT NULL, `CreatedByUserId` int NOT NULL, `LastUpdatedDate` datetime(6) NULL, `LastUpdatedByUserId` int NULL,
 `PostedDate` datetime(6) NULL, `PostedByUserId` int NULL, `VoidedDate` datetime(6) NULL, `VoidedByUserId` int NULL, `VoidReason` varchar(250) NULL,
 PRIMARY KEY (`Id`), UNIQUE KEY `IX_SalesReturn_Reference` (`UserConfigId`,`ReferenceNo`), UNIQUE KEY `IX_SalesReturn_Request` (`UserConfigId`,`RequestKey`),
 KEY `IX_SalesReturn_List` (`UserConfigId`,`Status`,`ReferenceDate`),
 CONSTRAINT `FK_SalesReturn_Config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
 CONSTRAINT `FK_SalesReturn_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
 CONSTRAINT `FK_SalesReturn_Invoice` FOREIGN KEY (`SalesInvoiceId`) REFERENCES `salesinvoice` (`Id`),
 CONSTRAINT `FK_SalesReturn_Receipt` FOREIGN KEY (`SalesReceiptId`) REFERENCES `salesreceipt` (`Id`),
 CONSTRAINT `FK_SalesReturn_Location` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
 CONSTRAINT `CK_SalesReturn_Source` CHECK ((SalesInvoiceId IS NOT NULL) + (SalesReceiptId IS NOT NULL) = 1),
 CONSTRAINT `CK_SalesReturn_Status` CHECK (`Status` IN (-1,0,1))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS `salesreturndetail` (
 `Id` int NOT NULL AUTO_INCREMENT, `SalesReturnId` int NOT NULL,
 `SalesInvoiceDetailId` int NULL, `SalesReceiptDetailId` int NULL, `ItemId` int NOT NULL,
 `ItemName` varchar(250) NOT NULL, `Unit` varchar(50) NULL,
 `Quantity` decimal(20,4) NOT NULL, `Rate` decimal(20,4) NOT NULL, `Cost` decimal(20,4) NOT NULL,
 `Amount` decimal(20,4) NOT NULL, `DiscountAmount` decimal(20,4) NOT NULL, `TaxAmount` decimal(20,4) NOT NULL,
 `TaxRateId` int NULL, `TaxName` varchar(100) NULL, `IsInventoryTransaction` tinyint(1) NOT NULL,
 `Notes` longtext NULL, `ComponentsJson` json NOT NULL,
 PRIMARY KEY (`Id`), UNIQUE KEY `IX_SalesReturnDetail_Invoice` (`SalesReturnId`,`SalesInvoiceDetailId`), UNIQUE KEY `IX_SalesReturnDetail_Receipt` (`SalesReturnId`,`SalesReceiptDetailId`),
 CONSTRAINT `FK_SalesReturnDetail_Return` FOREIGN KEY (`SalesReturnId`) REFERENCES `salesreturn` (`Id`),
 CONSTRAINT `FK_SalesReturnDetail_Invoice` FOREIGN KEY (`SalesInvoiceDetailId`) REFERENCES `salesinvoicedetail` (`Id`),
 CONSTRAINT `FK_SalesReturnDetail_Receipt` FOREIGN KEY (`SalesReceiptDetailId`) REFERENCES `salesreceiptdetail` (`Id`),
 CONSTRAINT `FK_SalesReturnDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
 CONSTRAINT `CK_SalesReturnDetail_Source` CHECK ((SalesInvoiceDetailId IS NOT NULL) + (SalesReceiptDetailId IS NOT NULL) = 1),
 CONSTRAINT `CK_SalesReturnDetail_Quantity` CHECK (Quantity > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
SET @srt_sql=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='journalentry' AND COLUMN_NAME='SalesReturnId'),'SELECT 1','ALTER TABLE journalentry ADD COLUMN SalesReturnId int NULL, ADD INDEX IX_JournalEntry_SalesReturn (SalesReturnId), ADD CONSTRAINT FK_JournalEntry_SalesReturn FOREIGN KEY (SalesReturnId) REFERENCES salesreturn(Id)');
PREPARE srt_stmt FROM @srt_sql;
EXECUTE srt_stmt;
DEALLOCATE PREPARE srt_stmt;
-- Permission ID is reserved without granting it to existing non-administrator roles.
INSERT INTO navigationitem (Id,Title,Type,Icon,Link,ParentId)
 SELECT 4130,'Sales Return / Credit Memo','basic','assignment_return','sales/sales-return',NULL
 WHERE NOT EXISTS(SELECT 1 FROM navigationitem WHERE Id=4130);
