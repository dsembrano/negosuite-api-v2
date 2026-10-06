-- Run after 006-008. Preserve existing percentage-only workflow documents.
DELIMITER $$
DROP PROCEDURE IF EXISTS AddSalesWorkflowDiscounts$$
CREATE PROCEDURE AddSalesWorkflowDiscounts() BEGIN
 IF NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' AND COLUMN_NAME='HasItemLevelDiscount') THEN ALTER TABLE salesworkflowdocument ADD COLUMN HasItemLevelDiscount tinyint(1) NOT NULL DEFAULT 1; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' AND COLUMN_NAME='DiscountMode') THEN ALTER TABLE salesworkflowdocument ADD COLUMN DiscountMode varchar(250) NOT NULL DEFAULT 'percent'; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowdocument' AND COLUMN_NAME='DiscountValue') THEN ALTER TABLE salesworkflowdocument ADD COLUMN DiscountValue decimal(20,4) NOT NULL DEFAULT 0; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesworkflowline' AND COLUMN_NAME='DiscountIsAmount') THEN ALTER TABLE salesworkflowline ADD COLUMN DiscountIsAmount tinyint(1) NOT NULL DEFAULT 0; END IF;
END$$
CALL AddSalesWorkflowDiscounts()$$
DROP PROCEDURE AddSalesWorkflowDiscounts$$
DELIMITER ;
