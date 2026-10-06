-- Apply after 006 and Sales Return view migration 003. Preserves all installed stock sources.
-- Backup view is a permanent dependency. Do not drop it or rerun older view migrations afterward.
DELIMITER $$
DROP PROCEDURE IF EXISTS InstallSalesWorkflowViews$$
CREATE PROCEDURE InstallSalesWorkflowViews()
BEGIN
 IF (SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ',') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction') <> 'UserConfigId,ReferenceNo,ReferenceDate,CustomerId,CustomerName,SupplierId,SupplierName,DetailId,ItemId,ItemName,ItemCost,AverageCost,ItemReorderPoint,LastPurchasedDate,Quantity,QuantityIn,QuantityOut,Rate,Amount,Status,Source,SourceName,TransactionType,InventoryLocationId,InventoryLocationName,Notes,ResponsibilityCenterEntry' OR NOT EXISTS(SELECT 1 FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unexpected or missing inventorytransaction view; inspect installed definition'; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction_before_salesworkflow') THEN
 SELECT CONCAT('CREATE VIEW inventorytransaction_before_salesworkflow AS ',VIEW_DEFINITION) INTO @sw_sql FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction';
 PREPARE sw_stmt FROM @sw_sql; EXECUTE sw_stmt; DEALLOCATE PREPARE sw_stmt;
 END IF;
 SET @sw_sql='CREATE OR REPLACE VIEW inventorytransaction AS
 SELECT b.* FROM inventorytransaction_before_salesworkflow b
 WHERE NOT EXISTS(SELECT 1 FROM documentrelationship h JOIN documentlinerelationship l ON l.RelationshipId=h.Id
 WHERE h.UserConfigId=b.UserConfigId AND h.SourceDocumentType=''DR'' AND h.TargetDocumentType=b.Source AND h.TargetDocumentType IN (''SI'',''SR'') AND l.TargetLineId=b.DetailId)
 UNION ALL
 SELECT d.UserConfigId,d.ReferenceNo,d.ReferenceDate,d.CustomerId,c.Name AS CustomerName,d.SupplierId,s.Name AS SupplierName,l.Id AS DetailId,l.ItemId,l.Description AS ItemName,i.Cost AS ItemCost,i.AverageCost,i.ReorderPoint AS ItemReorderPoint,i.LastPurchasedDate,-l.Quantity AS Quantity,0 AS QuantityIn,l.Quantity AS QuantityOut,l.Cost AS Rate,l.CostAmount AS Amount,d.Status,''DR'' AS Source,''Delivery'' AS SourceName,''OUT'' AS TransactionType,l.InventoryLocationId,w.Name AS InventoryLocationName,d.Notes,d.ResponsibilityCenterEntry
 FROM salesworkflowdocument d JOIN salesworkflowline l ON l.DocumentId=d.Id JOIN item i ON i.Id=l.ItemId JOIN customer c ON c.Id=d.CustomerId LEFT JOIN supplier s ON s.Id=d.SupplierId JOIN inventorylocation w ON w.Id=l.InventoryLocationId WHERE d.Kind=''DR'' AND d.Status=1 AND l.TrackInventory=1';
 PREPARE sw_stmt FROM @sw_sql; EXECUTE sw_stmt; DEALLOCATE PREPARE sw_stmt;
END$$
CALL InstallSalesWorkflowViews()$$
DROP PROCEDURE InstallSalesWorkflowViews$$
DELIMITER ;
