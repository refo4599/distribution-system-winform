using System;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Data.Repositories
{
    public class InboundRepository
    {
        private readonly SqlConnectionFactory _factory;

        public InboundRepository(SqlConnectionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        // ??????????????????????????????????????????????????????
        //  INSERT INBOUND ORDER — íÑÌÚ ÇáÜ Id ÇáÌÏíÏ
        // ??????????????????????????????????????????????????????
        public int InsertInboundOrder(
            SqlConnection conn,
            SqlTransaction tran,
            int customerId,
            DateTime? createdAt = null)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @"
                    INSERT INTO InboundOrders (CustomerId, CreatedAt)
                    VALUES (@CustomerId, @CreatedAt);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var pCust = cmd.CreateParameter();
                pCust.ParameterName = "@CustomerId";
                pCust.Value = customerId;
                cmd.Parameters.Add(pCust);

                var pDate = cmd.CreateParameter();
                pDate.ParameterName = "@CreatedAt";
                pDate.Value = (object)(createdAt ?? DateTime.Now);
                cmd.Parameters.Add(pDate);

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value
                    ? throw new InvalidOperationException("ÝÔá ÅÏÑÇÌ ÃãÑ ÇáæÇÑÏ — áã íõÑÌÚ Id.")
                    : Convert.ToInt32(result);
            }
        }

        // ??????????????????????????????????????????????????????
        //  INSERT INBOUND ITEM — ? íÍÝÙ BoxesPerCarton
        // ??????????????????????????????????????????????????????
        public void InsertInboundItem(
            SqlConnection conn,
            SqlTransaction tran,
            int inboundOrderId,
            int productId,
            int quantity,           // ÈÇáÚáÈÉ ÏÇÆãÇð
            decimal purchasePrice,
            int boxesPerCarton = 1) // ? ÚÏÏ ÇáÚáÈ Ýí ÇáßÑÊæäÉ — íÏÎáå ÇáãÓÊÎÏã
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandType = CommandType.Text;

                // ? íÍÝÙ BoxesPerCarton Ýí ÇáÕÝ — ãÔ ãä Products
                cmd.CommandText = @"
                    INSERT INTO InboundOrderItems
                        (InboundOrderId, ProductId, Quantity, PurchasePrice, BoxesPerCarton)
                    VALUES
                        (@InboundOrderId, @ProductId, @Quantity, @PurchasePrice, @BoxesPerCarton);";

                var p1 = cmd.CreateParameter(); p1.ParameterName = "@InboundOrderId"; p1.Value = inboundOrderId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@ProductId"; p2.Value = productId; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "@Quantity"; p3.Value = quantity; cmd.Parameters.Add(p3);
                var p4 = cmd.CreateParameter(); p4.ParameterName = "@PurchasePrice"; p4.Value = purchasePrice; cmd.Parameters.Add(p4);

                // ? BoxesPerCarton — ÇáÞíãÉ Çááí ÇáãÓÊÎÏã ÃÏÎáåÇ (12 Ãæ 24 Ãæ Ãí ÞíãÉ)
                var p5 = cmd.CreateParameter();
                p5.ParameterName = "@BoxesPerCarton";
                p5.Value = boxesPerCarton > 0 ? boxesPerCarton : 1;
                cmd.Parameters.Add(p5);

                cmd.ExecuteNonQuery();
            }
        }
    }
}