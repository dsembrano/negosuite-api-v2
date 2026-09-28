-- REFERENCE ONLY: table-only DDL captured 2026-09-25 from configured local MySQL 8.0.46.
-- Incomplete: routine, trigger and event definitions are NOT included. No row data.
-- Not an automatic migration. Contains DROP TABLE statements; restore only into an isolated test instance.
-- MySQL dump 10.13  Distrib 8.0.46, for Win64 (x86_64)
--
-- Source connection identity omitted.
-- ------------------------------------------------------
-- Server version	8.0.46

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `account`
--

DROP TABLE IF EXISTS `account`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `account` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(20) DEFAULT NULL,
  `Name` varchar(150) NOT NULL,
  `Type` char(1) DEFAULT NULL,
  `Nature` char(1) DEFAULT NULL,
  `CategoryId` int NOT NULL,
  `Level` char(1) DEFAULT NULL,
  `IsSubAccount` tinyint DEFAULT '0',
  `ParentAccountId` int DEFAULT NULL,
  `RequireCustomer` tinyint NOT NULL DEFAULT '0',
  `RequireSupplier` tinyint NOT NULL DEFAULT '0',
  `SLType` char(2) DEFAULT '',
  `Notes` longtext,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code_UNIQUE` (`UserConfigId`,`Code`),
  KEY `FK_Account_Account2_idx` (`ParentAccountId`),
  KEY `FK_Account_Category_idx` (`CategoryId`),
  CONSTRAINT `FK_Account_Account1` FOREIGN KEY (`ParentAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Account_Category` FOREIGN KEY (`CategoryId`) REFERENCES `accountcategory` (`Id`),
  CONSTRAINT `FK_account_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=2321 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `accountcategory`
--

DROP TABLE IF EXISTS `accountcategory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `accountcategory` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Type` varchar(10) NOT NULL,
  `OrderNo` int DEFAULT NULL,
  `AccountCodePrefix` varchar(20) DEFAULT NULL,
  `TemplateCategoryId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_accountcategory_config` (`UserConfigId`),
  CONSTRAINT `FK_accountcategory_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=247 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `accounttotal`
--

DROP TABLE IF EXISTS `accounttotal`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `accounttotal` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Period` varchar(6) NOT NULL,
  `AccountId` int NOT NULL,
  `Debit` decimal(20,4) NOT NULL,
  `Credit` decimal(20,4) NOT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IDX_AccountMonthTotal_Period` (`Period`,`AccountId`),
  KEY `FK_AccountMonthTotal_Account` (`AccountId`),
  CONSTRAINT `FK_AccountMonthTotal_Account` FOREIGN KEY (`AccountId`) REFERENCES `account` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=907 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `address`
--

DROP TABLE IF EXISTS `address`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `address` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Line1` varchar(150) NOT NULL,
  `Line2` varchar(150) DEFAULT NULL,
  `CityMunicipalityId` int NOT NULL,
  `PostalCode` varchar(20) DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_Address_CityMunicipality_idx` (`CityMunicipalityId`),
  CONSTRAINT `FK_Address_CityMunicipality` FOREIGN KEY (`CityMunicipalityId`) REFERENCES `citymunicipality` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `agingperiod`
--

DROP TABLE IF EXISTS `agingperiod`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `agingperiod` (
  `Id` int NOT NULL,
  `Name` varchar(150) DEFAULT NULL,
  `ShowCurrent` tinyint NOT NULL DEFAULT '0',
  `Period1` smallint NOT NULL,
  `Period2` smallint NOT NULL,
  `Period3` smallint NOT NULL,
  `Period4` smallint DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `appaymentdetail`
--

DROP TABLE IF EXISTS `appaymentdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `appaymentdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `PaymentJournalEntryId` int NOT NULL,
  `PayableJournalEntryId` int NOT NULL,
  `Amount` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_APPaymentDetail_JournalEntry1` (`PayableJournalEntryId`),
  KEY `FK_APPaymentDetail_JournalEntry2` (`PaymentJournalEntryId`),
  CONSTRAINT `FK_APPaymentDetail_JournalEntry1` FOREIGN KEY (`PayableJournalEntryId`) REFERENCES `journalentry` (`Id`),
  CONSTRAINT `FK_APPaymentDetail_JournalEntry2` FOREIGN KEY (`PaymentJournalEntryId`) REFERENCES `journalentry` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `appversion`
--

DROP TABLE IF EXISTS `appversion`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `appversion` (
  `Id` int NOT NULL,
  `VersionCode` varchar(45) DEFAULT NULL,
  `AndroidUpdateUrl` varchar(500) DEFAULT NULL,
  `IosUpdateUrl` varchar(500) DEFAULT NULL,
  `APKFilename` varchar(150) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bill`
--

DROP TABLE IF EXISTS `bill`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `bill` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `BillNo` varchar(50) NOT NULL,
  `BillDate` datetime(6) NOT NULL,
  `SupplierId` int NOT NULL,
  `PaymentTermId` int NOT NULL,
  `DueDate` datetime(6) NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Balance` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Taxes` json DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `IsTaxExclusive` tinyint(1) DEFAULT NULL,
  `DiscountIsBeforeTax` tinyint(1) DEFAULT NULL,
  `HasItemLevelDiscount` tinyint(1) DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_BillNo` (`UserConfigId`,`BillNo`),
  KEY `FK_Bill_Supplier` (`SupplierId`),
  KEY `FK_Bill_PaymentTerm_idx` (`PaymentTermId`),
  KEY `FK_Bill_InventoryLocation_idx` (`InventoryLocationId`),
  CONSTRAINT `FK_bill_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Bill_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_Bill_PaymenTerm` FOREIGN KEY (`PaymentTermId`) REFERENCES `paymentterm` (`Id`),
  CONSTRAINT `FK_Bill_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=4070 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `billdetail`
--

DROP TABLE IF EXISTS `billdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `billdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `BillId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,2) NOT NULL,
  `Rate` decimal(20,2) NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL,
  `TaxRateId` int DEFAULT NULL,
  `TaxAmount` decimal(20,4) DEFAULT '0.0000',
  `Notes` text,
  `IsInventoryTransaction` tinyint DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `LandedCost` decimal(20,4) DEFAULT NULL,
  `LandedCostJson` json DEFAULT NULL,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `PostedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_BillDetail_Item_idx` (`ItemId`),
  KEY `FK_BillDetail_Bill_idx` (`BillId`),
  KEY `FK_BillDetail_InventoryLocation_idx` (`InventoryLocationId`),
  CONSTRAINT `FK_BillDetail_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_BillDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_BillDetail_SalesInvoice` FOREIGN KEY (`BillId`) REFERENCES `bill` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=16316 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `billpayment`
--

