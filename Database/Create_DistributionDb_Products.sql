-- Create database and Products table for Distribution Management System
-- Run this script in SQL Server (e.g. SSMS)

IF DB_ID('DistributionDb') IS NULL
BEGIN
    CREATE DATABASE DistributionDb;
END
GO

USE DistributionDb;
GO

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
    DROP TABLE dbo.Products;
GO

CREATE TABLE dbo.Products
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    PurchasePrice DECIMAL(18,2) NOT NULL,
    SalePrice DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);
GO
