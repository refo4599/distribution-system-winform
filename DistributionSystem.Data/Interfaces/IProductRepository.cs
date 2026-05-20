using System.Collections.Generic;
using System.Data.SqlClient;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Data.Interfaces
{
    public interface IProductRepository
    {
        IEnumerable<Product> GetAll();
        Product GetById(int id);
        int Insert(Product product);
        int Insert(Product product, SqlConnection connection, SqlTransaction transaction);
        bool Update(Product product);
        bool Delete(int id);
    }
}
