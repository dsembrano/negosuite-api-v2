-- Sales Workflow R2. Run with an explicitly selected database after migrations 001-005.
-- New operational documents share a typed registry; existing SI/SR/SRT tables remain authoritative.
-- Never run against production as part of automated verification. MySQL DDL is not transactional.
DELIMITER $$
DROP PROCEDURE IF EXISTS PreflightSalesWorkflow$$
CREATE PROCEDURE PreflightSalesWorkflow() BEGIN
 IF DATABASE() IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Select a database'; END IF;
 IF EXISTS(SELECT 1 FROM navigationitem WHERE Id IN(4101,4102,4103) AND NOT ((Id=4101 AND Link='sales/sales-quotation') OR (Id=4102 AND Link='sales/sales-order') OR (Id=4103 AND Link='sales/delivery'))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Sales workflow permission ID conflict'; END IF;
END$$
CALL PreflightSalesWorkflow()$$
DROP PROCEDURE PreflightSalesWorkflow$$
DELIMITER ;

DELIMITER $$
DROP PROCEDURE IF EXISTS VerifySalesWorkflowSchema$$
CREATE PROCEDURE VerifySalesWorkflowSchema() BEGIN
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentrelationship') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentrelationship' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='UserConfigId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='SourceDocumentType' AND COLUMN_TYPE='varchar(8)') OR (COLUMN_NAME='SourceDocumentId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='TargetDocumentType' AND COLUMN_TYPE='varchar(8)') OR (COLUMN_NAME='TargetDocumentId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='RelationshipType' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='Status' AND COLUMN_TYPE='smallint') OR (COLUMN_NAME='CreatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CreatedByUserId' AND COLUMN_TYPE='int')))<>10 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected documentrelationship schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesconfiguration') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesconfiguration' AND ((COLUMN_NAME='UserConfigId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='SalesProcessingMode' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='EnableSalesQuotation' AND COLUMN_TYPE='tinyint(1)') OR (COLUMN_NAME='CogsRecognitionPoint' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='EnableInventoryCommitment' AND COLUMN_TYPE='tinyint(1)') OR (COLUMN_NAME='AllowNegativeInventory' AND COLUMN_TYPE='tinyint(1)') OR (COLUMN_NAME='Version' AND COLUMN_TYPE='bigint') OR (COLUMN_NAME='CreatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CreatedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='LastUpdatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='LastUpdatedByUserId' AND COLUMN_TYPE='int')))<>11 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected salesconfiguration schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='UserConfigId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Kind' AND COLUMN_TYPE='varchar(8)') OR (COLUMN_NAME='InvoiceId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='RequestKey' AND COLUMN_TYPE='varchar(36)') OR (COLUMN_NAME='RequestHash' AND COLUMN_TYPE='varchar(64)') OR (COLUMN_NAME='ReturnSourceJson' AND COLUMN_TYPE='json') OR (COLUMN_NAME='Status' AND COLUMN_TYPE='smallint') OR (COLUMN_NAME='Version' AND COLUMN_TYPE='bigint') OR (COLUMN_NAME='CreatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CreatedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='VoidedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='VoidedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='VoidReason' AND COLUMN_TYPE='varchar(250)')))<>14 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected salesworkflowinvoice schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentlinerelationship') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentlinerelationship' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='RelationshipId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='SourceLineId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='TargetLineId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Quantity' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='CreatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CreatedByUserId' AND COLUMN_TYPE='int')))<>7 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected documentlinerelationship schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='UserConfigId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Kind' AND COLUMN_TYPE='varchar(8)') OR (COLUMN_NAME='ReferenceNo' AND COLUMN_TYPE='varchar(50)') OR (COLUMN_NAME='ReferenceDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CustomerId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='SupplierId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='InventoryLocationId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='PaymentTermId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='RequestedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='ExpirationDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='Salesperson' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='PriceList' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='DeliveryAddress' AND COLUMN_TYPE='longtext') OR (COLUMN_NAME='ResponsibilityCenterEntry' AND COLUMN_TYPE='json') OR (COLUMN_NAME='Notes' AND COLUMN_TYPE='longtext') OR (COLUMN_NAME='IsTaxExclusive' AND COLUMN_TYPE='tinyint(1)') OR (COLUMN_NAME='Amount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='DiscountAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='TaxAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='Status' AND COLUMN_TYPE='smallint') OR (COLUMN_NAME='Disposition' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='CogsRecognitionPoint' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='Version' AND COLUMN_TYPE='bigint') OR (COLUMN_NAME='RequestKey' AND COLUMN_TYPE='varchar(36)') OR (COLUMN_NAME='RequestHash' AND COLUMN_TYPE='varchar(64)') OR (COLUMN_NAME='CreatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='CreatedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='LastUpdatedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='LastUpdatedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='PostedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='PostedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='VoidedDate' AND COLUMN_TYPE='datetime(6)') OR (COLUMN_NAME='VoidedByUserId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='VoidReason' AND COLUMN_TYPE='varchar(250)')))<>35 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected salesworkflowdocument schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowline') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowline' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='DocumentId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='ItemId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Description' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='Unit' AND COLUMN_TYPE='varchar(250)') OR (COLUMN_NAME='InventoryLocationId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Quantity' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='Rate' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='DiscountPercent' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='GrossAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='DiscountAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='TaxAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='Amount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='TaxRateId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='TaxSnapshot' AND COLUMN_TYPE='json') OR (COLUMN_NAME='TrackInventory' AND COLUMN_TYPE='tinyint(1)') OR (COLUMN_NAME='Cost' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='CostAmount' AND COLUMN_TYPE='decimal(20,4)') OR (COLUMN_NAME='SalesAccountId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='DiscountAccountId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='InventoryAccountId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='CostAccountId' AND COLUMN_TYPE='int')))<>22 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected salesworkflowline schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salespostingrecord') AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salespostingrecord' AND ((COLUMN_NAME='Id' AND COLUMN_TYPE='int') OR (COLUMN_NAME='UserConfigId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='DocumentType' AND COLUMN_TYPE='varchar(8)') OR (COLUMN_NAME='DocumentId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='DocumentLineId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='JournalEntryId' AND COLUMN_TYPE='int') OR (COLUMN_NAME='Effect' AND COLUMN_TYPE='varchar(250)')))<>7 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected salespostingrecord schema; review before migration'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentlinerelationship') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentlinerelationship' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='RelationshipId,SourceLineId,TargetLineId') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required documentlinerelationship uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentrelationship') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='documentrelationship' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='UserConfigId,SourceDocumentType,SourceDocumentId,TargetDocumentType,TargetDocumentId') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required documentrelationship uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salespostingrecord') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salespostingrecord' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='JournalEntryId') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required salespostingrecord uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='UserConfigId,Kind,ReferenceNo') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required salesworkflowdocument uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='UserConfigId,RequestKey') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required salesworkflowdocument uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='UserConfigId,Kind,InvoiceId') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required salesworkflowinvoice uniqueness is missing'; END IF;
 IF EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice') AND NOT EXISTS(SELECT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowinvoice' GROUP BY INDEX_NAME HAVING MIN(NON_UNIQUE)=0 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='UserConfigId,RequestKey') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Required salesworkflowinvoice uniqueness is missing'; END IF;
