-- Run once against the intended API v2 database before deploying the preference endpoint.
-- Additive: does not change legacy user.UserUIConfig or business records.
CREATE TABLE IF NOT EXISTS `user_page_preference` (
  `UserId` int NOT NULL,
  `CompanyId` int NOT NULL,
  `PageKey` varchar(64) NOT NULL,
  `ColumnsJson` json NOT NULL,
  `Version` bigint NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`UserId`, `CompanyId`, `PageKey`),
  KEY `IX_user_page_preference_CompanyId` (`CompanyId`),
  CONSTRAINT `FK_user_page_preference_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_user_page_preference_config_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `config` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
