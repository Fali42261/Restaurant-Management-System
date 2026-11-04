IF NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name=N'auth') EXEC(N'CREATE SCHEMA auth');
GO
IF OBJECT_ID(N'auth.Users',N'U') IS NULL
BEGIN
 CREATE TABLE auth.Users(
  UserId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuthUsers PRIMARY KEY,
  FullName NVARCHAR(120) NOT NULL,
  Email NVARCHAR(254) NOT NULL,
  CountryCode NVARCHAR(8) NULL,
  MobileNumber NVARCHAR(30) NULL,
  PasswordHash NVARCHAR(500) NOT NULL,
  IsActive BIT NOT NULL CONSTRAINT DF_AuthUsers_IsActive DEFAULT(1),
  IsEmailVerified BIT NOT NULL CONSTRAINT DF_AuthUsers_IsEmailVerified DEFAULT(0),
  IsDeleted BIT NOT NULL CONSTRAINT DF_AuthUsers_IsDeleted DEFAULT(0),
  CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AuthUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
  UpdatedAtUtc DATETIME2(3) NULL
 );
END
ELSE IF COL_LENGTH(N'auth.Users',N'CountryCode') IS NULL
BEGIN
 ALTER TABLE auth.Users ADD CountryCode NVARCHAR(8) NULL;
END
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'auth.Users') AND name=N'UX_AuthUsers_Email')
 CREATE UNIQUE INDEX UX_AuthUsers_Email ON auth.Users(Email) WHERE IsDeleted=0;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'auth.Users') AND name=N'UX_AuthUsers_Mobile')
 CREATE UNIQUE INDEX UX_AuthUsers_Mobile ON auth.Users(MobileNumber) WHERE MobileNumber IS NOT NULL AND IsDeleted=0;
GO
IF OBJECT_ID(N'auth.Roles',N'U') IS NULL
 CREATE TABLE auth.Roles(RoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuthRoles PRIMARY KEY,RoleName NVARCHAR(50) NOT NULL CONSTRAINT UQ_AuthRoles_RoleName UNIQUE);
IF OBJECT_ID(N'auth.UserRoles',N'U') IS NULL
 CREATE TABLE auth.UserRoles(UserId BIGINT NOT NULL,RoleId INT NOT NULL,CONSTRAINT PK_AuthUserRoles PRIMARY KEY(UserId,RoleId),CONSTRAINT FK_AuthUserRoles_Users FOREIGN KEY(UserId) REFERENCES auth.Users(UserId),CONSTRAINT FK_AuthUserRoles_Roles FOREIGN KEY(RoleId) REFERENCES auth.Roles(RoleId));
IF OBJECT_ID(N'auth.RefreshTokens',N'U') IS NULL
 CREATE TABLE auth.RefreshTokens(RefreshTokenId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuthRefreshTokens PRIMARY KEY,UserId BIGINT NOT NULL,TokenHash CHAR(64) NOT NULL CONSTRAINT UQ_AuthRefreshTokens_TokenHash UNIQUE,ExpiresAtUtc DATETIME2(3) NOT NULL,CreatedAtUtc DATETIME2(3) NOT NULL,RevokedAtUtc DATETIME2(3) NULL,ReplacedByTokenHash CHAR(64) NULL,CONSTRAINT FK_AuthRefreshTokens_Users FOREIGN KEY(UserId) REFERENCES auth.Users(UserId));
GO
INSERT auth.Roles(RoleName)
SELECT v.RoleName FROM(VALUES(N'Owner'),(N'Admin'),(N'Manager'),(N'Cashier'),(N'Kitchen'),(N'Waiter'),(N'Staff'))v(RoleName)
WHERE NOT EXISTS(SELECT 1 FROM auth.Roles r WHERE r.RoleName=v.RoleName);
GO
