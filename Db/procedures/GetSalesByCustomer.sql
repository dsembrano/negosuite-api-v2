-- Reference definition reconstructed from the user-supplied production procedure
-- and the empty responsibility-center filter fix confirmed by the user.
-- Not an export of the installed definition. Not automatically applied.
-- Deployment must preserve the target routine's approved DEFINER and grants.
DELIMITER $$
CREATE PROCEDURE GetSalesByCustomer(
    IN userConfigId VARCHAR(10),
    IN periodStart VARCHAR(10),
    IN periodEnd VARCHAR(10),
    IN transactionSource VARCHAR(2),
    IN array_string VARCHAR(100)
)
BEGIN
    SET @userConfig = CONCAT('UserConfigId = ', userConfigId);
    SET @periodParam = CONCAT('ReferenceDate >= ''', periodStart, ''' AND ReferenceDate <= ''', periodEnd, '''');
    SET @sourceParam = IF(transactionSource = '', '1 = 1', CONCAT('Source = ''', transactionSource, ''''));
    SET @rcParam = IF(
        COALESCE(TRIM(array_string), '') = '',
        '1 = 1',
        CONCAT('JSON_CONTAINS(JSON_EXTRACT(ResponsibilityCenterEntry, ''$[*].id''), JSON_ARRAY(', array_string, '), ''$'')')
    );
    SET @query = CONCAT('
        SELECT CustomerId, CustomerName,
            SUM(Cost) AS Cost,
            SUM(Amount) - SUM(IF(ISNULL(TaxAmount), 0, TaxAmount)) AS Sales,
            SUM(Amount) AS SalesWithTax,
            SUM(IF(ISNULL(TaxAmount), 0, TaxAmount)) AS TaxAmount,
            COUNT(*) AS InvoiceCount
        FROM salestransaction
        WHERE ', @userConfig, ' AND ', @periodParam, ' AND ', @sourceParam,
        ' AND Status = 1 AND ', @rcParam, '
        GROUP BY CustomerId, CustomerName
        ORDER BY CustomerName;');
    PREPARE stmt FROM @query;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
END$$
DELIMITER ;
