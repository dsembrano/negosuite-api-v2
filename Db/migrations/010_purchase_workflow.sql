-- Additive purchase workflow. Apply after 009; legacy RR/PU tables and units are unchanged.
DELIMITER $$
DROP PROCEDURE IF EXISTS CheckPurchasePermission$$
CREATE PROCEDURE CheckPurchasePermission()
BEGIN
 IF EXISTS(SELECT 1 FROM navigationitem WHERE Id=4211 AND Link<>'purchases/purchase-order') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Purchase Order permission ID 4211 conflict'; END IF;
END$$
CALL CheckPurchasePermission()$$
DROP PROCEDURE CheckPurchasePermission$$
DELIMITER ;
CREATE TABLE IF NOT EXISTS purchaseconfiguration (
 UserConfigId int NOT NULL PRIMARY KEY, ProcessingMode varchar(30) NOT NULL DEFAULT 'BOTH', GrniAccountId int NULL, Version bigint NOT NULL DEFAULT 0,
 CONSTRAINT FK_PC_Config FOREIGN KEY(UserConfigId) REFERENCES config(Id), CONSTRAINT FK_PC_Account FOREIGN KEY(GrniAccountId) REFERENCES account(Id)
);
CREATE TABLE IF NOT EXISTS purchaseworkflowdocument (
 Id int NOT NULL AUTO_INCREMENT PRIMARY KEY, UserConfigId int NOT NULL, Kind varchar(8) NOT NULL, ReferenceNo varchar(50) NOT NULL,
 ReferenceDate datetime(6) NOT NULL, SupplierId int NOT NULL, SupplierName varchar(250) NOT NULL, ResponsibilityCenterEntry json NOT NULL, PayloadJson json NOT NULL,
 Amount decimal(20,4) NOT NULL, Status smallint NOT NULL, Version bigint NOT NULL, LegacyId int NULL, RequestKey varchar(36) NOT NULL, RequestHash varchar(64) NOT NULL,
 CreatedDate datetime(6) NOT NULL, CreatedByUserId int NOT NULL, PostedDate datetime(6) NULL, VoidReason varchar(250) NULL, VoidedByUserId int NULL,
 UNIQUE KEY UX_PW_Request(UserConfigId,RequestKey), UNIQUE KEY UX_PW_Reference(UserConfigId,Kind,ReferenceNo), UNIQUE KEY UX_PW_Legacy(UserConfigId,Kind,LegacyId),
 CONSTRAINT FK_PW_Config FOREIGN KEY(UserConfigId) REFERENCES config(Id), CONSTRAINT FK_PW_Supplier FOREIGN KEY(SupplierId) REFERENCES supplier(Id)
);
CREATE TABLE IF NOT EXISTS purchaseworkflowlink (
 Id int NOT NULL AUTO_INCREMENT PRIMARY KEY, UserConfigId int NOT NULL, SourceId int NOT NULL, SourceLine int NOT NULL, TargetId int NOT NULL, TargetLine int NOT NULL, Quantity decimal(20,4) NOT NULL,
 UNIQUE KEY UX_PWL_SourceTarget(SourceId,SourceLine,TargetId,TargetLine), CONSTRAINT FK_PWL_Source FOREIGN KEY(SourceId) REFERENCES purchaseworkflowdocument(Id), CONSTRAINT FK_PWL_Target FOREIGN KEY(TargetId) REFERENCES purchaseworkflowdocument(Id)
);
INSERT INTO navigationitem(Id,Title,Type,Icon,Link,ParentId) SELECT 4211,'Purchase Order','basic','shopping_cart','purchases/purchase-order',NULL WHERE NOT EXISTS(SELECT 1 FROM navigationitem WHERE Id=4211);
