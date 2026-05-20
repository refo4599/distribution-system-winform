using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using DistributionSystem.Data.Data;

namespace DistributionSystem.Business.Services
{
    public class BackupService
    {
        private readonly SqlConnectionFactory _factory;
        private static readonly string AutoBackupFolder = @"D:\DistributionSystem\Backups";

        public BackupService()
        {
            _factory = new SqlConnectionFactory();
        }

        // ═══════════════════════════════════════════════════════
        //  AUTO DAILY BACKUP
        // ═══════════════════════════════════════════════════════
        public void AutoDailyBackup()
        {
            try
            {
                Directory.CreateDirectory(AutoBackupFolder);

                var files = Directory.GetFiles(AutoBackupFolder, "AutoBackup_*.bak");
                if (files.Length > 0)
                {
                    var lastBackup = files.Select(f => File.GetCreationTime(f)).Max();
                    if (lastBackup.Date == DateTime.Today) return;
                }

                string path = Path.Combine(AutoBackupFolder,
                    $"AutoBackup_{DateTime.Now:yyyy-MM-dd}.bak");

                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = 120;
                        cmd.CommandText = $@"
                            BACKUP DATABASE [DistributionDb]
                            TO DISK = N'{path.Replace("'", "''")}' 
                            WITH FORMAT, INIT, COMPRESSION,
                            NAME = N'AutoBackup_{DateTime.Now:yyyy-MM-dd}'";
                        cmd.ExecuteNonQuery();
                    }
                }

                var allFiles = Directory.GetFiles(AutoBackupFolder, "AutoBackup_*.bak")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(7);
                foreach (var old in allFiles)
                    File.Delete(old);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════
        //  MANUAL BACKUP — بيرجع المسار لو نجح، null لو فشل
        // ═══════════════════════════════════════════════════════
        public string BackupToPath(string path)
        {
            try
            {
                using (var conn = _factory.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = 120;
                        cmd.CommandText = $@"
                            BACKUP DATABASE [DistributionDb]
                            TO DISK = N'{path.Replace("'", "''")}' 
                            WITH FORMAT, INIT, COMPRESSION,
                            NAME = N'DistributionDb-Full Database Backup'";
                        cmd.ExecuteNonQuery();
                    }
                }
                return path;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  RESTORE — بيعمل restore من مسار معين
        // ═══════════════════════════════════════════════════════
        public void RestoreFromPath(string path)
        {
            string masterConnStr = _factory.CreateConnection().ConnectionString
                .Replace("Initial Catalog=DistributionDb", "Initial Catalog=master");

            using (var conn = new SqlConnection(masterConnStr))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 30;
                    cmd.CommandText = "ALTER DATABASE [DistributionDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = 300;
                        cmd.CommandText = $@"
                            RESTORE DATABASE [DistributionDb]
                            FROM DISK = N'{path.Replace("'", "''")}' 
                            WITH REPLACE, RECOVERY";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = 30;
                        cmd.CommandText = "ALTER DATABASE [DistributionDb] SET MULTI_USER";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "ALTER DATABASE [DistributionDb] SET MULTI_USER";
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch { }
                    throw;
                }
            }
        }

        public string AutoBackupFolder_Public => AutoBackupFolder;
    }
}