END$$
CALL VerifySalesWorkflowSchema()$$
DROP PROCEDURE VerifySalesWorkflowSchema$$
DELIMITER ;

CREATE TABLE IF NOT EXISTS `documentrelationship` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserConfigId` int NOT NULL,
    `SourceDocumentType` varchar(8) NULL,
    `SourceDocumentId` int NOT NULL,
    `TargetDocumentType` varchar(8) NULL,
    `TargetDocumentId` int NOT NULL,
    `RelationshipType` varchar(250) NULL,
    `Status` smallint NOT NULL,
    `CreatedDate` datetime(6) NOT NULL,
    `CreatedByUserId` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_documentrelationship_config_UserConfigId` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`) ON DELETE RESTRICT,
    UNIQUE KEY `IX_documentrelationship_UserConfigId_SourceDocumentType_SourceD~` (`UserConfigId`, `SourceDocumentType`, `SourceDocumentId`, `TargetDocumentType`, `TargetDocumentId`),
    KEY `IX_documentrelationship_UserConfigId_TargetDocumentType_TargetD~` (`UserConfigId`, `TargetDocumentType`, `TargetDocumentId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `salesconfiguration` (
    `UserConfigId` int NOT NULL,
    `SalesProcessingMode` varchar(250) NULL,
    `EnableSalesQuotation` tinyint(1) NOT NULL,
    `CogsRecognitionPoint` varchar(250) NULL,
    `EnableInventoryCommitment` tinyint(1) NOT NULL,
    `AllowNegativeInventory` tinyint(1) NOT NULL,
    `Version` bigint NOT NULL,
    `CreatedDate` datetime(6) NOT NULL,
    `CreatedByUserId` int NULL,
    `LastUpdatedDate` datetime(6) NULL,
    `LastUpdatedByUserId` int NULL,
    PRIMARY KEY (`UserConfigId`),
    CONSTRAINT `FK_salesconfiguration_config_UserConfigId` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT CK_SalesWorkflow_Mode CHECK (SalesProcessingMode IN ('DIRECT','ORDER_TO_SALES','BOTH')),
    CONSTRAINT CK_SalesWorkflow_Cogs CHECK (CogsRecognitionPoint IN ('DELIVERY','INVOICE'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `salesworkflowinvoice` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserConfigId` int NOT NULL,
    `Kind` varchar(8) NULL,
    `InvoiceId` int NOT NULL,
    `RequestKey` varchar(36) NULL,
    `RequestHash` varchar(64) NULL,
    `ReturnSourceJson` json NULL,
    `Status` smallint NOT NULL,
    `Version` bigint NOT NULL,
    `CreatedDate` datetime(6) NOT NULL,
    `CreatedByUserId` int NOT NULL,
    `VoidedDate` datetime(6) NULL,
    `VoidedByUserId` int NULL,
    `VoidReason` varchar(250) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_salesworkflowinvoice_config_UserConfigId` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`) ON DELETE RESTRICT,
    UNIQUE KEY `IX_salesworkflowinvoice_UserConfigId_Kind_InvoiceId` (`UserConfigId`, `Kind`, `InvoiceId`),
    UNIQUE KEY `IX_salesworkflowinvoice_UserConfigId_RequestKey` (`UserConfigId`, `RequestKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `documentlinerelationship` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RelationshipId` int NOT NULL,
    `SourceLineId` int NOT NULL,
    `TargetLineId` int NOT NULL,
    `Quantity` decimal(20,4) NOT NULL,
    `CreatedDate` datetime(6) NOT NULL,
    `CreatedByUserId` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_documentlinerelationship_documentrelationship_RelationshipId` FOREIGN KEY (`RelationshipId`) REFERENCES `documentrelationship` (`Id`) ON DELETE RESTRICT,
    UNIQUE KEY `IX_documentlinerelationship_RelationshipId_SourceLineId_TargetL~` (`RelationshipId`, `SourceLineId`, `TargetLineId`),
    KEY `IX_documentlinerelationship_SourceLineId` (`SourceLineId`),
    KEY `IX_documentlinerelationship_TargetLineId` (`TargetLineId`),
    CONSTRAINT CK_documentlinerelationship_Quantity CHECK (Quantity>0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `salesworkflowdocument` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserConfigId` int NOT NULL,
    `Kind` varchar(8) NULL,
    `ReferenceNo` varchar(50) NULL,
    `ReferenceDate` datetime(6) NOT NULL,
    `CustomerId` int NOT NULL,
    `SupplierId` int NULL,
    `InventoryLocationId` int NULL,
    `PaymentTermId` int NULL,
    `RequestedDate` datetime(6) NULL,
    `ExpirationDate` datetime(6) NULL,
    `Salesperson` varchar(250) NULL,
    `PriceList` varchar(250) NULL,
    `DeliveryAddress` longtext NULL,
    `ResponsibilityCenterEntry` json NULL,
    `Notes` longtext NULL,
    `IsTaxExclusive` tinyint(1) NOT NULL,
    `Amount` decimal(20,4) NOT NULL,
    `DiscountAmount` decimal(20,4) NOT NULL,
    `TaxAmount` decimal(20,4) NOT NULL,
    `Status` smallint NOT NULL,
    `Disposition` varchar(250) NULL,
    `CogsRecognitionPoint` varchar(250) NULL,
    `Version` bigint NOT NULL,
    `RequestKey` varchar(36) NULL,
    `RequestHash` varchar(64) NULL,
    `CreatedDate` datetime(6) NOT NULL,
    `CreatedByUserId` int NOT NULL,
    `LastUpdatedDate` datetime(6) NULL,
    `LastUpdatedByUserId` int NULL,
    `PostedDate` datetime(6) NULL,
    `PostedByUserId` int NULL,
    `VoidedDate` datetime(6) NULL,
    `VoidedByUserId` int NULL,
    `VoidReason` varchar(250) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_salesworkflowdocument_config_UserConfigId` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowdocument_customer_CustomerId` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowdocument_inventorylocation_InventoryLocationId` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowdocument_paymentterm_PaymentTermId` FOREIGN KEY (`PaymentTermId`) REFERENCES `paymentterm` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowdocument_supplier_SupplierId` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`) ON DELETE RESTRICT,
    KEY `IX_salesworkflowdocument_CustomerId` (`CustomerId`),
    KEY `IX_salesworkflowdocument_InventoryLocationId` (`InventoryLocationId`),
    KEY `IX_salesworkflowdocument_PaymentTermId` (`PaymentTermId`),
    KEY `IX_salesworkflowdocument_SupplierId` (`SupplierId`),
    KEY `IX_salesworkflowdocument_UserConfigId_CustomerId_ReferenceDate` (`UserConfigId`, `CustomerId`, `ReferenceDate`),
    UNIQUE KEY `IX_salesworkflowdocument_UserConfigId_Kind_ReferenceNo` (`UserConfigId`, `Kind`, `ReferenceNo`),
    KEY `IX_salesworkflowdocument_UserConfigId_Kind_Status_ReferenceDate` (`UserConfigId`, `Kind`, `Status`, `ReferenceDate`),
    UNIQUE KEY `IX_salesworkflowdocument_UserConfigId_RequestKey` (`UserConfigId`, `RequestKey`),
    CONSTRAINT CK_SalesWorkflow_Kind CHECK (Kind IN ('QT','SO','DR')),
    CONSTRAINT CK_SalesWorkflow_Status CHECK (Status IN (-1,0,1))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `salesworkflowline` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DocumentId` int NOT NULL,
    `ItemId` int NOT NULL,
    `Description` varchar(250) NULL,
    `Unit` varchar(250) NULL,
    `InventoryLocationId` int NULL,
    `Quantity` decimal(20,4) NOT NULL,
    `Rate` decimal(20,4) NOT NULL,
    `DiscountPercent` decimal(20,4) NOT NULL,
    `GrossAmount` decimal(20,4) NOT NULL,
    `DiscountAmount` decimal(20,4) NOT NULL,
    `TaxAmount` decimal(20,4) NOT NULL,
    `Amount` decimal(20,4) NOT NULL,
    `TaxRateId` int NULL,
    `TaxSnapshot` json NULL,
    `TrackInventory` tinyint(1) NOT NULL,
    `Cost` decimal(20,4) NOT NULL,
    `CostAmount` decimal(20,4) NOT NULL,
    `SalesAccountId` int NOT NULL,
    `DiscountAccountId` int NULL,
    `InventoryAccountId` int NULL,
    `CostAccountId` int NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_salesworkflowline_inventorylocation_InventoryLocationId` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowline_item_ItemId` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_salesworkflowline_salesworkflowdocument_DocumentId` FOREIGN KEY (`DocumentId`) REFERENCES `salesworkflowdocument` (`Id`) ON DELETE RESTRICT,
    KEY `IX_salesworkflowline_DocumentId` (`DocumentId`),
    KEY `IX_salesworkflowline_InventoryLocationId` (`InventoryLocationId`),
    KEY `IX_salesworkflowline_ItemId_InventoryLocationId` (`ItemId`, `InventoryLocationId`),
    CONSTRAINT CK_salesworkflowline_Quantity CHECK (Quantity>0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `salespostingrecord` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserConfigId` int NOT NULL,
    `DocumentType` varchar(8) NULL,
    `DocumentId` int NOT NULL,
    `DocumentLineId` int NULL,
    `JournalEntryId` int NOT NULL,
    `Effect` varchar(250) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_salespostingrecord_journalentry_JournalEntryId` FOREIGN KEY (`JournalEntryId`) REFERENCES `journalentry` (`Id`) ON DELETE RESTRICT,
    UNIQUE KEY `IX_salespostingrecord_JournalEntryId` (`JournalEntryId`),
    KEY `IX_salespostingrecord_UserConfigId_DocumentType_DocumentId` (`UserConfigId`, `DocumentType`, `DocumentId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO salesconfiguration(UserConfigId,SalesProcessingMode,EnableSalesQuotation,CogsRecognitionPoint,EnableInventoryCommitment,AllowNegativeInventory,Version,CreatedDate) SELECT Id,'BOTH',1,'INVOICE',1,0,1,UTC_TIMESTAMP(6) FROM config c WHERE NOT EXISTS(SELECT 1 FROM salesconfiguration s WHERE s.UserConfigId=c.Id);
INSERT INTO navigationitem(Id,Title,Type,Icon,Link,ParentId) SELECT 4101,'Sales Quotation','basic','request_quote','sales/sales-quotation',NULL WHERE NOT EXISTS(SELECT 1 FROM navigationitem WHERE Id=4101);
INSERT INTO navigationitem(Id,Title,Type,Icon,Link,ParentId) SELECT 4102,'Sales Order','basic','shopping_cart','sales/sales-order',NULL WHERE NOT EXISTS(SELECT 1 FROM navigationitem WHERE Id=4102);
INSERT INTO navigationitem(Id,Title,Type,Icon,Link,ParentId) SELECT 4103,'Delivery','basic','local_shipping','sales/delivery',NULL WHERE NOT EXISTS(SELECT 1 FROM navigationitem WHERE Id=4103);
