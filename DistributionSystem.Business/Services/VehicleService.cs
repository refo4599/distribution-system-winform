using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;

namespace DistributionSystem.Business.Services
{
    public class VehicleService : BaseService
    {
        private readonly SqlConnectionFactory _factory;

        public VehicleService()
        {
            _factory = new SqlConnectionFactory();
            EnsureSalePriceColumn();
            EnsureBoxesPerCartonColumn();
            EnsureReturnTables();
        }

        public void LogException(Exception ex) => LogError(ex);

        // ═══════════════════════════════════════════════════════
        //  ENSURE COLUMNS / TABLES
        // ═══════════════════════════════════════════════════════
        private void EnsureSalePriceColumn()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    bool exists = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1)
                            FROM   INFORMATION_SCHEMA.COLUMNS
                            WHERE  TABLE_NAME  = 'DispatchOrderItems'
                              AND  COLUMN_NAME = 'SalePrice'";
                        exists = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }
                    if (!exists)
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText =
                                "ALTER TABLE DispatchOrderItems ADD SalePrice DECIMAL(18,2) NOT NULL DEFAULT 0";
                            alter.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
        }

        private void EnsureBoxesPerCartonColumn()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    bool exists = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1)
                            FROM   INFORMATION_SCHEMA.COLUMNS
                            WHERE  TABLE_NAME  = 'DispatchOrderItems'
                              AND  COLUMN_NAME = 'BoxesPerCarton'";
                        exists = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }
                    if (!exists)
                    {
                        using (var alter = conn.CreateCommand())
                        {
                            alter.CommandText =
                                "ALTER TABLE DispatchOrderItems ADD BoxesPerCarton INT NOT NULL DEFAULT 1";
                            alter.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
        }

        private void EnsureReturnTables()
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            IF NOT EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                                WHERE TABLE_NAME = 'ReturnOrders'
                            )
                            CREATE TABLE ReturnOrders (
                                Id              INT IDENTITY(1,1) PRIMARY KEY,
                                VehicleId       INT           NOT NULL,
                                DispatchOrderId INT           NULL,
                                Notes           NVARCHAR(500) NULL,
                                CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE()
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            IF NOT EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                                WHERE TABLE_NAME = 'ReturnOrderItems'
                            )
                            CREATE TABLE ReturnOrderItems (
                                Id            INT IDENTITY(1,1) PRIMARY KEY,
                                ReturnOrderId INT NOT NULL,
                                ProductId     INT NOT NULL,
                                Quantity      INT NOT NULL
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            IF NOT EXISTS (
                                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                                WHERE TABLE_NAME  = 'ReturnOrders'
                                  AND COLUMN_NAME = 'DispatchOrderId'
                            )
                            ALTER TABLE ReturnOrders ADD DispatchOrderId INT NULL";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════
        //  VEHICLES
        // ═══════════════════════════════════════════════════════
        public List<VehicleDto> GetAllVehicles()
        {
            return Execute(() =>
            {
                var list = new List<VehicleDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Id, Name, RepName, IsActive FROM Vehicles ORDER BY Name";
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                list.Add(new VehicleDto
                                {
                                    Id = rdr.GetInt32(0),
                                    Name = rdr.GetString(1),
                                    RepName = rdr.GetString(2),
                                    IsActive = rdr.GetBoolean(3)
                                });
                    }
                }
                return list;
            });
        }

        public int SaveVehicle(VehicleDto dto)
        {
            return Execute(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        if (dto.Id == 0)
                        {
                            cmd.CommandText = @"
                                INSERT INTO Vehicles (Name, RepName, IsActive)
                                VALUES (@Name, @RepName, 1);
                                SELECT SCOPE_IDENTITY();";
                        }
                        else
                        {
                            cmd.CommandText = @"
                                UPDATE Vehicles SET Name=@Name, RepName=@RepName
                                WHERE Id=@Id;
                                SELECT @Id;";
                            AddParam(cmd, "@Id", dto.Id);
                        }
                        AddParam(cmd, "@Name", dto.Name);
                        AddParam(cmd, "@RepName", dto.RepName);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            });
        }

        public void DeleteVehicle(int id)
        {
            Execute<object>(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE Vehicles SET IsActive=0 WHERE Id=@Id";
                        AddParam(cmd, "@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                return null;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  DISPATCH ORDERS — GET ALL
        // ═══════════════════════════════════════════════════════
        public List<DispatchOrderDto> GetAllDispatchOrders()
        {
            return Execute(() =>
            {
                var list = new List<DispatchOrderDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    bool hasSalePrice = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='DispatchOrderItems' AND COLUMN_NAME='SalePrice'";
                        hasSalePrice = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }

                    bool hasBpc = false;
                    using (var chk2 = conn.CreateCommand())
                    {
                        chk2.CommandText = @"
                            SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='DispatchOrderItems' AND COLUMN_NAME='BoxesPerCarton'";
                        hasBpc = Convert.ToInt32(chk2.ExecuteScalar()) > 0;
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        string bpcCol = hasBpc
                            ? "ISNULL(doi.BoxesPerCarton, 1) AS BoxesPerCarton"
                            : "ISNULL(p.BoxesPerCarton,   1) AS BoxesPerCarton";

                        string salePriceCol = hasSalePrice
                            ? @"CASE
                                    WHEN ISNULL(doi.SalePrice,0) = 0
                                    THEN ISNULL(p.SalePrice, 0)
                                    ELSE doi.SalePrice
                                END AS SalePrice"
                            : "ISNULL(p.SalePrice, 0) AS SalePrice";

                        cmd.CommandText = $@"
                            SELECT
                                d.Id, d.VehicleId, v.Name, v.RepName,
                                ISNULL(d.Notes,''), d.Status, d.CreatedAt,
                                doi.ProductId, ISNULL(p.Name,''),
                                ISNULL(doi.Quantity,0),
                                ISNULL(doi.UnitCost,0),
                                {salePriceCol},
                                {bpcCol}
                            FROM DispatchOrders d
                            JOIN Vehicles v ON v.Id = d.VehicleId
                            LEFT JOIN DispatchOrderItems doi ON doi.DispatchOrderId = d.Id
                            LEFT JOIN Products p ON p.Id = doi.ProductId
                            ORDER BY d.CreatedAt DESC";

                        using (var rdr = cmd.ExecuteReader())
                        {
                            var map = new Dictionary<int, DispatchOrderDto>();
                            while (rdr.Read())
                            {
                                int id = rdr.GetInt32(0);
                                if (!map.ContainsKey(id))
                                    map[id] = new DispatchOrderDto
                                    {
                                        Id = id,
                                        VehicleId = rdr.GetInt32(1),
                                        VehicleName = rdr.GetString(2),
                                        RepName = rdr.GetString(3),
                                        Notes = rdr.GetString(4),
                                        Status = rdr.GetString(5),
                                        CreatedAt = rdr.GetDateTime(6)
                                    };

                                if (!rdr.IsDBNull(7))
                                    map[id].Items.Add(new DispatchOrderItemDto
                                    {
                                        ProductId = rdr.GetInt32(7),
                                        ProductName = rdr.GetString(8),
                                        Quantity = rdr.GetInt32(9),
                                        UnitCost = rdr.GetDecimal(10),
                                        SalePrice = rdr.GetDecimal(11),
                                        BoxesPerCarton = rdr.GetInt32(12)
                                    });
                            }
                            list = map.Values.ToList();
                        }
                    }
                }
                return list;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  DISPATCH ORDERS — GET BY VEHICLE
        // ═══════════════════════════════════════════════════════
        public List<DispatchOrderDto> GetDispatchOrdersByVehicle(int vehicleId)
        {
            return Execute(() =>
            {
                var list = new List<DispatchOrderDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    bool hasBpc = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='DispatchOrderItems' AND COLUMN_NAME='BoxesPerCarton'";
                        hasBpc = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }

                    string bpcCol = hasBpc
                        ? "ISNULL(doi.BoxesPerCarton, 1)"
                        : "ISNULL(p.BoxesPerCarton,   1)";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $@"
                            SELECT
                                d.Id, d.VehicleId, v.Name, v.RepName,
                                ISNULL(d.Notes,''), d.Status, d.CreatedAt,
                                doi.ProductId, ISNULL(p.Name,''),
                                ISNULL(doi.Quantity,0),
                                ISNULL(doi.UnitCost,0),
                                ISNULL(doi.SalePrice,0),
                                {bpcCol} AS BoxesPerCarton
                            FROM DispatchOrders d
                            JOIN Vehicles v ON v.Id = d.VehicleId
                            LEFT JOIN DispatchOrderItems doi ON doi.DispatchOrderId = d.Id
                            LEFT JOIN Products p ON p.Id = doi.ProductId
                            WHERE d.VehicleId = @VehicleId
                              AND EXISTS (
                                  SELECT 1 FROM DispatchOrderItems di
                                  WHERE di.DispatchOrderId = d.Id AND di.Quantity > 0
                              )
                            ORDER BY d.CreatedAt DESC";
                        AddParam(cmd, "@VehicleId", vehicleId);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            var map = new Dictionary<int, DispatchOrderDto>();
                            while (rdr.Read())
                            {
                                int id = rdr.GetInt32(0);
                                if (!map.ContainsKey(id))
                                    map[id] = new DispatchOrderDto
                                    {
                                        Id = id,
                                        VehicleId = rdr.GetInt32(1),
                                        VehicleName = rdr.GetString(2),
                                        RepName = rdr.GetString(3),
                                        Notes = rdr.GetString(4),
                                        Status = rdr.GetString(5),
                                        CreatedAt = rdr.GetDateTime(6)
                                    };

                                if (!rdr.IsDBNull(7))
                                    map[id].Items.Add(new DispatchOrderItemDto
                                    {
                                        ProductId = rdr.GetInt32(7),
                                        ProductName = rdr.GetString(8),
                                        Quantity = rdr.GetInt32(9),
                                        UnitCost = rdr.GetDecimal(10),
                                        SalePrice = rdr.GetDecimal(11),
                                        BoxesPerCarton = rdr.GetInt32(12)
                                    });
                            }
                            list = map.Values.ToList();
                        }
                    }
                }
                return list;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  GET WAREHOUSE BALANCE FOR A SINGLE PRODUCT — PUBLIC
        // ═══════════════════════════════════════════════════════
        public int GetProductWarehouseBalance(int productId)
        {
            return Execute(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    return GetWarehouseBalance(productId, conn);
                }
            });
        }

        // ═══════════════════════════════════════════════════════
        //  GET WAREHOUSE BALANCE FOR A SINGLE PRODUCT — PRIVATE
        // ═══════════════════════════════════════════════════════
        private int GetWarehouseBalance(int productId, IDbConnection conn, IDbTransaction tran = null)
        {
            using (var cmd = conn.CreateCommand())
            {
                if (tran != null) cmd.Transaction = tran;
                cmd.CommandText = @"
                    SELECT ISNULL(SUM(CASE
                        WHEN TransactionType IN ('Inbound','Return','CarReturn') THEN  Quantity
                        WHEN TransactionType IN ('CarLoad')                      THEN -Quantity
                        ELSE 0
                    END), 0)
                    FROM WarehouseTransactions
                    WHERE ProductId = @ProductId";
                AddParam(cmd, "@ProductId", productId);
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  SAVE DISPATCH ORDER
        // ═══════════════════════════════════════════════════════
        public int SaveDispatchOrder(DispatchOrderDto dto)
        {
            return Execute(() =>
            {
                if (dto.Items == null || dto.Items.Count == 0)
                    throw new ArgumentException("يجب إضافة منتج واحد على الأقل.");

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    bool hasBpc = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='DispatchOrderItems' AND COLUMN_NAME='BoxesPerCarton'";
                        hasBpc = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }

                    var grouped = dto.Items
                        .GroupBy(i => i.ProductId)
                        .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(i => i.Quantity) })
                        .ToList();

                    foreach (var g in grouped)
                    {
                        int available = GetWarehouseBalance(g.ProductId, conn);
                        if (available <= 0 || g.TotalQty > available)
                        {
                            string productName = g.ProductId.ToString();
                            using (var nameCmd = conn.CreateCommand())
                            {
                                nameCmd.CommandText = "SELECT ISNULL(Name,'') FROM Products WHERE Id=@Id";
                                AddParam(nameCmd, "@Id", g.ProductId);
                                var n = nameCmd.ExecuteScalar();
                                if (n != null && n != DBNull.Value) productName = n.ToString();
                            }

                            if (available <= 0)
                                throw new InvalidOperationException(
                                    $"المنتج \"{productName}\" غير متوفر في المخزن (الرصيد = 0).");
                            else
                                throw new InvalidOperationException(
                                    $"الكمية المطلوبة ({g.TotalQty} علبة) أكبر من المتوفر في المخزن ({available} علبة) للمنتج \"{productName}\".");
                        }
                    }

                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            int dispatchId;
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    INSERT INTO DispatchOrders (VehicleId, Notes, Status, CreatedAt)
                                    VALUES (@VehicleId, @Notes, 'Active', GETDATE());
                                    SELECT SCOPE_IDENTITY();";
                                AddParam(cmd, "@VehicleId", dto.VehicleId);
                                AddParam(cmd, "@Notes", dto.Notes ?? "");
                                dispatchId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            foreach (var item in dto.Items)
                            {
                                decimal unitCost = 0;
                                using (var costCmd = conn.CreateCommand())
                                {
                                    costCmd.Transaction = tran;
                                    costCmd.CommandText = @"
                                        SELECT ISNULL(
                                            CAST(SUM(wt.Quantity * wt.PurchasePrice) AS DECIMAL(18,2)) /
                                            NULLIF(SUM(wt.Quantity), 0),
                                        0)
                                        FROM WarehouseTransactions wt
                                        WHERE wt.ProductId = @PId
                                          AND wt.TransactionType IN ('Inbound','Return')";
                                    AddParam(costCmd, "@PId", item.ProductId);
                                    var r = costCmd.ExecuteScalar();
                                    if (r != null && r != DBNull.Value)
                                        unitCost = Convert.ToDecimal(r);
                                }

                                using (var iCmd = conn.CreateCommand())
                                {
                                    iCmd.Transaction = tran;
                                    if (hasBpc)
                                    {
                                        iCmd.CommandText = @"
                                            INSERT INTO DispatchOrderItems
                                                (DispatchOrderId, ProductId, Quantity, UnitCost, SalePrice, BoxesPerCarton)
                                            VALUES
                                                (@DispatchId, @ProductId, @Qty, @UnitCost, @SalePrice, @Bpc)";
                                        AddParam(iCmd, "@Bpc", item.BoxesPerCarton > 0 ? item.BoxesPerCarton : 1);
                                    }
                                    else
                                    {
                                        iCmd.CommandText = @"
                                            INSERT INTO DispatchOrderItems
                                                (DispatchOrderId, ProductId, Quantity, UnitCost, SalePrice)
                                            VALUES
                                                (@DispatchId, @ProductId, @Qty, @UnitCost, @SalePrice)";
                                    }
                                    AddParam(iCmd, "@DispatchId", dispatchId);
                                    AddParam(iCmd, "@ProductId", item.ProductId);
                                    AddParam(iCmd, "@Qty", item.Quantity);
                                    AddParam(iCmd, "@UnitCost", unitCost);
                                    AddParam(iCmd, "@SalePrice", item.SalePrice);
                                    iCmd.ExecuteNonQuery();
                                }

                                using (var wtCmd = conn.CreateCommand())
                                {
                                    wtCmd.Transaction = tran;
                                    wtCmd.CommandText = @"
                                        INSERT INTO WarehouseTransactions
                                            (ProductId, Quantity, TransactionType, ReferenceId, PurchasePrice, CreatedAt)
                                        VALUES
                                            (@ProductId, @Qty, 'CarLoad', @RefId, @UnitCost, GETDATE())";
                                    AddParam(wtCmd, "@ProductId", item.ProductId);
                                    AddParam(wtCmd, "@Qty", item.Quantity);
                                    AddParam(wtCmd, "@RefId", dispatchId);
                                    AddParam(wtCmd, "@UnitCost", unitCost);
                                    wtCmd.ExecuteNonQuery();
                                }
                            }

                            tran.Commit();
                            return dispatchId;
                        }
                        catch
                        {
                            try { tran.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            });
        }

        // ═══════════════════════════════════════════════════════
        //  DELETE DISPATCH ORDER
        // ═══════════════════════════════════════════════════════
        public void DeleteDispatchOrder(int id)
        {
            Execute<object>(() =>
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    DELETE FROM WarehouseTransactions
                                    WHERE TransactionType = 'CarLoad' AND ReferenceId = @Id";
                                AddParam(cmd, "@Id", id);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd2 = conn.CreateCommand())
                            {
                                cmd2.Transaction = tran;
                                cmd2.CommandText = "DELETE FROM DispatchOrderItems WHERE DispatchOrderId=@Id";
                                AddParam(cmd2, "@Id", id);
                                cmd2.ExecuteNonQuery();
                            }
                            using (var cmd3 = conn.CreateCommand())
                            {
                                cmd3.Transaction = tran;
                                cmd3.CommandText = "DELETE FROM DispatchOrders WHERE Id=@Id";
                                AddParam(cmd3, "@Id", id);
                                cmd3.ExecuteNonQuery();
                            }
                            tran.Commit();
                        }
                        catch
                        {
                            try { tran.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
                return null;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  SAVE RETURN ORDER
        // ═══════════════════════════════════════════════════════
        public void SaveReturnOrder(ReturnOrderDto dto)
        {
            Execute<object>(() =>
            {
                if (dto.Items == null || dto.Items.Count == 0)
                    throw new ArgumentException("يجب إضافة منتج واحد على الأقل.");

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    if (dto.DispatchOrderId.HasValue)
                    {
                        using (var chkCmd = conn.CreateCommand())
                        {
                            foreach (var item in dto.Items)
                            {
                                chkCmd.CommandText = @"
                                    SELECT ISNULL(Quantity, 0)
                                    FROM   DispatchOrderItems
                                    WHERE  DispatchOrderId = @DId AND ProductId = @PId";
                                chkCmd.Parameters.Clear();
                                AddParam(chkCmd, "@DId", dto.DispatchOrderId.Value);
                                AddParam(chkCmd, "@PId", item.ProductId);
                                var existing = chkCmd.ExecuteScalar();
                                int currentQty = existing == null || existing == DBNull.Value
                                    ? 0 : Convert.ToInt32(existing);

                                if (item.Quantity > currentQty)
                                    throw new InvalidOperationException(
                                        $"الكمية المرتجعة ({item.Quantity} علبة) أكبر من الكمية في أمر الصرف ({currentQty} علبة).");
                            }
                        }
                    }

                    using (var tran = conn.BeginTransaction())
                    {
                        try
                        {
                            int returnId;
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tran;
                                cmd.CommandText = @"
                                    INSERT INTO ReturnOrders (VehicleId, DispatchOrderId, Notes, CreatedAt)
                                    VALUES (@VId, @DId, @Notes, GETDATE());
                                    SELECT SCOPE_IDENTITY();";
                                AddParam(cmd, "@VId", dto.VehicleId);
                                AddParam(cmd, "@DId", dto.DispatchOrderId.HasValue
                                                            ? (object)dto.DispatchOrderId.Value
                                                            : DBNull.Value);
                                AddParam(cmd, "@Notes", dto.Notes ?? "");
                                returnId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            foreach (var item in dto.Items)
                            {
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.Transaction = tran;
                                    cmd.CommandText = @"
                                        INSERT INTO ReturnOrderItems (ReturnOrderId, ProductId, Quantity)
                                        VALUES (@RId, @PId, @Qty)";
                                    AddParam(cmd, "@RId", returnId);
                                    AddParam(cmd, "@PId", item.ProductId);
                                    AddParam(cmd, "@Qty", item.Quantity);
                                    cmd.ExecuteNonQuery();
                                }

                                using (var wtCmd = conn.CreateCommand())
                                {
                                    wtCmd.Transaction = tran;
                                    wtCmd.CommandText = @"
                                        INSERT INTO WarehouseTransactions
                                            (ProductId, Quantity, TransactionType, ReferenceId, PurchasePrice, CreatedAt)
                                        VALUES
                                            (@ProductId, @Qty, 'CarReturn', @RefId, 0, GETDATE())";
                                    AddParam(wtCmd, "@ProductId", item.ProductId);
                                    AddParam(wtCmd, "@Qty", item.Quantity);
                                    AddParam(wtCmd, "@RefId", returnId);
                                    wtCmd.ExecuteNonQuery();
                                }

                                if (dto.DispatchOrderId.HasValue)
                                {
                                    using (var updCmd = conn.CreateCommand())
                                    {
                                        updCmd.Transaction = tran;
                                        updCmd.CommandText = @"
                                            UPDATE DispatchOrderItems
                                            SET    Quantity = Quantity - @Qty
                                            WHERE  DispatchOrderId = @DId AND ProductId = @PId;

                                            DELETE FROM DispatchOrderItems
                                            WHERE  DispatchOrderId = @DId
                                              AND  ProductId       = @PId
                                              AND  Quantity        <= 0;";
                                        AddParam(updCmd, "@Qty", item.Quantity);
                                        AddParam(updCmd, "@DId", dto.DispatchOrderId.Value);
                                        AddParam(updCmd, "@PId", item.ProductId);
                                        updCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            tran.Commit();
                        }
                        catch
                        {
                            try { tran.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
                return null;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  GET SOLD QUANTITIES BY DISPATCH
        //  ✅ التعديل الوحيد على الكود الأصلي:
        //     أضفنا التحقق من DispatchOrderId (ربط مباشر) قبل الـ time window
        //     الـ Fallback بالوقت لسه موجود للفواتير القديمة (DispatchOrderId = NULL)
        // ═══════════════════════════════════════════════════════
        public Dictionary<int, int> GetSoldQuantitiesByDispatch(int dispatchId)
        {
            return Execute(() =>
            {
                var result = new Dictionary<int, int>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();

                    // هل عمود DispatchOrderId موجود في SalesInvoices؟
                    bool hasDispatchCol = false;
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = @"
                            SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='SalesInvoices' AND COLUMN_NAME='DispatchOrderId'";
                        hasDispatchCol = Convert.ToInt32(chk.ExecuteScalar()) > 0;
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        if (hasDispatchCol)
                        {
                            // ✅ ربط مباشر بأمر الصرف + Fallback بالوقت للفواتير القديمة
                            cmd.CommandText = @"
                                DECLARE @VehicleId  INT
                                DECLARE @DispatchAt DATETIME

                                SELECT @VehicleId  = VehicleId,
                                       @DispatchAt = CreatedAt
                                FROM   DispatchOrders
                                WHERE  Id = @DispatchId

                                DECLARE @NextDispatchAt DATETIME
                                SELECT @NextDispatchAt = MIN(CreatedAt)
                                FROM   DispatchOrders
                                WHERE  VehicleId = @VehicleId
                                  AND  Id        <> @DispatchId
                                  AND  CreatedAt >  @DispatchAt

                                SELECT sii.ProductId, SUM(sii.Quantity) AS SoldQty
                                FROM   SalesInvoiceItems sii
                                JOIN   SalesInvoices si ON si.Id = sii.InvoiceId
                                WHERE  si.VehicleId = @VehicleId
                                  AND (
                                      -- ✅ مرتبطة مباشرة بأمر الصرف ده
                                      si.DispatchOrderId = @DispatchId
                                      OR
                                      -- ✅ Fallback بالوقت للفواتير القديمة (DispatchOrderId = NULL)
                                      (
                                          si.DispatchOrderId IS NULL
                                          AND si.CreatedAt >= @DispatchAt
                                          AND (@NextDispatchAt IS NULL OR si.CreatedAt < @NextDispatchAt)
                                      )
                                  )
                                GROUP BY sii.ProductId";
                        }
                        else
                        {
                            // Fallback كامل — للبيئات اللي العمود مش موجود فيها بعد
                            cmd.CommandText = @"
                                DECLARE @VehicleId  INT
                                DECLARE @DispatchAt DATETIME

                                SELECT @VehicleId  = VehicleId,
                                       @DispatchAt = CreatedAt
                                FROM   DispatchOrders
                                WHERE  Id = @DispatchId

                                DECLARE @NextDispatchAt DATETIME
                                SELECT @NextDispatchAt = MIN(CreatedAt)
                                FROM   DispatchOrders
                                WHERE  VehicleId = @VehicleId
                                  AND  Id        <> @DispatchId
                                  AND  CreatedAt >  @DispatchAt

                                SELECT sii.ProductId, SUM(sii.Quantity) AS SoldQty
                                FROM   SalesInvoiceItems sii
                                JOIN   SalesInvoices si ON si.Id = sii.InvoiceId
                                WHERE  si.VehicleId = @VehicleId
                                  AND  si.CreatedAt >= @DispatchAt
                                  AND  (@NextDispatchAt IS NULL OR si.CreatedAt < @NextDispatchAt)
                                GROUP BY sii.ProductId";
                        }

                        AddParam(cmd, "@DispatchId", dispatchId);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                result[rdr.GetInt32(0)] = rdr.GetInt32(1);
                    }
                }
                return result;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  GET RETURNED QUANTITIES BY DISPATCH
        // ═══════════════════════════════════════════════════════
        public Dictionary<int, int> GetReturnedQuantitiesByDispatch(int dispatchOrderId)
        {
            return Execute(() =>
            {
                var result = new Dictionary<int, int>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT roi.ProductId, SUM(roi.Quantity) AS ReturnedQty
                            FROM ReturnOrders ro
                            JOIN ReturnOrderItems roi ON roi.ReturnOrderId = ro.Id
                            WHERE ro.DispatchOrderId = @DispatchOrderId
                            GROUP BY roi.ProductId";
                        AddParam(cmd, "@DispatchOrderId", dispatchOrderId);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                result[rdr.GetInt32(0)] = rdr.GetInt32(1);
                    }
                }
                return result;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  RETURN ORDERS — GET ALL
        // ═══════════════════════════════════════════════════════
        public List<ReturnOrderDto> GetAllReturnOrders()
        {
            return Execute(() =>
            {
                var list = new List<ReturnOrderDto>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                r.Id, r.VehicleId, v.Name AS VehicleName, v.RepName,
                                ISNULL(r.Notes,'') AS Notes, r.CreatedAt,
                                ISNULL(r.DispatchOrderId, 0) AS DispatchOrderId,
                                roi.ProductId, ISNULL(p.Name,'') AS ProductName,
                                ISNULL(roi.Quantity,0) AS Quantity
                            FROM ReturnOrders r
                            JOIN Vehicles v ON v.Id = r.VehicleId
                            LEFT JOIN ReturnOrderItems roi ON roi.ReturnOrderId = r.Id
                            LEFT JOIN Products p ON p.Id = roi.ProductId
                            ORDER BY r.CreatedAt DESC";
                        using (var rdr = cmd.ExecuteReader())
                        {
                            var map = new Dictionary<int, ReturnOrderDto>();
                            while (rdr.Read())
                            {
                                int id = rdr.GetInt32(0);
                                if (!map.ContainsKey(id))
                                {
                                    int dId = rdr.GetInt32(6);
                                    map[id] = new ReturnOrderDto
                                    {
                                        Id = id,
                                        VehicleId = rdr.GetInt32(1),
                                        VehicleName = rdr.GetString(2),
                                        RepName = rdr.GetString(3),
                                        Notes = rdr.GetString(4),
                                        CreatedAt = rdr.GetDateTime(5),
                                        DispatchOrderId = dId > 0 ? (int?)dId : null
                                    };
                                }
                                if (!rdr.IsDBNull(7))
                                    map[id].Items.Add(new ReturnOrderItemDto
                                    {
                                        ProductId = rdr.GetInt32(7),
                                        ProductName = rdr.GetString(8),
                                        Quantity = rdr.GetInt32(9)
                                    });
                            }
                            list = map.Values.ToList();
                        }
                    }
                }
                return list;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  GET ORIGINAL QUANTITIES BY DISPATCH
        // ═══════════════════════════════════════════════════════
        public Dictionary<int, int> GetOriginalQuantitiesByDispatch(int dispatchOrderId)
        {
            return Execute(() =>
            {
                var result = new Dictionary<int, int>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT ProductId, Quantity
                            FROM   DispatchOrderItems
                            WHERE  DispatchOrderId = @DispatchOrderId";
                        AddParam(cmd, "@DispatchOrderId", dispatchOrderId);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                result[rdr.GetInt32(0)] = rdr.GetInt32(1);
                    }
                }
                return result;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  VEHICLE BALANCES
        // ═══════════════════════════════════════════════════════
        public Dictionary<int, int> GetVehicleAllBalances(int vehicleId)
        {
            return Execute(() =>
            {
                var result = new Dictionary<int, int>();
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                wt.ProductId,
                                SUM(CASE
                                    WHEN wt.TransactionType = 'CarLoad'   THEN  wt.Quantity
                                    WHEN wt.TransactionType = 'Outbound'  THEN -wt.Quantity
                                    WHEN wt.TransactionType = 'Return'    THEN -wt.Quantity
                                    WHEN wt.TransactionType = 'CarReturn' THEN -wt.Quantity
                                    ELSE 0
                                END) AS Balance
                            FROM WarehouseTransactions wt
                            WHERE (
                                (wt.TransactionType = 'CarLoad' AND wt.ReferenceId IN
                                    (SELECT Id FROM DispatchOrders WHERE VehicleId = @VehicleId))
                                OR
                                (wt.TransactionType = 'Outbound' AND wt.ReferenceId IN
                                    (SELECT Id FROM SalesInvoices WHERE VehicleId = @VehicleId))
                                OR
                                (wt.TransactionType IN ('Return','CarReturn') AND wt.ReferenceId IN
                                    (SELECT Id FROM ReturnOrders WHERE VehicleId = @VehicleId))
                            )
                            GROUP BY wt.ProductId";
                        AddParam(cmd, "@VehicleId", vehicleId);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                            {
                                int bal = rdr.GetInt32(1);
                                if (bal > 0) result[rdr.GetInt32(0)] = bal;
                            }
                    }
                }
                return result;
            });
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════
        private static void AddParam(IDbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}