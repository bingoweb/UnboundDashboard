using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UnboundDashboard.Models;

namespace UnboundDashboard.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly LoggingService _logger;

        public DatabaseService(LoggingService logger)
        {
            _logger = logger;

            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.DatabaseDirectory);
            var dbFile = Path.Combine(dbPath, AppConstants.DatabaseFileName);

            _connectionString = $"Data Source={dbFile}";

            InitializeDatabase(dbPath);
        }

        private void InitializeDatabase(string dbPath)
        {
            try
            {
                if (!Directory.Exists(dbPath))
                {
                    Directory.CreateDirectory(dbPath);
                }

                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    var tableCommand = connection.CreateCommand();
                    tableCommand.CommandText = $@"
                        CREATE TABLE IF NOT EXISTS {AppConstants.MetricsTableName} (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Timestamp TEXT NOT NULL,
                            QPS REAL,
                            CacheHitPercent REAL,
                            CpuUsage REAL,
                            RamPercent REAL
                        )";
                    tableCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Veritabanı başlatılırken hata oluştu.", ex);
            }
        }

        public async Task InsertMetricsAsync(IEnumerable<MetricsHistoryRecord> records)
        {
            await Task.Run(async () =>
            {
                try
                {
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        using (var transaction = connection.BeginTransaction())
                        {
                            var command = connection.CreateCommand();
                            command.Transaction = transaction;
                            command.CommandText = $@"
                                INSERT INTO {AppConstants.MetricsTableName} (Timestamp, QPS, CacheHitPercent, CpuUsage, RamPercent)
                                VALUES ($timestamp, $qps, $cacheHitPercent, $cpuUsage, $ramPercent)";

                            var pTimestamp = command.CreateParameter(); pTimestamp.ParameterName = "$timestamp"; command.Parameters.Add(pTimestamp);
                            var pQps = command.CreateParameter(); pQps.ParameterName = "$qps"; command.Parameters.Add(pQps);
                            var pCacheHit = command.CreateParameter(); pCacheHit.ParameterName = "$cacheHitPercent"; command.Parameters.Add(pCacheHit);
                            var pCpu = command.CreateParameter(); pCpu.ParameterName = "$cpuUsage"; command.Parameters.Add(pCpu);
                            var pRam = command.CreateParameter(); pRam.ParameterName = "$ramPercent"; command.Parameters.Add(pRam);

                            foreach (var record in records)
                            {
                                pTimestamp.Value = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                                pQps.Value = record.QPS;
                                pCacheHit.Value = record.CacheHitPercent;
                                pCpu.Value = record.CpuUsage;
                                pRam.Value = record.RamPercent;

                                await command.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Metrikler veritabanına kaydedilirken hata oluştu.", ex);
                }
            });
        }
    }
}
