using System.Collections.Generic;
using DistributionSystem.Data.Dtos;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Data.Interfaces
{
    public interface IWarehouseRepository
    {
        PagedResult<WarehouseBalanceDataDto> GetWarehouseBalances(int page, int pageSize, string searchTerm);
        decimal? GetProductAverageCost(int productId);
        decimal GetTotalInventoryValue();
        int GetProductBalance(int productId);
        int AddTransaction(WarehouseTransaction tx);
        int AddTransaction(WarehouseTransaction tx, System.Data.SqlClient.SqlConnection connection, System.Data.SqlClient.SqlTransaction transaction);
        WarehouseTransaction GetTransactionById(int transactionId);
    }
}
