using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;

namespace DistributionSystem.Data.Repositories
{
    public class EmployeeRepository
    {
        private readonly SqlConnectionFactory _factory;

        public EmployeeRepository(SqlConnectionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        // ??????????????????????????????????????????????????????
        //  EMPLOYEES
        // ??????????????????????????????????????????????????????
        public List<Employee> GetAllEmployees()
        {
            var list = new List<Employee>();
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // ? √÷›‰« JobTitle ›Ì «·‹ SELECT (index 7)
                    cmd.CommandText = @"
                        SELECT e.Id, e.Name, e.Salary, e.RemainingBalance,
                               e.Notes, e.IsActive, e.CreatedAt, e.JobTitle
                        FROM Employees e
                        WHERE e.IsActive = 1
                        ORDER BY e.CreatedAt DESC";
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            list.Add(ReadEmployee(rdr));
                }
            }
            return list;
        }

        public int InsertEmployee(Employee e)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // ? √÷›‰« JobTitle ›Ì «·‹ INSERT
                    cmd.CommandText = @"
                        INSERT INTO Employees (Name, Salary, RemainingBalance, Notes, JobTitle, IsActive, CreatedAt)
                        VALUES (@Name, @Salary, @Salary, @Notes, @JobTitle, 1, @CreatedAt);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    AddParam(cmd, "@Name", e.Name);
                    AddParam(cmd, "@Salary", e.Salary);
                    AddParam(cmd, "@Notes", e.Notes ?? "");
                    AddParam(cmd, "@JobTitle", e.JobTitle ?? "");
                    AddParam(cmd, "@CreatedAt", DateTime.Now);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void UpdateEmployee(Employee e)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    // ? √÷›‰« JobTitle ›Ì «·‹ UPDATE
                    cmd.CommandText = @"
                        UPDATE Employees
                        SET Name = @Name, Salary = @Salary, Notes = @Notes, JobTitle = @JobTitle
                        WHERE Id = @Id";
                    AddParam(cmd, "@Name", e.Name);
                    AddParam(cmd, "@Salary", e.Salary);
                    AddParam(cmd, "@Notes", e.Notes ?? "");
                    AddParam(cmd, "@JobTitle", e.JobTitle ?? "");
                    AddParam(cmd, "@Id", e.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteEmployee(int id)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        Exec(conn, tran, "DELETE FROM EmployeeLoans WHERE EmployeeId = @Id", "@Id", id);
                        Exec(conn, tran, "DELETE FROM Employees WHERE Id = @Id", "@Id", id);
                        tran.Commit();
                    }
                    catch { tran.Rollback(); throw; }
                }
            }
        }

        // ??????????????????????????????????????????????????????
        //  LOANS
        // ??????????????????????????????????????????????????????
        public List<EmployeeLoan> GetLoansByEmployee(int employeeId)
        {
            var list = new List<EmployeeLoan>();
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Id, EmployeeId, Amount, Notes, CreatedAt
                        FROM EmployeeLoans
                        WHERE EmployeeId = @EmployeeId
                        ORDER BY CreatedAt DESC";
                    AddParam(cmd, "@EmployeeId", employeeId);
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            list.Add(new EmployeeLoan
                            {
                                Id = rdr.GetInt32(0),
                                EmployeeId = rdr.GetInt32(1),
                                Amount = rdr.GetDecimal(2),
                                Notes = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                                CreatedAt = rdr.GetDateTime(4)
                            });
                }
            }
            return list;
        }

        public void AddLoan(EmployeeLoan loan)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        decimal balance = 0;
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tran;
                            cmd.CommandText = "SELECT RemainingBalance FROM Employees WHERE Id = @Id";
                            AddParam(cmd, "@Id", loan.EmployeeId);
                            var result = cmd.ExecuteScalar();
                            if (result != null) balance = Convert.ToDecimal(result);
                        }
                        if (loan.Amount > balance)
                            throw new InvalidOperationException($"«·—’Ìœ «·„ «Õ {balance:N2} Ã‰ÌÂ ›ﬁÿ° ·« Ì„ﬂ‰ ’—› {loan.Amount:N2} Ã‰ÌÂ");
                        if (balance <= 0)
                            throw new InvalidOperationException("—’Ìœ «·„ÊŸ› ’›—° ·« Ì„ﬂ‰ ’—› ”·›…");

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tran;
                            cmd.CommandText = @"
                                INSERT INTO EmployeeLoans (EmployeeId, Amount, Notes, CreatedAt)
                                VALUES (@EmployeeId, @Amount, @Notes, @CreatedAt)";
                            AddParam(cmd, "@EmployeeId", loan.EmployeeId);
                            AddParam(cmd, "@Amount", loan.Amount);
                            AddParam(cmd, "@Notes", loan.Notes ?? "");
                            AddParam(cmd, "@CreatedAt", DateTime.Now);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tran;
                            cmd.CommandText = @"
                                UPDATE Employees
                                SET RemainingBalance = RemainingBalance - @Amount
                                WHERE Id = @Id";
                            AddParam(cmd, "@Amount", loan.Amount);
                            AddParam(cmd, "@Id", loan.EmployeeId);
                            cmd.ExecuteNonQuery();
                        }

                        tran.Commit();
                    }
                    catch { tran.Rollback(); throw; }
                }
            }
        }

        // ??????????????????????????????????????????????????????
        //  ADMIN EXPENSES
        // ??????????????????????????????????????????????????????
        public List<AdminExpense> GetAllExpenses()
        {
            var list = new List<AdminExpense>();
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Id, Description, Amount, CreatedAt FROM AdminExpenses ORDER BY CreatedAt DESC";
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            list.Add(new AdminExpense
                            {
                                Id = rdr.GetInt32(0),
                                Description = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                                Amount = rdr.GetDecimal(2),
                                CreatedAt = rdr.GetDateTime(3)
                            });
                }
            }
            return list;
        }

        public void InsertExpense(AdminExpense exp)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO AdminExpenses (Description, Amount, CreatedAt)
                        VALUES (@Description, @Amount, @CreatedAt)";
                    AddParam(cmd, "@Description", exp.Description ?? "");
                    AddParam(cmd, "@Amount", exp.Amount);
                    AddParam(cmd, "@CreatedAt", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteExpense(int id)
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM AdminExpenses WHERE Id = @Id";
                    AddParam(cmd, "@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ??????????????????????????????????????????????????????
        //  SUMMARIES
        // ??????????????????????????????????????????????????????
        public decimal GetTotalPaidToEmployees()
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM EmployeeLoans";
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        public decimal GetTotalAdminExpenses()
        {
            using (var conn = _factory.CreateConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM AdminExpenses";
                    return Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        // ? index 7 = JobTitle
        private static Employee ReadEmployee(SqlDataReader r) => new Employee
        {
            Id = r.GetInt32(0),
            Name = r.IsDBNull(1) ? "" : r.GetString(1),
            Salary = r.GetDecimal(2),
            RemainingBalance = r.GetDecimal(3),
            Notes = r.IsDBNull(4) ? "" : r.GetString(4),
            IsActive = r.GetBoolean(5),
            CreatedAt = r.GetDateTime(6),
            JobTitle = r.IsDBNull(7) ? "" : r.GetString(7),
        };

        private static void AddParam(SqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static void Exec(SqlConnection conn, SqlTransaction tran, string sql, string paramName, object paramVal)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = sql;
                AddParam(cmd, paramName, paramVal);
                cmd.ExecuteNonQuery();
            }
        }
    }
}