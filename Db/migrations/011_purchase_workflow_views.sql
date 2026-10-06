-- Preserve installed sources and explicitly suppress stock from receipt-linked bills.
-- The backup view is a permanent dependency; do not rerun older view migrations afterward.
DELIMITER $$
DROP PROCEDURE IF EXISTS InstallPurchaseWorkflowView$$
CREATE PROCEDURE InstallPurchaseWorkflowView()
BEGIN
 IF (SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction') <> 'UserConfigId,ReferenceNo,ReferenceDate,CustomerId,CustomerName,SupplierId,SupplierName,DetailId,ItemId,ItemName,ItemCost,AverageCost,ItemReorderPoint,LastPurchasedDate,Quantity,QuantityIn,QuantityOut,Rate,Amount,Status,Source,SourceName,TransactionType,InventoryLocationId,InventoryLocationName,Notes,ResponsibilityCenterEntry' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected inventorytransaction shape; inspect before migrating'; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction_before_purchaseworkflow') THEN
 SELECT CONCAT('CREATE VIEW inventorytransaction_before_purchaseworkflow AS ',VIEW_DEFINITION) INTO @pw_sql FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction';
 PREPARE pw FROM @pw_sql; EXECUTE pw; DEALLOCATE PREPARE pw;
 END IF;
 SET @pw_sql='CREATE OR REPLACE VIEW inventorytransaction AS
 SELECT b.* FROM inventorytransaction_before_purchaseworkflow b
 WHERE NOT EXISTS(SELECT 1 FROM purchaseworkflowdocument d JOIN billdetail l ON l.BillId=d.LegacyId WHERE d.UserConfigId=b.UserConfigId AND d.Kind=''PB'' AND b.Source=''PU'' AND b.DetailId=l.Id)
 UNION ALL
 SELECT d.UserConfigId,d.ReferenceNo,d.ReferenceDate,NULL AS CustomerId,NULL AS CustomerName,d.SupplierId,s.Name AS SupplierName,
 -x.LineNo AS DetailId,x.ItemId,i.Name AS ItemName,i.Cost AS ItemCost,i.AverageCost,i.ReorderPoint AS ItemReorderPoint,i.LastPurchasedDate,
 0 AS Quantity,0 AS QuantityIn,0 AS QuantityOut,0 AS Rate,x.InventoryAdjustment AS Amount,d.Status,''LC'' AS Source,''Landed Cost Adjustment'' AS SourceName,''VALUE'' AS TransactionType,
 w.Id AS InventoryLocationId,w.Name AS InventoryLocationName,JSON_UNQUOTE(JSON_EXTRACT(d.PayloadJson,''$.Notes'')) AS Notes,d.ResponsibilityCenterEntry
 FROM purchaseworkflowdocument d JOIN supplier s ON s.Id=d.SupplierId
 JOIN JSON_TABLE(d.PayloadJson,''$.Lines[*]'' COLUMNS(LineNo FOR ORDINALITY, ItemId int PATH ''$.ItemId'', InventoryAdjustment decimal(20,4) PATH ''$.InventoryAdjustment'')) x
 JOIN item i ON i.Id=x.ItemId JOIN inventorylocation w ON w.Id=CAST(JSON_UNQUOTE(JSON_EXTRACT(d.PayloadJson,''$.InventoryLocationId'')) AS UNSIGNED)
 WHERE d.Kind=''LC'' AND d.Status=1 AND x.InventoryAdjustment<>0';
 PREPARE pw FROM @pw_sql; EXECUTE pw; DEALLOCATE PREPARE pw;
END$$
CALL InstallPurchaseWorkflowView()$$
DROP PROCEDURE InstallPurchaseWorkflowView$$
DELIMITER ;