DROP TABLE IF EXISTS `billpayment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `billpayment` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `SupplierId` int NOT NULL,
  `PaymentModeId` int NOT NULL,
  `CheckNo` varchar(45) DEFAULT NULL,
  `PaidThroughAccountId` int NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `Amount` decimal(20,4) DEFAULT '0.0000',
  `Balance` decimal(20,4) DEFAULT '0.0000',
  `PostedDate` datetime DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_BillsPayment_Supplier_idx` (`SupplierId`),
  KEY `FK_BillsPayment_Account_idx` (`PaidThroughAccountId`),
  KEY `FK_BillsPayment_PaymentMode_idx` (`PaymentModeId`),
  CONSTRAINT `FK_BillPayment_Account` FOREIGN KEY (`PaidThroughAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_billpayment_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_BillPayment_Customer` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`),
  CONSTRAINT `FK_BillPayment_PaymentMode` FOREIGN KEY (`PaymentModeId`) REFERENCES `paymentmode` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `citymunicipality`
--

DROP TABLE IF EXISTS `citymunicipality`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `citymunicipality` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(150) NOT NULL,
  `StateProvinceId` int NOT NULL,
  `PostalCode` varchar(20) DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_CityMunicipality_StateProvince` (`StateProvinceId`),
  CONSTRAINT `FK_CityMunicipality_StateProvince` FOREIGN KEY (`StateProvinceId`) REFERENCES `stateprovince` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=1690 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `config`
--

DROP TABLE IF EXISTS `config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `config` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Uuid` varchar(36) NOT NULL,
  `CompanyName` varchar(150) NOT NULL,
  `Address1` varchar(150) DEFAULT NULL,
  `Address2` varchar(150) DEFAULT NULL,
  `PhoneNo` varchar(50) NOT NULL,
  `FaxNo` varchar(50) DEFAULT NULL,
  `Email` varchar(50) NOT NULL,
  `Website` varchar(150) DEFAULT NULL,
  `TIN` varchar(50) DEFAULT NULL,
  `IndustryId` int DEFAULT NULL,
  `CompanyAbout` text,
  `CountryId` int DEFAULT NULL,
  `CurrentPeriod` datetime(6) DEFAULT NULL,
  `ARTradeAccountId` int DEFAULT NULL,
  `ARAgingBaseDate` tinyint DEFAULT '1',
  `ARAgingShowCurrent` tinyint DEFAULT '0',
  `ARAgingPeriod1` smallint DEFAULT NULL,
  `ARAgingPeriod2` smallint DEFAULT NULL,
  `ARAgingPeriod3` smallint DEFAULT NULL,
  `ARAgingPeriod4` smallint DEFAULT '0',
  `APTradeAccountId` int DEFAULT NULL,
  `DiscountAccountId` int DEFAULT NULL,
  `PurchaseDiscountAccountId` int DEFAULT NULL,
  `DateFormat` varchar(45) DEFAULT NULL,
  `CompanyLogoURL` varchar(255) DEFAULT NULL,
  `InvoiceShowShippingAddress` tinyint DEFAULT NULL,
  `InvoiceMargin` varchar(45) DEFAULT '',
  `InvoiceLogoURL` varchar(255) DEFAULT '',
  `InvoiceLogoPosition` char(1) DEFAULT '',
  `InvoiceLogoWidth` varchar(10) DEFAULT '',
  `InvoiceLogoHeight` varchar(10) DEFAULT '',
  `InvoiceLogoStyleClass` varchar(150) DEFAULT '',
  `InvoiceTemplate` varchar(45) DEFAULT NULL,
  `SalesReceiptTemplate` varchar(45) DEFAULT NULL,
  `PaymentVerifier` varchar(45) DEFAULT NULL,
  `PaymentVerifierPosition` varchar(45) DEFAULT NULL,
  `PaymentApprover` varchar(45) DEFAULT NULL,
  `PaymentApproverPosition` varchar(45) DEFAULT NULL,
  `PaymentVoucherTemplate` varchar(45) DEFAULT NULL,
  `PaymentAdjustmentTypes` json DEFAULT NULL,
  `APAgingBaseDate` tinyint DEFAULT '1',
  `APAgingShowCurrent` tinyint DEFAULT '0',
  `APAgingPeriod1` smallint DEFAULT NULL,
  `APAgingPeriod2` smallint DEFAULT NULL,
  `APAgingPeriod3` smallint DEFAULT NULL,
  `APAgingPeriod4` smallint DEFAULT '0',
  `IncomeStatementConfig` json DEFAULT NULL,
  `RequireAccountCode` tinyint DEFAULT NULL,
  `ShowAccountCodeInList` tinyint DEFAULT '0',
  `JournalVoucherVerifier` varchar(45) DEFAULT NULL,
  `JournalVoucherVerifierPosition` varchar(45) DEFAULT NULL,
  `JournalVoucherApprover` varchar(45) DEFAULT NULL,
  `JournalVoucherApproverPosition` varchar(45) DEFAULT NULL,
  `JournalVoucherTemplate` varchar(45) DEFAULT NULL,
  `SubscriptionPlanId` int DEFAULT NULL,
  `SubscriptionDate` datetime DEFAULT NULL,
  `Trial` tinyint DEFAULT NULL,
  `TrialEndDate` datetime DEFAULT NULL,
  `BillingMode` varchar(1) DEFAULT NULL,
  `IsTemplate` tinyint DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `MaxUserCount` tinyint DEFAULT NULL,
  `LandedCostItemsJson` json DEFAULT NULL,
  `MetabaseDashboard` tinyint DEFAULT NULL,
  `PaymentModes` json DEFAULT NULL,
  `AutoReferenceNoConfig` json DEFAULT NULL,
  `PointOfSales` tinyint DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Uuid_UNIQUE` (`Uuid`),
  KEY `FK_Config_ARTrade_Account_idx` (`ARTradeAccountId`),
  KEY `FK_Config_APTrade_Account_idx` (`APTradeAccountId`),
  KEY `FK_Config_Discount_Account_idx` (`DiscountAccountId`),
  KEY `FTK_Config_Industry_idx` (`IndustryId`),
  KEY `FK_Config_Country_idx` (`CountryId`),
  KEY `FK_Config_PurchaseDiscount_Account_idx` (`PurchaseDiscountAccountId`),
  KEY `FK_Config_SubscriptionPlan_Id` (`SubscriptionPlanId`),
  CONSTRAINT `FK_Config_APTrade_Account` FOREIGN KEY (`APTradeAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Config_ARTrade_Account` FOREIGN KEY (`ARTradeAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Config_Country` FOREIGN KEY (`CountryId`) REFERENCES `country` (`Id`),
  CONSTRAINT `FK_Config_Discount_Account` FOREIGN KEY (`DiscountAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Config_Industry` FOREIGN KEY (`IndustryId`) REFERENCES `industry` (`Id`),
  CONSTRAINT `FK_Config_PurchaseDiscount_Account` FOREIGN KEY (`PurchaseDiscountAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Config_SubscriptionPlan_Id` FOREIGN KEY (`SubscriptionPlanId`) REFERENCES `subscriptionplan` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=20 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `contact`
--

DROP TABLE IF EXISTS `contact`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `contact` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `FirstName` varchar(100) NOT NULL,
  `Lastname` varchar(100) DEFAULT NULL,
  `Title` varchar(100) DEFAULT NULL,
  `Email` varchar(100) DEFAULT NULL,
  `AlternateEmail` varchar(100) DEFAULT NULL,
  `PhotoId` int DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `contactphone`
--

DROP TABLE IF EXISTS `contactphone`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `contactphone` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ContactId` int NOT NULL,
  `ContactNumber` varchar(20) NOT NULL,
  `ContactNumberTypeId` int NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_ContactPhone_Contact` (`ContactId`),
  CONSTRAINT `FK_ContactPhone_Contact` FOREIGN KEY (`ContactId`) REFERENCES `contact` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `country`
--

DROP TABLE IF EXISTS `country`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `country` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(10) NOT NULL,
  `Name` varchar(150) NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `creditor`
--

DROP TABLE IF EXISTS `creditor`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `creditor` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(20) NOT NULL,
  `Name` varchar(100) NOT NULL,
  `Address1` varchar(150) DEFAULT NULL,
  `Address2` varchar(150) DEFAULT NULL,
  `PhoneNo` varchar(50) DEFAULT NULL,
  `Email` varchar(50) DEFAULT NULL,
  `ContactName` varchar(50) DEFAULT NULL,
  `CreditLimit` decimal(20,4) DEFAULT NULL,
  `TIN` varchar(50) DEFAULT NULL,
  `CreditorTypeId` int NOT NULL,
  `IsVAT` smallint NOT NULL DEFAULT '0',
  `IsGenericName` smallint NOT NULL DEFAULT '0',
  `IsActive` smallint NOT NULL DEFAULT '0',
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_Creditor_CreditorType` (`CreditorTypeId`),
  CONSTRAINT `FK_Creditor_CreditorType` FOREIGN KEY (`CreditorTypeId`) REFERENCES `creditortype` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `creditortype`
--

DROP TABLE IF EXISTS `creditortype`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `creditortype` (
  `Id` int NOT NULL,
  `Name` varchar(50) DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `currency`
--

DROP TABLE IF EXISTS `currency`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `currency` (
  `Id` smallint NOT NULL,
  `Code` char(10) NOT NULL,
  `Name` char(40) NOT NULL,
  `ExchangeRate` decimal(10,4) NOT NULL,
  `IsBase` smallint NOT NULL,
  `AltCode` varchar(15) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `customer`
--

DROP TABLE IF EXISTS `customer`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `customer` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(20) DEFAULT NULL,
  `Name` varchar(150) NOT NULL,
  `CreditLimit` decimal(20,4) DEFAULT '0.0000',
  `TIN` varchar(50) DEFAULT NULL,
  `TaxRateId` int DEFAULT NULL,
  `PaymentTermId` int DEFAULT NULL,
  `Notes` longtext,
  `BusinessStyle` varchar(150) DEFAULT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '0',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code_UNIQUE` (`Code`),
  KEY `FK_Customer_TaxRate_idx` (`TaxRateId`),
  KEY `FK_Customer_PaymenrTerm_idx` (`PaymentTermId`),
  KEY `FK_customer_config` (`UserConfigId`),
  CONSTRAINT `FK_customer_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Customer_PaymenrTerm` FOREIGN KEY (`PaymentTermId`) REFERENCES `paymentterm` (`Id`),
  CONSTRAINT `FK_Customer_TaxRate` FOREIGN KEY (`TaxRateId`) REFERENCES `taxrate` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=5182 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `customeraddress`
--

DROP TABLE IF EXISTS `customeraddress`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `customeraddress` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `CustomerId` int NOT NULL,
  `AddressLine1` varchar(150) DEFAULT NULL,
  `AddressLine2` varchar(150) DEFAULT NULL,
  `CityMunicipalityId` int DEFAULT NULL,
  `PostalCode` varchar(20) DEFAULT NULL,
  `IsDeliveryAddress` tinyint(1) NOT NULL,
  `IsBillingAddress` tinyint(1) NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_CustomerAddress_Customer` (`CustomerId`),
  KEY `FK_CustomerAddress_CityMunicipality_idx` (`CityMunicipalityId`),
  CONSTRAINT `FK_CustomerAddress_CityMunicipality` FOREIGN KEY (`CityMunicipalityId`) REFERENCES `citymunicipality` (`Id`),
  CONSTRAINT `FK_CustomerAddress_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=4834 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `customercontact`
--

DROP TABLE IF EXISTS `customercontact`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `customercontact` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `CustomerId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Title` varchar(150) DEFAULT NULL,
  `Email` varchar(150) NOT NULL,
  `PhoneNo` varchar(20) NOT NULL,
  `AlternatePhoneNo` varchar(20) DEFAULT NULL,
  `AlternateEmail` varchar(150) DEFAULT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  `CreatedDate` datetime NOT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_CustomerContact_Customer_idx` (`CustomerId`),
  CONSTRAINT `FK_CustomerContact_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=57 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `debtor`
--

DROP TABLE IF EXISTS `debtor`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `debtor` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(20) NOT NULL,
  `Name` varchar(100) NOT NULL,
  `Address1` varchar(150) DEFAULT NULL,
  `Address2` varchar(150) DEFAULT NULL,
  `PostalCode` varchar(20) DEFAULT NULL,
  `PhoneNo` varchar(50) DEFAULT NULL,
  `Email` varchar(50) DEFAULT NULL,
  `ContactName` varchar(50) DEFAULT NULL,
  `CreditLimit` decimal(20,4) DEFAULT NULL,
  `TIN` varchar(50) DEFAULT NULL,
  `DebtorTypeId` int NOT NULL,
  `IsGenericName` smallint NOT NULL DEFAULT '0',
  `IsActive` smallint NOT NULL DEFAULT '0',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_Debtor_DebtorType` (`DebtorTypeId`),
  CONSTRAINT `FK_Debtor_DebtorType` FOREIGN KEY (`DebtorTypeId`) REFERENCES `debtortype` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `debtortype`
--

DROP TABLE IF EXISTS `debtortype`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `debtortype` (
  `Id` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `discounttype`
--

DROP TABLE IF EXISTS `discounttype`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `discounttype` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Rate` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountAccountId` int DEFAULT NULL,
  `DiscountIsBeforeTax` smallint DEFAULT NULL,
  `LockedRate` smallint DEFAULT NULL,
  `TaxRateId` int DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `DiscountType_Account_FK_idx` (`DiscountAccountId`),
  KEY `DiscountType_TaxRate_FK_idx` (`TaxRateId`),
  KEY `DiscountType_Config_FK_idx` (`UserConfigId`),
  CONSTRAINT `DiscountType_Account_FK` FOREIGN KEY (`DiscountAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `DiscountType_Config_FK` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `DiscountType_TaxRate_FK` FOREIGN KEY (`TaxRateId`) REFERENCES `taxrate` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `emaillog`
--

DROP TABLE IF EXISTS `emaillog`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `emaillog` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Uuid` varchar(36) NOT NULL,
  `Email` varchar(150) NOT NULL,
  `Data` json DEFAULT NULL,
  `ConfigId` int DEFAULT NULL,
  `Status` smallint NOT NULL,
  `CreatedDate` datetime NOT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `ExpiryDate` datetime DEFAULT NULL,
  `Action` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UUId_UNIQUE` (`Uuid`)
) ENGINE=InnoDB AUTO_INCREMENT=105 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `expensepayment`
--

DROP TABLE IF EXISTS `expensepayment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `expensepayment` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `SupplierId` int DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `Payee` varchar(150) NOT NULL,
  `PaymentModeId` int NOT NULL,
  `CheckNo` varchar(45) DEFAULT NULL,
  `PaidThroughAccountId` int NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `Amount` decimal(20,4) DEFAULT '0.0000',
  `Balance` decimal(20,4) DEFAULT '0.0000',
  `PostedDate` datetime DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_ExpensePayment_Supplier_idx` (`SupplierId`),
  KEY `FK_ExpensePayment_Account_idx` (`PaidThroughAccountId`),
  KEY `FK_ExpensePayment_PaymentMode_idx` (`PaymentModeId`),
  CONSTRAINT `FK_ExpensePayment_Account` FOREIGN KEY (`PaidThroughAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_ExpensePayment_PaymentMode` FOREIGN KEY (`PaymentModeId`) REFERENCES `paymentmode` (`Id`),
  CONSTRAINT `FK_ExpensePayment_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `generaljournal`
--

DROP TABLE IF EXISTS `generaljournal`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `generaljournal` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` char(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  CONSTRAINT `FK_generaljournal_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=4693 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `generalledger`
--

DROP TABLE IF EXISTS `generalledger`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `generalledger` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `AccountId` int NOT NULL,
  `Period` datetime(6) NOT NULL,
  `BeginBalance` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `TotalDebit` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `TotalCredit` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `BalanceEnd` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `DateCreated` datetime(6) DEFAULT NULL,
  `DateEdited` datetime(6) DEFAULT NULL,
  `CreatedBy` varchar(50) DEFAULT NULL,
  `EditedBy` varchar(50) DEFAULT NULL,
  `DateProcessed` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `industry`
--

DROP TABLE IF EXISTS `industry`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `industry` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(150) NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventoryadjustment`
--

DROP TABLE IF EXISTS `inventoryadjustment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventoryadjustment` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `AdjustmentAccountId` int DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `SupplierId` int DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_StockTransfer_InventoryLocation_idx` (`InventoryLocationId`),
  KEY `FK_StockTransfer_AdjustmentAccount_idx` (`AdjustmentAccountId`),
  KEY `FK_inventoryadjustment_customer` (`CustomerId`),
  KEY `FK_inventoryadjustment_supplier` (`SupplierId`),
  CONSTRAINT `FK_inventoryadjustment_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_inventoryadjustment_customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_inventoryadjustment_supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`),
  CONSTRAINT `FK_StockTransfer_AdjustmentAccount` FOREIGN KEY (`AdjustmentAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_StockTransfer_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=846 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventoryadjustmentdetail`
--

DROP TABLE IF EXISTS `inventoryadjustmentdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventoryadjustmentdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `InventoryAdjustmentId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,2) NOT NULL,
  `Rate` decimal(20,2) DEFAULT NULL,
  `Amount` decimal(20,2) DEFAULT NULL,
  `Notes` text,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_InventoryAdjustmentDetail_Item_idx` (`ItemId`),
  KEY `FK_InventoryAdjustmentDetail_StockTransfer_idx` (`InventoryAdjustmentId`),
  CONSTRAINT `FK_InventoryAdjustmentDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_InventoryAdjustmentDetail_StockTrans` FOREIGN KEY (`InventoryAdjustmentId`) REFERENCES `inventoryadjustment` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=11300 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventorylocation`
--

DROP TABLE IF EXISTS `inventorylocation`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventorylocation` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(10) DEFAULT NULL,
  `Name` varchar(50) NOT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '1',
  `Notes` longtext,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_inventorylocation_config` (`UserConfigId`),
  CONSTRAINT `FK_inventorylocation_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=30 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `item`
--

DROP TABLE IF EXISTS `item`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `item` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(20) DEFAULT NULL,
  `Name` varchar(150) NOT NULL,
  `Type` char(1) NOT NULL,
  `Unit` varchar(45) NOT NULL,
  `ItemCategoryId` int DEFAULT NULL,
  `ToPurchase` tinyint NOT NULL DEFAULT '1',
  `Cost` decimal(20,4) DEFAULT '0.0000',
  `AverageCost` decimal(20,4) DEFAULT NULL,
  `PurchaseAccountId` int DEFAULT NULL,
  `PurchaseTaxRateId` int DEFAULT NULL,
  `ToSell` tinyint NOT NULL DEFAULT '1',
  `Rate` decimal(20,4) DEFAULT '0.0000',
  `SalesAccountId` int DEFAULT NULL,
  `SalesTaxRateId` int DEFAULT NULL,
  `Notes` longtext,
  `TrackInventory` tinyint(1) DEFAULT NULL,
  `InventoryAccountId` int DEFAULT NULL,
  `OpeningQuantity` decimal(20,2) DEFAULT '0.00',
  `OpeningQuantityDate` datetime DEFAULT NULL,
  `ReorderPoint` decimal(20,2) DEFAULT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '1',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `LastPurchasedDate` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code_UNIQUE` (`UserConfigId`,`Code`),
  KEY `Item_Purchase_account_idx` (`PurchaseAccountId`),
  KEY `Item_Sales_account_idx` (`SalesAccountId`),
  KEY `Item_Purchase_TaxRate_idx` (`PurchaseTaxRateId`),
  KEY `Item_Sales_TaxRate_idx` (`SalesTaxRateId`),
  KEY `FK_Item_ItemCategory_idx` (`ItemCategoryId`),
  CONSTRAINT `FK_item_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Item_ItemCategory` FOREIGN KEY (`ItemCategoryId`) REFERENCES `itemcategory` (`Id`),
  CONSTRAINT `FK_Item_Purchase_account` FOREIGN KEY (`PurchaseAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Item_Purchase_TaxRate` FOREIGN KEY (`PurchaseTaxRateId`) REFERENCES `taxrate` (`Id`),
  CONSTRAINT `FK_Item_Sales_account` FOREIGN KEY (`SalesAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Item_Sales_TaxRate` FOREIGN KEY (`SalesTaxRateId`) REFERENCES `taxrate` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=20239 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `item_bak`
--

DROP TABLE IF EXISTS `item_bak`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `item_bak` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(20) DEFAULT NULL,
  `Name` varchar(150) NOT NULL,
  `Type` char(1) NOT NULL,
  `Unit` varchar(45) NOT NULL,
  `ItemCategoryId` int DEFAULT NULL,
  `ToPurchase` tinyint NOT NULL DEFAULT '1',
  `Cost` decimal(20,4) DEFAULT '0.0000',
  `PurchaseAccountId` int DEFAULT NULL,
  `PurchaseTaxRateId` int DEFAULT NULL,
  `ToSell` tinyint NOT NULL DEFAULT '1',
  `Rate` decimal(20,4) DEFAULT '0.0000',
  `SalesAccountId` int DEFAULT NULL,
  `SalesTaxRateId` int DEFAULT NULL,
  `Notes` longtext,
  `TrackInventory` tinyint(1) DEFAULT NULL,
  `InventoryAccountId` int DEFAULT NULL,
  `OpeningQuantity` decimal(20,2) DEFAULT '0.00',
  `OpeningQuantityDate` datetime DEFAULT NULL,
  `ReorderPoint` decimal(20,2) DEFAULT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '1',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code_UNIQUE` (`UserConfigId`,`Code`),
  KEY `Item_bak_Purchase_account_idx` (`PurchaseAccountId`),
  KEY `Item_bak_Sales_account_idx` (`SalesAccountId`),
  KEY `Item_bak_Purchase_TaxRate_idx` (`PurchaseTaxRateId`),
  KEY `Item_bak_Sales_TaxRate_idx` (`SalesTaxRateId`),
  CONSTRAINT `FK_item_bak_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Item_bak_Purchase_account` FOREIGN KEY (`PurchaseAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Item_bak_Purchase_TaxRate` FOREIGN KEY (`PurchaseTaxRateId`) REFERENCES `taxrate` (`Id`),
  CONSTRAINT `FK_Item_bak_Sales_account` FOREIGN KEY (`SalesAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Item_bak_Sales_TaxRate` FOREIGN KEY (`SalesTaxRateId`) REFERENCES `taxrate` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=18518 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `itemcategory`
--

DROP TABLE IF EXISTS `itemcategory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `itemcategory` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '1',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `journalentry`
--

DROP TABLE IF EXISTS `journalentry`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `journalentry` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `JournalDate` datetime(6) NOT NULL,
  `AccountId` int NOT NULL,
  `Amount` decimal(20,4) NOT NULL,
  `Balance` decimal(20,4) NOT NULL,
  `Nature` char(1) NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `SupplierId` int DEFAULT NULL,
  `DebtorId` int DEFAULT NULL,
  `CreditorId` int DEFAULT NULL,
  `Notes` text,
  `Source` varchar(4) NOT NULL,
  `CurrencyXRate` decimal(10,4) DEFAULT NULL,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `PostedByUserId` int DEFAULT NULL,
  `SalesInvoiceId` int DEFAULT NULL,
  `SalesInvoicePaymentId` int DEFAULT NULL,
  `SalesReceiptId` int DEFAULT NULL,
  `BillId` int DEFAULT NULL,
  `BillPaymentId` int DEFAULT NULL,
  `ExpensePaymentId` int DEFAULT NULL,
  `PaymentId` int DEFAULT NULL,
  `GeneralJournalId` int DEFAULT NULL,
  `PaymentToJournalEntryId` int DEFAULT NULL,
  `InventoryAdjustmentId` int DEFAULT NULL,
  `StockIssuanceId` int DEFAULT NULL,
  `ReceivingReportId` int DEFAULT NULL,
  `TaxRateId` int DEFAULT NULL,
  `DueDate` datetime DEFAULT NULL,
  `IsComputed` tinyint DEFAULT NULL,
  `Particular` text,
  `Payee` varchar(150) DEFAULT NULL,
  `Payor` varchar(150) DEFAULT NULL,
  `PaymentAdjustmentEntry` json DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_JournalEntry_Account_idx` (`AccountId`),
  KEY `FK_JournalEntry_Debtor_idx` (`DebtorId`),
  KEY `FK_JournalEntry_Creditor_idx` (`CreditorId`),
  KEY `FK_Journal_Entry_Customer_idx` (`CustomerId`),
  KEY `FK_JournalEntry_SaleslJournal_idx` (`SalesInvoiceId`),
  KEY `FK_JournalEntry_GeneralJournal_idx` (`GeneralJournalId`),
  KEY `FK_JournalEntry_SalesInvoicePayment_idx` (`SalesInvoicePaymentId`),
  KEY `FK_JournalEntry_PaymentToJournalEntry_idx` (`PaymentToJournalEntryId`),
  KEY `FK_JournalEntry_SaleslReceipt` (`SalesReceiptId`),
  KEY `FK_JournalEntry_Bill` (`BillId`),
  KEY `FK_JournalEntry_TaxRate` (`TaxRateId`),
  KEY `FK_JournalEntry_payment` (`PaymentId`),
  KEY `FK_journalentry_config` (`UserConfigId`),
  KEY `FK_JournalEntry_InventoryAdjustment` (`InventoryAdjustmentId`),
  KEY `FK_JournalEntry_StockIssuance` (`StockIssuanceId`),
  KEY `IDX_JournalEntry_Balance` (`Balance`),
  CONSTRAINT `FK_JournalEntry_Account` FOREIGN KEY (`AccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_JournalEntry_Bill` FOREIGN KEY (`BillId`) REFERENCES `bill` (`Id`),
  CONSTRAINT `FK_journalentry_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_JournalEntry_Creditor` FOREIGN KEY (`CreditorId`) REFERENCES `creditor` (`Id`),
  CONSTRAINT `FK_JournalEntry_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_JournalEntry_Debtor` FOREIGN KEY (`DebtorId`) REFERENCES `debtor` (`Id`),
  CONSTRAINT `FK_JournalEntry_GenrealJournal` FOREIGN KEY (`GeneralJournalId`) REFERENCES `generaljournal` (`Id`),
  CONSTRAINT `FK_JournalEntry_InventoryAdjustment` FOREIGN KEY (`InventoryAdjustmentId`) REFERENCES `inventoryadjustment` (`Id`),
  CONSTRAINT `FK_JournalEntry_payment` FOREIGN KEY (`PaymentId`) REFERENCES `payment` (`Id`),
  CONSTRAINT `FK_JournalEntry_PaymentToJournalEntry` FOREIGN KEY (`PaymentToJournalEntryId`) REFERENCES `journalentry` (`Id`),
  CONSTRAINT `FK_JournalEntry_SalesInvoicePayment` FOREIGN KEY (`SalesInvoicePaymentId`) REFERENCES `salesinvoicepayment` (`Id`),
  CONSTRAINT `FK_JournalEntry_SaleslInvoice` FOREIGN KEY (`SalesInvoiceId`) REFERENCES `salesinvoice` (`Id`),
  CONSTRAINT `FK_JournalEntry_SaleslReceipt` FOREIGN KEY (`SalesReceiptId`) REFERENCES `salesreceipt` (`Id`),
  CONSTRAINT `FK_JournalEntry_StockIssuance` FOREIGN KEY (`StockIssuanceId`) REFERENCES `stockissuance` (`Id`),
  CONSTRAINT `FK_JournalEntry_TaxRate` FOREIGN KEY (`TaxRateId`) REFERENCES `taxrate` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=369547 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `navigationitem`
--

DROP TABLE IF EXISTS `navigationitem`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `navigationitem` (
  `Id` int NOT NULL,
  `Title` varchar(100) NOT NULL,
  `Subtitle` varchar(100) DEFAULT NULL,
  `Type` varchar(100) DEFAULT NULL,
  `Icon` varchar(100) DEFAULT NULL,
  `Link` varchar(100) DEFAULT NULL,
  `ParentId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_NavigationItem_Parent_idx` (`ParentId`),
  CONSTRAINT `FK_NavigationItem_Parent` FOREIGN KEY (`ParentId`) REFERENCES `navigationitem` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `payment`
--

DROP TABLE IF EXISTS `payment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `payment` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `SupplierId` int DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `IsBillPayment` tinyint NOT NULL,
  `Payee` varchar(150) NOT NULL,
  `PaymentModeId` int NOT NULL,
  `CheckNo` varchar(45) DEFAULT NULL,
  `PaidThroughAccountId` int NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `Amount` decimal(20,4) DEFAULT '0.0000',
  `Balance` decimal(20,4) DEFAULT '0.0000',
  `PostedDate` datetime DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_Payment_Supplier_idx` (`SupplierId`),
  KEY `FK_Payment_Customer_idx` (`CustomerId`),
  KEY `FK_Payment_Account_idx` (`PaidThroughAccountId`),
  KEY `FK_Payment_PaymentMode_idx` (`PaymentModeId`),
  CONSTRAINT `FK_Payment_Account` FOREIGN KEY (`PaidThroughAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_payment_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Payment_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_Payment_PaymentMode` FOREIGN KEY (`PaymentModeId`) REFERENCES `paymentmode` (`Id`),
  CONSTRAINT `FK_Payment_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=9675 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `paymentmode`
--

DROP TABLE IF EXISTS `paymentmode`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `paymentmode` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(100) NOT NULL,
  `IsActive` smallint NOT NULL DEFAULT '0',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `paymentterm`
--

DROP TABLE IF EXISTS `paymentterm`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `paymentterm` (
  `Id` int NOT NULL,
  `Code` varchar(10) NOT NULL,
  `Name` varchar(100) NOT NULL,
  `Days` smallint DEFAULT NULL,
  `IsActive` smallint NOT NULL DEFAULT '0',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `receivingreport`
--

DROP TABLE IF EXISTS `receivingreport`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `receivingreport` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `PurchaseOrderNo` varchar(50) DEFAULT NULL,
  `DeliveryReceiptNo` varchar(50) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL,
  `Balance` decimal(20,4) DEFAULT NULL,
  `DiscountAmount` varchar(45) DEFAULT NULL,
  `DiscountPercent` varchar(45) DEFAULT NULL,
  `IsTaxExclusive` tinyint(1) DEFAULT NULL,
  `DiscountIsBeforeTax` tinyint(1) DEFAULT NULL,
  `HasItemLevelDiscount` tinyint(1) DEFAULT NULL,
  `Taxes` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `CreditAccountId` int DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `SupplierId` int NOT NULL,
  `LandedCostsJson` json DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_Receiving_InventoryLocation_idx` (`InventoryLocationId`),
  KEY `FK_Receiving_ReceivingAccount_idx` (`CreditAccountId`),
  KEY `FK_Receiving_customer` (`CustomerId`),
  KEY `FK_Receiving_supplier` (`SupplierId`),
  CONSTRAINT `FK_Receiving_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_Receiving_customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_Receiving_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_Receiving_ReceivingAccount` FOREIGN KEY (`CreditAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_Receiving_supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `receivingreportdetail`
--

DROP TABLE IF EXISTS `receivingreportdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `receivingreportdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ReceivingReportId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,2) NOT NULL,
  `Rate` decimal(20,2) DEFAULT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT NULL,
  `DiscountPercent` decimal(20,4) DEFAULT NULL,
  `Amount` decimal(20,2) DEFAULT NULL,
  `TaxRateId` int DEFAULT NULL,
  `TaxAmount` decimal(20,4) DEFAULT '0.0000',
  `Notes` text,
  `InventoryLocationId` int NOT NULL,
  `LandedCost` decimal(20,4) DEFAULT NULL,
  `LandedCostJson` json DEFAULT NULL,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_ReceivingDetail_Item_idx` (`ItemId`),
  KEY `FK_ReceivingDetail_Receiving_idx` (`ReceivingReportId`),
  CONSTRAINT `FK_ReceivingDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_ReceivingDetail_Receiving` FOREIGN KEY (`ReceivingReportId`) REFERENCES `receivingreport` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `responsibilitycenter`
--

DROP TABLE IF EXISTS `responsibilitycenter`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `responsibilitycenter` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(10) DEFAULT NULL,
  `Name` varchar(50) NOT NULL,
  `Status` tinyint(1) NOT NULL DEFAULT '1',
  `ResponsibilityCenterTypeId` int NOT NULL,
  `Notes` longtext,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_ResponsibilityCenter_ResponsibilityCenterType_idx` (`ResponsibilityCenterTypeId`),
  KEY `FK_responsibilitycenter_config` (`UserConfigId`),
  CONSTRAINT `FK_responsibilitycenter_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_ResponsibilityCenter_ResponsibilityCenterType` FOREIGN KEY (`ResponsibilityCenterTypeId`) REFERENCES `responsibilitycentertype` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=97 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `responsibilitycenterledger`
--

DROP TABLE IF EXISTS `responsibilitycenterledger`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `responsibilitycenterledger` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `AccountId` int NOT NULL,
  `Period` datetime(6) NOT NULL,
  `ResponsibilityCenter1Id` smallint NOT NULL,
  `ResponsibilityCenter2Id` smallint NOT NULL,
  `ResponsibilityCenter3Id` smallint NOT NULL,
  `ResponsibilityCenter4Id` smallint DEFAULT NULL,
  `BeginBalance` decimal(20,4) NOT NULL,
  `TotalDebit` decimal(20,4) NOT NULL,
  `TotalCredit` decimal(20,4) NOT NULL,
  `BalanceEnd` decimal(20,4) DEFAULT NULL,
  `DateProcessed` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `responsibilitycentertype`
--

DROP TABLE IF EXISTS `responsibilitycentertype`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `responsibilitycentertype` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `IsActive` smallint NOT NULL,
  `RequiredBy` varchar(45) NOT NULL,
  `RequiredByTags` json DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_responsibilitycentertype_config` (`UserConfigId`),
  CONSTRAINT `FK_responsibilitycentertype_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=100 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salesinvoice`
--

DROP TABLE IF EXISTS `salesinvoice`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `salesinvoice` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `InvoiceNo` varchar(50) NOT NULL,
  `InvoiceDate` datetime(6) NOT NULL,
  `CustomerId` int NOT NULL,
  `SupplierId` int DEFAULT NULL,
  `BillingAddress` longtext,
  `BillingContactName` varchar(100) DEFAULT NULL,
  `BillingContactEmail` varchar(100) DEFAULT NULL,
  `ShippingAddress` longtext,
  `ShippingContactName` varchar(100) DEFAULT NULL,
  `ShippingContactEmail` varchar(100) DEFAULT NULL,
  `PaymentTermId` int NOT NULL,
  `DueDate` datetime(6) NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Balance` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Taxes` json DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `IsTaxExclusive` tinyint(1) DEFAULT NULL,
  `DiscountIsBeforeTax` tinyint(1) DEFAULT NULL,
  `HasItemLevelDiscount` tinyint(1) DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `PurchaseOrderNo` varchar(45) DEFAULT NULL,
  `TermsConditions` longtext,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_SalesInvoice` (`UserConfigId`,`InvoiceNo`),
  KEY `FK_SalesInvoice_Customer` (`CustomerId`),
  KEY `FK_SalesInvoice_PaymentTerm_idx` (`PaymentTermId`),
  KEY `FK_SalesInvoice_InventoryLocation_idx` (`InventoryLocationId`),
  KEY `FK_SalesInvoice_Supplier_idx` (`SupplierId`),
  CONSTRAINT `FK_salesinvoice_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_SalesInvoice_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_SalesInvoice_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_SalesInvoice_PaymenTerm` FOREIGN KEY (`PaymentTermId`) REFERENCES `paymentterm` (`Id`),
  CONSTRAINT `FK_SalesInvoice_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=20039 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salesinvoicedetail`
--

DROP TABLE IF EXISTS `salesinvoicedetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `salesinvoicedetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `SalesInvoiceId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,4) NOT NULL,
  `Cost` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Rate` decimal(20,4) NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL,
  `TaxRateId` int DEFAULT NULL,
  `TaxAmount` decimal(20,4) DEFAULT '0.0000',
  `TaxExemptAmount` decimal(20,4) DEFAULT NULL,
  `Notes` text,
  `IsInventoryTransaction` tinyint DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `PostedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_SalesInvoiceDetail_Item_idx` (`ItemId`),
  KEY `FK_SalesInvoiceDetail_SalesInvoice_idx` (`SalesInvoiceId`),
  KEY `FK_SalesInvoiceDetail_InventoryLocation_idx` (`InventoryLocationId`),
  CONSTRAINT `FK_SalesInvoiceDetail_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_SalesInvoiceDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_SalesInvoiceDetail_SalesInvoice` FOREIGN KEY (`SalesInvoiceId`) REFERENCES `salesinvoice` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=40473 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salesinvoicepayment`
--

DROP TABLE IF EXISTS `salesinvoicepayment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `salesinvoicepayment` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `CustomerId` int NOT NULL,
  `PaymentModeId` int NOT NULL,
  `DepositToAccountId` int NOT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `Amount` decimal(20,4) DEFAULT '0.0000',
  `Balance` decimal(20,4) DEFAULT '0.0000',
  `PostedDate` datetime DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_SalesInvoicePayment` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_SalesInvoicePayment_Customer_idx` (`CustomerId`),
  KEY `FK_SalesInvoicePayment_Account_idx` (`DepositToAccountId`),
  KEY `FK_SalesInvoicePayment_PaymentMode_idx` (`PaymentModeId`),
  CONSTRAINT `FK_SalesInvoicePayment_Account` FOREIGN KEY (`DepositToAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_salesinvoicepayment_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_SalesInvoicePayment_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_SalesInvoicePayment_PaymentMode` FOREIGN KEY (`PaymentModeId`) REFERENCES `paymentmode` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=10163 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salesreceipt`
--

DROP TABLE IF EXISTS `salesreceipt`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `salesreceipt` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReceiptNo` varchar(50) NOT NULL,
  `ReceiptDate` datetime(6) NOT NULL,
  `CustomerId` int NOT NULL,
  `BillingAddress` longtext,
  `BillingContactName` varchar(100) DEFAULT NULL,
  `BillingContactEmail` varchar(100) DEFAULT NULL,
  `ShippingAddress` longtext,
  `ShippingContactName` varchar(100) DEFAULT NULL,
  `ShippingContactEmail` varchar(100) DEFAULT NULL,
  `PaymentModeId` int NOT NULL,
  `PaymentDetails` json DEFAULT NULL,
  `DepositToAccountId` int NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Balance` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Taxes` json DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `IsTaxExclusive` tinyint(1) DEFAULT NULL,
  `DiscountIsBeforeTax` tinyint(1) DEFAULT NULL,
  `HasItemLevelDiscount` tinyint(1) DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `PurchaseOrderNo` varchar(45) DEFAULT NULL,
  `IsPOS` tinyint(1) DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_SalesReceipt` (`UserConfigId`,`ReceiptNo`),
  KEY `FK_SalesReceipt_Customer` (`CustomerId`),
  KEY `FK_SalesReceipt_PamentMode_idx` (`PaymentModeId`),
  KEY `FK_SalesReceipt_Account_idx` (`DepositToAccountId`),
  KEY `FK_SalesReceipt_InventoryLocation_idx` (`InventoryLocationId`),
  CONSTRAINT `FK_SalesReceipt_Account` FOREIGN KEY (`DepositToAccountId`) REFERENCES `account` (`Id`),
  CONSTRAINT `FK_salesreceipt_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_SalesReceipt_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_SalesReceipt_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_SalesReceipt_PaymentMode` FOREIGN KEY (`PaymentModeId`) REFERENCES `paymentmode` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=14089 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `salesreceiptdetail`
--

DROP TABLE IF EXISTS `salesreceiptdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `salesreceiptdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `SalesReceiptId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,4) NOT NULL,
  `Cost` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `Rate` decimal(20,4) NOT NULL,
  `DiscountAmount` decimal(20,4) DEFAULT '0.0000',
  `DiscountPercent` decimal(4,2) DEFAULT NULL,
  `Amount` decimal(20,4) NOT NULL,
  `TaxRateId` int DEFAULT NULL,
  `TaxAmount` decimal(20,4) DEFAULT '0.0000',
  `TaxExemptAmount` decimal(20,4) DEFAULT NULL,
  `Notes` text,
  `IsInventoryTransaction` tinyint DEFAULT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `PostedByUserId` int DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_SalesReceiptDetail_Item_idx` (`ItemId`),
  KEY `FK_SalesReceiptDetail_SalesReceipt_idx` (`SalesReceiptId`),
  KEY `FK_SalesReceiptDetail_InventoryLocation_idx` (`InventoryLocationId`),
  CONSTRAINT `FK_SalesReceiptDetail_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_SalesReceiptDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_SalesReceiptDetail_SalesReceipt` FOREIGN KEY (`SalesReceiptId`) REFERENCES `salesreceipt` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=19426 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `stateprovince`
--

DROP TABLE IF EXISTS `stateprovince`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `stateprovince` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(150) NOT NULL,
  `Capital` varchar(150) DEFAULT NULL,
  `CountryId` int NOT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_StateProvince_Country` (`CountryId`),
  CONSTRAINT `FK_StateProvince_Country` FOREIGN KEY (`CountryId`) REFERENCES `country` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `stockissuance`
--

DROP TABLE IF EXISTS `stockissuance`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `stockissuance` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `InventoryLocationId` int DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `CustomerId` int DEFAULT NULL,
  `SupplierId` int DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_StockIssuance_InventoryLocation_idx` (`InventoryLocationId`),
  KEY `FK_StockIssuance_customer` (`CustomerId`),
  KEY `FK_StockIssuance_supplier` (`SupplierId`),
  CONSTRAINT `FK_StockIssuance_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_StockIssuance_customer` FOREIGN KEY (`CustomerId`) REFERENCES `customer` (`Id`),
  CONSTRAINT `FK_StockIssuance_InventoryLocation` FOREIGN KEY (`InventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_StockIssuance_supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `stockissuancedetail`
--

DROP TABLE IF EXISTS `stockissuancedetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `stockissuancedetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `StockIssuanceId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,2) NOT NULL,
  `Cost` decimal(20,2) DEFAULT NULL,
  `Notes` text,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_StockIssuanceDetail_Item_idx` (`ItemId`),
  KEY `FK_StockIssuanceDetail_StockTransfer_idx` (`StockIssuanceId`),
  CONSTRAINT `FK_StockIssuanceDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_StockIssuanceDetail_StockTrans` FOREIGN KEY (`StockIssuanceId`) REFERENCES `stockissuance` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `stocktransfer`
--

DROP TABLE IF EXISTS `stocktransfer`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `stocktransfer` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `ReferenceNo` varchar(50) NOT NULL,
  `ReferenceDate` datetime(6) NOT NULL,
  `Notes` longtext,
  `Status` smallint NOT NULL,
  `FromInventoryLocationId` int DEFAULT NULL,
  `ToInventoryLocationId` int DEFAULT NULL,
  `ResponsibilityCenterEntry` json DEFAULT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ReferenceNo` (`UserConfigId`,`ReferenceNo`),
  KEY `FK_StockTransfer_FromInventoryLocation_idx` (`FromInventoryLocationId`),
  KEY `FK_StockTransfer_ToInventoryLocation_idx` (`ToInventoryLocationId`),
  CONSTRAINT `FK_stocktransfer_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_StockTransfer_FromInventoryLocation` FOREIGN KEY (`FromInventoryLocationId`) REFERENCES `inventorylocation` (`Id`),
  CONSTRAINT `FK_StockTransfer_ToInventoryLocation` FOREIGN KEY (`ToInventoryLocationId`) REFERENCES `inventorylocation` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=3260 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `stocktransferdetail`
--

DROP TABLE IF EXISTS `stocktransferdetail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `stocktransferdetail` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `StockTransferId` int NOT NULL,
  `ItemId` int NOT NULL,
  `Quantity` decimal(20,2) NOT NULL,
  `Notes` text,
  `Status` smallint NOT NULL,
  `PostedDate` datetime DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_StockTransferDetail_Item_idx` (`ItemId`),
  KEY `FK_StockTransferDetail_StockTransfer_idx` (`StockTransferId`),
  CONSTRAINT `FK_StockTransferDetail_Item` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`),
  CONSTRAINT `FK_StockTransferDetail_StockTrans` FOREIGN KEY (`StockTransferId`) REFERENCES `stocktransfer` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=21126 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `subscriptionplan`
--

DROP TABLE IF EXISTS `subscriptionplan`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `subscriptionplan` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(150) NOT NULL,
  `Notes` text NOT NULL,
  `PriceBilledMonthly` decimal(20,4) NOT NULL,
  `PriceBilledYearly` decimal(20,4) NOT NULL,
  `MinimumUsers` smallint DEFAULT NULL,
  `PricePerAdditionalUser` decimal(20,4) DEFAULT NULL,
  `IsActive` tinyint NOT NULL,
  `Data` json DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `supplier`
--

DROP TABLE IF EXISTS `supplier`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `supplier` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Code` varchar(20) DEFAULT NULL,
  `Name` varchar(100) NOT NULL,
  `TIN` varchar(50) DEFAULT NULL,
  `IsVatReg` tinyint(1) DEFAULT '0',
  `PaymentTermId` int DEFAULT NULL,
  `TaxRateId` int DEFAULT NULL,
  `Notes` longtext,
  `Status` tinyint NOT NULL DEFAULT '0',
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_supplier_config` (`UserConfigId`),
  CONSTRAINT `FK_supplier_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=260 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `supplieraddress`
--

DROP TABLE IF EXISTS `supplieraddress`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `supplieraddress` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `SupplierId` int NOT NULL,
  `AddressLine1` varchar(150) DEFAULT NULL,
  `AddressLine2` varchar(150) DEFAULT NULL,
  `CityMunicipalityId` int DEFAULT NULL,
  `PostalCode` varchar(20) DEFAULT NULL,
  `IsPrimaryAddress` tinyint(1) NOT NULL DEFAULT '1',
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_SupplierAddress_Supplier_idx` (`SupplierId`),
  CONSTRAINT `FK_SupplierAddress_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=47 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `suppliercontact`
--

DROP TABLE IF EXISTS `suppliercontact`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `suppliercontact` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `SupplierId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Title` varchar(150) DEFAULT NULL,
  `Email` varchar(150) NOT NULL,
  `PhoneNo` varchar(20) NOT NULL,
  `AlternatePhoneNo` varchar(20) DEFAULT NULL,
  `AlternateEmail` varchar(150) DEFAULT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  `CreatedDate` datetime NOT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_SupplierContact_Supplier_idx` (`SupplierId`),
  CONSTRAINT `FK_SupplierContact_Supplier` FOREIGN KEY (`SupplierId`) REFERENCES `supplier` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=24 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `taxrate`
--

DROP TABLE IF EXISTS `taxrate`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `taxrate` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Name` varchar(150) NOT NULL,
  `Rate` decimal(20,4) NOT NULL DEFAULT '0.0000',
  `TaxAccountId` int DEFAULT NULL,
  `ApplyToSalesOrPurchase` varchar(2) DEFAULT NULL,
  `SalesAccountId` int DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_TaxRate_Tax_Account_idx` (`TaxAccountId`),
  KEY `FK_taxrate_config` (`UserConfigId`),
  CONSTRAINT `FK_taxrate_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `transactionsequence`
--

DROP TABLE IF EXISTS `transactionsequence`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `transactionsequence` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int NOT NULL,
  `Source` varchar(10) NOT NULL,
  `LastSequence` int NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_Sequences_Config_idx` (`UserConfigId`),
  CONSTRAINT `FK_Sequences_Config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `user`
--

DROP TABLE IF EXISTS `user`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `user` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Username` varchar(50) DEFAULT NULL,
  `Email` varchar(150) NOT NULL,
  `MobileNo` varchar(20) DEFAULT NULL,
  `Password` varchar(250) NOT NULL,
  `Name` varchar(150) NOT NULL,
  `UserTypeId` int DEFAULT NULL,
  `Avatar` varchar(150) DEFAULT NULL,
  `Status` smallint DEFAULT NULL,
  `ConfigId` int DEFAULT NULL,
  `UserRoleId` int DEFAULT NULL,
  `UserUIConfig` json DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Email_UNIQUE` (`Email`),
  UNIQUE KEY `Username_UNIQUE` (`Username`),
  UNIQUE KEY `MobileNo_UNIQUE` (`MobileNo`),
  KEY `FK_User_UserRole` (`UserRoleId`),
  KEY `FK_User_Config` (`ConfigId`),
  CONSTRAINT `FK_User_Config` FOREIGN KEY (`ConfigId`) REFERENCES `config` (`Id`),
  CONSTRAINT `FK_User_UserRole` FOREIGN KEY (`UserRoleId`) REFERENCES `userrole` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=60 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `userlog`
--

DROP TABLE IF EXISTS `userlog`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `userlog` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `Email` varchar(150) NOT NULL,
  `Name` varchar(150) DEFAULT NULL,
  `SignInDate` datetime NOT NULL,
  `AccessToken` varchar(255) DEFAULT NULL,
  `Platform` varchar(45) DEFAULT NULL,
  `RefreshToken` varchar(255) DEFAULT NULL,
  `ExpiryDate` datetime DEFAULT NULL,
  `IsRevoked` tinyint DEFAULT NULL,
  `PlatformVersion` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `IDX_RefreshToken` (`RefreshToken`)
) ENGINE=InnoDB AUTO_INCREMENT=16287 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `userrole`
--

DROP TABLE IF EXISTS `userrole`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `userrole` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserConfigId` int DEFAULT NULL,
  `Name` varchar(150) NOT NULL,
  `Notes` longtext,
  `Permission` json DEFAULT NULL,
  `AdvancePermission` json DEFAULT NULL,
  `ColumnRestriction` json DEFAULT NULL,
  `MobileAppPermission` json DEFAULT NULL,
  `IsAdmin` tinyint(1) NOT NULL DEFAULT '0',
  `EnableAIChatBot` tinyint(1) DEFAULT NULL,
  `CreatedDate` datetime(6) DEFAULT NULL,
  `LastUpdatedDate` datetime(6) DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `FK_userrole_config` (`UserConfigId`),
  CONSTRAINT `FK_userrole_config` FOREIGN KEY (`UserConfigId`) REFERENCES `config` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `usertype`
--

DROP TABLE IF EXISTS `usertype`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `usertype` (
  `Id` int NOT NULL,
  `Name` varchar(50) NOT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `voucherconfig`
--

DROP TABLE IF EXISTS `voucherconfig`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `voucherconfig` (
  `Id` int NOT NULL,
  `VoucherType` char(2) NOT NULL,
  `ControlNoResetTypeId` int DEFAULT NULL,
  `PrintNumCopy` int DEFAULT '0',
  `SignatoryName1` varchar(100) DEFAULT NULL,
  `SignatoryPosition1` varchar(100) DEFAULT NULL,
  `SignatoryCaption1` varchar(100) DEFAULT NULL,
  `SignatoryName2` varchar(100) DEFAULT NULL,
  `SignatoryPosition2` varchar(100) DEFAULT NULL,
  `SignatoryCaption2` varchar(100) DEFAULT NULL,
  `SignatoryName3` varchar(100) DEFAULT NULL,
  `SignatoryPosition3` varchar(100) DEFAULT NULL,
  `SignatoryCaption3` varchar(100) DEFAULT NULL,
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_vouchdefault` (`VoucherType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `vouchercontrolno`
--

DROP TABLE IF EXISTS `vouchercontrolno`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `vouchercontrolno` (
  `Id` int NOT NULL,
  `Period` datetime(6) NOT NULL,
  `VoucherType` char(2) NOT NULL,
  `LastControlNo` int NOT NULL DEFAULT '0',
  `CreatedDate` datetime DEFAULT NULL,
  `LastUpdatedDate` datetime DEFAULT NULL,
  `CreatedByUserId` int DEFAULT NULL,
  `LastUpdatedByUserId` int DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed
