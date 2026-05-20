using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Data.Repositories
{
    public class CustomerRepository : BaseRepository
    {
        public CustomerRepository(SqlConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }

        public IEnumerable<Customer> GetAll()
        {
            var list = new List<Customer>();
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(
                    "SELECT Id, Name, Phone, Address, CreatedAt, CustomerType FROM dbo.Customers ORDER BY Id", conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Customer
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Phone = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Address = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CreatedAt = reader.GetDateTime(4),
                                CustomerType = reader.GetInt32(5)
                            });
                        }
                    }
                }
            }
            catch (Exception) { throw; }

            return list;
        }

        public Customer GetById(int id)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(
                    "SELECT Id, Name, Phone, Address, CreatedAt, CustomerType FROM dbo.Customers WHERE Id = @Id", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Customer
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Phone = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Address = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CreatedAt = reader.GetDateTime(4),
                                CustomerType = reader.GetInt32(5)
                            };
                        }
                    }
                }
            }
            catch (Exception) { throw; }

            return null;
        }

        public int Insert(Customer customer)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(
                    @"INSERT INTO dbo.Customers (Name, Phone, Address, CustomerType)
                      VALUES (@Name, @Phone, @Address, @CustomerType);
                      SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 200) { Value = customer.Name });
                    cmd.Parameters.Add(new SqlParameter("@Phone", SqlDbType.NVarChar, 50) { Value = (object)customer.Phone ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@Address", SqlDbType.NVarChar, 500) { Value = (object)customer.Address ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@CustomerType", SqlDbType.Int) { Value = customer.CustomerType });

                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception) { throw; }
        }

        public bool Update(Customer customer)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand(
                    @"UPDATE dbo.Customers
                      SET Name=@Name, Phone=@Phone, Address=@Address, CustomerType=@CustomerType
                      WHERE Id=@Id", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 200) { Value = customer.Name });
                    cmd.Parameters.Add(new SqlParameter("@Phone", SqlDbType.NVarChar, 50) { Value = (object)customer.Phone ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@Address", SqlDbType.NVarChar, 500) { Value = (object)customer.Address ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@CustomerType", SqlDbType.Int) { Value = customer.CustomerType });
                    cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = customer.Id });

                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception) { throw; }
        }

        public bool Delete(int id)
        {
            try
            {
                using (var conn = Connection)
                using (var cmd = new SqlCommand("DELETE FROM dbo.Customers WHERE Id = @Id", conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception) { throw; }
        }
    }
}