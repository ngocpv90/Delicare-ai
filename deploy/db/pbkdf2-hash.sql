-- T48.3: tính hash mật khẩu PBKDF2-HMAC-SHA256 ngay trong SQL Server – CÙNG định dạng với
-- EncriptFunctionsHelper.GeneratePassword (T48.0, D-29): PBKDF2$100000$<salt 16 byte base64>$<hash 32 byte base64>.
-- Dùng trong deploy/docker/db-init.sh (container chỉ có sqlcmd, không có .NET / python / openssl).
-- KHÔNG tạo / sửa object nào trong DB, không đọc / ghi bảng: chỉ tính rồi SELECT ra 1 dòng.
-- Biến sqlcmd: PasswordHex = mật khẩu dạng UTF-8 đổi sang hex (không có 0x). db-init.sh truyền qua file tạm, không qua dòng lệnh.
SET NOCOUNT ON;
DECLARE @Iterations INT = 100000;
DECLARE @Password VARBINARY(MAX) = CONVERT(VARBINARY(MAX), N'$(PasswordHex)', 2);
IF DATALENGTH(@Password) IS NULL OR DATALENGTH(@Password) = 0
BEGIN
    RAISERROR(N'Mật khẩu rỗng.', 16, 1);
    RETURN;
END
DECLARE @Salt VARBINARY(16) = CRYPT_GEN_RANDOM(16);

-- HMAC-SHA256 (RFC 2104): khóa > 64 byte thì băm trước, rồi đệm 0x00 bên phải cho đủ 64 byte.
IF DATALENGTH(@Password) > 64 SET @Password = HASHBYTES('SHA2_256', @Password);
DECLARE @Key BINARY(64) = CAST(@Password AS BINARY(64));
DECLARE @InnerPad VARBINARY(64) = 0x, @OuterPad VARBINARY(64) = 0x, @i INT = 0, @Chunk BIGINT;
WHILE @i < 8
BEGIN
    SET @Chunk = CAST(SUBSTRING(@Key, @i * 8 + 1, 8) AS BIGINT);
    SET @InnerPad = @InnerPad + CAST(@Chunk ^ CAST(0x3636363636363636 AS BIGINT) AS BINARY(8));
    SET @OuterPad = @OuterPad + CAST(@Chunk ^ CAST(0x5C5C5C5C5C5C5C5C AS BIGINT) AS BINARY(8));
    SET @i += 1;
END

-- PBKDF2 (RFC 8018), độ dài khóa 32 byte = đúng 1 khối SHA-256: T = U1 xor U2 xor ... xor U100000.
DECLARE @U VARBINARY(32) = HASHBYTES('SHA2_256', @OuterPad + HASHBYTES('SHA2_256', @InnerPad + @Salt + 0x00000001));
DECLARE @T1 BIGINT = CAST(SUBSTRING(@U, 1, 8) AS BIGINT),
        @T2 BIGINT = CAST(SUBSTRING(@U, 9, 8) AS BIGINT),
        @T3 BIGINT = CAST(SUBSTRING(@U, 17, 8) AS BIGINT),
        @T4 BIGINT = CAST(SUBSTRING(@U, 25, 8) AS BIGINT),
        @n INT = 1;
WHILE @n < @Iterations
BEGIN
    SET @U = HASHBYTES('SHA2_256', @OuterPad + HASHBYTES('SHA2_256', @InnerPad + @U));
    SET @T1 ^= CAST(SUBSTRING(@U, 1, 8) AS BIGINT);
    SET @T2 ^= CAST(SUBSTRING(@U, 9, 8) AS BIGINT);
    SET @T3 ^= CAST(SUBSTRING(@U, 17, 8) AS BIGINT);
    SET @T4 ^= CAST(SUBSTRING(@U, 25, 8) AS BIGINT);
    SET @n += 1;
END
DECLARE @Hash VARBINARY(32) = CAST(@T1 AS BINARY(8)) + CAST(@T2 AS BINARY(8)) + CAST(@T3 AS BINARY(8)) + CAST(@T4 AS BINARY(8));

SELECT 'PBKDF2$' + CAST(@Iterations AS VARCHAR(10)) + '$'
     + CAST(N'' AS XML).value('xs:base64Binary(sql:variable("@Salt"))', 'VARCHAR(100)') + '$'
     + CAST(N'' AS XML).value('xs:base64Binary(sql:variable("@Hash"))', 'VARCHAR(100)') AS PasswordHash;
